namespace Acroball.Domain;

/// <summary>
/// A summary of a PDF file as inspected on disk.
/// </summary>
/// <param name="FilePath">Absolute path of the inspected file.</param>
/// <param name="PageCount">Number of pages in the document.</param>
/// <param name="FileSizeBytes">Size of the file on disk, in bytes.</param>
/// <param name="IsEncrypted">Whether the document is protected by encryption.</param>
/// <param name="Metadata">The document information dictionary.</param>
/// <param name="PdfVersion">The declared PDF version, e.g. <c>"1.7"</c>, when known.</param>
public sealed record PdfDocumentInfo(
    string FilePath,
    int PageCount,
    long FileSizeBytes,
    bool IsEncrypted,
    DocumentMetadata Metadata,
    string? PdfVersion = null);

