using Acroball.Application.Operations;

namespace Acroball.Application.Jobs;

/// <summary>
/// Compress request executed via the shared job framework.
/// </summary>
public sealed class CompressJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public CompressJobRequest(
        string inputFile,
        string outputFile,
        CompressionProfile profile = CompressionProfile.Balanced,
        string? password = null)
    {
        InputFile = inputFile;
        OutputFile = outputFile;
        Profile = profile;
        Password = password;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>How aggressively to compress.</summary>
    public CompressionProfile Profile { get; }

    /// <summary>Password for the source file, when encrypted.</summary>
    public string? Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Compress";

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
    public CompressRequest ToEngineRequest() => new(InputFile, OutputFile, Profile, Password);
}
