namespace TimeTrack.Api.Security;

public static class AuthConstants
{
    public const string SubjectClaimType = "sub";
    public const string NameClaimType = "name";
    public const string RoleClaimType = "role";

    public const string ManagerPolicy = "Manager";
    public const string LoginRateLimitPolicy = "login";
    public const string WebAppCorsPolicy = "WebApp";
}
