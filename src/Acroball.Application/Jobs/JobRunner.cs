using Acroball.Application.Abstractions;
using Acroball.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Acroball.Application.Jobs;

/// <summary>
/// Shared job execution pipeline used by business flows and view models.
/// </summary>
public sealed class JobRunner : IJobExecutor
{
    private readonly ILogger<JobRunner> _logger;

    /// <summary>Creates the runner.</summary>
    public JobRunner(ILogger<JobRunner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JobExecutionResult> ExecuteAsync<TRequest>(
        TRequest request,
        Func<TRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>> handler,
        IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default)
        where TRequest : JobRequestBase
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        var validationMessage = request.Validate();
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            _logger.LogWarning("Job {JobName} failed validation: {ValidationMessage}", request.DisplayName, validationMessage);
            return new JobExecutionResult(JobOutcome.Failed, validationMessage, null, null, TimeSpan.Zero);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new JobExecutionResult(JobOutcome.Cancelled, "Cancelled before execution started.", null, null, TimeSpan.Zero);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var context = new JobExecutionContext
        {
            CancellationToken = cancellationToken,
            Progress = progress,
            StartedAt = startedAt,
        };

        try
        {
            var result = await handler(request, context, cancellationToken).ConfigureAwait(false);
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            return result with { Elapsed = elapsed };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Job {JobName} cancelled", request.DisplayName);
            return new JobExecutionResult(JobOutcome.Cancelled, "The operation was cancelled.", null, null, DateTimeOffset.UtcNow - startedAt);
        }
        catch (PdfOperationException ex)
        {
            _logger.LogWarning(ex, "Job {JobName} failed with a PDF operation error", request.DisplayName);
            return new JobExecutionResult(JobOutcome.Failed, ex.Message, null, null, DateTimeOffset.UtcNow - startedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobName} failed", request.DisplayName);
            return new JobExecutionResult(JobOutcome.Failed, "The operation failed unexpectedly.", null, null, DateTime.UtcNow - startedAt);
        }
    }
}
