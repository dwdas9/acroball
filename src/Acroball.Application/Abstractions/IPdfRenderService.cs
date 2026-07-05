namespace Acroball.Application.Abstractions;

/// <summary>
/// A rasterized page, PNG-encoded so the contract stays free of UI types.
/// </summary>
/// <param name="PageNumber">1-based page number that was rendered.</param>
/// <param name="WidthPx">Rendered bitmap width in pixels.</param>
/// <param name="HeightPx">Rendered bitmap height in pixels.</param>
/// <param name="EncodedPng">The PNG bytes.</param>
public sealed record RenderedPageImage(
    int PageNumber,
    int WidthPx,
    int HeightPx,
    byte[] EncodedPng);

/// <summary>
/// Rasterizes PDF pages for thumbnails and previews.
/// </summary>
/// <remarks>
/// The planned backend (PDFium) is not thread-safe; implementations must
/// serialize native access internally, and callers should treat rendering as
/// a queued, sequential resource. See ADR-0002.
/// </remarks>
public interface IPdfRenderService
{
    /// <summary>
    /// Renders one page to a bitmap whose width is <paramref name="targetWidthPx"/>
    /// pixels (height follows the page aspect ratio).
    /// </summary>
    Task<RenderedPageImage> RenderPageAsync(
        string filePath,
        int pageNumber,
        int targetWidthPx,
        string? password = null,
        CancellationToken cancellationToken = default);
}

