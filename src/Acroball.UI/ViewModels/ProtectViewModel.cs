using System.Diagnostics;
using Avalonia.Threading;
using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain;
using Acroball.Domain.Exceptions;
using Acroball.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Acroball.UI.ViewModels;

/// <summary>Which direction the Protect tool is currently operating in.</summary>
public enum ProtectMode
{
    /// <summary>Add a password / set permissions.</summary>
    Encrypt,

    /// <summary>Remove an existing password and its restrictions.</summary>
    Decrypt,
}

/// <summary>
/// Protect tool view model that delegates execution to the shared job
/// framework. Unlike every other tool, this one performs two distinct
/// actions (encrypt or decrypt) on a single page, selected via <see cref="Mode"/>.
/// </summary>
public sealed partial class ProtectViewModel : PageViewModel
{
    private readonly IJobExecutor _jobExecutor;
    private readonly IPdfEngine _pdfEngine;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILogger<ProtectViewModel> _logger;
    private readonly DispatcherTimer _elapsedTimer;
    private CancellationTokenSource? _executionCancellation;
    private DateTimeOffset _executionStartedAt;
    private int _inspectGeneration;
    private bool _isOutputFileNameCustom;
    private bool _suppressOutputFileSync;

    /// <summary>Creates the view model.</summary>
    public ProtectViewModel(
        IJobExecutor jobExecutor,
        IPdfEngine pdfEngine,
        IFileDialogService fileDialogService,
        ILogger<ProtectViewModel> logger)
    {
        _jobExecutor = jobExecutor;
        _pdfEngine = pdfEngine;
        _fileDialogService = fileDialogService;
        _logger = logger;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += OnElapsedTimerTick;

        _outputFileName = "Protected.pdf";
        _outputFile = "Protected.pdf";
        _progressMessage = "Choose a PDF to begin.";
        _elapsedText = "0:00";
    }

    /// <inheritdoc />
    public override string Title => "Protect";

    /// <inheritdoc />
    public override string IconKey => "Lock";

    /// <summary>The file selected to protect or unprotect.</summary>
    [ObservableProperty]
    private string _inputFile = string.Empty;

    /// <summary>Set when the selected file could not be inspected (and isn't just password-protected).</summary>
    [ObservableProperty]
    private string? _inputFileError;

    /// <summary>Whether the selected file is currently being inspected.</summary>
    [ObservableProperty]
    private bool _isLoadingDocumentInfo;

    /// <summary>Whether the tool is adding or removing protection.</summary>
    [ObservableProperty]
    private ProtectMode _mode = ProtectMode.Encrypt;

    /// <summary>Password that currently opens the input file, if any. Not required for a plain, unencrypted file.</summary>
    [ObservableProperty]
    private string _currentPassword = string.Empty;

    /// <summary>New user (open) password to set. Encrypt mode only.</summary>
    [ObservableProperty]
    private string _userPassword = string.Empty;

    /// <summary>New owner (permissions) password to set. Encrypt mode only.</summary>
    [ObservableProperty]
    private string _ownerPassword = string.Empty;

    /// <summary>Encryption algorithm strength. Encrypt mode only.</summary>
    [ObservableProperty]
    private EncryptionStrength _strength = EncryptionStrength.Aes256;

    /// <summary>Allow printing.</summary>
    [ObservableProperty]
    private bool _allowPrint = true;

    /// <summary>Allow modifying document contents.</summary>
    [ObservableProperty]
    private bool _allowModifyContents = true;

    /// <summary>Allow copying content.</summary>
    [ObservableProperty]
    private bool _allowCopyContents = true;

    /// <summary>Allow annotations.</summary>
    [ObservableProperty]
    private bool _allowAnnotate = true;

    /// <summary>Allow filling forms.</summary>
    [ObservableProperty]
    private bool _allowFillForms = true;

    /// <summary>Allow assembling the document (insert, delete, rotate pages).</summary>
    [ObservableProperty]
    private bool _allowAssembleDocument = true;

    /// <summary>Allow high-quality printing.</summary>
    [ObservableProperty]
    private bool _allowPrintHighQuality = true;

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

    /// <summary>Elapsed time for the active or most recent operation.</summary>
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

    /// <summary>Whether the Encrypt-only fields should be shown.</summary>
    public bool IsEncryptMode => Mode == ProtectMode.Encrypt;

    /// <summary>Whether the Decrypt-only guidance should be shown.</summary>
    public bool IsDecryptMode => Mode == ProtectMode.Decrypt;

    /// <summary>Label for the execute button, depends on <see cref="Mode"/>.</summary>
    public string ExecuteButtonLabel => IsEncryptMode ? "Add Password" : "Remove Password";

    /// <summary>Sets the input file and detects whether it is already encrypted.</summary>
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
            InputFileError = null;
            RefreshExecutionState();
            return;
        }

        IsLoadingDocumentInfo = true;
        RefreshExecutionState();
        try
        {
            await _pdfEngine.InspectAsync(path).ConfigureAwait(true);
            if (generation != _inspectGeneration)
            {
                return;
            }

            InputFileError = null;
            Mode = ProtectMode.Encrypt;
        }
        catch (InvalidPdfPasswordException)
        {
            if (generation != _inspectGeneration)
            {
                return;
            }

            InputFileError = null;
            Mode = ProtectMode.Decrypt;
        }
        catch (Exception ex)
        {
            if (generation != _inspectGeneration)
            {
                return;
            }

            InputFileError = "Could not read this PDF. It may be corrupted or not a valid PDF.";
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

    /// <summary>Lets the user pick a PDF file to protect or unprotect.</summary>
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

    /// <summary>Executes the encrypt or decrypt job, depending on <see cref="Mode"/>.</summary>
    public async Task ExecuteProtectAsync()
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
        ProgressMessage = IsEncryptMode ? "Preparing to protect the PDF..." : "Preparing to remove the password...";
        _executionStartedAt = DateTimeOffset.UtcNow;
        UpdateElapsedText(_executionStartedAt);
        _elapsedTimer.Start();

        var progress = new Progress<JobProgress>(value =>
        {
            ProgressPercent = value.Fraction;
            ProgressMessage = value.Message ?? "Working...";
            UpdateElapsedText(_executionStartedAt);
        });

        try
        {
            JobExecutionResult result;
            if (IsEncryptMode)
            {
                var password = string.IsNullOrEmpty(CurrentPassword) ? null : CurrentPassword;
                var options = new EncryptionOptions(
                    string.IsNullOrEmpty(UserPassword) ? null : UserPassword,
                    string.IsNullOrEmpty(OwnerPassword) ? null : OwnerPassword,
                    BuildPermissions(),
                    Strength);
                var request = new EncryptJobRequest(InputFile, OutputFile, options, password);

                result = await _jobExecutor.ExecuteAsync(
                    request,
                    (job, context, cancellationToken) => new EncryptJob(_pdfEngine).ExecuteAsync(job, context, cancellationToken),
                    progress,
                    cancellation.Token).ConfigureAwait(true);
            }
            else
            {
                var request = new DecryptJobRequest(InputFile, OutputFile, CurrentPassword);

                result = await _jobExecutor.ExecuteAsync(
                    request,
                    (job, context, cancellationToken) => new DecryptJob(_pdfEngine).ExecuteAsync(job, context, cancellationToken),
                    progress,
                    cancellation.Token).ConfigureAwait(true);
            }

            IsExecutionComplete = true;
            IsSuccess = result.Succeeded;
            LastSummary = result.OutputSummary;
            LastError = result.ErrorMessage;
            ProgressPercent = result.Succeeded ? 1.0 : 0;
            ProgressMessage = result.Succeeded
                ? (IsEncryptMode ? "Protection completed." : "Password removed.")
                : result.ErrorMessage ?? (IsEncryptMode ? "Protection failed." : "Password removal failed.");
            ElapsedText = FormatElapsed(result.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Protect job failed");
            IsExecutionComplete = true;
            IsSuccess = false;
            LastError = IsEncryptMode ? "Protection failed unexpectedly." : "Password removal failed unexpectedly.";
            ProgressMessage = LastError;
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

    /// <summary>Cancels the currently running job.</summary>
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

    /// <summary>Resets the workflow so the user can protect another file.</summary>
    public void ProtectAnother()
    {
        _isOutputFileNameCustom = false;
        InputFile = string.Empty;
        InputFileError = null;
        Mode = ProtectMode.Encrypt;
        CurrentPassword = string.Empty;
        UserPassword = string.Empty;
        OwnerPassword = string.Empty;
        Strength = EncryptionStrength.Aes256;
        AllowPrint = true;
        AllowModifyContents = true;
        AllowCopyContents = true;
        AllowAnnotate = true;
        AllowFillForms = true;
        AllowAssembleDocument = true;
        AllowPrintHighQuality = true;
        LastSummary = null;
        LastError = null;
        IsExecutionComplete = false;
        IsSuccess = false;
        ProgressPercent = 0;
        ProgressMessage = "Choose a PDF to begin.";
        SetOutputFileName("Protected.pdf", markCustom: false);
        RefreshExecutionState();
    }

    [RelayCommand]
    internal async Task PickInputFileCommand() => await PickInputFileAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task PickOutputFolderCommand() => await PickOutputFolderAsync().ConfigureAwait(true);

    [RelayCommand]
    internal async Task ExecuteProtectCommand() => await ExecuteProtectAsync().ConfigureAwait(true);

    [RelayCommand]
    internal void CancelExecutionCommand() => CancelExecution();

    [RelayCommand]
    internal void OpenOutputFolderCommand() => OpenOutputFolder();

    [RelayCommand]
    internal void ProtectAnotherCommand() => ProtectAnother();

    partial void OnModeChanged(ProtectMode value)
    {
        OnPropertyChanged(nameof(IsEncryptMode));
        OnPropertyChanged(nameof(IsDecryptMode));
        OnPropertyChanged(nameof(ExecuteButtonLabel));
        UpdateSuggestedOutputFileName();
        RefreshExecutionState();
    }

    partial void OnCurrentPasswordChanged(string value)
        => RefreshExecutionState();

    partial void OnUserPasswordChanged(string value)
        => RefreshExecutionState();

    partial void OnOwnerPasswordChanged(string value)
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

    private PdfPermissions BuildPermissions()
    {
        var permissions = PdfPermissions.None;
        if (AllowPrint) permissions |= PdfPermissions.Print;
        if (AllowModifyContents) permissions |= PdfPermissions.ModifyContents;
        if (AllowCopyContents) permissions |= PdfPermissions.CopyContents;
        if (AllowAnnotate) permissions |= PdfPermissions.Annotate;
        if (AllowFillForms) permissions |= PdfPermissions.FillForms;
        if (AllowAssembleDocument) permissions |= PdfPermissions.AssembleDocument;
        if (AllowPrintHighQuality) permissions |= PdfPermissions.PrintHighQuality;
        return permissions;
    }

    private string? GetValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(InputFile))
        {
            return "Choose a PDF file to protect.";
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

        if (IsEncryptMode)
        {
            var hasPassword = !string.IsNullOrEmpty(UserPassword) || !string.IsNullOrEmpty(OwnerPassword);
            if (!hasPassword)
            {
                return "Set a user password, an owner password, or both.";
            }
        }
        else if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            return "Enter the file's current password.";
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
            OutputFileName = string.IsNullOrWhiteSpace(fileName) ? "Protected.pdf" : fileName;
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

    private string SuggestOutputFileName(string inputFile)
    {
        if (string.IsNullOrWhiteSpace(inputFile))
        {
            return "Protected.pdf";
        }

        var stem = Path.GetFileNameWithoutExtension(inputFile);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return "Protected.pdf";
        }

        return IsEncryptMode ? $"{stem}-protected.pdf" : $"{stem}-unprotected.pdf";
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
