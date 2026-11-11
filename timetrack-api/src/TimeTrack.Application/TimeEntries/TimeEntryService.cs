using FluentValidation;
using Microsoft.Extensions.Logging;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Exceptions;
using TimeTrack.Domain.Common;
using TimeTrack.Domain.Projects;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Application.TimeEntries;

public interface ITimeEntryService
{
    Task<IReadOnlyList<TimeEntryDto>> ListMineAsync(TimeEntryQuery query, CancellationToken cancellationToken);

    Task<TimeEntryDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<TimeEntryDto> CreateAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken);

    Task<TimeEntryDto> UpdateAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<TimeEntryDto> SubmitAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class TimeEntryService(
    ITimeEntryRepository timeEntries,
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<TimeEntryQuery> queryValidator,
    IValidator<CreateTimeEntryRequest> createValidator,
    IValidator<UpdateTimeEntryRequest> updateValidator,
    ILogger<TimeEntryService> logger) : ITimeEntryService
{
    public async Task<IReadOnlyList<TimeEntryDto>> ListMineAsync(TimeEntryQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);

        var entries = await timeEntries.ListForUserAsync(
            currentUser.UserId,
            query.From,
            query.To,
            query.Status,
            cancellationToken);

        return entries.Select(e => e.ToDto()).ToList();
    }

    public async Task<TimeEntryDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var details = await timeEntries.GetDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(TimeEntry), id);

        // Consultants only see their own entries; managers can see any entry they review.
        if (details.Entry.UserId != currentUser.UserId && !currentUser.IsManager)
        {
            throw new NotFoundException(nameof(TimeEntry), id);
        }

        return details.ToDto();
    }

    public async Task<TimeEntryDto> CreateAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        await EnsureProjectIsActiveAsync(request.ProjectId, cancellationToken);
        await EnsureDailyLimitAsync(request.WorkDate, request.Hours, excludeEntryId: null, cancellationToken);

        var entry = TimeEntry.Create(
            currentUser.UserId,
            request.ProjectId,
            request.WorkDate,
            request.Hours,
            request.Description,
            timeProvider.GetUtcNow());

        timeEntries.Add(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "TimeEntryService.CreateAsync: entry {EntryId} created by {UserId} with {Hours} hours",
            entry.Id,
            currentUser.UserId,
            entry.Hours);

        return await GetAsync(entry.Id, cancellationToken);
    }

    public async Task<TimeEntryDto> UpdateAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entry = await GetOwnedEntryAsync(id, cancellationToken);

        // The client sends the stamp it read. If it changed, someone else modified the entry
        // in the meantime: return 409 instead of silently overwriting their changes.
        if (entry.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw new ConcurrencyConflictException();
        }

        await EnsureProjectIsActiveAsync(request.ProjectId, cancellationToken);
        await EnsureDailyLimitAsync(request.WorkDate, request.Hours, entry.Id, cancellationToken);

        entry.UpdateDetails(
            request.ProjectId,
            request.WorkDate,
            request.Hours,
            request.Description,
            timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("TimeEntryService.UpdateAsync: entry {EntryId} updated", entry.Id);

        return await GetAsync(entry.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await GetOwnedEntryAsync(id, cancellationToken);
        entry.EnsureCanBeDeleted();

        timeEntries.Remove(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("TimeEntryService.DeleteAsync: entry {EntryId} deleted", id);
    }

    public async Task<TimeEntryDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await GetOwnedEntryAsync(id, cancellationToken);
        entry.Submit(timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("TimeEntryService.SubmitAsync: entry {EntryId} submitted for approval", id);

        return await GetAsync(entry.Id, cancellationToken);
    }

    private async Task<TimeEntry> GetOwnedEntryAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await timeEntries.GetByIdAsync(id, cancellationToken);

        // 404 (not 403) when it isn't yours: don't confirm that other users' entry ids exist.
        if (entry is null || entry.UserId != currentUser.UserId)
        {
            throw new NotFoundException(nameof(TimeEntry), id);
        }

        return entry;
    }

    private async Task EnsureProjectIsActiveAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), projectId);

        if (!project.IsActive)
        {
            throw new DomainException("TimeEntry.InactiveProject", $"Project {project.Code} is not active.");
        }
    }

    private async Task EnsureDailyLimitAsync(
        DateOnly workDate,
        decimal hours,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var alreadyLogged = await timeEntries.GetLoggedHoursAsync(
            currentUser.UserId,
            workDate,
            excludeEntryId,
            cancellationToken);

        if (alreadyLogged + hours > TimeEntry.MaxHoursPerDay)
        {
            throw new DomainException(
                "TimeEntry.DailyLimitExceeded",
                $"You already logged {alreadyLogged} hours on {workDate:yyyy-MM-dd}. The daily limit is {TimeEntry.MaxHoursPerDay} hours.");
        }
    }
}
