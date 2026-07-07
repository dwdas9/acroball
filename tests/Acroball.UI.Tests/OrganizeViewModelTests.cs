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

public sealed class OrganizeViewModelTests
{
    [Fact]
    public void Constructor_initializes_with_expected_state()
    {
        var viewModel = CreateViewModel();

        Assert.Empty(viewModel.Pages);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.CanExecute);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.False(viewModel.HasPendingPassword);
        Assert.Equal("Organized.pdf", viewModel.OutputFileName);
        Assert.Equal("Add at least one PDF page.", viewModel.ValidationMessage);
    }

    [Fact]
    public async Task AddFileAsync_adds_one_tile_per_page()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.AddFileAsync(alpha);

        Assert.Equal(3, viewModel.Pages.Count);
        Assert.Equal([1, 2, 3], viewModel.Pages.Select(p => p.SourcePageNumber));
        Assert.All(viewModel.Pages, p => Assert.Equal(alpha, p.SourceFile));
        Assert.Equal("3 page(s) from 1 file(s)", viewModel.PageCountCaption);
    }

    [Fact]
    public async Task AddFileAsync_on_encrypted_file_opens_password_prompt()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(locked, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.AddFileAsync(locked);

        Assert.True(viewModel.HasPendingPassword);
        Assert.Equal("Locked.pdf", viewModel.PendingPasswordFileName);
        Assert.Empty(viewModel.Pages);
    }

    [Fact]
    public async Task SubmitPendingPassword_with_correct_password_adds_pages()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(locked, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));
        pdfEngine.Setup(x => x.InspectAsync(locked, "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(locked, 2, 1234, true, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.AddFileAsync(locked);

        viewModel.PendingPasswordInput = "hunter2";
        await viewModel.SubmitPendingPasswordAsync();

        Assert.False(viewModel.HasPendingPassword);
        Assert.Equal(2, viewModel.Pages.Count);
    }

    [Fact]
    public async Task SubmitPendingPassword_with_wrong_password_keeps_prompt_open_with_error()
    {
        using var fixture = new PdfFixture();
        var locked = fixture.CreateFile("Locked.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(locked, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidPdfPasswordException(locked));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.AddFileAsync(locked);

        viewModel.PendingPasswordInput = "wrong";
        await viewModel.SubmitPendingPasswordAsync();

        Assert.True(viewModel.HasPendingPassword);
        Assert.Equal("Incorrect password.", viewModel.PendingPasswordError);
        Assert.Empty(viewModel.Pages);
    }

    [Fact]
    public async Task RemovePage_removes_the_tile()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 2, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.AddFileAsync(alpha);
        var firstPageId = viewModel.Pages[0].Id;

        viewModel.RemovePage(firstPageId);

        Assert.Single(viewModel.Pages);
        Assert.Equal(2, viewModel.Pages[0].SourcePageNumber);
    }

    [Fact]
    public async Task RotatePage_accumulates_rotation()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 1, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.AddFileAsync(alpha);
        var id = viewModel.Pages[0].Id;

        viewModel.RotatePage(id, Rotation.Clockwise90);
        viewModel.RotatePage(id, Rotation.Clockwise90);

        Assert.Equal(Rotation.Rotate180, viewModel.Pages[0].RotationDelta);
    }

    [Fact]
    public async Task MovePageBefore_reorders_pages()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.AddFileAsync(alpha);

        var page1 = viewModel.Pages[0];
        var page3 = viewModel.Pages[2];

        viewModel.MovePageBefore(page3.Id, page1.Id);

        Assert.Equal([3, 1, 2], viewModel.Pages.Select(p => p.SourcePageNumber));
    }

    [Fact]
    public async Task Execute_organize_sets_complete_state_when_job_succeeds()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var outputDirectory = fixture.DirectoryPath;

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 2, 1234, false, DocumentMetadata.Empty));

        var executor = new Mock<IJobExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<ComposeJobRequest>(), It.IsAny<Func<ComposeJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(), It.IsAny<IProgress<JobProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "Assembled 2 page(s) from 1 file(s) into Organized.pdf", Path.Combine(outputDirectory, "Organized.pdf"), TimeSpan.FromSeconds(1)));

        var viewModel = CreateViewModel(executor.Object, pdfEngine.Object);
        await viewModel.AddFileAsync(alpha);
        viewModel.OutputDirectory = outputDirectory;

        await viewModel.ExecuteOrganizeAsync();

        Assert.True(viewModel.IsExecutionComplete);
        Assert.True(viewModel.IsSuccess);
        Assert.Equal("Assembled 2 page(s) from 1 file(s) into Organized.pdf", viewModel.LastSummary);
        Assert.Equal("Organize completed.", viewModel.ProgressMessage);
    }

    [Fact]
    public async Task Organize_another_resets_workflow_state()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 2, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.AddFileAsync(alpha);
        viewModel.OutputDirectory = fixture.DirectoryPath;
        viewModel.IsExecutionComplete = true;
        viewModel.IsSuccess = true;
        viewModel.LastSummary = "done";

        viewModel.OrganizeAnother();

        Assert.Empty(viewModel.Pages);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.False(viewModel.IsSuccess);
        Assert.Null(viewModel.LastSummary);
        Assert.Equal("Organized.pdf", viewModel.OutputFileName);
    }

    private static OrganizeViewModel CreateViewModel(IJobExecutor? executor = null, IPdfEngine? pdfEngine = null, IPdfRenderService? pdfRenderService = null)
    {
        executor ??= new Mock<IJobExecutor>().Object;
        pdfEngine ??= new Mock<IPdfEngine>().Object;

        if (pdfRenderService is null)
        {
            var renderMock = new Mock<IPdfRenderService>();
            renderMock.Setup(x => x.RenderPageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Rendering is not exercised by these tests."));
            pdfRenderService = renderMock.Object;
        }

        return new OrganizeViewModel(
            executor,
            pdfEngine,
            pdfRenderService,
            new StubFileDialogService(),
            NullLogger<OrganizeViewModel>.Instance);
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
