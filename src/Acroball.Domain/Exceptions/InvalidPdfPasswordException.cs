namespace Acroball.Domain.Exceptions;

/// <summary>
/// Raised when a PDF is encrypted and the supplied password is missing or wrong.
/// </summary>
public sealed class InvalidPdfPasswordException : PdfOperationException
{
    /// <summary>Creates the exception for the given file.</summary>
    /// <param name="filePath">The file that could not be opened.</param>
    public InvalidPdfPasswordException(string filePath)
        : base($"\"{Path.GetFileName(filePath)}\" is password-protected and the password was missing or incorrect.")
    {
        FilePath = filePath;
    }

    /// <summary>The file that could not be opened.</summary>
    public string FilePath { get; }
}

