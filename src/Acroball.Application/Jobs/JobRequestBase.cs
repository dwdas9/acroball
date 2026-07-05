namespace Acroball.Application.Jobs;

/// <summary>
/// Base type for all jobs executed through the shared framework.
/// </summary>
public abstract class JobRequestBase
{
    /// <summary>Human-readable job caption.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Optional validation error message.</summary>
    public virtual string? Validate() => null;
}
