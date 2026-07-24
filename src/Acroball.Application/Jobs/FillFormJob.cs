using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete fill-form job handoff for the shared framework.
/// </summary>
public sealed class FillFormJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the fill-form job.</summary>
    public FillFormJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the fill-form flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(FillFormJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.FillFormAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Filled {request.Values.Count} field(s) into {Path.GetFileName(request.OutputFile)}",
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
                "Access was denied while saving the filled PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The filled PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Filling the form failed unexpectedly.", ex);
        }
    }
}
