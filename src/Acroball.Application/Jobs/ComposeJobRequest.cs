using Acroball.Application.Operations;

namespace Acroball.Application.Jobs;

/// <summary>
/// Compose request executed via the shared job framework: assembles a new
/// document from an explicit, possibly cross-file, page list. This is the
/// engine-facing primitive behind the visual page organizer.
/// </summary>
public sealed class ComposeJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public ComposeJobRequest(
        IReadOnlyList<PageAssignment> pages,
        string outputFile,
        IReadOnlyDictionary<string, string>? passwords = null)
    {
        Pages = pages;
        OutputFile = outputFile;
        Passwords = passwords;
    }

    /// <summary>The output pages, in order.</summary>
    public IReadOnlyList<PageAssignment> Pages { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>Optional per-source passwords, keyed by source file path.</summary>
    public IReadOnlyDictionary<string, string>? Passwords { get; }

    /// <inheritdoc />
    public override string DisplayName => "Organize";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (Pages.Count == 0)
        {
            return "Add at least one page.";
        }

        foreach (var sourceFile in Pages.Select(p => p.SourceFile).Distinct())
        {
            if (!File.Exists(sourceFile))
            {
                return $"\"{Path.GetFileName(sourceFile)}\" could not be found.";
            }
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(OutputFile)))
        {
            return "Choose an output file name.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public ComposeRequest ToEngineRequest() => new(Pages, OutputFile, Passwords);
}
