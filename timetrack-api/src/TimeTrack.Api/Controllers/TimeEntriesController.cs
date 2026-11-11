using Microsoft.AspNetCore.Mvc;
using TimeTrack.Application.TimeEntries;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/time-entries")]
[Produces("application/json")]
public sealed class TimeEntriesController(ITimeEntryService timeEntryService) : ControllerBase
{
    /// <summary>Lists the current user's entries, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TimeEntryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<TimeEntryDto>> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] TimeEntryStatus? status,
        CancellationToken cancellationToken) =>
        timeEntryService.ListMineAsync(new TimeEntryQuery(from, to, status), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TimeEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<TimeEntryDto> Get(Guid id, CancellationToken cancellationToken) =>
        timeEntryService.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<TimeEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TimeEntryDto>> Create(CreateTimeEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await timeEntryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TimeEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<TimeEntryDto> Update(Guid id, UpdateTimeEntryRequest request, CancellationToken cancellationToken) =>
        timeEntryService.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await timeEntryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Sends a draft (or a corrected rejected entry) to the manager for approval.</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<TimeEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<TimeEntryDto> Submit(Guid id, CancellationToken cancellationToken) =>
        timeEntryService.SubmitAsync(id, cancellationToken);
}
