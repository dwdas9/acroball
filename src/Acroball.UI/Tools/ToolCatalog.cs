namespace Acroball.UI.Tools;

/// <summary>
/// Describes one tool in the catalog.
/// </summary>
/// <param name="Id">Stable machine id used for navigation and shortcuts.</param>
/// <param name="Label">Short display name.</param>
/// <param name="Description">One-line description shown on the home card.</param>
/// <param name="IconKey">Icon key resolved against Theme/Icons.axaml.</param>
/// <param name="Milestone">The milestone that delivers the tool, e.g. <c>"M2"</c>.</param>
public sealed record ToolDefinition(
    string Id,
    string Label,
    string Description,
    string IconKey,
    string Milestone);

/// <summary>
/// The built-in tool catalog. Static in Milestone 1; plugin-contributed tools
/// will extend this through the SDK later (ADR-0005).
/// </summary>
public static class ToolCatalog
{
    /// <summary>All built-in tools, in home-screen order.</summary>
    public static IReadOnlyList<ToolDefinition> All { get; } =
    [
        new("viewer", "Viewer", "Browse a PDF's pages in a continuous scroll.", "Viewer", "M6"),
        new("merge", "Merge", "Combine several PDFs into one, in the order you choose.", "Merge", "M3"),
        new("split", "Split", "Break a PDF apart by page ranges into separate files.", "Split", "M2"),
        new("organize", "Organize", "Reorder, rotate, delete and move pages visually.", "Organize", "M3"),
        new("rotate", "Rotate", "Rotate all pages or a selection, in 90Â° steps.", "Rotate", "M2"),
        new("extract", "Extract", "Pull selected pages out into a new document.", "Extract", "M2"),
        new("compress", "Compress", "Shrink file size with control over quality.", "Compress", "M4"),
        new("protect", "Protect", "Add or remove passwords and set permissions.", "Lock", "M4"),
        new("metadata", "Metadata", "View and edit title, author and document info.", "Tag", "M4"),
        new("fill-form", "Fill Form", "Fill in AcroForm fields and save a completed copy.", "Form", "M7"),
    ];

    /// <summary>Returns the tool with the given id.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when no tool has that id.</exception>
    public static ToolDefinition GetById(string id)
        => All.FirstOrDefault(t => t.Id == id)
           ?? throw new KeyNotFoundException($"Unknown tool id \"{id}\".");
}

