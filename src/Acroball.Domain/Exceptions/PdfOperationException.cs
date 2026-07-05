namespace Acroball.Domain.Exceptions;

/// <summary>
/// Base exception for failures raised by PDF operations. UI layers translate
/// these into user-facing messages; the inner exception carries backend detail.
/// </summary>
public class PdfOperationException : Exception
{
    /// <summary>Creates the exception with a user-presentable message.</summary>
    public PdfOperationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a user-presentable message and backend cause.</summary>
    public PdfOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

