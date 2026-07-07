using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using PDFtoImage.Exceptions;
using SkiaSharp;
using Acroball.Application.Abstractions;
using Acroball.Domain.Exceptions;

namespace Acroball.Infrastructure.Pdf;

/// <summary>
/// <see cref="IPdfRenderService"/> implemented over PDFtoImage/PDFium (ADR-0002,
/// ADR-0010).
/// </summary>
/// <remarks>
/// PDFium is not thread-safe, so every render call is serialized behind a
/// single-slot <see cref="SemaphoreSlim"/>; concurrent requests queue rather
/// than racing the native library. The <see cref="SupportedOSPlatformAttribute"/>
/// set below matches exactly the desktop platforms Avalonia ships on
/// (ADR-0003) and is what lets this class call PDFtoImage's platform-gated
/// APIs; the same attributes must accompany the DI registration call site in
/// <c>InfrastructureServiceCollectionExtensions</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("linux")]
public sealed class PdfRenderService : IPdfRenderService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<PdfRenderService> _logger;

    /// <summary>Creates the render service.</summary>
    public PdfRenderService(ILogger<PdfRenderService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RenderedPageImage> RenderPageAsync(
        string filePath,
        int pageNumber,
        int targetWidthPx,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Input PDF was not found.", filePath);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => RenderCore(filePath, pageNumber, targetWidthPx, password),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private RenderedPageImage RenderCore(string filePath, int pageNumber, int targetWidthPx, string? password)
    {
        var bytes = File.ReadAllBytes(filePath);
        var options = new RenderOptions(Width: targetWidthPx, WithAspectRatio: true);

        try
        {
            using var bitmap = Conversion.ToImage(bytes, page: pageNumber - 1, password: password, options: options);
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return new RenderedPageImage(pageNumber, bitmap.Width, bitmap.Height, png.ToArray());
        }
        catch (PdfPasswordProtectedException ex)
        {
            _logger.LogDebug(ex, "Password required to render {Path}", filePath);
            throw new InvalidPdfPasswordException(filePath);
        }
        catch (PdfPageNotFoundException ex)
        {
            throw new PdfOperationException($"Page {pageNumber} does not exist in \"{Path.GetFileName(filePath)}\".", ex);
        }
        catch (PdfException ex)
        {
            throw new CorruptPdfException(filePath, ex);
        }
    }
}
