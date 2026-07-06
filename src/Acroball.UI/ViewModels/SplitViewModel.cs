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
/// Split tool view model that delegates execution to the shared job framework.
/// </summary>
public sealed partial class SplitViewModel : PageViewModel
{
    private readonly IJobExecutor _jobExecutor;
    private readonly IPdfEngine _pdfEngine;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<SplitViewModel> _logger;
    private readonly DispatcherTimer _elapsedTimer;
    private CancellationTokenSource? _executionCancellation;
    private DateTimeOffset _executionStartedAt;
    private int _inspectGeneration;

    /// <summary>Creates the view model.</summary>
    public SplitViewModel(
        IJobExecutor jobExecutor,
        IPdfEngine pdfEngine,
        IFileDialogService fileDialogService,
        ILogger<SplitViewModel> logger)
    {
        _jobExecutor = jobExecutor;
        _pdfEngine = pdfEngine;
        _fileDialogService = fileDialogService;
        _logger = logger;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += OnElapsedTimerTick;

        _progressMessage = "Choose a PDF to begin.";
        _elapsedText = "0:00";
    }

    /// <inheritdoc />
    public override string Title => "Split";

    /// <inheritdoc />
    public override string IconKey => "Split";

    /// <summary>The file selected for splitting.</summary>
    [ObservableProperty]
    private string _inputFile = string.Empty;

    /// <summary>Page count of the selected file, once known. Zero means unknown.</summary>
    [ObservableProperty]
    private int _documentPageCount;

    /// <summary>Set when the selected file could not be inspected.</summary>
    [ObservableProperty]
    private string? _inputFileError;

    /// <summary>Whether the document's page count is currently being read.</summary>
    [ObservableProperty]
    private bool _isLoadingDocumentInfo;

    /// <summary>Page ranges to split by, e.g. "1-3, 5, 7-".</summary>
    [ObservableProperty]
    private string _pageRangeText = string.Empty;

    /// <summary>Directory that receives the split files.</summary>
    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    /// <summary>Template for output file names.</summary>
    [ObservableProperty]
    private string _fileNameTemplate = "{name}-{index}";

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

    /// <summary>Elapsed time for the active or most recent split.</summary>
    [ObservableProperty]
    private string _elapsedText = "0:00";

    /// <summary>Friendly validation message shown before execution.</summary>
    public string? ValidationMessage => GetValidationMessage();

    /// <summary>Human-readable page count caption, blank until known.</summary>
    public string PageCountCaption => DocumentPageCount > 0 ? $"{DocumentPageCount} page(s)" : string.Empty;

    /// <summary>Whether a success summary should be shown.</summary>
    public bool HasSuccessResult => IsExecutionComplete && IsSuccess;

    /// <summary>Whether an error summary should be shown.</summary>
    public bool HasFailureResult => IsExecutionComplete && !IsSuccess;

    /// <summary>Sets the input file and reads its page count.</summary>
    public async Task SetInputFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        InputFile = path;
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

    /// <summary>Lets the user pick a PDF file to split.</summary>
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

    /// <summary>Executes the split job.</summary>
    public async Task ExecuteSplitAsync()
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
        ProgressMessage = "Preparing to split PDF...";
        _executionStartedAt = DateTimeOffset.UtcNow;
        UpdateElapsedText(_executionStartedAt);
        _elapsedTimer.Start();

        PageRange.TryParseList(PageRangeText, DocumentPageCount, out var ranges, out _);
        var request = new SplitJobRequest(InputFile, OutputDirectory, ranges, FileNameTemplate);

        try
        {
            var result = await _jobExecutor.ExecuteAsync(
                request,
                (job, context, cancellationToken) =>
                {
                    var splitJob = new SplitJob(_pdfEngine);
                    return splitJob.ExecuteAsync(job, context, cancellationToken);
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
            ProgressMessage = result.Succeeded ? "Split completed." : result.ErrorMessage ?? "Split failed.";
            ElapsedText = FormatElapsed(result.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Split job failed");
            IsExecutionComplete = true;
            IsSuccess = false;
            LastError = "Split failed unexpectedly.";
            ProgressMessage = "Split failed unexpectedly.";
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

    /// <summary>Cancels the currently running split job.</summary>
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
        if (string.IsNullOrWhiteSpace(OutputDirectory) || !Directory.Exists(OutputDirectory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = OutputDirectory, UseShellExecute = true });
    }

    /// <summary>Resets the workflow so the user can split another file.</summary>
    public void SplitAnother()
    {
        InputFile = string.Empty;
        DocumentPageCount = 0;
        InputFileError = null;
        PageRangeText = string.Empty;
        LastSummary = null;
        LastError = null;
        IsExecutionComplete = false;
        IsSuccess = false;
        ProgressPercent = 0;
        ProgressMessage = "Choose a PDF to begin.";
        RefreshExecutionState();
    }

    [RelayCommand]
    internal async Task PickInputFileCommand() => await PickInputFileAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task PickOutputFolderCommand() => await PickOutputFolderAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task ExecuteSplitCommand() => await ExecuteSplitAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelExecutionCommand() => CancelExecution();

    [RelayCommand]
    internal void OpenOutputFolderCommand() => OpenOutputFolder();

    [RelayCommand]
    internal void SplitAnotherCommand() => SplitAnother();

    partial void OnPageRangeTextChanged(string value)
        => RefreshExecutionState();

    partial void OnOutputDirectoryChanged(string value)
        => RefreshExecutionState();

    partial void OnFileNameTemplateChanged(string value)
        => RefreshExecutionState();

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
            return "Choose a PDF file to split.";
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

        if (!PageRange.TryParseList(PageRangeText, DocumentPageCount, out _, out var rangeError))
        {
            return rangeError;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            return "Choose an output folder.";
        }

        if (string.IsNullOrWhiteSpace(FileNameTemplate))
        {
            return "Choose a file name template.";
        }

        return null;
    }

    private void RefreshExecutionState()
    {
        OnPropertyChanged(nameof(CanExecute));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(PageCountCaption));
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

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        }

        return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
