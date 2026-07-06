using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete split job handoff for the shared framework.
/// </summary>
public sealed class SplitJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the split job.</summary>
    public SplitJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the split flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(SplitJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.SplitAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Split into {request.Ranges.Count} file(s) in {request.OutputDirectory}",
                request.OutputDirectory,
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
                "Access was denied while saving the split PDFs. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The split PDFs could not be written. The folder may be read-only or a file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Split failed unexpectedly.", ex);
        }
    }
}
