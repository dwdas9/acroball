using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class SplitViewModelTests
{
    [Fact]
    public void Constructor_initializes_with_expected_state()
    {
        var viewModel = CreateViewModel();

        Assert.Empty(viewModel.InputFile);
        Assert.Equal(0, viewModel.DocumentPageCount);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.CanExecute);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.Equal("Choose a PDF file to split.", viewModel.ValidationMessage);
    }

    [Fact]
    public async Task Selecting_file_reads_page_count_via_inspect()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 5, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.SetInputFileAsync(alpha);

        Assert.Equal(5, viewModel.DocumentPageCount);
        Assert.Equal("5 page(s)", viewModel.PageCountCaption);
    }

    [Fact]
    public async Task Missing_file_is_reported_in_validation()
    {
        using var fixture = new PdfFixture();
        var missing = fixture.CreateFile("Missing.pdf");
        File.Delete(missing);

        var viewModel = CreateViewModel();

        await viewModel.SetInputFileAsync(missing);

        Assert.Contains("could not be found", viewModel.ValidationMessage);
        Assert.False(viewModel.CanExecute);
    }

    [Fact]
    public async Task Invalid_page_range_surfaces_range_error()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 5, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);

        viewModel.PageRangeText = "99";

        Assert.Contains("beyond the last page", viewModel.ValidationMessage);
    }

    [Fact]
    public async Task Execute_split_sets_complete_state_when_job_succeeds()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var outputDirectory = fixture.DirectoryPath;

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 5, 1234, false, DocumentMetadata.Empty));

        var executor = new Mock<IJobExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<SplitJobRequest>(), It.IsAny<Func<SplitJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(), It.IsAny<IProgress<JobProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "Split into 2 file(s) in " + outputDirectory, outputDirectory, TimeSpan.FromSeconds(2)));

        var viewModel = CreateViewModel(executor.Object, pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.PageRangeText = "1-2,3-5";
        viewModel.OutputDirectory = outputDirectory;

        await viewModel.ExecuteSplitAsync();

        Assert.True(viewModel.IsExecutionComplete);
        Assert.True(viewModel.IsSuccess);
        Assert.Equal("Split into 2 file(s) in " + outputDirectory, viewModel.LastSummary);
        Assert.Equal("Split completed.", viewModel.ProgressMessage);
    }

    [Fact]
    public async Task Split_another_resets_workflow_state()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 5, 1234, false, DocumentMetadata.Empty));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = fixture.DirectoryPath;
        viewModel.IsExecutionComplete = true;
        viewModel.IsSuccess = true;
        viewModel.LastSummary = "done";

        viewModel.SplitAnother();

        Assert.Empty(viewModel.InputFile);
        Assert.Equal(0, viewModel.DocumentPageCount);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.False(viewModel.IsSuccess);
        Assert.Null(viewModel.LastSummary);
    }

    private static SplitViewModel CreateViewModel(IJobExecutor? executor = null, IPdfEngine? pdfEngine = null)
    {
        executor ??= new Mock<IJobExecutor>().Object;
        pdfEngine ??= new Mock<IPdfEngine>().Object;
        return new SplitViewModel(
            executor,
            pdfEngine,
            new StubFileDialogService(),
            NullLogger<SplitViewModel>.Instance);
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
