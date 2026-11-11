using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Application.Approvals;
using TimeTrack.Application.Auth;
using TimeTrack.Application.Dashboard;
using TimeTrack.Application.Projects;
using TimeTrack.Application.TimeEntries;

namespace TimeTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITimeEntryService, TimeEntryService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
