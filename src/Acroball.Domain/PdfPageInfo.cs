namespace Acroball.Domain;

/// <summary>
/// Geometry of a single page within a PDF document.
/// </summary>
/// <param name="PageNumber">1-based page number.</param>
/// <param name="WidthPoints">Media box width in PDF points (1/72 inch).</param>
/// <param name="HeightPoints">Media box height in PDF points (1/72 inch).</param>
/// <param name="Rotation">The page's stored rotation.</param>
public sealed record PdfPageInfo(
    int PageNumber,
    double WidthPoints,
    double HeightPoints,
    Rotation Rotation)
{
    /// <summary>
    /// <see langword="true"/> when the page presents wider than tall once its
    /// stored rotation is applied.
    /// </summary>
    public bool IsLandscape
    {
        get
        {
            var swapped = Rotation is Rotation.Clockwise90 or Rotation.CounterClockwise90;
            var effectiveWidth = swapped ? HeightPoints : WidthPoints;
            var effectiveHeight = swapped ? WidthPoints : HeightPoints;
            return effectiveWidth > effectiveHeight;
        }
    }
}

