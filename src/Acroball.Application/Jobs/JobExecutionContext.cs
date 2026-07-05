namespace Acroball.Application.Jobs;

/// <summary>
/// Context passed to a job handler so it can report progress and understand the
/// execution environment.
/// </summary>
public sealed class JobExecutionContext
{
    /// <summary>Cancellation token for the active execution.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Optional progress reporter.</summary>
    public IProgress<JobProgress>? Progress { get; init; }

    /// <summary>Clock started at job execution begin.</summary>
    public DateTimeOffset StartedAt { get; init; }
}
