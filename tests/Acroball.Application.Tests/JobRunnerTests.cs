using Acroball.Application.Jobs;
using Acroball.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Acroball.Application.Tests;

public sealed class JobRunnerTests
{
    [Fact]
    public async Task Executes_successful_job_and_returns_result()
    {
        var runner = new JobRunner(NullLogger<JobRunner>.Instance);
        var request = new TestJobRequest("demo");

        var result = await runner.ExecuteAsync(
            request,
            static (job, context, cancellationToken) => Task.FromResult(new JobExecutionResult(JobOutcome.Succeeded, null, job.Name, null, TimeSpan.Zero)),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("demo", result.OutputSummary);
        Assert.True(result.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Fails_when_validation_fails()
    {
        var runner = new JobRunner(NullLogger<JobRunner>.Instance);
        var request = new TestJobRequest(string.Empty);

        var result = await runner.ExecuteAsync(
            request,
            static (job, context, cancellationToken) => Task.FromResult(new JobExecutionResult(JobOutcome.Succeeded, null, job.Name, null, TimeSpan.Zero)),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("Validation", result.ErrorMessage);
    }

    [Fact]
    public async Task Preserves_pdf_operation_error_messages()
    {
        var runner = new JobRunner(NullLogger<JobRunner>.Instance);
        var request = new TestJobRequest("demo");

        var result = await runner.ExecuteAsync(
            request,
            static (_, _, _) => throw new PdfOperationException("Password is required."),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Password is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task Cancels_and_reports_cancellation()
    {
        var runner = new JobRunner(NullLogger<JobRunner>.Instance);
        var request = new TestJobRequest("demo");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await runner.ExecuteAsync(
            request,
            static (job, context, cancellationToken) => Task.FromResult(new JobExecutionResult(JobOutcome.Succeeded, null, job.Name, null, TimeSpan.Zero)),
            cancellationToken: cts.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(JobOutcome.Cancelled, result.Outcome);
    }

    private sealed class TestJobRequest : JobRequestBase
    {
        public TestJobRequest(string name) => Name = name;

        public string Name { get; }

        public override string DisplayName => "Test job";

        public override string? Validate() => string.IsNullOrWhiteSpace(Name) ? "Validation failed" : null;
    }
}
