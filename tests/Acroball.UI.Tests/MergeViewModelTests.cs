using Acroball.Application.Abstractions;
using Acroball.Application.Jobs;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Acroball.UI.Tests;

public sealed class MergeViewModelTests
{
    [Fact]
    public void Constructor_initializes_with_expected_state()
    {
        var viewModel = CreateViewModel();

        Assert.Empty(viewModel.Files);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.CanExecute);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.Equal("Merged.pdf", viewModel.OutputFileName);
        Assert.Equal("Add at least one PDF file to merge.", viewModel.ValidationMessage);
    }

    [Fact]
    public void Adding_files_updates_suggested_output_name()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var beta = fixture.CreateFile("Beta.pdf");

        var viewModel = CreateViewModel();

        viewModel.AddFile(alpha);
        viewModel.AddFile(beta);

        Assert.Equal("Alpha + Beta.pdf", viewModel.OutputFileName);
    }

    [Fact]
    public void Duplicate_files_are_ignored()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var viewModel = CreateViewModel();

        viewModel.AddFile(alpha);
        viewModel.AddFile(alpha);

        Assert.Single(viewModel.Files);
    }

    [Fact]
    public void Missing_files_are_reported_in_validation()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var missing = fixture.CreateFile("Missing.pdf");
        File.Delete(missing);

        var viewModel = CreateViewModel();
        viewModel.AddFile(alpha);
        viewModel.AddFile(missing);
        viewModel.OutputDirectory = fixture.DirectoryPath;

        Assert.Contains("could not be found", viewModel.ValidationMessage);
        Assert.False(viewModel.CanExecute);
    }

    [Fact]
    public void MoveFileBefore_reorders_the_collection()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var beta = fixture.CreateFile("Beta.pdf");
        var gamma = fixture.CreateFile("Gamma.pdf");

        var viewModel = CreateViewModel();
        viewModel.AddFiles([alpha, beta, gamma]);

        viewModel.MoveFileBefore(gamma, alpha);

        Assert.Equal([gamma, alpha, beta], viewModel.Files);
    }

    [Fact]
    public async Task Execute_merge_sets_complete_state_when_job_succeeds()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");
        var outputDirectory = fixture.DirectoryPath;

        var executor = new Mock<IJobExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<MergeJobRequest>(), It.IsAny<Func<MergeJobRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>>>(), It.IsAny<IProgress<JobProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobExecutionResult(JobOutcome.Succeeded, null, "Merged 1 file(s) into Alpha.pdf (1 pages)", Path.Combine(outputDirectory, "Merged.pdf"), TimeSpan.FromSeconds(2)));

        var viewModel = CreateViewModel(executor.Object);
        viewModel.AddFile(alpha);
        viewModel.OutputDirectory = outputDirectory;

        await viewModel.ExecuteMergeAsync();

        Assert.True(viewModel.IsExecutionComplete);
        Assert.True(viewModel.IsSuccess);
        Assert.Equal("Merged 1 file(s) into Alpha.pdf (1 pages)", viewModel.LastSummary);
        Assert.Equal("Merge completed.", viewModel.ProgressMessage);
        Assert.Equal(Path.Combine(outputDirectory, "Merged.pdf"), viewModel.OutputFilePath);
    }

    [Fact]
    public void Merge_another_resets_workflow_state()
    {
        using var fixture = new PdfFixture();
        var alpha = fixture.CreateFile("Alpha.pdf");

        var viewModel = CreateViewModel();
        viewModel.AddFile(alpha);
        viewModel.OutputDirectory = fixture.DirectoryPath;
        viewModel.IsExecutionComplete = true;
        viewModel.IsSuccess = true;
        viewModel.LastSummary = "done";

        viewModel.MergeAnother();

        Assert.Empty(viewModel.Files);
        Assert.False(viewModel.IsExecutionComplete);
        Assert.False(viewModel.IsSuccess);
        Assert.Null(viewModel.LastSummary);
        Assert.Equal("Merged.pdf", viewModel.OutputFileName);
    }

    private static MergeViewModel CreateViewModel(IJobExecutor? executor = null)
    {
        executor ??= new Mock<IJobExecutor>().Object;
        return new MergeViewModel(
            executor,
            new Mock<IPdfEngine>().Object,
            new StubFileDialogService(),
            NullLogger<MergeViewModel>.Instance);
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
