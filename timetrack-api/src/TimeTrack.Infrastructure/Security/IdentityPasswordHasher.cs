using Microsoft.AspNetCore.Identity;
using TimeTrack.Domain.Users;
using AppPasswordHasher = TimeTrack.Application.Common.Abstractions.IPasswordHasher;

namespace TimeTrack.Infrastructure.Security;

/// <summary>
/// Reuses ASP.NET Core Identity's hasher (PBKDF2 with salt and many iterations)
/// instead of inventing our own crypto.
/// </summary>
internal sealed class IdentityPasswordHasher : AppPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
