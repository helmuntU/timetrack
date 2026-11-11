using TimeTrack.Domain.Users;

namespace TimeTrack.Application.Common.Abstractions;

/// <summary>Who is calling the API. Implemented in the API layer from the JWT claims.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    bool IsManager { get; }
}

public interface IPasswordHasher
{
    string Hash(User user, string password);

    bool Verify(User user, string password);
}

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) Generate(User user);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
