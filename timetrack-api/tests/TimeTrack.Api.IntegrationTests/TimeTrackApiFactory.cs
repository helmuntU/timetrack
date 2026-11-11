using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using TimeTrack.Application.Auth;

namespace TimeTrack.Api.IntegrationTests;

/// <summary>
/// Boots the real API in memory (same Program.cs, middleware, auth and EF Core)
/// against a throw-away SQLite database seeded with the demo data.
/// </summary>
public sealed class TimeTrackApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"timetrack-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TimeTrack"] = $"Data Source={_databasePath}",
                ["Database:Provider"] = "Sqlite",
                ["Database:SeedDemoData"] = "true",
                ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
                ["RateLimiting:LoginPermitLimit"] = "1000",
            });
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "Demo123!"),
            JsonOptions);

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // SQLite keeps pooled connections open; release them so the temp file can be deleted.
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // The OS temp folder is cleaned eventually; a leftover test database is harmless.
        }
    }
}
