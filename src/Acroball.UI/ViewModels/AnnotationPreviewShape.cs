using Avalonia;
using Avalonia.Media;
using Acroball.Domain.Annotations;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Screen-space rendering data for one committed annotation, captured
/// directly from the pointer interaction that created it (in parallel with
/// the PDF-space <see cref="AnnotationEdit"/> built from the same
/// interaction — one is never derived from the other). Rasterization of the
/// real annotation only happens once, on save; this is a lightweight visual
/// stand-in so the user can see what they've drawn in the meantime.
/// </summary>
public sealed class AnnotationPreviewShape
{
    /// <summary>The kind of annotation this previews.</summary>
    public required AnnotationKind Kind { get; init; }

    /// <summary>Left edge, in display pixels, for Highlight/FreeText/Square.</summary>
    public double Left { get; init; }

    /// <summary>Top edge, in display pixels, for Highlight/FreeText/Square.</summary>
    public double Top { get; init; }

    /// <summary>Width, in display pixels, for Highlight/FreeText/Square.</summary>
    public double Width { get; init; }

    /// <summary>Height, in display pixels, for Highlight/FreeText/Square.</summary>
    public double Height { get; init; }

    /// <summary>Stroke points, in display pixels, for Ink.</summary>
    public Points? Points { get; init; }

    /// <summary>The typed text, for FreeText.</summary>
    public string? Text { get; init; }

    /// <summary>Border/stroke brush.</summary>
    public IBrush? Stroke { get; init; }

    /// <summary>Fill brush (translucent for Highlight, null for an outline-only shape).</summary>
    public IBrush? Fill { get; init; }

    /// <summary>Whether this previews as a positioned box (Highlight/FreeText/Square).</summary>
    public bool IsBox => Kind != AnnotationKind.Ink;

    /// <summary>Whether this previews as a polyline (Ink).</summary>
    public bool IsPolyline => Kind == AnnotationKind.Ink;

    /// <summary>Whether this previews with a text label (FreeText).</summary>
    public bool IsFreeText => Kind == AnnotationKind.FreeText;
}
