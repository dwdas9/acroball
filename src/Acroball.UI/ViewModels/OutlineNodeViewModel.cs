using Acroball.Domain;

namespace Acroball.UI.ViewModels;

/// <summary>
/// Binding wrapper around one <see cref="PdfOutlineNode"/> for the Viewer's
/// bookmark tree. Purely presentational — navigation-only, no editing.
/// </summary>
public sealed class OutlineNodeViewModel
{
    /// <summary>Wraps <paramref name="node"/> and its descendants.</summary>
    public OutlineNodeViewModel(PdfOutlineNode node)
    {
        Title = node.Title;
        DestinationPageNumber = node.DestinationPageNumber;
        Children = node.Children.Select(child => new OutlineNodeViewModel(child)).ToList();
    }

    /// <summary>The bookmark's display text.</summary>
    public string Title { get; }

    /// <summary>1-based target page, or null when the bookmark doesn't resolve to a page in this document.</summary>
    public int? DestinationPageNumber { get; }

    /// <summary>Nested bookmarks, in document order.</summary>
    public IReadOnlyList<OutlineNodeViewModel> Children { get; }
}
