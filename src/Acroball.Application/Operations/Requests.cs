using Acroball.Domain;
using Acroball.Domain.Annotations;

namespace Acroball.Application.Operations;

/// <summary>Merge several PDFs, in order, into one output file.</summary>
/// <param name="InputFiles">Absolute paths of the source files, in merge order.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Passwords">Optional per-input passwords, keyed by input file path.</param>
public sealed record MergeRequest(
    IReadOnlyList<string> InputFiles,
    string OutputFile,
    IReadOnlyDictionary<string, string>? Passwords = null);

/// <summary>Split one PDF into several files, one per page range.</summary>
/// <param name="InputFile">Absolute path of the source file.</param>
/// <param name="OutputDirectory">Directory that receives the output files.</param>
/// <param name="Ranges">The ranges to write; each produces one output file.</param>
/// <param name="Password">Password for the source file, when encrypted.</param>
/// <param name="FileNameTemplate">
/// Template for output names. <c>{name}</c> is the source file name without
/// extension, <c>{index}</c> the 1-based range index, <c>{range}</c> the range text.
/// </param>
public sealed record SplitRequest(
    string InputFile,
    string OutputDirectory,
    IReadOnlyList<PageRange> Ranges,
    string? Password = null,
    string FileNameTemplate = "{name}-{index}");

/// <summary>Copy selected pages of a PDF into a new single file.</summary>
/// <param name="InputFile">Absolute path of the source file.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Ranges">Pages to extract, in the order given (duplicates allowed).</param>
/// <param name="Password">Password for the source file, when encrypted.</param>
public sealed record ExtractPagesRequest(
    string InputFile,
    string OutputFile,
    IReadOnlyList<PageRange> Ranges,
    string? Password = null);

/// <summary>Rotate selected pages of a PDF.</summary>
/// <param name="InputFile">Absolute path of the source file.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Ranges">Pages to rotate.</param>
/// <param name="Rotation">Rotation to add to each selected page's current rotation.</param>
/// <param name="Password">Password for the source file, when encrypted.</param>
public sealed record RotatePagesRequest(
    string InputFile,
    string OutputFile,
    IReadOnlyList<PageRange> Ranges,
    Rotation Rotation,
    string? Password = null);

/// <summary>One page in a composed output document.</summary>
/// <param name="SourceFile">Absolute path of the file the page comes from.</param>
/// <param name="SourcePageNumber">1-based page number within <paramref name="SourceFile"/>.</param>
/// <param name="RotationDelta">Extra rotation applied on top of the page's stored rotation.</param>
public sealed record PageAssignment(
    string SourceFile,
    int SourcePageNumber,
    Rotation RotationDelta = Rotation.None);

/// <summary>
/// Assemble a new PDF from an explicit page list, possibly spanning several
/// source files. This is the primitive behind the visual page organizer:
/// reorder, delete, duplicate and cross-document moves all reduce to it.
/// </summary>
/// <param name="Pages">The output pages, in order.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Passwords">Optional per-source passwords, keyed by source file path.</param>
public sealed record ComposeRequest(
    IReadOnlyList<PageAssignment> Pages,
    string OutputFile,
    IReadOnlyDictionary<string, string>? Passwords = null);

/// <summary>Encrypt a PDF with the given passwords and permissions.</summary>
/// <param name="InputFile">Absolute path of the source file.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Options">Passwords, permissions and algorithm strength.</param>
/// <param name="CurrentPassword">Password for the source file, when it is already encrypted.</param>
public sealed record EncryptRequest(
    string InputFile,
    string OutputFile,
    EncryptionOptions Options,
    string? CurrentPassword = null);

/// <summary>Remove encryption from a PDF.</summary>
/// <param name="InputFile">Absolute path of the encrypted source file.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Password">A password that opens the source file.</param>
public sealed record DecryptRequest(
    string InputFile,
    string OutputFile,
    string Password);

/// <summary>How aggressively <see cref="CompressRequest"/> may trade fidelity for size.</summary>
public enum CompressionProfile
{
    /// <summary>Only lossless work: recompress streams, drop unused objects.</summary>
    Lossless,

    /// <summary>Lossless work plus moderate image downsampling. The default.</summary>
    Balanced,

    /// <summary>Prioritize size; images may be visibly recompressed.</summary>
    Aggressive,
}

/// <summary>Rewrite a PDF to reduce its file size.</summary>
/// <param name="InputFile">Absolute path of the source file.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Profile">How aggressively to compress. See <see cref="CompressionProfile"/>.</param>
/// <param name="Password">Password for the source file, when encrypted.</param>
public sealed record CompressRequest(
    string InputFile,
    string OutputFile,
    CompressionProfile Profile = CompressionProfile.Balanced,
    string? Password = null);

/// <summary>Replace the document information dictionary of a PDF.</summary>
/// <param name="InputFile">Absolute path of the source file.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Metadata">The metadata to write.</param>
/// <param name="Password">Password for the source file, when encrypted.</param>
public sealed record UpdateMetadataRequest(
    string InputFile,
    string OutputFile,
    DocumentMetadata Metadata,
    string? Password = null);

/// <summary>Add annotations to a PDF, writing the result to a new file.</summary>
/// <param name="InputFile">Absolute path of the source file.</param>
/// <param name="OutputFile">Absolute path of the file to create.</param>
/// <param name="Annotations">The annotations to add, across one or more pages.</param>
/// <param name="Password">Password for the source file, when encrypted.</param>
public sealed record SaveAnnotationsRequest(
    string InputFile,
    string OutputFile,
    IReadOnlyList<AnnotationEdit> Annotations,
    string? Password = null);

