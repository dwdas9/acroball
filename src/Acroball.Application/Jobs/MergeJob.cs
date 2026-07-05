using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete merge job handoff for the shared framework.
/// </summary>
public sealed class MergeJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the merge job.</summary>
    public MergeJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the merge flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(MergeJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.MergeAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            var mergedInfo = await _pdfEngine.InspectAsync(request.OutputFile, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Merged {request.InputFiles.Count} file(s) into {Path.GetFileName(request.OutputFile)} ({mergedInfo.PageCount} pages)",
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
                "Access was denied while saving the merged PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The merged PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Merge failed unexpectedly.", ex);
        }
    }
}
