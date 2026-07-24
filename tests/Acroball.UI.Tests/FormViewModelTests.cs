using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain.Exceptions;
using Acroball.Domain.Forms;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class FormViewModelTests
{
    [Fact]
    public void Constructor_initializes_with_expected_state()
    {
        var viewModel = CreateViewModel();

        Assert.Empty(viewModel.Fields);
        Assert.False(viewModel.HasDocument);
        Assert.False(viewModel.IsLoadingDocument);
        Assert.False(viewModel.HasPendingPassword);
        Assert.False(viewModel.HasFields);
        Assert.False(viewModel.HasNoFields);
    }

    [Fact]
    public async Task OpenFileAsync_populates_fields_from_engine()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfFormFieldInfo>
            {
                new("Name", FormFieldKind.Text, false, "existing", null),
                new("Agree", FormFieldKind.CheckBox, false, "/Off", ["/Yes", "/Off"]),
            });

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.OpenFileAsync(alpha);

        Assert.True(viewModel.HasDocument);
        Assert.True(viewModel.HasFields);
        Assert.False(viewModel.HasNoFields);
        Assert.Equal(2, viewModel.Fields.Count);
        Assert.Equal("existing", viewModel.Fields[0].Value);
        Assert.False(viewModel.Fields[1].IsChecked);
    }

    [Fact]
    public async Task OpenFileAsync_with_no_fields_sets_has_no_fields()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfFormFieldInfo>());

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.OpenFileAsync(alpha);

        Assert.True(viewModel.HasDocument);
        Assert.True(viewModel.HasNoFields);
    }

    [Fact]
    public async Task OpenFileAsync_on_encrypted_file_opens_password_prompt()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(locked, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.OpenFileAsync(locked);

        Assert.True(viewModel.HasPendingPassword);
        Assert.Equal("Locked.pdf", viewModel.PendingPasswordFileName);
        Assert.False(viewModel.HasDocument);
    }

    [Fact]
    public async Task SubmitPendingPassword_with_correct_password_opens_document()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(locked, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));
        pdfEngine.Setup(x => x.GetFormFieldsAsync(locked, "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfFormFieldInfo> { new("Name", FormFieldKind.Text, false, null, null) });

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.OpenFileAsync(locked);

        viewModel.PendingPasswordInput = "hunter2";
        await viewModel.SubmitPendingPasswordAsync();

        Assert.False(viewModel.HasPendingPassword);
        Assert.True(viewModel.HasDocument);
        Assert.Single(viewModel.Fields);
    }

    [Fact]
    public async Task CloseDocument_clears_fields_and_current_file()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfFormFieldInfo> { new("Name", FormFieldKind.Text, false, null, null) });

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.OpenFileAsync(alpha);

        viewModel.CloseDocument();

        Assert.False(viewModel.HasDocument);
        Assert.Empty(viewModel.Fields);
    }

    [Fact]
    public async Task SaveAsync_with_no_fillable_values_does_not_prompt_for_output()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfFormFieldInfo>());

        var fileDialog = new Mock<IFileDialogService>();
        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object, fileDialogService: fileDialog.Object);
        await viewModel.OpenFileAsync(alpha);

        await viewModel.SaveAsync();

        fileDialog.Verify(x => x.PickSaveFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_success_sets_result_message_and_passes_flatten_flag()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfFormFieldInfo> { new("Name", FormFieldKind.Text, false, null, null) });

        var fileDialog = new Mock<IFileDialogService>();
        fileDialog.Setup(x => x.PickSaveFileAsync(It.IsAny<string>())).ReturnsAsync(fixture.DirectoryPath + "\\Alpha-filled.pdf");

        FillFormJobRequest? capturedRequest = null;
        var jobExecutor = new Mock<IJobExecutor>();
        jobExecutor
            .Setup(x => x.ExecuteAsync(
                It.IsAny<FillFormJobRequest>(),
                It.IsAny<Func<FillFormJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(),
                It.IsAny<IProgress<JobProgress>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FillFormJobRequest, Func<FillFormJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>, IProgress<JobProgress>, CancellationToken>(
                (req, _, _, _) => capturedRequest = req)
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "Filled 1 field(s)", "out.pdf", TimeSpan.FromSeconds(1)));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object, jobExecutor: jobExecutor.Object, fileDialogService: fileDialog.Object);
        await viewModel.OpenFileAsync(alpha);
        viewModel.Fields[0].Value = "Ada Lovelace";
        viewModel.FlattenAfterFill = true;

        await viewModel.SaveAsync();

        Assert.True(viewModel.HasSuccessResult);
        Assert.Equal("Filled 1 field(s)", viewModel.ResultMessage);
        Assert.False(viewModel.IsSaving);
        Assert.NotNull(capturedRequest);
        Assert.Equal("Ada Lovelace", capturedRequest!.Values[0].Value);
        Assert.True(capturedRequest.FlattenAfterFill);
    }

    [Fact]
    public async Task SaveAsync_when_user_cancels_output_picker_does_not_call_executor()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.GetFormFieldsAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PdfFormFieldInfo> { new("Name", FormFieldKind.Text, false, null, null) });

        var fileDialog = new Mock<IFileDialogService>();
        fileDialog.Setup(x => x.PickSaveFileAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        var jobExecutor = new Mock<IJobExecutor>();
        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object, jobExecutor: jobExecutor.Object, fileDialogService: fileDialog.Object);
        await viewModel.OpenFileAsync(alpha);
        viewModel.Fields[0].Value = "Ada";

        await viewModel.SaveAsync();

        jobExecutor.Verify(
            x => x.ExecuteAsync(
                It.IsAny<FillFormJobRequest>(),
                It.IsAny<Func<FillFormJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(),
                It.IsAny<IProgress<JobProgress>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(viewModel.LastSaveSucceeded);
    }

    private static FormViewModel CreateViewModel(IPdfEngine? pdfEngine = null, IJobExecutor? jobExecutor = null, IFileDialogService? fileDialogService = null)
    {
        pdfEngine ??= new Mock<IPdfEngine>().Object;
        jobExecutor ??= new Mock<IJobExecutor>().Object;
        fileDialogService ??= new StubFileDialogService();

        return new FormViewModel(
            pdfEngine,
            jobExecutor,
            fileDialogService,
            NullLogger<FormViewModel>.Instance);
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
