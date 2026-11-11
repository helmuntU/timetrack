using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Application.Dashboard;

public sealed record DailyHoursDto(DateOnly Date, decimal Hours);

public sealed record ProjectHoursDto(Guid ProjectId, string ProjectCode, string ProjectName, decimal Hours);

public sealed record WeeklySummaryDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    decimal TotalHours,
    decimal DraftHours,
    decimal SubmittedHours,
    decimal ApprovedHours,
    decimal RejectedHours,
    IReadOnlyList<DailyHoursDto> DailyHours,
    IReadOnlyList<ProjectHoursDto> HoursByProject);

public interface IDashboardService
{
    Task<WeeklySummaryDto> GetWeeklySummaryAsync(DateOnly? weekOf, CancellationToken cancellationToken);
}

public sealed class DashboardService(
    ITimeEntryRepository timeEntries,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<WeeklySummaryDto> GetWeeklySummaryAsync(DateOnly? weekOf, CancellationToken cancellationToken)
    {
        var referenceDate = weekOf ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var weekStart = GetMonday(referenceDate);
        var weekEnd = weekStart.AddDays(6);

        var entries = await timeEntries.ListForUserAsync(
            currentUser.UserId,
            weekStart,
            weekEnd,
            status: null,
            cancellationToken);

        // One week of one user is a handful of rows, so aggregating in memory is simpler
        // and portable across database providers.
        var dailyHours = Enumerable.Range(0, 7)
            .Select(offset => weekStart.AddDays(offset))
            .Select(date => new DailyHoursDto(
                date,
                entries.Where(e => e.Entry.WorkDate == date).Sum(e => e.Entry.Hours)))
            .ToList();

        var hoursByProject = entries
            .GroupBy(e => new { e.Entry.ProjectId, e.ProjectCode, e.ProjectName })
            .Select(g => new ProjectHoursDto(g.Key.ProjectId, g.Key.ProjectCode, g.Key.ProjectName, g.Sum(e => e.Entry.Hours)))
            .OrderByDescending(p => p.Hours)
            .ToList();

        decimal SumByStatus(TimeEntryStatus status) =>
            entries.Where(e => e.Entry.Status == status).Sum(e => e.Entry.Hours);

        return new WeeklySummaryDto(
            weekStart,
            weekEnd,
            entries.Sum(e => e.Entry.Hours),
            SumByStatus(TimeEntryStatus.Draft),
            SumByStatus(TimeEntryStatus.Submitted),
            SumByStatus(TimeEntryStatus.Approved),
            SumByStatus(TimeEntryStatus.Rejected),
            dailyHours,
            hoursByProject);
    }

    internal static DateOnly GetMonday(DateOnly date)
    {
        // DayOfWeek.Sunday is 0, so Sunday must go back 6 days, not forward 1.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
