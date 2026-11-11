using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Infrastructure.Persistence;
using TimeTrack.Infrastructure.Persistence.Repositories;
using TimeTrack.Infrastructure.Security;

namespace TimeTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration is read when the DbContext options are built, not at registration time,
        // so tests (and any later configuration source) can override the provider and connection.
        services.AddDbContext<TimeTrackDbContext>((serviceProvider, options) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var provider = config["Database:Provider"] ?? "Sqlite";
            var connectionString = config.GetConnectionString("TimeTrack")
                ?? throw new InvalidOperationException("Connection string 'TimeTrack' is not configured.");

            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                // Retries transient failures (network blips, failovers): important in Azure SQL.
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddHostedService<DatabaseInitializerHostedService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        return services;
    }
}
