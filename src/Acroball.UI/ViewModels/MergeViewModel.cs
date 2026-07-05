using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia.Threading;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Merge tool view model that delegates execution to the shared job framework.
/// </summary>
public sealed partial class MergeViewModel : PageViewModel
{
    private readonly IJobExecutor _jobExecutor;
    private readonly IPdfEngine _pdfEngine;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<MergeViewModel> _logger;
    private readonly DispatcherTimer _elapsedTimer;
    private CancellationTokenSource? _executionCancellation;
    private DateTimeOffset _executionStartedAt;
    private bool _isOutputFileNameCustom;
    private bool _suppressOutputFileSync;

    /// <summary>Creates the view model.</summary>
    public MergeViewModel(
        IJobExecutor jobExecutor,
        IPdfEngine pdfEngine,
        IFileDialogService fileDialogService,
        ILogger<MergeViewModel> logger)
    {
        _jobExecutor = jobExecutor;
        _pdfEngine = pdfEngine;
        _fileDialogService = fileDialogService;
        _logger = logger;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += OnElapsedTimerTick;

        Files = [];
        Files.CollectionChanged += OnFilesCollectionChanged;

        _outputFileName = "Merged.pdf";
        _outputFile = "Merged.pdf";
        _progressMessage = "Add PDFs to begin.";
        _elapsedText = "0:00";
    }

    /// <inheritdoc />
    public override string Title => "Merge";

    /// <inheritdoc />
    public override string IconKey => "Merge";

    /// <summary>Files selected for merge.</summary>
    public ObservableCollection<string> Files { get; }

    /// <summary>Current output file path.</summary>
    [ObservableProperty]
    private string _outputFile = string.Empty;

    /// <summary>Current output directory.</summary>
    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    /// <summary>Editable output file name.</summary>
    [ObservableProperty]
    private string _outputFileName = string.Empty;

    /// <summary>The file currently selected in the list.</summary>
    [ObservableProperty]
    private string? _selectedFile;

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

    /// <summary>Elapsed time for the active or most recent merge.</summary>
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

    /// <summary>Adds a file to the list.</summary>
    public void AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!Files.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            Files.Add(path);
        }
    }

    /// <summary>Adds several files to the list.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        if (paths is null)
        {
            return;
        }

        foreach (var path in paths)
        {
            AddFile(path);
        }
    }

    /// <summary>Removes the selected file.</summary>
    public void RemoveFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Files.Remove(path);
        if (SelectedFile == path)
        {
            SelectedFile = Files.FirstOrDefault();
        }
    }

    /// <summary>Clears the whole file list.</summary>
    public void ClearFiles()
    {
        Files.Clear();
        SelectedFile = null;
    }

    /// <summary>Moves the file up in the list.</summary>
    public void MoveUp(string? path)
        => MoveByOffset(path, -1);

    /// <summary>Moves the file down in the list.</summary>
    public void MoveDown(string? path)
        => MoveByOffset(path, 1);

    /// <summary>Moves one file before another file, used by drag-and-drop reordering.</summary>
    public void MoveFileBefore(string? sourcePath, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetPath) ||
            string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourceIndex = Files.IndexOf(sourcePath);
        var targetIndex = Files.IndexOf(targetPath);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        Files.Move(sourceIndex, targetIndex);
        SelectedFile = sourcePath;
    }

    /// <summary>Lets the user pick PDF files to merge.</summary>
    public async Task PickFilesAsync()
    {
        var files = await _fileDialogService.PickFilesAsync().ConfigureAwait(true);
        if (files is null)
        {
            return;
        }

        AddFiles(files);
    }

    /// <summary>Selects an output file path.</summary>
    public async Task PickOutputFileAsync()
    {
        var output = await _fileDialogService.PickSaveFileAsync(Path.GetFileName(OutputFile)).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        _isOutputFileNameCustom = true;
        SetOutputFile(output);
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

    /// <summary>Executes the merge job.</summary>
    public async Task ExecuteMergeAsync()
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
        ProgressMessage = "Preparing to merge PDFs...";
        _executionStartedAt = DateTimeOffset.UtcNow;
        UpdateElapsedText(_executionStartedAt);
        _elapsedTimer.Start();

        var request = new MergeJobRequest(Files.ToArray(), OutputFile, OutputDirectory);

        try
        {
            var result = await _jobExecutor.ExecuteAsync(
                request,
                (job, context, cancellationToken) =>
                {
                    var mergeJob = new MergeJob(_pdfEngine);
                    return mergeJob.ExecuteAsync(job, context, cancellationToken);
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
            ProgressMessage = result.Succeeded ? "Merge completed." : result.ErrorMessage ?? "Merge failed.";
            ElapsedText = FormatElapsed(result.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Merge job failed");
            IsExecutionComplete = true;
            IsSuccess = false;
            LastError = "Merge failed unexpectedly.";
            ProgressMessage = "Merge failed unexpectedly.";
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

    /// <summary>Cancels the currently running merge job.</summary>
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

    /// <summary>Resets the workflow so the user can merge another set of files.</summary>
    public void MergeAnother()
    {
        _isOutputFileNameCustom = false;
        Files.Clear();
        SelectedFile = null;
        LastSummary = null;
        LastError = null;
        IsExecutionComplete = false;
        IsSuccess = false;
        ProgressPercent = 0;
        ProgressMessage = "Add PDFs to begin.";
        UpdateSuggestedOutputFileName(force: true);
        RefreshExecutionState();
    }

    [RelayCommand]
    internal void AddFileCommand(string path) => AddFile(path);

    [RelayCommand]
    internal void RemoveSelectedFileCommand() => RemoveFile(SelectedFile);

    [RelayCommand]
    internal void ClearFilesCommand() => ClearFiles();

    [RelayCommand]
    internal void MoveUpCommand() => MoveUp(SelectedFile);

    [RelayCommand]
    internal void MoveDownCommand() => MoveDown(SelectedFile);

    [RelayCommand]
    internal async Task PickFilesCommand() => await PickFilesAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task PickOutputFileCommand() => await PickOutputFileAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task PickOutputFolderCommand() => await PickOutputFolderAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task ExecuteMergeCommand() => await ExecuteMergeAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelExecutionCommand() => CancelExecution();

    [RelayCommand]
    internal void OpenOutputFolderCommand() => OpenOutputFolder();

    [RelayCommand]
    internal void MergeAnotherCommand() => MergeAnother();

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

    private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedFile is not null && !Files.Contains(SelectedFile, StringComparer.OrdinalIgnoreCase))
        {
            SelectedFile = Files.FirstOrDefault();
        }

        UpdateSuggestedOutputFileName();
        RefreshExecutionState();
    }

    private void OnElapsedTimerTick(object? sender, EventArgs e)
    {
        UpdateElapsedText(DateTimeOffset.UtcNow);
    }

    private void MoveByOffset(string? path, int offset)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var index = Files.IndexOf(path);
        if (index < 0)
        {
            return;
        }

        var targetIndex = index + offset;
        if (targetIndex < 0 || targetIndex >= Files.Count)
        {
            return;
        }

        Files.Move(index, targetIndex);
        SelectedFile = path;
    }

    private string? GetValidationMessage()
    {
        if (Files.Count == 0)
        {
            return "Add at least one PDF file to merge.";
        }

        if (FindDuplicateFile() is { } duplicate)
        {
            return $"Remove duplicate file \"{Path.GetFileName(duplicate)}\" before merging.";
        }

        var missingFile = Files.FirstOrDefault(file => !File.Exists(file));
        if (!string.IsNullOrWhiteSpace(missingFile))
        {
            return $"\"{Path.GetFileName(missingFile)}\" could not be found.";
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

    private string? FindDuplicateFile()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Files)
        {
            if (!seen.Add(file))
            {
                return file;
            }
        }

        return null;
    }

    private void UpdateSuggestedOutputFileName(bool force = false)
    {
        if (_isOutputFileNameCustom && !force)
        {
            return;
        }

        var suggested = SuggestOutputFileName(Files);
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
            OutputFileName = string.IsNullOrWhiteSpace(fileName) ? "Merged.pdf" : fileName;
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

    private static string SuggestOutputFileName(IReadOnlyCollection<string> files)
    {
        if (files.Count == 0)
        {
            return "Merged.pdf";
        }

        if (files.Count == 1)
        {
            return "Merged.pdf";
        }

        var stems = files
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(SanitizeFileNamePart)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (stems.Count == 0)
        {
            return "Merged.pdf";
        }

        var preview = string.Join(" + ", stems.Take(3));
        if (stems.Count > 3)
        {
            preview += " + ...";
        }

        return EnsurePdfExtension(TrimFileName(preview, 120));
    }

    private static string SanitizeFileNamePart(string value)
    {
        var sanitized = value;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '_');
        }

        return sanitized.Trim();
    }

    private static string EnsurePdfExtension(string value)
        => value.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? value : value + ".pdf";

    private static string TrimFileName(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength].TrimEnd();

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        }

        return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
