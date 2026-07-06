using Acroball.Application.Operations;
using Acroball.Domain;

namespace Acroball.Application.Jobs;

/// <summary>
/// Split request executed via the shared job framework.
/// </summary>
public sealed class SplitJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public SplitJobRequest(
        string inputFile,
        string outputDirectory,
        IReadOnlyList<PageRange> ranges,
        string fileNameTemplate = "{name}-{index}",
        string? password = null)
    {
        InputFile = inputFile;
        OutputDirectory = outputDirectory;
        Ranges = ranges;
        FileNameTemplate = fileNameTemplate;
        Password = password;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>Directory that receives the output files.</summary>
    public string OutputDirectory { get; }

    /// <summary>The ranges to write; each produces one output file.</summary>
    public IReadOnlyList<PageRange> Ranges { get; }

    /// <summary>Template for output file names.</summary>
    public string FileNameTemplate { get; }

    /// <summary>Password for the source file, when encrypted.</summary>
    public string? Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Split PDF";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (!File.Exists(InputFile))
        {
            return $"\"{Path.GetFileName(InputFile)}\" could not be found.";
        }

        if (Ranges.Count == 0)
        {
            return "Choose at least one page range to split.";
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            return "Choose an output folder.";
        }

        if (string.IsNullOrWhiteSpace(FileNameTemplate))
        {
            return "Choose a file name template.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public SplitRequest ToEngineRequest() => new(InputFile, OutputDirectory, Ranges, Password, FileNameTemplate);
}
