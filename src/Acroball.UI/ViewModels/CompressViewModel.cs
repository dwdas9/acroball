using System.Diagnostics;
using Avalonia.Threading;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Application.Operations;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Compress tool view model that delegates execution to the shared job framework.
/// </summary>
public sealed partial class CompressViewModel : PageViewModel
{
    private readonly IJobExecutor _jobExecutor;
    private readonly IPdfEngine _pdfEngine;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<CompressViewModel> _logger;
    private readonly DispatcherTimer _elapsedTimer;
    private CancellationTokenSource? _executionCancellation;
    private DateTimeOffset _executionStartedAt;
    private int _inspectGeneration;
    private bool _isOutputFileNameCustom;
    private bool _suppressOutputFileSync;

    /// <summary>Creates the view model.</summary>
    public CompressViewModel(
        IJobExecutor jobExecutor,
        IPdfEngine pdfEngine,
        IFileDialogService fileDialogService,
        ILogger<CompressViewModel> logger)
    {
        _jobExecutor = jobExecutor;
        _pdfEngine = pdfEngine;
        _fileDialogService = fileDialogService;
        _logger = logger;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += OnElapsedTimerTick;

        _outputFileName = "Compressed.pdf";
        _outputFile = "Compressed.pdf";
        _progressMessage = "Choose a PDF to begin.";
        _elapsedText = "0:00";
    }

    /// <inheritdoc />
    public override string Title => "Compress";

    /// <inheritdoc />
    public override string IconKey => "Compress";

    /// <summary>The file selected for compression.</summary>
    [ObservableProperty]
    private string _inputFile = string.Empty;

    /// <summary>Page count of the selected file, once known. Zero means unknown.</summary>
    [ObservableProperty]
    private int _documentPageCount;

    /// <summary>Size on disk of the selected file, in bytes. Zero means unknown.</summary>
    [ObservableProperty]
    private long _originalFileSizeBytes;

    /// <summary>Set when the selected file could not be inspected.</summary>
    [ObservableProperty]
    private string? _inputFileError;

    /// <summary>Whether the document's info is currently being read.</summary>
    [ObservableProperty]
    private bool _isLoadingDocumentInfo;

    /// <summary>How aggressively to compress.</summary>
    [ObservableProperty]
    private CompressionProfile _profile = CompressionProfile.Balanced;

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

    /// <summary>Elapsed time for the active or most recent compression.</summary>
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

    /// <summary>Human-readable size/page caption, blank until known.</summary>
    public string DocumentInfoCaption => DocumentPageCount > 0
        ? $"{DocumentPageCount} page(s), {FormatBytes(OriginalFileSizeBytes)}"
        : string.Empty;

    /// <summary>Sets the input file, reads its size and page count, and refreshes the suggested output name.</summary>
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
            OriginalFileSizeBytes = 0;
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
            OriginalFileSizeBytes = info.FileSizeBytes;
            InputFileError = null;
        }
        catch (Exception ex)
        {
            if (generation != _inspectGeneration)
            {
                return;
            }

            DocumentPageCount = 0;
            OriginalFileSizeBytes = 0;
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

    /// <summary>Lets the user pick a PDF file to compress.</summary>
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

    /// <summary>Executes the compress job.</summary>
    public async Task ExecuteCompressAsync()
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
        ProgressMessage = "Preparing to compress...";
        _executionStartedAt = DateTimeOffset.UtcNow;
        UpdateElapsedText(_executionStartedAt);
        _elapsedTimer.Start();

        var request = new CompressJobRequest(InputFile, OutputFile, Profile);

        try
        {
            var result = await _jobExecutor.ExecuteAsync(
                request,
                (job, context, cancellationToken) =>
                {
                    var compressJob = new CompressJob(_pdfEngine);
                    return compressJob.ExecuteAsync(job, context, cancellationToken);
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
            ProgressMessage = result.Succeeded ? "Compression completed." : result.ErrorMessage ?? "Compression failed.";
            ElapsedText = FormatElapsed(result.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compress job failed");
            IsExecutionComplete = true;
            IsSuccess = false;
            LastError = "Compression failed unexpectedly.";
            ProgressMessage = "Compression failed unexpectedly.";
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

    /// <summary>Cancels the currently running compress job.</summary>
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

    /// <summary>Resets the workflow so the user can compress another file.</summary>
    public void CompressAnother()
    {
        _isOutputFileNameCustom = false;
        InputFile = string.Empty;
        DocumentPageCount = 0;
        OriginalFileSizeBytes = 0;
        InputFileError = null;
        Profile = CompressionProfile.Balanced;
        LastSummary = null;
        LastError = null;
        IsExecutionComplete = false;
        IsSuccess = false;
        ProgressPercent = 0;
        ProgressMessage = "Choose a PDF to begin.";
        SetOutputFileName("Compressed.pdf", markCustom: false);
        RefreshExecutionState();
    }

    [RelayCommand]
    internal async Task PickInputFileCommand() => await PickInputFileAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task PickOutputFolderCommand() => await PickOutputFolderAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task ExecuteCompressCommand() => await ExecuteCompressAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelExecutionCommand() => CancelExecution();

    [RelayCommand]
    internal void OpenOutputFolderCommand() => OpenOutputFolder();

    [RelayCommand]
    internal void CompressAnotherCommand() => CompressAnother();

    partial void OnProfileChanged(CompressionProfile value)
        => RefreshExecutionState();

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

    private void OnElapsedTimerTick(object? sender, EventArgs e)
    {
        UpdateElapsedText(DateTimeOffset.UtcNow);
    }

    private string? GetValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(InputFile))
        {
            return "Choose a PDF file to compress.";
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

        if (DocumentPageCount <= 0)
        {
            return "Choose a valid PDF file.";
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
            OutputFileName = string.IsNullOrWhiteSpace(fileName) ? "Compressed.pdf" : fileName;
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
        OnPropertyChanged(nameof(DocumentInfoCaption));
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
            return "Compressed.pdf";
        }

        var stem = Path.GetFileNameWithoutExtension(inputFile);
        return string.IsNullOrWhiteSpace(stem) ? "Compressed.pdf" : $"{stem}-compressed.pdf";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        }

        return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private static string FormatBytes(long bytes)
    {
        const double Kb = 1024;
        const double Mb = Kb * 1024;

        return bytes switch
        {
            >= (long)Mb => $"{bytes / Mb:0.#} MB",
            >= (long)Kb => $"{bytes / Kb:0.#} KB",
            _ => $"{bytes} B",
        };
    }
}
