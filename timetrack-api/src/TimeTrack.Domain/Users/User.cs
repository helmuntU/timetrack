using TimeTrack.Domain.Common;

namespace TimeTrack.Domain.Users;

public sealed class User : Entity
{
    private User()
    {
    }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsManager => Role == UserRole.Manager;

    public static User Create(string email, string fullName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainException("User.InvalidEmail", "A valid email is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("User.InvalidName", "The full name is required.");
        }

        return new User
        {
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            Role = role,
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("User.InvalidPassword", "The password hash cannot be empty.");
        }

        PasswordHash = passwordHash;
    }
}
