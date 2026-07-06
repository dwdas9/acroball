using Acroball.Application.Operations;
using Acroball.Domain;

namespace Acroball.Application.Jobs;

/// <summary>
/// Rotate-pages request executed via the shared job framework.
/// </summary>
public sealed class RotatePagesJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public RotatePagesJobRequest(
        string inputFile,
        string outputFile,
        IReadOnlyList<PageRange> ranges,
        Rotation rotation,
        string? password = null)
    {
        InputFile = inputFile;
        OutputFile = outputFile;
        Ranges = ranges;
        Rotation = rotation;
        Password = password;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>Pages to rotate.</summary>
    public IReadOnlyList<PageRange> Ranges { get; }

    /// <summary>Rotation to add to each selected page's current rotation.</summary>
    public Rotation Rotation { get; }

    /// <summary>Password for the source file, when encrypted.</summary>
    public string? Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Rotate Pages";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (!File.Exists(InputFile))
        {
            return $"\"{Path.GetFileName(InputFile)}\" could not be found.";
        }

        if (Ranges.Count == 0)
        {
            return "Choose at least one page to rotate.";
        }

        if (Rotation == Rotation.None)
        {
            return "Choose a rotation other than 0°.";
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(OutputFile)))
        {
            return "Choose an output file name.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public RotatePagesRequest ToEngineRequest() => new(InputFile, OutputFile, Ranges, Rotation, Password);
}
