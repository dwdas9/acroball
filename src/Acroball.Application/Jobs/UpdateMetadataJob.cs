using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete update-metadata job handoff for the shared framework.
/// </summary>
public sealed class UpdateMetadataJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the update-metadata job.</summary>
    public UpdateMetadataJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the metadata update flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(UpdateMetadataJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.UpdateMetadataAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Updated metadata for {Path.GetFileName(request.OutputFile)}",
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
                "Access was denied while saving the updated PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The updated PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Metadata update failed unexpectedly.", ex);
        }
    }
}
