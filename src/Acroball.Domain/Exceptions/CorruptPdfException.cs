namespace Acroball.Domain.Exceptions;

/// <summary>
/// Raised when a file is not a structurally valid PDF.
/// </summary>
public sealed class CorruptPdfException : PdfOperationException
{
    /// <summary>Creates the exception for the given file.</summary>
    /// <param name="filePath">The file that failed to parse.</param>
    /// <param name="innerException">The backend parse failure.</param>
    public CorruptPdfException(string filePath, Exception innerException)
        : base($"\"{Path.GetFileName(filePath)}\" is damaged or is not a PDF file.", innerException)
    {
        FilePath = filePath;
    }

    /// <summary>The file that failed to parse.</summary>
    public string FilePath { get; }
}

