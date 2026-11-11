using TimeTrack.Domain.Common;

namespace TimeTrack.Domain.TimeEntries;

/// <summary>
/// Aggregate root for a block of hours a consultant logs against a project.
/// Lifecycle: Draft -> Submitted -> Approved, or Submitted -> Rejected -> (edit) -> Submitted.
/// All state changes go through methods, so the business rules can't be bypassed.
/// </summary>
public sealed class TimeEntry : Entity
{
    public const decimal HoursIncrement = 0.25m;
    public const decimal MaxHoursPerEntry = 8m;
    public const decimal MaxHoursPerDay = 12m;
    public const int MaxDescriptionLength = 500;
    public const int MaxRejectionReasonLength = 300;

    private TimeEntry()
    {
    }

    public Guid UserId { get; private set; }

    public Guid ProjectId { get; private set; }

    public DateOnly WorkDate { get; private set; }

    public decimal Hours { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public TimeEntryStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. It changes on every modification, so two people
    /// editing the same entry can't silently overwrite each other. A Guid stamp works
    /// the same way on SQL Server and SQLite (unlike rowversion).
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; }

    public bool CanBeEdited => Status is TimeEntryStatus.Draft or TimeEntryStatus.Rejected;

    public static TimeEntry Create(
        Guid userId,
        Guid projectId,
        DateOnly workDate,
        decimal hours,
        string description,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("TimeEntry.InvalidUser", "The user is required.");
        }

        var entry = new TimeEntry
        {
            UserId = userId,
            Status = TimeEntryStatus.Draft,
            CreatedAtUtc = now.UtcDateTime,
        };

        entry.ApplyDetails(projectId, workDate, hours, description, now);
        return entry;
    }

    public void UpdateDetails(Guid projectId, DateOnly workDate, decimal hours, string description, DateTimeOffset now)
    {
        EnsureCanBeEdited();
        ApplyDetails(projectId, workDate, hours, description, now);

        // Editing a rejected entry sends it back to draft so the consultant can resubmit it.
        if (Status == TimeEntryStatus.Rejected)
        {
            Status = TimeEntryStatus.Draft;
            RejectionReason = null;
            ReviewedBy = null;
            ReviewedAtUtc = null;
        }
    }

    public void Submit(DateTimeOffset now)
    {
        if (Status is not (TimeEntryStatus.Draft or TimeEntryStatus.Rejected))
        {
            throw new DomainException(
                "TimeEntry.InvalidTransition",
                $"An entry in status {Status} cannot be submitted.");
        }

        Status = TimeEntryStatus.Submitted;
        RejectionReason = null;
        Touch(now);
    }

    public void Approve(Guid reviewerId, DateTimeOffset now)
    {
        EnsureCanBeReviewed(reviewerId);

        Status = TimeEntryStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewedAtUtc = now.UtcDateTime;
        Touch(now);
    }

    public void Reject(Guid reviewerId, string reason, DateTimeOffset now)
    {
        EnsureCanBeReviewed(reviewerId);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("TimeEntry.ReasonRequired", "A reason is required to reject an entry.");
        }

        if (reason.Trim().Length > MaxRejectionReasonLength)
        {
            throw new DomainException(
                "TimeEntry.ReasonTooLong",
                $"The rejection reason can't exceed {MaxRejectionReasonLength} characters.");
        }

        Status = TimeEntryStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedBy = reviewerId;
        ReviewedAtUtc = now.UtcDateTime;
        Touch(now);
    }

    public void EnsureCanBeDeleted()
    {
        if (Status != TimeEntryStatus.Draft)
        {
            throw new DomainException("TimeEntry.NotDeletable", "Only draft entries can be deleted.");
        }
    }

    public static void ValidateHours(decimal hours)
    {
        if (hours <= 0 || hours > MaxHoursPerEntry)
        {
            throw new DomainException(
                "TimeEntry.InvalidHours",
                $"Hours must be greater than 0 and at most {MaxHoursPerEntry} per entry.");
        }

        if (hours % HoursIncrement != 0)
        {
            throw new DomainException(
                "TimeEntry.InvalidIncrement",
                "Hours must be logged in increments of 15 minutes (0.25).");
        }
    }

    private void ApplyDetails(Guid projectId, DateOnly workDate, decimal hours, string description, DateTimeOffset now)
    {
        if (projectId == Guid.Empty)
        {
            throw new DomainException("TimeEntry.InvalidProject", "The project is required.");
        }

        if (workDate > DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new DomainException("TimeEntry.FutureDate", "You can't log hours for a future date.");
        }

        ValidateHours(hours);

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("TimeEntry.DescriptionRequired", "A description of the work is required.");
        }

        if (description.Trim().Length > MaxDescriptionLength)
        {
            throw new DomainException(
                "TimeEntry.DescriptionTooLong",
                $"The description can't exceed {MaxDescriptionLength} characters.");
        }

        ProjectId = projectId;
        WorkDate = workDate;
        Hours = hours;
        Description = description.Trim();
        Touch(now);
    }

    private void EnsureCanBeEdited()
    {
        if (!CanBeEdited)
        {
            throw new DomainException(
                "TimeEntry.NotEditable",
                $"An entry in status {Status} can't be modified.");
        }
    }

    private void EnsureCanBeReviewed(Guid reviewerId)
    {
        if (Status != TimeEntryStatus.Submitted)
        {
            throw new DomainException("TimeEntry.NotSubmitted", "Only submitted entries can be reviewed.");
        }

        if (reviewerId == UserId)
        {
            throw new DomainException("TimeEntry.SelfReview", "You can't review your own time entries.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = now.UtcDateTime;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
