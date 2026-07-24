using Avalonia.Media.Imaging;
using Acroball.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Acroball.UI.ViewModels;

/// <summary>
/// One tile in the Organize page grid: a single page from a single source
/// file, with its own rotation and thumbnail loading state.
/// </summary>
public sealed partial class OrganizePageViewModel : ObservableObject
{
    /// <summary>Creates the tile.</summary>
    public OrganizePageViewModel(string sourceFile, int sourcePageNumber)
    {
        SourceFile = sourceFile;
        SourcePageNumber = sourcePageNumber;
    }

    /// <summary>Stable identity for drag-and-drop and removal, independent of list position.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Absolute path of the file this page comes from.</summary>
    public string SourceFile { get; }

    /// <summary>1-based page number within <see cref="SourceFile"/>.</summary>
    public int SourcePageNumber { get; }

    /// <summary>Cancels this tile's in-flight thumbnail render, if any, when the tile is removed.</summary>
    public CancellationTokenSource ThumbnailCancellation { get; } = new();

    /// <summary>Extra rotation applied on top of the page's stored rotation.</summary>
    [ObservableProperty]
    private Rotation _rotationDelta;

    /// <summary>Rendered thumbnail, once loaded.</summary>
    [ObservableProperty]
    private Bitmap? _thumbnail;

    /// <summary>Whether the thumbnail is currently being rendered.</summary>
    [ObservableProperty]
    private bool _isLoadingThumbnail = true;

    /// <summary>Set when the thumbnail failed to render.</summary>
    [ObservableProperty]
    private string? _thumbnailError;

    /// <summary>File name, for the tile caption.</summary>
    public string SourceFileName => Path.GetFileName(SourceFile);

    /// <summary>Clockwise angle, in degrees, to apply to the thumbnail visual.</summary>
    public double RotationAngle => RotationDelta.ToDegrees();

    partial void OnRotationDeltaChanged(Rotation value)
        => OnPropertyChanged(nameof(RotationAngle));
}
