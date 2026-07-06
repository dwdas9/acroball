using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete decrypt (remove password) job handoff for the shared framework.
/// </summary>
public sealed class DecryptJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the decrypt job.</summary>
    public DecryptJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the decrypt flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(DecryptJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.DecryptAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Removed password protection from {Path.GetFileName(request.OutputFile)}",
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
                "Access was denied while saving the unprotected PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The unprotected PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Password removal failed unexpectedly.", ex);
        }
    }
}
