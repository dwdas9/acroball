using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Application.Operations;
using Acroball.Domain;
using Acroball.Domain.Exceptions;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Organize tool view model: a visual page list, possibly spanning several
/// source files, that reduces to a <see cref="ComposeJobRequest"/> on execute.
/// </summary>
public sealed partial class OrganizeViewModel : PageViewModel
{
    private const int ThumbnailWidthPx = 140;

    private readonly IJobExecutor _jobExecutor;
    private readonly IPdfEngine _pdfEngine;
    private readonly IPdfRenderService _pdfRenderService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<OrganizeViewModel> _logger;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly Dictionary<string, string> _passwords = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _executionCancellation;
    private DateTimeOffset _executionStartedAt;
    private bool _isOutputFileNameCustom;
    private bool _suppressOutputFileSync;

    /// <summary>Creates the view model.</summary>
    public OrganizeViewModel(
        IJobExecutor jobExecutor,
        IPdfEngine pdfEngine,
        IPdfRenderService pdfRenderService,
        IFileDialogService fileDialogService,
        ILogger<OrganizeViewModel> logger)
    {
        _jobExecutor = jobExecutor;
        _pdfEngine = pdfEngine;
        _pdfRenderService = pdfRenderService;
        _fileDialogService = fileDialogService;
        _logger = logger;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += OnElapsedTimerTick;

        Pages = [];
        Pages.CollectionChanged += (_, _) => RefreshExecutionState();

        _outputFileName = "Organized.pdf";
        _outputFile = "Organized.pdf";
        _progressMessage = "Add PDFs to begin.";
        _elapsedText = "0:00";
    }

    /// <inheritdoc />
    public override string Title => "Organize";

    /// <inheritdoc />
    public override string IconKey => "Organize";

    /// <summary>The working page list, in output order.</summary>
    public ObservableCollection<OrganizePageViewModel> Pages { get; }

    /// <summary>The file currently awaiting a password before its pages can be added, if any.</summary>
    [ObservableProperty]
    private string? _pendingPasswordFile;

    /// <summary>Password entered for <see cref="PendingPasswordFile"/>.</summary>
    [ObservableProperty]
    private string _pendingPasswordInput = string.Empty;

    /// <summary>Set when a submitted password was rejected.</summary>
    [ObservableProperty]
    private string? _pendingPasswordError;

    /// <summary>Set when a file could not be added for a reason other than a password.</summary>
    [ObservableProperty]
    private string? _addFileError;

    /// <summary>Whether a password prompt is currently open.</summary>
    public bool HasPendingPassword => PendingPasswordFile is not null;

    /// <summary>File name of <see cref="PendingPasswordFile"/>, for the prompt.</summary>
    public string PendingPasswordFileName => PendingPasswordFile is null ? string.Empty : Path.GetFileName(PendingPasswordFile);

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

    /// <summary>Elapsed time for the active or most recent run.</summary>
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

    /// <summary>Human-readable page/file count caption, blank until pages exist.</summary>
    public string PageCountCaption => Pages.Count == 0
        ? string.Empty
        : $"{Pages.Count} page(s) from {Pages.Select(p => p.SourceFile).Distinct().Count()} file(s)";

    /// <summary>Lets the user pick one or more PDF files to add pages from.</summary>
    public async Task PickFilesAsync()
    {
        var files = await _fileDialogService.PickFilesAsync().ConfigureAwait(true);
        if (files is null)
        {
            return;
        }

        foreach (var path in files)
        {
            await AddFileAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>Adds every page of the PDF at <paramref name="path"/> to the working list.</summary>
    public async Task AddFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            AddFileError = $"\"{Path.GetFileName(path)}\" could not be found.";
            return;
        }

        AddFileError = null;
        await TryAddPagesAsync(path, password: null).ConfigureAwait(true);
    }

    /// <summary>Retries adding <see cref="PendingPasswordFile"/> with <see cref="PendingPasswordInput"/>.</summary>
    public async Task SubmitPendingPasswordAsync()
    {
        if (PendingPasswordFile is null)
        {
            return;
        }

        await TryAddPagesAsync(PendingPasswordFile, PendingPasswordInput).ConfigureAwait(true);
    }

    /// <summary>Dismisses the password prompt without adding the file.</summary>
    public void CancelPendingPassword()
    {
        PendingPasswordFile = null;
        PendingPasswordInput = string.Empty;
        PendingPasswordError = null;
    }

    /// <summary>Removes one page tile.</summary>
    public void RemovePage(Guid id)
    {
        var page = Pages.FirstOrDefault(p => p.Id == id);
        if (page is null)
        {
            return;
        }

        page.ThumbnailCancellation.Cancel();
        page.ThumbnailCancellation.Dispose();
        page.Thumbnail?.Dispose();
        Pages.Remove(page);
    }

    /// <summary>Adds <paramref name="delta"/> to one page's rotation.</summary>
    public void RotatePage(Guid id, Rotation delta)
    {
        var page = Pages.FirstOrDefault(p => p.Id == id);
        if (page is null)
        {
            return;
        }

        page.RotationDelta = page.RotationDelta.Add(delta);
    }

    /// <summary>Moves one page before another, used by drag-and-drop reordering.</summary>
    public void MovePageBefore(Guid sourceId, Guid targetId)
    {
        if (sourceId == targetId)
        {
            return;
        }

        var sourceIndex = IndexOf(sourceId);
        var targetIndex = IndexOf(targetId);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        Pages.Move(sourceIndex, targetIndex);
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

    /// <summary>Executes the organize (compose) job.</summary>
    public async Task ExecuteOrganizeAsync()
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
        ProgressMessage = "Preparing to organize...";
        _executionStartedAt = DateTimeOffset.UtcNow;
        UpdateElapsedText(_executionStartedAt);
        _elapsedTimer.Start();

        var assignments = Pages
            .Select(p => new PageAssignment(p.SourceFile, p.SourcePageNumber, p.RotationDelta))
            .ToList();
        var passwords = _passwords.Count > 0 ? new Dictionary<string, string>(_passwords) : null;
        var request = new ComposeJobRequest(assignments, OutputFile, passwords);

        try
        {
            var result = await _jobExecutor.ExecuteAsync(
                request,
                (job, context, cancellationToken) =>
                {
                    var composeJob = new ComposeJob(_pdfEngine);
                    return composeJob.ExecuteAsync(job, context, cancellationToken);
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
            ProgressMessage = result.Succeeded ? "Organize completed." : result.ErrorMessage ?? "Organize failed.";
            ElapsedText = FormatElapsed(result.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Organize job failed");
            IsExecutionComplete = true;
            IsSuccess = false;
            LastError = "Organize failed unexpectedly.";
            ProgressMessage = "Organize failed unexpectedly.";
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

    /// <summary>Cancels the currently running organize job.</summary>
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

    /// <summary>Resets the workflow so the user can organize another set of pages.</summary>
    public void OrganizeAnother()
    {
        _isOutputFileNameCustom = false;
        foreach (var page in Pages.ToArray())
        {
            RemovePage(page.Id);
        }

        _passwords.Clear();
        CancelPendingPassword();
        AddFileError = null;
        LastSummary = null;
        LastError = null;
        IsExecutionComplete = false;
        IsSuccess = false;
        ProgressPercent = 0;
        ProgressMessage = "Add PDFs to begin.";
        SetOutputFileName("Organized.pdf", markCustom: false);
        RefreshExecutionState();
    }

    [RelayCommand]
    internal async Task PickFilesCommand() => await PickFilesAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task SubmitPendingPasswordCommand() => await SubmitPendingPasswordAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelPendingPasswordCommand() => CancelPendingPassword();

    [RelayCommand]
    internal void RemovePageCommand(Guid id) => RemovePage(id);

    [RelayCommand]
    internal void RotateLeftCommand(Guid id) => RotatePage(id, Rotation.CounterClockwise90);

    [RelayCommand]
    internal void RotateRightCommand(Guid id) => RotatePage(id, Rotation.Clockwise90);

    [RelayCommand]
    internal async Task PickOutputFolderCommand() => await PickOutputFolderAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task ExecuteOrganizeCommand() => await ExecuteOrganizeAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelExecutionCommand() => CancelExecution();

    [RelayCommand]
    internal void OpenOutputFolderCommand() => OpenOutputFolder();

    [RelayCommand]
    internal void OrganizeAnotherCommand() => OrganizeAnother();

    partial void OnPendingPasswordFileChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPendingPassword));
        OnPropertyChanged(nameof(PendingPasswordFileName));
    }

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

    private async Task TryAddPagesAsync(string path, string? password)
    {
        try
        {
            var info = await _pdfEngine.InspectAsync(path, password).ConfigureAwait(true);

            if (password is not null)
            {
                _passwords[path] = password;
            }

            for (var pageNumber = 1; pageNumber <= info.PageCount; pageNumber++)
            {
                var page = new OrganizePageViewModel(path, pageNumber);
                Pages.Add(page);
                _ = LoadThumbnailAsync(page);
            }

            PendingPasswordFile = null;
            PendingPasswordInput = string.Empty;
            PendingPasswordError = null;
        }
        catch (InvalidPdfPasswordException)
        {
            PendingPasswordFile = path;
            PendingPasswordInput = string.Empty;
            PendingPasswordError = password is null ? null : "Incorrect password.";
        }
        catch (Exception ex)
        {
            AddFileError = $"Could not read \"{Path.GetFileName(path)}\". It may be corrupted or not a valid PDF.";
            _logger.LogWarning(ex, "Failed to add {Path} to the organizer", path);
        }
    }

    private async Task LoadThumbnailAsync(OrganizePageViewModel page)
    {
        try
        {
            var password = _passwords.GetValueOrDefault(page.SourceFile);
            var rendered = await _pdfRenderService.RenderPageAsync(
                page.SourceFile,
                page.SourcePageNumber,
                ThumbnailWidthPx,
                password,
                page.ThumbnailCancellation.Token).ConfigureAwait(true);

            using var stream = new MemoryStream(rendered.EncodedPng);
            page.Thumbnail = new Bitmap(stream);
        }
        catch (OperationCanceledException)
        {
            // The tile was removed before its thumbnail finished rendering.
        }
        catch (Exception ex)
        {
            page.ThumbnailError = "Preview unavailable";
            _logger.LogWarning(ex, "Failed to render thumbnail for {Path} page {PageNumber}", page.SourceFile, page.SourcePageNumber);
        }
        finally
        {
            page.IsLoadingThumbnail = false;
        }
    }

    private int IndexOf(Guid id)
    {
        for (var i = 0; i < Pages.Count; i++)
        {
            if (Pages[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnElapsedTimerTick(object? sender, EventArgs e)
    {
        UpdateElapsedText(DateTimeOffset.UtcNow);
    }

    private string? GetValidationMessage()
    {
        if (Pages.Count == 0)
        {
            return "Add at least one PDF page.";
        }

        var missingFile = Pages.Select(p => p.SourceFile).Distinct().FirstOrDefault(f => !File.Exists(f));
        if (missingFile is not null)
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
            OutputFileName = string.IsNullOrWhiteSpace(fileName) ? "Organized.pdf" : fileName;
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

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        }

        return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
