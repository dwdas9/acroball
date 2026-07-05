namespace Acroball.Application.Jobs;

/// <summary>Outcome of a job execution.</summary>
public enum JobOutcome
{
    /// <summary>The job completed successfully.</summary>
    Succeeded,

    /// <summary>The job failed with an error.</summary>
    Failed,

    /// <summary>The job was cancelled.</summary>
    Cancelled,
}

/// <summary>Result returned by the shared job execution framework.</summary>
public sealed record JobExecutionResult(
    JobOutcome Outcome,
    string? ErrorMessage,
    string? OutputSummary,
    string? OutputPath,
    TimeSpan Elapsed,
    bool OpenOutputRequested = false)
{
    /// <summary>Whether the job completed successfully.</summary>
    public bool Succeeded => Outcome == JobOutcome.Succeeded;
}
