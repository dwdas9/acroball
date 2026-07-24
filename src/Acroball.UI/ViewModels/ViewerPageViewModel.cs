using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Acroball.Domain;
using Acroball.Domain.Annotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Acroball.UI.ViewModels;

/// <summary>
/// One page in the Viewer's continuous scroll list. Geometry is known as soon
/// as the document opens; the bitmap is loaded lazily as the page's container
/// is realized by the virtualizing panel, and freed again when it derealizes.
/// </summary>
public sealed partial class ViewerPageViewModel : ObservableObject
{
    /// <summary>Fixed on-screen width every page is displayed at.</summary>
    public const int DisplayWidthPx = 900;

    /// <summary>Creates the page entry from its known geometry.</summary>
    public ViewerPageViewModel(PdfPageInfo info)
    {
        PageNumber = info.PageNumber;
        WidthPoints = info.WidthPoints;
        HeightPoints = info.HeightPoints;
        Rotation = info.Rotation;
    }

    /// <summary>1-based page number.</summary>
    public int PageNumber { get; }

    /// <summary>Media box width in PDF points.</summary>
    public double WidthPoints { get; }

    /// <summary>Media box height in PDF points.</summary>
    public double HeightPoints { get; }

    /// <summary>The page's stored rotation (already reflected by the rendered bitmap).</summary>
    public Rotation Rotation { get; }

    /// <summary>Annotations committed on this page, pending a save. PDF-space, ready to hand to <see cref="Acroball.Application.Operations.SaveAnnotationsRequest"/>.</summary>
    public ObservableCollection<AnnotationEdit> Annotations { get; } = [];

    /// <summary>Screen-space visual stand-ins for <see cref="Annotations"/>, one per entry, in the same order.</summary>
    public ObservableCollection<AnnotationPreviewShape> PreviewShapes { get; } = [];

    /// <summary>
    /// Reserved on-screen height at <see cref="DisplayWidthPx"/>, computed from
    /// geometry alone so the page's slot in the virtualized list never resizes
    /// once its bitmap arrives.
    /// </summary>
    public double PlaceholderHeightPx
    {
        get
        {
            var swapped = Rotation is Rotation.Clockwise90 or Rotation.CounterClockwise90;
            var width = swapped ? HeightPoints : WidthPoints;
            var height = swapped ? WidthPoints : HeightPoints;
            return width <= 0 ? DisplayWidthPx : DisplayWidthPx * (height / width);
        }
    }

    /// <summary>Rendered page bitmap, once loaded.</summary>
    [ObservableProperty]
    private Bitmap? _renderedImage;

    /// <summary>Whether the bitmap is currently being rendered.</summary>
    [ObservableProperty]
    private bool _isLoadingImage;

    /// <summary>Set when the page failed to render.</summary>
    [ObservableProperty]
    private string? _renderError;

    /// <summary>
    /// Cancellation for the in-flight render, if any. Owned and replaced by
    /// the view's container-realize/derealize handlers via
    /// <see cref="ViewerViewModel.LoadPageImageAsync"/>/<see cref="ViewerViewModel.UnloadPageImage"/>.
    /// </summary>
    public CancellationTokenSource? RenderCancellation { get; set; }
}
