using Acroball.Domain;

namespace Acroball.UI.Services;

/// <summary>
/// Maps a pointer position on the Viewer's rendered page bitmap (display
/// pixels, top-left origin) to PDF page space (points, bottom-left origin,
/// unrotated) — the coordinate space every
/// <see cref="Acroball.Domain.Annotations.AnnotationEdit"/> is defined in.
/// Because PDFium bakes the page's stored rotation into the rendered bitmap
/// (ADR-0011), this inverts that rotation, not just scale — see ADR-0013 for
/// the corner-by-corner derivation this implements.
/// </summary>
public static class AnnotationCoordinateMapper
{
    /// <summary>Converts one point from display pixels to PDF points.</summary>
    public static (double X, double Y) ToPdfPoint(
        double screenX,
        double screenY,
        double displayWidthPx,
        double pageWidthPoints,
        double pageHeightPoints,
        Rotation rotation)
    {
        var swapped = rotation is Rotation.Clockwise90 or Rotation.CounterClockwise90;
        var effectiveWidth = swapped ? pageHeightPoints : pageWidthPoints;
        var scale = displayWidthPx / effectiveWidth;

        return rotation switch
        {
            Rotation.Clockwise90 => (screenY / scale, screenX / scale),
            Rotation.Rotate180 => (pageWidthPoints - (screenX / scale), screenY / scale),
            Rotation.CounterClockwise90 => (pageWidthPoints - (screenY / scale), pageHeightPoints - (screenX / scale)),
            _ => (screenX / scale, pageHeightPoints - (screenY / scale)),
        };
    }
}
