namespace Acroball.Domain;

/// <summary>
/// One entry in a PDF's outline (bookmark) tree.
/// </summary>
/// <param name="Title">The bookmark's display text.</param>
/// <param name="DestinationPageNumber">
/// 1-based page number this bookmark jumps to, or <see langword="null"/> when
/// the target isn't a simple in-document page (a named destination or an
/// external URI, neither of which this reads).
/// </param>
/// <param name="IsExpanded">Whether the outline entry is stored open by default.</param>
/// <param name="Children">Nested outline entries, in document order.</param>
public sealed record PdfOutlineNode(
    string Title,
    int? DestinationPageNumber,
    bool IsExpanded,
    IReadOnlyList<PdfOutlineNode> Children);
