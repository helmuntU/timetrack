using FluentValidation;
using Microsoft.Extensions.Logging;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Exceptions;
using TimeTrack.Application.TimeEntries;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Application.Approvals;

public interface IApprovalService
{
    Task<IReadOnlyList<TimeEntryDto>> ListPendingAsync(CancellationToken cancellationToken);

    Task<TimeEntryDto> ApproveAsync(Guid id, CancellationToken cancellationToken);

    Task<TimeEntryDto> RejectAsync(Guid id, RejectTimeEntryRequest request, CancellationToken cancellationToken);
}

public sealed class ApprovalService(
    ITimeEntryRepository timeEntries,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<RejectTimeEntryRequest> rejectValidator,
    ILogger<ApprovalService> logger) : IApprovalService
{
    public async Task<IReadOnlyList<TimeEntryDto>> ListPendingAsync(CancellationToken cancellationToken)
    {
        EnsureManager();

        var pending = await timeEntries.ListPendingReviewAsync(currentUser.UserId, cancellationToken);
        return pending.Select(e => e.ToDto()).ToList();
    }

    public async Task<TimeEntryDto> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureManager();

        var entry = await GetEntryAsync(id, cancellationToken);
        entry.Approve(currentUser.UserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ApprovalService.ApproveAsync: entry {EntryId} approved by {ReviewerId}",
            id,
            currentUser.UserId);

        return await GetDtoAsync(id, cancellationToken);
    }

    public async Task<TimeEntryDto> RejectAsync(Guid id, RejectTimeEntryRequest request, CancellationToken cancellationToken)
    {
        EnsureManager();
        await rejectValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entry = await GetEntryAsync(id, cancellationToken);
        entry.Reject(currentUser.UserId, request.Reason, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ApprovalService.RejectAsync: entry {EntryId} rejected by {ReviewerId}",
            id,
            currentUser.UserId);

        return await GetDtoAsync(id, cancellationToken);
    }

    private void EnsureManager()
    {
        if (!currentUser.IsManager)
        {
            throw new ForbiddenAccessException("Only managers can review time entries.");
        }
    }

    private async Task<TimeEntry> GetEntryAsync(Guid id, CancellationToken cancellationToken) =>
        await timeEntries.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException(nameof(TimeEntry), id);

    private async Task<TimeEntryDto> GetDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var details = await timeEntries.GetDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(TimeEntry), id);

        return details.ToDto();
    }
}
