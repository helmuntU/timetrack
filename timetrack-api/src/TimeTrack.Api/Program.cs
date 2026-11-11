using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TimeTrack.Api.ErrorHandling;
using TimeTrack.Api.Security;
using TimeTrack.Application;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Domain.Users;
using TimeTrack.Infrastructure;
using TimeTrack.Infrastructure.Persistence;
using TimeTrack.Infrastructure.Security;

// TimeTrack API: ASP.NET Core Web API with Clean Architecture (Domain, Application, Infrastructure, Api).

var builder = WebApplication.CreateBuilder(args);

// ---- Layers ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Web ----
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddHealthChecks().AddDbContextCheck<TimeTrackDbContext>("database");

// ---- Authentication: JWT bearer ----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configured through the options pattern so it uses the same validated JwtOptions as the token generator.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        // Keep the original claim names ("sub", "role") instead of the long legacy SOAP-style URIs.
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AuthConstants.NameClaimType,
            RoleClaimType = AuthConstants.RoleClaimType,
        };
    });

// ---- Authorization: secure by default ----
builder.Services
    .AddAuthorizationBuilder()
    // Every endpoint requires a valid token unless it is explicitly marked [AllowAnonymous].
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AuthConstants.ManagerPolicy, policy => policy.RequireRole(nameof(UserRole.Manager)));

// ---- Rate limiting: protects the login endpoint against brute force ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthConstants.LoginRateLimitPolicy, httpContext =>
    {
        var permitLimit = httpContext.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:LoginPermitLimit", 5);

        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

// ---- CORS for the Angular app ----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(
    AuthConstants.WebAppCorsPolicy,
    policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---- Pipeline (order matters) ----
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}
else
{
    // TLS terminates at the platform (App Service / ingress); HSTS tells browsers to stay on HTTPS.
    app.UseHsts();
}

app.UseCors(AuthConstants.WebAppCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// The database is created/migrated and seeded by DatabaseInitializerHostedService on startup.
await app.RunAsync();

// Exposes Program to the integration tests (WebApplicationFactory<Program>).
public partial class Program
{
}
