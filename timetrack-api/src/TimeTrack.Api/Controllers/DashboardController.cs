using Microsoft.AspNetCore.Mvc;
using TimeTrack.Application.Dashboard;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Hours per day, per project and per status for the week (Monday to Sunday) that contains <paramref name="weekOf"/>.</summary>
    [HttpGet("weekly-summary")]
    [ProducesResponseType<WeeklySummaryDto>(StatusCodes.Status200OK)]
    public Task<WeeklySummaryDto> WeeklySummary([FromQuery] DateOnly? weekOf, CancellationToken cancellationToken) =>
        dashboardService.GetWeeklySummaryAsync(weekOf, cancellationToken);
}
