using Acroball.Application.Operations;
using Acroball.Domain;

namespace Acroball.Application.Jobs;

/// <summary>
/// Encrypt (protect) request executed via the shared job framework.
/// </summary>
public sealed class EncryptJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public EncryptJobRequest(
        string inputFile,
        string outputFile,
        EncryptionOptions options,
        string? currentPassword = null)
    {
        InputFile = inputFile;
        OutputFile = outputFile;
        Options = options;
        CurrentPassword = currentPassword;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>Passwords, permissions and algorithm strength to apply.</summary>
    public EncryptionOptions Options { get; }

    /// <summary>Password for the source file, when it is already encrypted.</summary>
    public string? CurrentPassword { get; }

    /// <inheritdoc />
    public override string DisplayName => "Protect PDF";

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

        if (!Options.HasAnyPassword)
        {
            return "Set a user password, an owner password, or both.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public EncryptRequest ToEngineRequest() => new(InputFile, OutputFile, Options, CurrentPassword);
}
