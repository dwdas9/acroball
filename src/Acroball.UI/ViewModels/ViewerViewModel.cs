using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain.Annotations;
using Acroball.Domain.Exceptions;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Viewer tool view model: opens one PDF, displays it as a continuous,
/// virtualized scroll of pages rendered through <see cref="IPdfRenderService"/>,
/// and lets the user mark it up with annotations (ADR-0013) saved via
/// <see cref="SaveAnnotationsJob"/>.
/// </summary>
public sealed partial class ViewerViewModel : PageViewModel
{
    /// <summary>Colors offered on the annotation toolbar.</summary>
    public static IReadOnlyList<AnnotationColor> AvailableColors { get; } =
    [
        new(220, 38, 38),   // red
        new(217, 164, 6),   // amber
        new(22, 163, 74),   // green
        new(37, 99, 235),   // blue
    ];

    private readonly IPdfEngine _pdfEngine;
    private readonly IPdfRenderService _pdfRenderService;
    private readonly IJobExecutor _jobExecutor;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<ViewerViewModel> _logger;
    private string? _openPassword;

    /// <summary>Creates the view model.</summary>
    public ViewerViewModel(
        IPdfEngine pdfEngine,
        IPdfRenderService pdfRenderService,
        IJobExecutor jobExecutor,
        IFileDialogService fileDialogService,
        ILogger<ViewerViewModel> logger)
    {
        _pdfEngine = pdfEngine;
        _pdfRenderService = pdfRenderService;
        _jobExecutor = jobExecutor;
        _fileDialogService = fileDialogService;
        _logger = logger;

        Pages = [];
        Outline = [];
        SelectedColor = AvailableColors[0];
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

    /// <summary>The annotation tool currently armed for drawing, or null when clicking/scrolling normally.</summary>
    [ObservableProperty]
    private AnnotationKind? _selectedAnnotationTool;

    /// <summary>Color used for newly drawn annotations.</summary>
    [ObservableProperty]
    private AnnotationColor _selectedColor;

    /// <summary>Fill/stroke opacity used for newly drawn Highlight annotations.</summary>
    [ObservableProperty]
    private double _selectedOpacity = 0.4;

    /// <summary>Stroke width, in PDF points, used for newly drawn Square/Ink annotations.</summary>
    [ObservableProperty]
    private double _selectedStrokeWidth = 3;

    /// <summary>Whether any page has an annotation waiting to be saved.</summary>
    public bool HasPendingAnnotations => Pages.Any(p => p.Annotations.Count > 0);

    /// <summary>Whether an annotation save is in progress.</summary>
    [ObservableProperty]
    private bool _isSavingAnnotations;

    /// <summary>The latest annotation-save result message, success or failure.</summary>
    [ObservableProperty]
    private string? _annotationsSaveMessage;

    /// <summary>Whether the latest annotation save succeeded; null before any save has run.</summary>
    [ObservableProperty]
    private bool? _annotationsSaveSucceeded;

    [RelayCommand]
    internal void SelectHighlightTool() => SelectedAnnotationTool = AnnotationKind.Highlight;

    [RelayCommand]
    internal void SelectFreeTextTool() => SelectedAnnotationTool = AnnotationKind.FreeText;

    [RelayCommand]
    internal void SelectInkTool() => SelectedAnnotationTool = AnnotationKind.Ink;

    [RelayCommand]
    internal void SelectSquareTool() => SelectedAnnotationTool = AnnotationKind.Square;

    [RelayCommand]
    internal void SelectNoneTool() => SelectedAnnotationTool = null;

    [RelayCommand]
    internal void SelectColor(AnnotationColor color) => SelectedColor = color;

    /// <summary>
    /// Commits a Square annotation. <paramref name="pdfRect"/> is the
    /// already-mapped PDF-space rectangle (see <see cref="Services.AnnotationCoordinateMapper"/>);
    /// <paramref name="screenRect"/> is the same interaction's raw screen-space
    /// rectangle, used only for the live preview.
    /// </summary>
    public void AddSquareAnnotation(ViewerPageViewModel page, (double X, double Y, double Width, double Height) pdfRect, (double Left, double Top, double Width, double Height) screenRect)
    {
        page.Annotations.Add(new SquareAnnotationEdit(page.PageNumber, pdfRect.X, pdfRect.Y, pdfRect.Width, pdfRect.Height, SelectedColor, StrokeWidthPoints: SelectedStrokeWidth));
        page.PreviewShapes.Add(new AnnotationPreviewShape
        {
            Kind = AnnotationKind.Square,
            Left = screenRect.Left,
            Top = screenRect.Top,
            Width = screenRect.Width,
            Height = screenRect.Height,
            Stroke = ToBrush(SelectedColor),
        });
        OnPropertyChanged(nameof(HasPendingAnnotations));
    }

    /// <summary>Commits a Highlight annotation covering one rectangular quad.</summary>
    public void AddHighlightAnnotation(ViewerPageViewModel page, QuadPoints pdfQuad, (double Left, double Top, double Width, double Height) screenRect)
    {
        page.Annotations.Add(new HighlightAnnotationEdit(page.PageNumber, [pdfQuad], SelectedColor, Opacity: SelectedOpacity));
        page.PreviewShapes.Add(new AnnotationPreviewShape
        {
            Kind = AnnotationKind.Highlight,
            Left = screenRect.Left,
            Top = screenRect.Top,
            Width = screenRect.Width,
            Height = screenRect.Height,
            Fill = ToBrush(SelectedColor, SelectedOpacity),
        });
        OnPropertyChanged(nameof(HasPendingAnnotations));
    }

    /// <summary>Commits a FreeText annotation with a fixed position and size.</summary>
    public void AddFreeTextAnnotation(ViewerPageViewModel page, (double X, double Y, double Width, double Height) pdfRect, (double Left, double Top, double Width, double Height) screenRect, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        page.Annotations.Add(new FreeTextAnnotationEdit(page.PageNumber, pdfRect.X, pdfRect.Y, pdfRect.Width, pdfRect.Height, text, SelectedColor));
        page.PreviewShapes.Add(new AnnotationPreviewShape
        {
            Kind = AnnotationKind.FreeText,
            Left = screenRect.Left,
            Top = screenRect.Top,
            Width = screenRect.Width,
            Height = screenRect.Height,
            Stroke = ToBrush(SelectedColor),
            Text = text,
        });
        OnPropertyChanged(nameof(HasPendingAnnotations));
    }

    /// <summary>Commits an Ink annotation with a single freehand stroke.</summary>
    public void AddInkAnnotation(ViewerPageViewModel page, InkStroke pdfStroke, Avalonia.Points screenPoints)
    {
        if (pdfStroke.Points.Count < 2)
        {
            return;
        }

        page.Annotations.Add(new InkAnnotationEdit(page.PageNumber, [pdfStroke], SelectedColor, StrokeWidthPoints: SelectedStrokeWidth));
        page.PreviewShapes.Add(new AnnotationPreviewShape
        {
            Kind = AnnotationKind.Ink,
            Points = screenPoints,
            Stroke = ToBrush(SelectedColor),
        });
        OnPropertyChanged(nameof(HasPendingAnnotations));
    }

    /// <summary>Gathers every page's pending annotations and writes them to a new file the user chooses.</summary>
    public async Task SaveAnnotationsAsync()
    {
        if (CurrentFile is null)
        {
            return;
        }

        var allAnnotations = Pages.SelectMany(p => p.Annotations).ToList();
        if (allAnnotations.Count == 0)
        {
            return;
        }

        var suggestedName = Path.GetFileNameWithoutExtension(CurrentFile) + "-annotated.pdf";
        var outputPath = await _fileDialogService.PickSaveFileAsync(suggestedName).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        IsSavingAnnotations = true;
        AnnotationsSaveMessage = null;
        AnnotationsSaveSucceeded = null;
        try
        {
            var request = new SaveAnnotationsJobRequest(CurrentFile, allAnnotations, outputPath, _openPassword);
            var result = await _jobExecutor.ExecuteAsync(
                request,
                (req, context, ct) => new SaveAnnotationsJob(_pdfEngine).ExecuteAsync(req, context, ct),
                progress: null,
                CancellationToken.None).ConfigureAwait(true);

            AnnotationsSaveSucceeded = result.Succeeded;
            AnnotationsSaveMessage = result.Succeeded ? result.OutputSummary : result.ErrorMessage;
        }
        finally
        {
            IsSavingAnnotations = false;
        }
    }

    [RelayCommand]
    internal async Task SaveAnnotationsCommand() => await SaveAnnotationsAsync().ConfigureAwait(true);

    private static IBrush ToBrush(AnnotationColor color, double opacity = 1.0)
        => new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(opacity * 255, 0, 255), color.R, color.G, color.B));

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
