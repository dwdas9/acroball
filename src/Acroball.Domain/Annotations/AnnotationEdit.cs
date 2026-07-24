namespace Acroball.Domain.Annotations;

/// <summary>The kind of markup an <see cref="AnnotationEdit"/> represents.</summary>
public enum AnnotationKind
{
    /// <summary>A translucent fill over one or more quadrilaterals, typically over text.</summary>
    Highlight,

    /// <summary>A text box drawn at a fixed position and size.</summary>
    FreeText,

    /// <summary>One or more freehand strokes.</summary>
    Ink,

    /// <summary>A stroked (and optionally filled) rectangle.</summary>
    Square,
}

/// <summary>An RGB color, kept free of any UI framework's color type.</summary>
public sealed record AnnotationColor(byte R, byte G, byte B);

/// <summary>
/// Four corner points of one highlighted quadrilateral, in PDF-spec order:
/// (X1,Y1)/(X2,Y2) the top edge, (X3,Y3)/(X4,Y4) the bottom edge.
/// </summary>
public sealed record QuadPoints(
    double X1, double Y1,
    double X2, double Y2,
    double X3, double Y3,
    double X4, double Y4);

/// <summary>One point along an ink stroke.</summary>
public sealed record InkPoint(double X, double Y);

/// <summary>One continuous freehand stroke, as an ordered list of points.</summary>
public sealed record InkStroke(IReadOnlyList<InkPoint> Points);

/// <summary>
/// One annotation to add to a document. All coordinates are PDF points,
/// page-local, bottom-left origin, in unrotated page space — callers are
/// responsible for any screen-to-page mapping before constructing one of these.
/// </summary>
/// <param name="PageNumber">1-based page this annotation is placed on.</param>
public abstract record AnnotationEdit(int PageNumber);

/// <summary>A translucent highlight over one or more quadrilaterals.</summary>
public sealed record HighlightAnnotationEdit(
    int PageNumber,
    IReadOnlyList<QuadPoints> Quads,
    AnnotationColor Color,
    double Opacity = 0.4) : AnnotationEdit(PageNumber);

/// <summary>A text box with a fixed position, size and font size.</summary>
public sealed record FreeTextAnnotationEdit(
    int PageNumber,
    double X,
    double Y,
    double Width,
    double Height,
    string Text,
    AnnotationColor Color,
    double FontSize = 12) : AnnotationEdit(PageNumber);

/// <summary>One or more freehand strokes.</summary>
public sealed record InkAnnotationEdit(
    int PageNumber,
    IReadOnlyList<InkStroke> Strokes,
    AnnotationColor Color,
    double StrokeWidthPoints = 1.5) : AnnotationEdit(PageNumber);

/// <summary>A stroked, optionally filled, rectangle.</summary>
public sealed record SquareAnnotationEdit(
    int PageNumber,
    double X,
    double Y,
    double Width,
    double Height,
    AnnotationColor Color,
    AnnotationColor? FillColor = null,
    double StrokeWidthPoints = 1.5) : AnnotationEdit(PageNumber);
