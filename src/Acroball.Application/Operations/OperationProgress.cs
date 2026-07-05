namespace Acroball.Application.Operations;

/// <summary>
/// Progress of a long-running PDF operation.
/// </summary>
/// <param name="Fraction">Completion in the range 0.0â€“1.0.</param>
/// <param name="Message">Optional short status, e.g. <c>"Merging report.pdf (2/5)"</c>.</param>
public readonly record struct OperationProgress(double Fraction, string? Message = null);

