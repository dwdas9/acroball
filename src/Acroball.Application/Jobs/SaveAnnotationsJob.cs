using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete save-annotations job handoff for the shared framework.
/// </summary>
public sealed class SaveAnnotationsJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the save-annotations job.</summary>
    public SaveAnnotationsJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the save-annotations flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(SaveAnnotationsJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.SaveAnnotationsAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Added {request.Annotations.Count} annotation(s) to {Path.GetFileName(request.OutputFile)}",
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
                "Access was denied while saving the annotated PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The annotated PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Saving annotations failed unexpectedly.", ex);
        }
    }
}
