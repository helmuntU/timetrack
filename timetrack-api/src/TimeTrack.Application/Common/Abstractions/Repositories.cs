using TimeTrack.Application.Common.Models;
using TimeTrack.Domain.Projects;
using TimeTrack.Domain.TimeEntries;
using TimeTrack.Domain.Users;

namespace TimeTrack.Application.Common.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    Task<PagedResult<Project>> SearchAsync(
        string? search,
        bool activeOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    void Add(Project project);
}

public interface ITimeEntryRepository
{
    Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<TimeEntryDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TimeEntryDetails>> ListForUserAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        TimeEntryStatus? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TimeEntryDetails>> ListPendingReviewAsync(Guid reviewerId, CancellationToken cancellationToken);

    /// <summary>Total hours a user already logged on a date, optionally ignoring one entry (the one being edited).</summary>
    Task<decimal> GetLoggedHoursAsync(Guid userId, DateOnly workDate, Guid? excludeEntryId, CancellationToken cancellationToken);

    void Add(TimeEntry entry);

    void Remove(TimeEntry entry);
}
