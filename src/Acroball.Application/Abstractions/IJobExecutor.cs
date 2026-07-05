using Acroball.Application.Jobs;

namespace Acroball.Application.Abstractions;

/// <summary>
/// Executes a validated job through a common pipeline that handles logging,
/// progress, cancellation and timing.
/// </summary>
public interface IJobExecutor
{
    /// <summary>
    /// Executes <paramref name="handler"/> for <paramref name="request"/>.
    /// </summary>
    Task<JobExecutionResult> ExecuteAsync<TRequest>(
        TRequest request,
        Func<TRequest, JobExecutionContext, CancellationToken, Task<JobExecutionResult>> handler,
        IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default)
        where TRequest : JobRequestBase;
}
