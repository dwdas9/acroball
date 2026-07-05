namespace Acroball.Domain;

/// <summary>
/// The document information dictionary of a PDF: title, author and friends.
/// </summary>
/// <param name="Title">Document title, or <see langword="null"/> when unset.</param>
/// <param name="Author">Document author, or <see langword="null"/> when unset.</param>
/// <param name="Subject">Document subject, or <see langword="null"/> when unset.</param>
/// <param name="Keywords">Free-form keyword string, or <see langword="null"/> when unset.</param>
/// <param name="Creator">The application that created the original document.</param>
/// <param name="Producer">The library that produced the PDF. Typically read-only in practice.</param>
/// <param name="CreationDate">When the document was created, if recorded.</param>
/// <param name="ModificationDate">When the document was last modified, if recorded.</param>
public sealed record DocumentMetadata(
    string? Title = null,
    string? Author = null,
    string? Subject = null,
    string? Keywords = null,
    string? Creator = null,
    string? Producer = null,
    DateTimeOffset? CreationDate = null,
    DateTimeOffset? ModificationDate = null)
{
    /// <summary>An empty metadata set.</summary>
    public static DocumentMetadata Empty { get; } = new();
}

