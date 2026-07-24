using Acroball.Domain.Forms;
using Acroball.Application.Operations;

namespace Acroball.Application.Jobs;

/// <summary>
/// Fill-form request executed via the shared job framework: writes an
/// explicit set of field values into a new copy of the source document.
/// </summary>
public sealed class FillFormJobRequest : JobRequestBase
{
    /// <summary>Creates the request.</summary>
    public FillFormJobRequest(
        string inputFile,
        IReadOnlyList<FormFieldValue> values,
        string outputFile,
        bool flattenAfterFill = false,
        string? password = null)
    {
        InputFile = inputFile;
        Values = values;
        OutputFile = outputFile;
        FlattenAfterFill = flattenAfterFill;
        Password = password;
    }

    /// <summary>Absolute path of the source file.</summary>
    public string InputFile { get; }

    /// <summary>The field values to write.</summary>
    public IReadOnlyList<FormFieldValue> Values { get; }

    /// <summary>Absolute path of the file to create.</summary>
    public string OutputFile { get; }

    /// <summary>When true, every field is marked read-only after filling.</summary>
    public bool FlattenAfterFill { get; }

    /// <summary>Password for the source file, when encrypted.</summary>
    public string? Password { get; }

    /// <inheritdoc />
    public override string DisplayName => "Fill Form";

    /// <inheritdoc />
    public override string? Validate()
    {
        if (!File.Exists(InputFile))
        {
            return $"\"{Path.GetFileName(InputFile)}\" could not be found.";
        }

        if (Values.Count == 0)
        {
            return "Enter at least one field value.";
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(OutputFile)))
        {
            return "Choose an output file name.";
        }

        return null;
    }

    /// <summary>Builds the underlying engine request.</summary>
    public FillFormRequest ToEngineRequest() => new(InputFile, OutputFile, Values, FlattenAfterFill, Password);
}
