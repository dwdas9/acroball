using Acroball.Application.Operations;
using Acroball.Domain;

namespace Acroball.Application.Jobs;

/// <summary>
/// Update-metadata request executed via the shared job framework.
/// </summary>
public sealed class UpdateMetadataJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public UpdateMetadataJobRequest(
        string inputFile,
        string outputFile,
        DocumentMetadata metadata,
        string? password = null)
    {
        InputFile = inputFile;
        OutputFile = outputFile;
        Metadata = metadata;
        Password = password;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>The metadata to write.</summary>
    public DocumentMetadata Metadata { get; }

    /// <summary>Password for the source file, when encrypted.</summary>
    public string? Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Update Metadata";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (!File.Exists(InputFile))
        {
            return $"\"{Path.GetFileName(InputFile)}\" could not be found.";
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(OutputFile)))
        {
            return "Choose an output file name.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public UpdateMetadataRequest ToEngineRequest() => new(InputFile, OutputFile, Metadata, Password);
}
