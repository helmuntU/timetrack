using System.Security.Claims;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Domain.Users;

namespace TimeTrack.Api.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var subject = Principal?.FindFirst(AuthConstants.SubjectClaimType)?.Value;

            return Guid.TryParse(subject, out var userId)
                ? userId
                : throw new UnauthorizedAccessException("The request has no authenticated user.");
        }
    }

    public bool IsManager => Principal?.IsInRole(nameof(UserRole.Manager)) ?? false;

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
