using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TimeTrack.Infrastructure.Persistence;

/// <summary>
/// Prepares the database when the host starts. IHostedLifecycleService.StartingAsync runs
/// before the web server begins accepting requests, so no request ever hits an empty database.
/// It also runs inside WebApplicationFactory, which is what the integration tests use.
/// </summary>
internal sealed class DatabaseInitializerHostedService(IServiceProvider services, IConfiguration configuration)
    : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) =>
        DatabaseInitializer.InitializeAsync(
            services,
            seedDemoData: configuration.GetValue("Database:SeedDemoData", false),
            cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
