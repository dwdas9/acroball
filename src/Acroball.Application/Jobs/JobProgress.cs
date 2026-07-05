namespace Acroball.Application.Jobs;

/// <summary>
/// Progress update emitted while a job is executing.
/// </summary>
/// <param name="Fraction">Completion fraction in the range 0.0 to 1.0.</param>
/// <param name="Message">Optional status message.</param>
public readonly record struct JobProgress(double Fraction, string? Message = null);
