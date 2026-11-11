using Microsoft.EntityFrameworkCore;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Models;
using TimeTrack.Domain.Projects;
using TimeTrack.Domain.TimeEntries;
using TimeTrack.Domain.Users;

namespace TimeTrack.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(TimeTrackDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}

internal sealed class ProjectRepository(TimeTrackDbContext db) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Projects.AnyAsync(p => p.Code == code, cancellationToken);

    public async Task<PagedResult<Project>> SearchAsync(
        string? search,
        bool activeOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // LIKE is case-insensitive on SQL Server (default collation) and on SQLite (ASCII),
            // and the wildcards typed by the user are escaped so "%" doesn't match everything.
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(p =>
                EF.Functions.Like(p.Code, pattern, "\\") ||
                EF.Functions.Like(p.Name, pattern, "\\") ||
                EF.Functions.Like(p.ClientName, pattern, "\\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Project>(items, page, pageSize, totalCount);
    }

    public void Add(Project project) => db.Projects.Add(project);

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}

internal sealed class TimeEntryRepository(TimeTrackDbContext db) : ITimeEntryRepository
{
    public Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.TimeEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<TimeEntryDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await db.TimeEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        var details = await ToDetailsAsync([entry], cancellationToken);
        return details[0];
    }

    public async Task<IReadOnlyList<TimeEntryDetails>> ListForUserAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        TimeEntryStatus? status,
        CancellationToken cancellationToken)
    {
        var query = db.TimeEntries.AsNoTracking().Where(e => e.UserId == userId);

        if (from is not null)
        {
            query = query.Where(e => e.WorkDate >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(e => e.WorkDate <= to.Value);
        }

        if (status is not null)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var entries = await query
            .OrderByDescending(e => e.WorkDate)
            .ThenByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await ToDetailsAsync(entries, cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntryDetails>> ListPendingReviewAsync(Guid reviewerId, CancellationToken cancellationToken)
    {
        var entries = await db.TimeEntries
            .AsNoTracking()
            .Where(e => e.Status == TimeEntryStatus.Submitted && e.UserId != reviewerId)
            .OrderBy(e => e.WorkDate)
            .ThenBy(e => e.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return await ToDetailsAsync(entries, cancellationToken);
    }

    public async Task<decimal> GetLoggedHoursAsync(
        Guid userId,
        DateOnly workDate,
        Guid? excludeEntryId,
        CancellationToken cancellationToken)
    {
        var hours = await db.TimeEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.WorkDate == workDate && e.Id != excludeEntryId)
            .Select(e => e.Hours)
            .ToListAsync(cancellationToken);

        // Summed in memory: a user has only a few entries per day, and SQLite (used in local
        // development and integration tests) can't aggregate decimal columns server side.
        return hours.Sum();
    }

    public void Add(TimeEntry entry) => db.TimeEntries.Add(entry);

    public void Remove(TimeEntry entry) => db.TimeEntries.Remove(entry);

    /// <summary>
    /// Loads project and user names for a batch of entries with two extra queries
    /// (one per table, using IN), instead of one query per entry: no N+1 problem.
    /// </summary>
    private async Task<IReadOnlyList<TimeEntryDetails>> ToDetailsAsync(
        IReadOnlyList<TimeEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var projectIds = entries.Select(e => e.ProjectId).Distinct().ToList();
        var userIds = entries.Select(e => e.UserId).Distinct().ToList();

        var projects = await db.Projects
            .AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Name })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var userNames = await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return entries
            .Select(e => new TimeEntryDetails(
                e,
                projects[e.ProjectId].Code,
                projects[e.ProjectId].Name,
                userNames[e.UserId]))
            .ToList();
    }
}
