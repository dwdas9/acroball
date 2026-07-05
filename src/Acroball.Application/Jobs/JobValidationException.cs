namespace Acroball.Application.Jobs;

/// <summary>Raised when a job request fails validation.</summary>
public sealed class JobValidationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public JobValidationException(string message) : base(message)
    {
    }
}
