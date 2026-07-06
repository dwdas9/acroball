using Acroball.Application.Operations;
using Acroball.Domain;

namespace Acroball.Application.Jobs;

/// <summary>
/// Extract-pages request executed via the shared job framework.
/// </summary>
public sealed class ExtractPagesJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public ExtractPagesJobRequest(
        string inputFile,
        string outputFile,
        IReadOnlyList<PageRange> ranges,
        string? password = null)
    {
        InputFile = inputFile;
        OutputFile = outputFile;
        Ranges = ranges;
        Password = password;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>Pages to extract, in the order given (duplicates allowed).</summary>
    public IReadOnlyList<PageRange> Ranges { get; }

    /// <summary>Password for the source file, when encrypted.</summary>
    public string? Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Extract Pages";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (!File.Exists(InputFile))
        {
            return $"\"{Path.GetFileName(InputFile)}\" could not be found.";
        }

        if (Ranges.Count == 0)
        {
            return "Choose at least one page to extract.";
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(OutputFile)))
        {
            return "Choose an output file name.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public ExtractPagesRequest ToEngineRequest() => new(InputFile, OutputFile, Ranges, Password);
}
