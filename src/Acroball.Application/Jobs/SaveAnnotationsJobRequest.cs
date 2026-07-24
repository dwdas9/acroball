using Acroball.Domain.Annotations;
using Acroball.Application.Operations;

namespace Acroball.Application.Jobs;

/// <summary>
/// Save-annotations request executed via the shared job framework: writes an
/// explicit set of annotations, across one or more pages, into a new copy of
/// the source document.
/// </summary>
public sealed class SaveAnnotationsJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public SaveAnnotationsJobRequest(
        string inputFile,
        IReadOnlyList<AnnotationEdit> annotations,
        string outputFile,
        string? password = null)
    {
        InputFile = inputFile;
        Annotations = annotations;
        OutputFile = outputFile;
        Password = password;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>The annotations to add, in order.</summary>
    public IReadOnlyList<AnnotationEdit> Annotations { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>Password for the source file, when encrypted.</summary>
    public string? Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Save Annotations";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (!File.Exists(InputFile))
        {
            return $"\"{Path.GetFileName(InputFile)}\" could not be found.";
        }

        if (Annotations.Count == 0)
        {
            return "Add at least one annotation.";
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(OutputFile)))
        {
            return "Choose an output file name.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public SaveAnnotationsRequest ToEngineRequest() => new(InputFile, OutputFile, Annotations, Password);
}
