using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete encrypt (protect) job handoff for the shared framework.
/// </summary>
public sealed class EncryptJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the encrypt job.</summary>
    public EncryptJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the encrypt flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(EncryptJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.EncryptAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            var strengthLabel = request.Options.Strength == EncryptionStrength.Aes256 ? 256 : 128;
            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                $"Encrypted {Path.GetFileName(request.OutputFile)} with AES-{strengthLabel}",
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
                "Access was denied while saving the protected PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The protected PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Protection failed unexpectedly.", ex);
        }
    }
}
