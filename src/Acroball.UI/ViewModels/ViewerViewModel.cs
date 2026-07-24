using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Acroball.Application.Abstractions;
using Acroball.Domain.Exceptions;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Viewer tool view model: opens one PDF and displays it as a continuous,
/// virtualized scroll of pages rendered through <see cref="IPdfRenderService"/>.
/// </summary>
public sealed partial class ViewerViewModel : PageViewModel
{
    private readonly IPdfEngine _pdfEngine;
    private readonly IPdfRenderService _pdfRenderService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<ViewerViewModel> _logger;
    private string? _openPassword;

    /// <summary>Creates the view model.</summary>
    public ViewerViewModel(
        IPdfEngine pdfEngine,
        IPdfRenderService pdfRenderService,
        IFileDialogService fileDialogService,
        ILogger<ViewerViewModel> logger)
    {
        _pdfEngine = pdfEngine;
        _pdfRenderService = pdfRenderService;
        _fileDialogService = fileDialogService;
        _logger = logger;

        Pages = [];
        Outline = [];
    }

    /// <inheritdoc />
    public override string Title => "Viewer";

    /// <inheritdoc />
    public override string IconKey => "Viewer";

    /// <summary>The open document's pages, in order.</summary>
    public ObservableCollection<ViewerPageViewModel> Pages { get; }

    /// <summary>The open document's bookmark tree, empty until a document with bookmarks is open.</summary>
    public ObservableCollection<OutlineNodeViewModel> Outline { get; }

    /// <summary>Raised when a bookmark that resolves to a page is picked; the view is expected to scroll there.</summary>
    public event Action<int>? ScrollToPageRequested;

    /// <summary>Absolute path of the currently open document, if any.</summary>
    [ObservableProperty]
    private string? _currentFile;

    /// <summary>Whether a document is currently being opened.</summary>
    [ObservableProperty]
    private bool _isLoadingDocument;

    /// <summary>Set when a file could not be opened for a reason other than a password.</summary>
    [ObservableProperty]
    private string? _openFileError;

    /// <summary>The file currently awaiting a password before it can be opened, if any.</summary>
    [ObservableProperty]
    private string? _pendingPasswordFile;

    /// <summary>Password entered for <see cref="PendingPasswordFile"/>.</summary>
    [ObservableProperty]
    private string _pendingPasswordInput = string.Empty;

    /// <summary>Set when a submitted password was rejected.</summary>
    [ObservableProperty]
    private string? _pendingPasswordError;

    /// <summary>Whether a document is currently open.</summary>
    public bool HasDocument => CurrentFile is not null;

    /// <summary>Whether a password prompt is currently open.</summary>
    public bool HasPendingPassword => PendingPasswordFile is not null;

    /// <summary>File name of <see cref="PendingPasswordFile"/>, for the prompt.</summary>
    public string PendingPasswordFileName => PendingPasswordFile is null ? string.Empty : Path.GetFileName(PendingPasswordFile);

    /// <summary>File name of <see cref="CurrentFile"/>, for the header.</summary>
    public string CurrentFileName => CurrentFile is null ? string.Empty : Path.GetFileName(CurrentFile);

    /// <summary>Human-readable page count caption, blank until a document is open.</summary>
    public string PageCountCaption => Pages.Count == 0 ? string.Empty : $"{Pages.Count} page(s)";

    /// <summary>Whether the open document has a bookmark tree to show.</summary>
    public bool HasOutline => Outline.Count > 0;

    /// <summary>Requests that the view scroll to <paramref name="pageNumber"/>, e.g. in response to a bookmark click.</summary>
    public void RequestScrollToPage(int pageNumber) => ScrollToPageRequested?.Invoke(pageNumber);

    /// <summary>Lets the user pick a PDF file to open.</summary>
    public async Task PickFileAsync()
    {
        var files = await _fileDialogService.PickFilesAsync().ConfigureAwait(true);
        var path = files?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await OpenFileAsync(path).ConfigureAwait(true);
    }

    /// <summary>Opens the PDF at <paramref name="path"/>, replacing any currently open document.</summary>
    public async Task OpenFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            OpenFileError = $"\"{Path.GetFileName(path)}\" could not be found.";
            return;
        }

        OpenFileError = null;
        await TryOpenAsync(path, password: null).ConfigureAwait(true);
    }

    /// <summary>Retries opening <see cref="PendingPasswordFile"/> with <see cref="PendingPasswordInput"/>.</summary>
    public async Task SubmitPendingPasswordAsync()
    {
        if (PendingPasswordFile is null)
        {
            return;
        }

        await TryOpenAsync(PendingPasswordFile, PendingPasswordInput).ConfigureAwait(true);
    }

    /// <summary>Dismisses the password prompt without opening the file.</summary>
    public void CancelPendingPassword()
    {
        PendingPasswordFile = null;
        PendingPasswordInput = string.Empty;
        PendingPasswordError = null;
    }

    /// <summary>Closes the open document and releases every page's bitmap.</summary>
    public void CloseDocument()
    {
        foreach (var page in Pages)
        {
            UnloadPageImage(page);
        }

        Pages.Clear();
        Outline.Clear();
        CurrentFile = null;
        _openPassword = null;
        OpenFileError = null;
        RefreshDocumentState();
    }

    [RelayCommand]
    internal async Task PickFileCommand() => await PickFileAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task SubmitPendingPasswordCommand() => await SubmitPendingPasswordAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelPendingPasswordCommand() => CancelPendingPassword();

    [RelayCommand]
    internal void CloseDocumentCommand() => CloseDocument();

    partial void OnPendingPasswordFileChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPendingPassword));
        OnPropertyChanged(nameof(PendingPasswordFileName));
    }

    /// <summary>
    /// Starts (or restarts) rendering one page's bitmap. Called by the view
    /// when the virtualizing panel realizes that page's container.
    /// </summary>
    public async Task LoadPageImageAsync(ViewerPageViewModel page)
    {
        if (CurrentFile is null)
        {
            return;
        }

        page.RenderCancellation?.Cancel();
        page.RenderCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        page.RenderCancellation = cancellation;
        page.IsLoadingImage = true;
        page.RenderError = null;

        try
        {
            var rendered = await _pdfRenderService.RenderPageAsync(
                CurrentFile,
                page.PageNumber,
                ViewerPageViewModel.DisplayWidthPx,
                _openPassword,
                cancellation.Token).ConfigureAwait(true);

            using var stream = new MemoryStream(rendered.EncodedPng);
            page.RenderedImage = new Bitmap(stream);
        }
        catch (OperationCanceledException)
        {
            // The page scrolled out of view before its render finished.
        }
        catch (Exception ex)
        {
            page.RenderError = "Preview unavailable";
            _logger.LogWarning(ex, "Failed to render page {PageNumber} of {Path}", page.PageNumber, CurrentFile);
        }
        finally
        {
            if (ReferenceEquals(page.RenderCancellation, cancellation))
            {
                page.IsLoadingImage = false;
            }
        }
    }

    /// <summary>
    /// Cancels an in-flight render and frees the bitmap. Called by the view
    /// when the virtualizing panel derealizes that page's container.
    /// </summary>
    public void UnloadPageImage(ViewerPageViewModel page)
    {
        page.RenderCancellation?.Cancel();
        page.RenderCancellation?.Dispose();
        page.RenderCancellation = null;
        page.RenderedImage?.Dispose();
        page.RenderedImage = null;
        page.IsLoadingImage = false;
        page.RenderError = null;
    }

    private async Task TryOpenAsync(string path, string? password)
    {
        IsLoadingDocument = true;
        try
        {
            var pages = await _pdfEngine.GetPagesAsync(path, password, CancellationToken.None).ConfigureAwait(true);

            CloseDocument();
            CurrentFile = path;
            _openPassword = password;
            foreach (var pageInfo in pages)
            {
                Pages.Add(new ViewerPageViewModel(pageInfo));
            }

            await LoadOutlineAsync(path, password).ConfigureAwait(true);

            PendingPasswordFile = null;
            PendingPasswordInput = string.Empty;
            PendingPasswordError = null;
            RefreshDocumentState();
        }
        catch (InvalidPdfPasswordException)
        {
            PendingPasswordFile = path;
            PendingPasswordInput = string.Empty;
            PendingPasswordError = password is null ? null : "Incorrect password.";
        }
        catch (Exception ex)
        {
            OpenFileError = $"Could not open \"{Path.GetFileName(path)}\". It may be corrupted or not a valid PDF.";
            _logger.LogWarning(ex, "Failed to open {Path} in the viewer", path);
        }
        finally
        {
            IsLoadingDocument = false;
        }
    }

    private async Task LoadOutlineAsync(string path, string? password)
    {
        try
        {
            var outline = await _pdfEngine.GetOutlineAsync(path, password, CancellationToken.None).ConfigureAwait(true);
            foreach (var node in outline)
            {
                Outline.Add(new OutlineNodeViewModel(node));
            }

            OnPropertyChanged(nameof(HasOutline));
        }
        catch (Exception ex)
        {
            // Bookmarks are a navigation aid, not core to viewing; a failure
            // here must not prevent the document itself from opening.
            _logger.LogWarning(ex, "Failed to read outline for {Path}", path);
        }
    }

    private void RefreshDocumentState()
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(CurrentFileName));
        OnPropertyChanged(nameof(PageCountCaption));
        OnPropertyChanged(nameof(HasOutline));
    }
}
