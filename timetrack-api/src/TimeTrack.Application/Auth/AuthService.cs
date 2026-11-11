using FluentValidation;
using Microsoft.Extensions.Logging;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Exceptions;
using TimeTrack.Domain.Users;

namespace TimeTrack.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(Guid Id, string Email, string FullName, UserRole Role);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ICurrentUser currentUser,
    IValidator<LoginRequest> validator,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);

        // Same error for "user not found" and "wrong password": never reveal which emails exist.
        if (user is null || !passwordHasher.Verify(user, request.Password))
        {
            logger.LogWarning("AuthService.LoginAsync: failed login attempt for {Email}", request.Email);
            throw new InvalidCredentialsException();
        }

        var (token, expiresAtUtc) = tokenGenerator.Generate(user);
        logger.LogInformation("AuthService.LoginAsync: user {UserId} logged in", user.Id);

        return new AuthResponse(token, expiresAtUtc, user.ToDto());
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("User", currentUser.UserId);

        return user.ToDto();
    }
}

internal static class UserMappings
{
    public static UserDto ToDto(this User user) => new(user.Id, user.Email, user.FullName, user.Role);
}
