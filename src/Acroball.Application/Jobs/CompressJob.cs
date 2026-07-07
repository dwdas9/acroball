using Acroball.Application.Abstractions;
using Acroball.Application.Operations;
using Acroball.Domain.Exceptions;

namespace Acroball.Application.Jobs;

/// <summary>
/// Concrete compress job handoff for the shared framework.
/// </summary>
public sealed class CompressJob
{
    private readonly IPdfEngine _pdfEngine;

    /// <summary>Creates the compress job.</summary>
    public CompressJob(IPdfEngine pdfEngine)
    {
        _pdfEngine = pdfEngine;
    }

    /// <summary>Executes the compress flow for <paramref name="request"/>.</summary>
    public async Task<JobExecutionResult> ExecuteAsync(CompressJobRequest request, JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var originalSize = new FileInfo(request.InputFile).Length;
            var engineRequest = request.ToEngineRequest();
            await _pdfEngine.CompressAsync(
                engineRequest,
                new Progress<OperationProgress>(progress =>
                {
                    context.Progress?.Report(new JobProgress(progress.Fraction, progress.Message));
                }),
                cancellationToken).ConfigureAwait(false);

            var newSize = new FileInfo(request.OutputFile).Length;
            return new JobExecutionResult(
                JobOutcome.Succeeded,
                null,
                BuildSummary(request.OutputFile, originalSize, newSize),
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
                "Access was denied while saving the compressed PDF. Choose a different output folder or check permissions.",
                ex);
        }
        catch (IOException ex)
        {
            throw new PdfOperationException(
                "The compressed PDF could not be written. The folder may be read-only or the file may be in use.",
                ex);
        }
        catch (Exception ex)
        {
            throw new PdfOperationException("Compress failed unexpectedly.", ex);
        }
    }

    private static string BuildSummary(string outputFile, long originalSize, long newSize)
    {
        var fileName = Path.GetFileName(outputFile);
        if (newSize >= originalSize)
        {
            return $"Wrote {fileName}. No further reduction was possible for this file.";
        }

        var savedPercent = (1.0 - (double)newSize / originalSize) * 100.0;
        return $"Wrote {fileName}: {FormatBytes(originalSize)} → {FormatBytes(newSize)} ({savedPercent:0}% smaller)";
    }

    private static string FormatBytes(long bytes)
    {
        const double Kb = 1024;
        const double Mb = Kb * 1024;

        return bytes switch
        {
            >= (long)Mb => $"{bytes / Mb:0.#} MB",
            >= (long)Kb => $"{bytes / Kb:0.#} KB",
            _ => $"{bytes} B",
        };
    }
}
