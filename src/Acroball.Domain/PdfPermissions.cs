namespace Acroball.Domain;

/// <summary>
/// User-access permissions that can be granted on an encrypted PDF.
/// </summary>
/// <remarks>
/// These map onto the permission bits defined by the PDF specification
/// (ISO 32000, table 22). The mapping to concrete bit positions is the
/// responsibility of the PDF backend.
/// </remarks>
[Flags]
public enum PdfPermissions
{
    /// <summary>No permissions granted.</summary>
    None = 0,

    /// <summary>Print the document (possibly at reduced quality).</summary>
    Print = 1 << 0,

    /// <summary>Modify the document's contents.</summary>
    ModifyContents = 1 << 1,

    /// <summary>Copy or otherwise extract text and graphics.</summary>
    CopyContents = 1 << 2,

    /// <summary>Add or modify annotations.</summary>
    Annotate = 1 << 3,

    /// <summary>Fill in existing interactive form fields.</summary>
    FillForms = 1 << 4,

    /// <summary>Extract text and graphics in support of accessibility.</summary>
    ExtractForAccessibility = 1 << 5,

    /// <summary>Assemble the document: insert, rotate or delete pages.</summary>
    AssembleDocument = 1 << 6,

    /// <summary>Print at full quality.</summary>
    PrintHighQuality = 1 << 7,

    /// <summary>All permissions granted.</summary>
    All = Print | ModifyContents | CopyContents | Annotate | FillForms
        | ExtractForAccessibility | AssembleDocument | PrintHighQuality,
}

