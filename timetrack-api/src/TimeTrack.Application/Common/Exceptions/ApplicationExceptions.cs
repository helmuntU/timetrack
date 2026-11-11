namespace TimeTrack.Application.Common.Exceptions;

/// <summary>The requested resource doesn't exist (or the user can't see it). Maps to HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, Guid id)
        : base($"{resource} '{id}' was not found.")
    {
        Resource = resource;
    }

    public string Resource { get; }
}

/// <summary>The user is authenticated but not allowed to perform the action. Maps to HTTP 403.</summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}

/// <summary>The resource changed since the client read it. Maps to HTTP 409.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The entry was modified by someone else. Reload it and try again.")
    {
    }

    public ConcurrencyConflictException(Exception innerException)
        : base("The entry was modified by someone else. Reload it and try again.", innerException)
    {
    }
}

/// <summary>Wrong email or password. Maps to HTTP 401 with a generic message.</summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }
}
