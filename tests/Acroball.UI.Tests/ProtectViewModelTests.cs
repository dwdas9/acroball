using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain;
using Acroball.Domain.Exceptions;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class ProtectViewModelTests
{
    [Fact]
    public void Constructor_initializes_with_expected_state()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(ProtectMode.Encrypt, viewModel.Mode);
        Assert.Equal(EncryptionStrength.Aes256, viewModel.Strength);
        Assert.True(viewModel.AllowPrint);
        Assert.True(viewModel.AllowModifyContents);
        Assert.True(viewModel.AllowCopyContents);
        Assert.True(viewModel.AllowAnnotate);
        Assert.True(viewModel.AllowFillForms);
        Assert.True(viewModel.AllowAssembleDocument);
        Assert.True(viewModel.AllowPrintHighQuality);
        Assert.False(viewModel.CanExecute);
        Assert.Equal("Choose a PDF file to protect.", viewModel.ValidationMessage);
    }

    [Fact]
    public async Task Selecting_plain_file_keeps_encrypt_mode()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.SetInputFileAsync(alpha);

        Assert.Equal(ProtectMode.Encrypt, viewModel.Mode);
        Assert.Null(viewModel.InputFileError);
        Assert.Equal("Alpha-protected.pdf", viewModel.OutputFileName);
    }

    [Fact]
    public async Task Selecting_encrypted_file_flips_to_decrypt_mode()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(alpha));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.SetInputFileAsync(alpha);

        Assert.Equal(ProtectMode.Decrypt, viewModel.Mode);
        Assert.Null(viewModel.InputFileError);
        Assert.Equal("Alpha-unprotected.pdf", viewModel.OutputFileName);
    }

    [Fact]
    public async Task Encrypt_validation_requires_a_password()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = fixture.DirectoryPath;

        Assert.Equal("Set a user password, an owner password, or both.", viewModel.ValidationMessage);
        Assert.False(viewModel.CanExecute);

        viewModel.UserPassword = "hunter2";

        Assert.Null(viewModel.ValidationMessage);
        Assert.True(viewModel.CanExecute);
    }

    [Fact]
    public async Task Decrypt_validation_requires_current_password()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(alpha));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = fixture.DirectoryPath;

        Assert.Equal("Enter the file's current password.", viewModel.ValidationMessage);
        Assert.False(viewModel.CanExecute);

        viewModel.CurrentPassword = "hunter2";

        Assert.Null(viewModel.ValidationMessage);
        Assert.True(viewModel.CanExecute);
    }

    [Fact]
    public async Task Execute_encrypt_sets_complete_state_when_job_succeeds()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var outputDirectory = fixture.DirectoryPath;

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, DocumentMetadata.Empty));

        var executor = new Mock<IJobExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<EncryptJobRequest>(), It.IsAny<Func<EncryptJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(), It.IsAny<IProgress<JobProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "Encrypted Alpha-protected.pdf with AES-256", Path.Combine(outputDirectory, "Alpha-protected.pdf"), TimeSpan.FromSeconds(1)));

        var viewModel = CreateViewModel(executor.Object, pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = outputDirectory;
        viewModel.UserPassword = "hunter2";

        await viewModel.ExecuteProtectAsync();

        Assert.True(viewModel.IsExecutionComplete);
        Assert.True(viewModel.IsSuccess);
        Assert.Equal("Encrypted Alpha-protected.pdf with AES-256", viewModel.LastSummary);
    }

    [Fact]
    public async Task Execute_decrypt_sets_complete_state_when_job_succeeds()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var outputDirectory = fixture.DirectoryPath;

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(alpha));

        var executor = new Mock<IJobExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<DecryptJobRequest>(), It.IsAny<Func<DecryptJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(), It.IsAny<IProgress<JobProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "Removed password protection from Alpha-unprotected.pdf", Path.Combine(outputDirectory, "Alpha-unprotected.pdf"), TimeSpan.FromSeconds(1)));

        var viewModel = CreateViewModel(executor.Object, pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = outputDirectory;
        viewModel.CurrentPassword = "hunter2";

        await viewModel.ExecuteProtectAsync();

        Assert.True(viewModel.IsExecutionComplete);
        Assert.True(viewModel.IsSuccess);
        Assert.Equal("Removed password protection from Alpha-unprotected.pdf", viewModel.LastSummary);
    }

    [Fact]
    public async Task Protect_another_resets_workflow_and_fields()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(alpha));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.CurrentPassword = "hunter2";
        viewModel.AllowPrint = false;
        viewModel.Strength = EncryptionStrength.Aes128;
        viewModel.IsExecutionComplete = true;
        viewModel.IsSuccess = true;
        viewModel.LastSummary = "done";

        viewModel.ProtectAnother();

        Assert.Empty(viewModel.InputFile);
        Assert.Equal(ProtectMode.Encrypt, viewModel.Mode);
        Assert.Equal(EncryptionStrength.Aes256, viewModel.Strength);
        Assert.True(viewModel.AllowPrint);
        Assert.Empty(viewModel.CurrentPassword);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.False(viewModel.IsSuccess);
        Assert.Null(viewModel.LastSummary);
        Assert.Equal("Protected.pdf", viewModel.OutputFileName);
    }

    private static ProtectViewModel CreateViewModel(IJobExecutor? executor = null, IPdfEngine? pdfEngine = null)
    {
        executor ??= new Mock<IJobExecutor>().Object;
        pdfEngine ??= new Mock<IPdfEngine>().Object;
        return new ProtectViewModel(
            executor,
            pdfEngine,
            new StubFileDialogService(),
            NullLogger<ProtectViewModel>.Instance);
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public Task<IReadOnlyList<string>?> PickFilesAsync() => Task.FromResult<IReadOnlyList<string>?>(null);

        public Task<string?> PickSaveFileAsync(string initialFileName = "") => Task.FromResult<string?>(null);

        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
    }

    private sealed class PdfFixture : IDisposable
    {
        public PdfFixture()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"acroball-ui-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public string CreateFile(string fileName)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllText(path, "%PDF-1.4\n%");
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}
