using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Security;
using TimeTrack.Application.Approvals;
using TimeTrack.Application.TimeEntries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/approvals")]
[Produces("application/json")]
[Authorize(Policy = AuthConstants.ManagerPolicy)]
public sealed class ApprovalsController(IApprovalService approvalService) : ControllerBase
{
    /// <summary>Submitted entries from other users, oldest first.</summary>
    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<TimeEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<TimeEntryDto>> Pending(CancellationToken cancellationToken) =>
        approvalService.ListPendingAsync(cancellationToken);

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<TimeEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<TimeEntryDto> Approve(Guid id, CancellationToken cancellationToken) =>
        approvalService.ApproveAsync(id, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<TimeEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<TimeEntryDto> Reject(Guid id, RejectTimeEntryRequest request, CancellationToken cancellationToken) =>
        approvalService.RejectAsync(id, request, cancellationToken);
}
