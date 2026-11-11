using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Application.Common.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>A time entry plus the data the UI needs from other aggregates, read in one query.</summary>
public sealed record TimeEntryDetails(TimeEntry Entry, string ProjectCode, string ProjectName, string UserFullName);
