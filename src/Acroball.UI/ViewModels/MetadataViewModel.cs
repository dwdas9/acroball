using System.Diagnostics;
using Avalonia.Threading;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Metadata tool view model that delegates execution to the shared job framework.
/// </summary>
public sealed partial class MetadataViewModel : PageViewModel
{
    private readonly IJobExecutor _jobExecutor;
    private readonly IPdfEngine _pdfEngine;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<MetadataViewModel> _logger;
    private readonly DispatcherTimer _elapsedTimer;
    private CancellationTokenSource? _executionCancellation;
    private DateTimeOffset _executionStartedAt;
    private int _inspectGeneration;
    private bool _isOutputFileNameCustom;
    private bool _suppressOutputFileSync;

    /// <summary>Creates the view model.</summary>
    public MetadataViewModel(
        IJobExecutor jobExecutor,
        IPdfEngine pdfEngine,
        IFileDialogService fileDialogService,
        ILogger<MetadataViewModel> logger)
    {
        _jobExecutor = jobExecutor;
        _pdfEngine = pdfEngine;
        _fileDialogService = fileDialogService;
        _logger = logger;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += OnElapsedTimerTick;

        _outputFileName = "Metadata.pdf";
        _outputFile = "Metadata.pdf";
        _progressMessage = "Choose a PDF to begin.";
        _elapsedText = "0:00";
    }

    /// <inheritdoc />
    public override string Title => "Metadata";

    /// <inheritdoc />
    public override string IconKey => "Tag";

    /// <summary>The file selected for metadata editing.</summary>
    [ObservableProperty]
    private string _inputFile = string.Empty;

    /// <summary>Page count of the selected file, once known. Zero means unknown.</summary>
    [ObservableProperty]
    private int _documentPageCount;

    /// <summary>Set when the selected file could not be inspected.</summary>
    [ObservableProperty]
    private string? _inputFileError;

    /// <summary>Whether the document's existing metadata is currently being read.</summary>
    [ObservableProperty]
    private bool _isLoadingDocumentInfo;

    /// <summary>Editable document title.</summary>
    [ObservableProperty]
    private string _documentTitle = string.Empty;

    /// <summary>Editable document author.</summary>
    [ObservableProperty]
    private string _author = string.Empty;

    /// <summary>Editable document subject.</summary>
    [ObservableProperty]
    private string _subject = string.Empty;

    /// <summary>Editable document keywords.</summary>
    [ObservableProperty]
    private string _keywords = string.Empty;

    /// <summary>Editable document creator.</summary>
    [ObservableProperty]
    private string _creator = string.Empty;

    /// <summary>
    /// Editable creation date. NOTE: clearing this to null will NOT clear the
    /// document's creation date on save — the engine treats a null
    /// <see cref="Acroball.Domain.DocumentMetadata.CreationDate"/> as "leave
    /// unchanged," and there is no sentinel for "clear it" the way
    /// <see cref="string.Empty"/> serves that purpose for the text fields.
    /// </summary>
    [ObservableProperty]
    private DateTimeOffset? _creationDate;

    /// <summary>Current output file path.</summary>
    [ObservableProperty]
    private string _outputFile = string.Empty;

    /// <summary>Current output directory.</summary>
    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    /// <summary>Editable output file name.</summary>
    [ObservableProperty]
    private string _outputFileName = string.Empty;

    /// <summary>Whether the current form is valid.</summary>
    public bool CanExecute => !IsBusy && string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>Whether the job is currently running.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Whether the latest execution completed successfully.</summary>
    [ObservableProperty]
    private bool _isSuccess;

    /// <summary>Whether the latest execution completed and the result is known.</summary>
    [ObservableProperty]
    private bool _isExecutionComplete;

    /// <summary>The latest summary text shown to the user.</summary>
    [ObservableProperty]
    private string? _lastSummary;

    /// <summary>The latest error shown to the user.</summary>
    [ObservableProperty]
    private string? _lastError;

    /// <summary>Current job progress fraction.</summary>
    [ObservableProperty]
    private double _progressPercent;

    /// <summary>Current descriptive progress message.</summary>
    [ObservableProperty]
    private string? _progressMessage;

    /// <summary>Elapsed time for the active or most recent save.</summary>
    [ObservableProperty]
    private string _elapsedText = "0:00";

    /// <summary>Friendly validation message shown before execution.</summary>
    public string? ValidationMessage => GetValidationMessage();

    /// <summary>Whether a success summary should be shown.</summary>
    public bool HasSuccessResult => IsExecutionComplete && IsSuccess;

    /// <summary>Whether an error summary should be shown.</summary>
    public bool HasFailureResult => IsExecutionComplete && !IsSuccess;

    /// <summary>Full output path assembled from the folder and file name.</summary>
    public string OutputFilePath => OutputFile;

    /// <summary>Informational caption shown once the existing metadata has loaded.</summary>
    public string MetadataStatusCaption
    {
        get
        {
            if (IsLoadingDocumentInfo)
            {
                return "Reading document...";
            }

            if (InputFileError is not null || string.IsNullOrWhiteSpace(InputFile) || !File.Exists(InputFile))
            {
                return string.Empty;
            }

            return $"{DocumentPageCount} page(s) · existing metadata loaded";
        }
    }

    /// <summary>Sets the input file and reads its existing metadata.</summary>
    public async Task SetInputFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        InputFile = path;
        UpdateSuggestedOutputFileName();
        var generation = ++_inspectGeneration;

        if (!File.Exists(path))
        {
            DocumentPageCount = 0;
            InputFileError = null;
            RefreshExecutionState();
            return;
        }

        IsLoadingDocumentInfo = true;
        RefreshExecutionState();
        try
        {
            var info = await _pdfEngine.InspectAsync(path).ConfigureAwait(true);
            if (generation != _inspectGeneration)
            {
                return;
            }

            DocumentPageCount = info.PageCount;
            InputFileError = null;
            ApplyMetadata(info.Metadata);
        }
        catch (Exception ex)
        {
            if (generation != _inspectGeneration)
            {
                return;
            }

            DocumentPageCount = 0;
            InputFileError = "Could not read this PDF. It may be encrypted, corrupted, or not a valid PDF.";
            _logger.LogWarning(ex, "Failed to inspect {Path}", path);
        }
        finally
        {
            if (generation == _inspectGeneration)
            {
                IsLoadingDocumentInfo = false;
            }

            RefreshExecutionState();
        }
    }

    private void ApplyMetadata(DocumentMetadata metadata)
    {
        DocumentTitle = metadata.Title ?? string.Empty;
        Author = metadata.Author ?? string.Empty;
        Subject = metadata.Subject ?? string.Empty;
        Keywords = metadata.Keywords ?? string.Empty;
        Creator = metadata.Creator ?? string.Empty;
        CreationDate = metadata.CreationDate;
    }

    /// <summary>Lets the user pick a PDF file to edit metadata for.</summary>
    public async Task PickInputFileAsync()
    {
        var files = await _fileDialogService.PickFilesAsync().ConfigureAwait(true);
        var path = files?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await SetInputFileAsync(path).ConfigureAwait(true);
    }

    /// <summary>Selects an output folder.</summary>
    public async Task PickOutputFolderAsync()
    {
        var folder = await _fileDialogService.PickFolderAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        OutputDirectory = folder;
    }

    /// <summary>Executes the metadata update job.</summary>
    public async Task ExecuteUpdateAsync()
    {
        var validationMessage = GetValidationMessage();
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            IsExecutionComplete = true;
            IsSuccess = false;
            LastSummary = null;
            LastError = validationMessage;
            ProgressMessage = validationMessage;
            RefreshExecutionState();
            return;
        }

        _executionCancellation?.Dispose();
        _executionCancellation = new CancellationTokenSource();
        var cancellation = _executionCancellation;

        IsBusy = true;
        IsExecutionComplete = false;
        IsSuccess = false;
        LastError = null;
        LastSummary = null;
        ProgressPercent = 0;
        ProgressMessage = "Preparing to save metadata...";
        _executionStartedAt = DateTimeOffset.UtcNow;
        UpdateElapsedText(_executionStartedAt);
        _elapsedTimer.Start();

        var metadata = new DocumentMetadata(
            DocumentTitle,
            Author,
            Subject,
            Keywords,
            Creator,
            CreationDate: CreationDate);
        var request = new UpdateMetadataJobRequest(InputFile, OutputFile, metadata);

        try
        {
            var result = await _jobExecutor.ExecuteAsync(
                request,
                (job, context, cancellationToken) =>
                {
                    var metadataJob = new UpdateMetadataJob(_pdfEngine);
                    return metadataJob.ExecuteAsync(job, context, cancellationToken);
                },
                new Progress<JobProgress>(value =>
                {
                    ProgressPercent = value.Fraction;
                    ProgressMessage = value.Message ?? "Working...";
                    UpdateElapsedText(_executionStartedAt);
                }),
                cancellation.Token).ConfigureAwait(true);

            IsExecutionComplete = true;
            IsSuccess = result.Succeeded;
            LastSummary = result.OutputSummary;
            LastError = result.ErrorMessage;
            ProgressPercent = result.Succeeded ? 1.0 : 0;
            ProgressMessage = result.Succeeded ? "Metadata saved." : result.ErrorMessage ?? "Metadata update failed.";
            ElapsedText = FormatElapsed(result.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata job failed");
            IsExecutionComplete = true;
            IsSuccess = false;
            LastError = "Metadata update failed unexpectedly.";
            ProgressMessage = "Metadata update failed unexpectedly.";
        }
        finally
        {
            _elapsedTimer.Stop();

            if (ReferenceEquals(_executionCancellation, cancellation))
            {
                _executionCancellation?.Dispose();
                _executionCancellation = null;
            }

            IsBusy = false;
            RefreshExecutionState();
        }
    }

    /// <summary>Cancels the currently running metadata job.</summary>
    public void CancelExecution()
    {
        if (_executionCancellation is null || _executionCancellation.IsCancellationRequested)
        {
            return;
        }

        _executionCancellation.Cancel();
        ProgressMessage = "Cancellation requested...";
    }

    /// <summary>Opens the output folder for the last successful operation.</summary>
    public void OpenOutputFolder()
    {
        var directory = OutputDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.GetDirectoryName(OutputFile);
        }

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    /// <summary>Resets the workflow so the user can edit another file.</summary>
    public void EditAnother()
    {
        _isOutputFileNameCustom = false;
        InputFile = string.Empty;
        DocumentPageCount = 0;
        InputFileError = null;
        DocumentTitle = string.Empty;
        Author = string.Empty;
        Subject = string.Empty;
        Keywords = string.Empty;
        Creator = string.Empty;
        CreationDate = null;
        LastSummary = null;
        LastError = null;
        IsExecutionComplete = false;
        IsSuccess = false;
        ProgressPercent = 0;
        ProgressMessage = "Choose a PDF to begin.";
        SetOutputFileName("Metadata.pdf", markCustom: false);
        RefreshExecutionState();
    }

    [RelayCommand]
    internal async Task PickInputFileCommand() => await PickInputFileAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task PickOutputFolderCommand() => await PickOutputFolderAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task ExecuteUpdateCommand() => await ExecuteUpdateAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelExecutionCommand() => CancelExecution();

    [RelayCommand]
    internal void OpenOutputFolderCommand() => OpenOutputFolder();

    [RelayCommand]
    internal void EditAnotherCommand() => EditAnother();

    partial void OnOutputFileChanged(string value)
    {
        if (_suppressOutputFileSync)
        {
            return;
        }

        SyncOutputPartsFromFile(value);
        _isOutputFileNameCustom = true;
        RefreshExecutionState();
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        if (_suppressOutputFileSync)
        {
            return;
        }

        UpdateOutputFileFromParts();
        RefreshExecutionState();
    }

    partial void OnOutputFileNameChanged(string value)
    {
        if (_suppressOutputFileSync)
        {
            return;
        }

        _isOutputFileNameCustom = true;
        UpdateOutputFileFromParts();
        RefreshExecutionState();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshExecutionState();
        OnPropertyChanged(nameof(HasSuccessResult));
        OnPropertyChanged(nameof(HasFailureResult));
    }

    partial void OnIsExecutionCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSuccessResult));
        OnPropertyChanged(nameof(HasFailureResult));
        RefreshExecutionState();
    }

    partial void OnIsSuccessChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSuccessResult));
        OnPropertyChanged(nameof(HasFailureResult));
    }

    partial void OnProgressPercentChanged(double value)
        => RefreshExecutionState();

    partial void OnProgressMessageChanged(string? value)
        => RefreshExecutionState();

    partial void OnDocumentTitleChanged(string value)
        => RefreshExecutionState();

    partial void OnAuthorChanged(string value)
        => RefreshExecutionState();

    partial void OnSubjectChanged(string value)
        => RefreshExecutionState();

    partial void OnKeywordsChanged(string value)
        => RefreshExecutionState();

    partial void OnCreatorChanged(string value)
        => RefreshExecutionState();

    partial void OnCreationDateChanged(DateTimeOffset? value)
        => RefreshExecutionState();

    private void OnElapsedTimerTick(object? sender, EventArgs e)
    {
        UpdateElapsedText(DateTimeOffset.UtcNow);
    }

    private string? GetValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(InputFile))
        {
            return "Choose a PDF file to edit metadata for.";
        }

        if (!File.Exists(InputFile))
        {
            return $"\"{Path.GetFileName(InputFile)}\" could not be found.";
        }

        if (InputFileError is not null)
        {
            return InputFileError;
        }

        if (IsLoadingDocumentInfo)
        {
            return "Reading document...";
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            return "Choose an output folder.";
        }

        if (string.IsNullOrWhiteSpace(OutputFileName))
        {
            return "Choose an output file name.";
        }

        if (!IsValidFileName(OutputFileName))
        {
            return "Choose a valid output file name.";
        }

        return null;
    }

    private void UpdateSuggestedOutputFileName()
    {
        if (_isOutputFileNameCustom)
        {
            return;
        }

        var suggested = SuggestOutputFileName(InputFile);
        SetOutputFileName(suggested, markCustom: false);
    }

    private void UpdateOutputFileFromParts()
        => SetOutputFile(BuildOutputFilePath(OutputDirectory, OutputFileName));

    private void SyncOutputPartsFromFile(string value)
    {
        var directory = Path.GetDirectoryName(value) ?? string.Empty;
        var fileName = Path.GetFileName(value);

        _suppressOutputFileSync = true;
        try
        {
            OutputDirectory = directory;
            OutputFileName = string.IsNullOrWhiteSpace(fileName) ? "Metadata.pdf" : fileName;
        }
        finally
        {
            _suppressOutputFileSync = false;
        }

        RefreshExecutionState();
    }

    private void SetOutputFileName(string value, bool markCustom)
    {
        _suppressOutputFileSync = true;
        try
        {
            OutputFileName = value;
        }
        finally
        {
            _suppressOutputFileSync = false;
        }

        _isOutputFileNameCustom = markCustom;
        UpdateOutputFileFromParts();
    }

    private void SetOutputFile(string value)
    {
        _suppressOutputFileSync = true;
        try
        {
            OutputFile = value;
        }
        finally
        {
            _suppressOutputFileSync = false;
        }
    }

    private void RefreshExecutionState()
    {
        OnPropertyChanged(nameof(CanExecute));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(OutputFilePath));
        OnPropertyChanged(nameof(MetadataStatusCaption));
        OnPropertyChanged(nameof(HasSuccessResult));
        OnPropertyChanged(nameof(HasFailureResult));
    }

    private void UpdateElapsedText(DateTimeOffset now)
    {
        if (!IsBusy)
        {
            ElapsedText = FormatElapsed(TimeSpan.Zero);
            return;
        }

        ElapsedText = FormatElapsed(now - _executionStartedAt);
    }

    private static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            if (fileName.Contains(invalid))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildOutputFilePath(string? directory, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return fileName ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return directory;
        }

        return Path.Combine(directory, fileName);
    }

    private static string SuggestOutputFileName(string inputFile)
    {
        if (string.IsNullOrWhiteSpace(inputFile))
        {
            return "Metadata.pdf";
        }

        var stem = Path.GetFileNameWithoutExtension(inputFile);
        return string.IsNullOrWhiteSpace(stem) ? "Metadata.pdf" : $"{stem}-metadata.pdf";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        }

        return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
