using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete extract-pages job handoff for the shared framework.
/// </summary>
public sealed class ExtractPagesJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the extract-pages job.</summary>
    public ExtractPagesJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the extraction flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(ExtractPagesJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.ExtractPagesAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            var outputInfo = await _pdfEngine.InspectAsync(request.OutputFile, cancellationToken: cancellationToken).ConfigureAwait(false);
            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Extracted {outputInfo.PageCount} page(s) into {Path.GetFileName(request.OutputFile)}",
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
                "Access was denied while saving the extracted PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The extracted PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Extraction failed unexpectedly.", ex);
        }
    }
}
