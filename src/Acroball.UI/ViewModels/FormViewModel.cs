using System.Collections.ObjectModel;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain.Forms;
using Acroball.Domain.Exceptions;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Fill Form tool view model: opens a PDF, lists its AcroForm fields for
/// editing, and writes the filled values to a new file via <see cref="FillFormJob"/>.
/// </summary>
public sealed partial class FormViewModel : PageViewModel
{
    private readonly IPdfEngine _pdfEngine;
    private readonly IJobExecutor _jobExecutor;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<FormViewModel> _logger;
    private string? _openPassword;

    /// <summary>Creates the view model.</summary>
    public FormViewModel(
        IPdfEngine pdfEngine,
        IJobExecutor jobExecutor,
        IFileDialogService fileDialogService,
        ILogger<FormViewModel> logger)
    {
        _pdfEngine = pdfEngine;
        _jobExecutor = jobExecutor;
        _fileDialogService = fileDialogService;
        _logger = logger;

        Fields = [];
    }

    /// <inheritdoc />
    public override string Title => "Fill Form";

    /// <inheritdoc />
    public override string IconKey => "Form";

    /// <summary>The open document's form fields, in document order.</summary>
    public ObservableCollection<FormFieldViewModel> Fields { get; }

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

    /// <summary>Whether every field should be marked read-only after filling.</summary>
    [ObservableProperty]
    private bool _flattenAfterFill;

    /// <summary>Whether a fill is currently being saved.</summary>
    [ObservableProperty]
    private bool _isSaving;

    /// <summary>The latest save result message, success or failure.</summary>
    [ObservableProperty]
    private string? _resultMessage;

    /// <summary>Whether the latest save succeeded; null before any save has run.</summary>
    [ObservableProperty]
    private bool? _lastSaveSucceeded;

    /// <summary>Whether a document is currently open.</summary>
    public bool HasDocument => CurrentFile is not null;

    /// <summary>Whether a password prompt is currently open.</summary>
    public bool HasPendingPassword => PendingPasswordFile is not null;

    /// <summary>File name of <see cref="PendingPasswordFile"/>, for the prompt.</summary>
    public string PendingPasswordFileName => PendingPasswordFile is null ? string.Empty : Path.GetFileName(PendingPasswordFile);

    /// <summary>File name of <see cref="CurrentFile"/>, for the header.</summary>
    public string CurrentFileName => CurrentFile is null ? string.Empty : Path.GetFileName(CurrentFile);

    /// <summary>Whether the open document has any AcroForm fields.</summary>
    public bool HasFields => Fields.Count > 0;

    /// <summary>Whether the open document has no fields to fill (a real, expected state — not an error).</summary>
    public bool HasNoFields => HasDocument && !IsLoadingDocument && Fields.Count == 0;

    /// <summary>Whether the latest save completed successfully.</summary>
    public bool HasSuccessResult => LastSaveSucceeded == true;

    /// <summary>Whether the latest save completed and failed.</summary>
    public bool HasFailureResult => LastSaveSucceeded == false;

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

    /// <summary>Closes the open document and clears every field.</summary>
    public void CloseDocument()
    {
        Fields.Clear();
        CurrentFile = null;
        _openPassword = null;
        OpenFileError = null;
        ResultMessage = null;
        LastSaveSucceeded = null;
        RefreshState();
    }

    /// <summary>Writes every fillable field's current value to a new file the user chooses.</summary>
    public async Task SaveAsync()
    {
        if (CurrentFile is null)
        {
            return;
        }

        var values = Fields.Where(f => f.IsFillable).Select(f => new FormFieldValue(f.FullyQualifiedName, f.Value)).ToList();
        if (values.Count == 0)
        {
            return;
        }

        var suggestedName = Path.GetFileNameWithoutExtension(CurrentFile) + "-filled.pdf";
        var outputPath = await _fileDialogService.PickSaveFileAsync(suggestedName).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        IsSaving = true;
        ResultMessage = null;
        LastSaveSucceeded = null;
        try
        {
            var request = new FillFormJobRequest(CurrentFile, values, outputPath, FlattenAfterFill, _openPassword);
            var result = await _jobExecutor.ExecuteAsync(
                request,
                (req, context, ct) => new FillFormJob(_pdfEngine).ExecuteAsync(req, context, ct),
                progress: null,
                CancellationToken.None).ConfigureAwait(true);

            LastSaveSucceeded = result.Succeeded;
            ResultMessage = result.Succeeded ? result.OutputSummary : result.ErrorMessage;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    internal async Task PickFileCommand() => await PickFileAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task SubmitPendingPasswordCommand() => await SubmitPendingPasswordAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelPendingPasswordCommand() => CancelPendingPassword();

    [RelayCommand]
    internal void CloseDocumentCommand() => CloseDocument();

    [RelayCommand]
    internal async Task SaveCommand() => await SaveAsync().ConfigureAwait(true);

    partial void OnPendingPasswordFileChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPendingPassword));
        OnPropertyChanged(nameof(PendingPasswordFileName));
    }

    partial void OnLastSaveSucceededChanged(bool? value)
    {
        OnPropertyChanged(nameof(HasSuccessResult));
        OnPropertyChanged(nameof(HasFailureResult));
    }

    private async Task TryOpenAsync(string path, string? password)
    {
        IsLoadingDocument = true;
        try
        {
            var fields = await _pdfEngine.GetFormFieldsAsync(path, password, CancellationToken.None).ConfigureAwait(true);

            Fields.Clear();
            CurrentFile = path;
            _openPassword = password;
            foreach (var field in fields)
            {
                Fields.Add(new FormFieldViewModel(field));
            }

            ResultMessage = null;
            LastSaveSucceeded = null;
            PendingPasswordFile = null;
            PendingPasswordInput = string.Empty;
            PendingPasswordError = null;
            RefreshState();
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
            _logger.LogWarning(ex, "Failed to open {Path} for form filling", path);
        }
        finally
        {
            IsLoadingDocument = false;
            RefreshState();
        }
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(CurrentFileName));
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasNoFields));
    }
}
