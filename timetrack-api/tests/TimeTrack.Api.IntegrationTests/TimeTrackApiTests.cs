using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TimeTrack.Application.Auth;
using TimeTrack.Application.Common.Models;
using TimeTrack.Application.Projects;
using TimeTrack.Application.TimeEntries;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Api.IntegrationTests;

public sealed class TimeTrackApiTests(TimeTrackApiFactory factory) : IClassFixture<TimeTrackApiFactory>
{
    private const string Consultant = "consultant@timetrack.dev";
    private const string Manager = "manager@timetrack.dev";

    private static readonly JsonSerializerOptions Json = TimeTrackApiFactory.JsonOptions;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(Consultant, "Demo123!"),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.FullName.ShouldBe("Felipe Urueta");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(Consultant, "wrong-password"),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/time-entries");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Approvals_AsConsultant_Returns403()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Consultant);

        var response = await client.GetAsync("/api/v1/approvals/pending");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SearchProjects_FiltersByTerm()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Consultant);

        var result = await client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/v1/projects?search=azure", Json);

        result!.Items.ShouldHaveSingleItem().Code.ShouldBe("CLD-2026-021");
    }

    [Fact]
    public async Task CreateEntry_WithInvalidHours_Returns400WithFieldErrors()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Consultant);
        var project = await GetProjectAsync(client, "CLD-2026-021");

        var response = await client.PostAsJsonAsync(
            "/api/v1/time-entries",
            new CreateTimeEntryRequest(project.Id, Today.AddDays(-20), 9, "Too many hours"),
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"hours\"");
    }

    [Fact]
    public async Task FullFlow_CreateSubmitAndApprove()
    {
        var consultant = await factory.CreateAuthenticatedClientAsync(Consultant);
        var manager = await factory.CreateAuthenticatedClientAsync(Manager);
        var project = await GetProjectAsync(consultant, "DAI-2026-003");

        // 1. Consultant logs hours.
        var createResponse = await consultant.PostAsJsonAsync(
            "/api/v1/time-entries",
            new CreateTimeEntryRequest(project.Id, Today.AddDays(-10), 4.5m, "Data governance workshop"),
            Json);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>(Json))!;
        created.Status.ShouldBe(TimeEntryStatus.Draft);

        // 2. Consultant submits.
        var submitResponse = await consultant.PostAsync($"/api/v1/time-entries/{created.Id}/submit", content: null);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3. Manager sees it in the queue and approves it.
        var pending = await manager.GetFromJsonAsync<List<TimeEntryDto>>("/api/v1/approvals/pending", Json);
        pending!.ShouldContain(e => e.Id == created.Id);

        var approveResponse = await manager.PostAsync($"/api/v1/approvals/{created.Id}/approve", content: null);
        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var approved = (await approveResponse.Content.ReadFromJsonAsync<TimeEntryDto>(Json))!;
        approved.Status.ShouldBe(TimeEntryStatus.Approved);

        // 4. An approved entry can no longer be edited: 422 with a business error code.
        var editResponse = await consultant.PutAsJsonAsync(
            $"/api/v1/time-entries/{created.Id}",
            new UpdateTimeEntryRequest(project.Id, Today.AddDays(-10), 5, "Edited", approved.ConcurrencyStamp),
            Json);
        editResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await editResponse.Content.ReadAsStringAsync()).ShouldContain("TimeEntry.NotEditable");
    }

    [Fact]
    public async Task UpdateEntry_WithStaleConcurrencyStamp_Returns409()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Consultant);
        var project = await GetProjectAsync(client, "CLD-2026-021");

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/time-entries",
            new CreateTimeEntryRequest(project.Id, Today.AddDays(-12), 2, "Original"),
            Json);
        var created = (await createResponse.Content.ReadFromJsonAsync<TimeEntryDto>(Json))!;

        // First edit succeeds and changes the stamp.
        var firstEdit = await client.PutAsJsonAsync(
            $"/api/v1/time-entries/{created.Id}",
            new UpdateTimeEntryRequest(project.Id, created.WorkDate, 3, "First edit", created.ConcurrencyStamp),
            Json);
        firstEdit.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second edit still uses the old stamp: someone else changed it in the meantime.
        var staleEdit = await client.PutAsJsonAsync(
            $"/api/v1/time-entries/{created.Id}",
            new UpdateTimeEntryRequest(project.Id, created.WorkDate, 4, "Stale edit", created.ConcurrencyStamp),
            Json);

        staleEdit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateEntry_OverDailyLimit_Returns422()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Consultant);
        var project = await GetProjectAsync(client, "AUD-2026-014");
        var date = Today.AddDays(-15);

        (await client.PostAsJsonAsync(
            "/api/v1/time-entries",
            new CreateTimeEntryRequest(project.Id, date, 8, "Fieldwork"),
            Json)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var overLimit = await client.PostAsJsonAsync(
            "/api/v1/time-entries",
            new CreateTimeEntryRequest(project.Id, date, 4.25m, "Late fieldwork"),
            Json);

        overLimit.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await overLimit.Content.ReadAsStringAsync()).ShouldContain("TimeEntry.DailyLimitExceeded");
    }

    private static async Task<ProjectDto> GetProjectAsync(HttpClient client, string code)
    {
        var result = await client.GetFromJsonAsync<PagedResult<ProjectDto>>($"/api/v1/projects?search={code}", Json);
        return result!.Items.Single(p => p.Code == code);
    }
}
