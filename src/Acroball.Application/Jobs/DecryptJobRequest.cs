using Acroball.Application.Operations;

namespace Acroball.Application.Jobs;

/// <summary>
/// Decrypt (remove password) request executed via the shared job framework.
/// </summary>
public sealed class DecryptJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public DecryptJobRequest(string inputFile, string outputFile, string password)
    {
        InputFile = inputFile;
        OutputFile = outputFile;
        Password = password;
    }

    /// <summary>Absolute path of the encrypted source file.</summary>
    public string InputFile { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>A password that opens the source file.</summary>
    public string Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Remove PDF Password";

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

        if (string.IsNullOrEmpty(Password))
        {
            return "Enter the file's current password.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public DecryptRequest ToEngineRequest() => new(InputFile, OutputFile, Password);
}
