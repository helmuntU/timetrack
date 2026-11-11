namespace TimeTrack.Domain.Common;

/// <summary>
/// Raised when a business rule is broken. The <see cref="Code"/> is stable and
/// safe to expose to API clients (the front end can translate it).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
