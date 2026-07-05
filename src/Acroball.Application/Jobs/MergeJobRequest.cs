using Acroball.Application.Operations;

namespace Acroball.Application.Jobs;

/// <summary>
/// Merge request executed via the shared job framework.
/// </summary>
public sealed class MergeJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public MergeJobRequest(IReadOnlyList<string> inputFiles, string outputFile, string? outputDirectory = null)
    {
        InputFiles = inputFiles;
        OutputFile = outputFile;
        OutputDirectory = outputDirectory;
    }

    /// <summary>Input files in merge order.</summary>
    public IReadOnlyList<string> InputFiles { get; }

    /// <summary>Output file path.</summary>
    public string OutputFile { get; }

    /// <summary>Directory that should receive the merged file when relevant.</summary>
    public string? OutputDirectory { get; }

    /// <inheritdoc />
    public override string DisplayName => "Merge PDFs";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (InputFiles.Count == 0)
        {
            return "Select at least one PDF file to merge.";
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var inputFile in InputFiles)
        {
            if (!seen.Add(inputFile))
            {
                return $"Remove duplicate file \"{Path.GetFileName(inputFile)}\" before merging.";
            }

            if (!File.Exists(inputFile))
            {
                return $"\"{Path.GetFileName(inputFile)}\" could not be found.";
            }
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(OutputFile)))
        {
            return "Choose an output file name.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public MergeRequest ToEngineRequest() => new(InputFiles, OutputFile);
}
