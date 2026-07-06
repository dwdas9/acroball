using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.Domain;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class MetadataViewModelTests
{
    [Fact]
    public void Constructor_initializes_with_expected_state()
    {
        var viewModel = CreateViewModel();

        Assert.Empty(viewModel.InputFile);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.CanExecute);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.Equal("Metadata.pdf", viewModel.OutputFileName);
        Assert.Equal("Choose a PDF file to edit metadata for.", viewModel.ValidationMessage);
    }

    [Fact]
    public async Task Selecting_file_prefills_fields_from_existing_metadata()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var metadata = new DocumentMetadata(Title: "Old Title", Author: "Jane Doe");
        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, metadata));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);

        await viewModel.SetInputFileAsync(alpha);

        Assert.Equal("Old Title", viewModel.DocumentTitle);
        Assert.Equal("Jane Doe", viewModel.Author);
        Assert.Equal(string.Empty, viewModel.Subject);
        Assert.Equal("Alpha-metadata.pdf", viewModel.OutputFileName);
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
    public async Task Execute_update_sets_complete_state_when_job_succeeds()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var outputDirectory = fixture.DirectoryPath;

        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, DocumentMetadata.Empty));

        var executor = new Mock<IJobExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<UpdateMetadataJobRequest>(), It.IsAny<Func<UpdateMetadataJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(), It.IsAny<IProgress<JobProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "Updated metadata for Alpha-metadata.pdf", Path.Combine(outputDirectory, "Alpha-metadata.pdf"), TimeSpan.FromSeconds(2)));

        var viewModel = CreateViewModel(executor.Object, pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = outputDirectory;

        await viewModel.ExecuteUpdateAsync();

        Assert.True(viewModel.IsExecutionComplete);
        Assert.True(viewModel.IsSuccess);
        Assert.Equal("Updated metadata for Alpha-metadata.pdf", viewModel.LastSummary);
        Assert.Equal("Metadata saved.", viewModel.ProgressMessage);
    }

    [Fact]
    public async Task Edit_another_resets_workflow_and_editable_fields()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var metadata = new DocumentMetadata(Title: "Old Title", Author: "Jane Doe", CreationDate: DateTimeOffset.UtcNow);
        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, metadata));

        var viewModel = CreateViewModel(pdfEngine: pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = fixture.DirectoryPath;
        viewModel.IsExecutionComplete = true;
        viewModel.IsSuccess = true;
        viewModel.LastSummary = "done";

        viewModel.EditAnother();

        Assert.Empty(viewModel.InputFile);
        Assert.Empty(viewModel.DocumentTitle);
        Assert.Empty(viewModel.Author);
        Assert.Null(viewModel.CreationDate);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.False(viewModel.IsSuccess);
        Assert.Null(viewModel.LastSummary);
        Assert.Equal("Metadata.pdf", viewModel.OutputFileName);
    }

    [Fact]
    public async Task Blank_field_is_sent_as_empty_string_not_null()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var outputDirectory = fixture.DirectoryPath;

        var metadata = new DocumentMetadata(Title: "Old Title");
        var pdfEngine = new Mock<IPdfEngine>();
        pdfEngine.Setup(x => x.InspectAsync(alpha, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDocumentInfo(alpha, 3, 1234, false, metadata));

        UpdateMetadataJobRequest? capturedRequest = null;
        var executor = new Mock<IJobExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<UpdateMetadataJobRequest>(), It.IsAny<Func<UpdateMetadataJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(), It.IsAny<IProgress<JobProgress>>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateMetadataJobRequest, Func<UpdateMetadataJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>, IProgress<JobProgress>?, CancellationToken>((request, _, _, _) => capturedRequest = request)
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "done", Path.Combine(outputDirectory, "Alpha-metadata.pdf"), TimeSpan.Zero));

        var viewModel = CreateViewModel(executor.Object, pdfEngine.Object);
        await viewModel.SetInputFileAsync(alpha);
        viewModel.OutputDirectory = outputDirectory;
        viewModel.DocumentTitle = string.Empty;

        await viewModel.ExecuteUpdateAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(string.Empty, capturedRequest!.Metadata.Title);
        Assert.NotNull(capturedRequest.Metadata.Title);
    }

    private static MetadataViewModel CreateViewModel(IJobExecutor? executor = null, IPdfEngine? pdfEngine = null)
    {
        executor ??= new Mock<IJobExecutor>().Object;
        pdfEngine ??= new Mock<IPdfEngine>().Object;
        return new MetadataViewModel(
            executor,
            pdfEngine,
            new StubFileDialogService(),
            NullLogger<MetadataViewModel>.Instance);
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
