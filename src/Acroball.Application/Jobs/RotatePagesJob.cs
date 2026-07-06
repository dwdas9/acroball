using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete rotate-pages job handoff for the shared framework.
/// </summary>
public sealed class RotatePagesJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the rotate-pages job.</summary>
    public RotatePagesJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the rotate flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(RotatePagesJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.RotatePagesAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            var totalPages = request.Ranges.Sum(r => r.Count);
            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Rotated {totalPages} page(s) by {request.Rotation.ToDegrees()}° in {Path.GetFileName(request.OutputFile)}",
                request.OutputFile,
                TimeSpan.Zero,
                OpenOutputRequested: true);
        }
        catch (PdfOperationException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfOperationException(
                "Access was denied while saving the rotated PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The rotated PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Rotate failed unexpectedly.", ex);
        }
    }
}
