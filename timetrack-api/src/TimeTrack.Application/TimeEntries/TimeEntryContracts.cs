using FluentValidation;
using TimeTrack.Application.Common.Models;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Application.TimeEntries;

public sealed record TimeEntryDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    DateOnly WorkDate,
    decimal Hours,
    string Description,
    TimeEntryStatus Status,
    string? RejectionReason,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyStamp);

public sealed record TimeEntryQuery(DateOnly? From, DateOnly? To, TimeEntryStatus? Status);

public sealed record CreateTimeEntryRequest(Guid ProjectId, DateOnly WorkDate, decimal Hours, string Description);

public sealed record UpdateTimeEntryRequest(
    Guid ProjectId,
    DateOnly WorkDate,
    decimal Hours,
    string Description,
    Guid ConcurrencyStamp);

public sealed record RejectTimeEntryRequest(string Reason);

/// <summary>
/// Shape validation (required fields, ranges) so the UI gets field-level errors.
/// The domain still enforces the real business rules: the validator is the first gate, not the only one.
/// </summary>
public sealed class CreateTimeEntryRequestValidator : AbstractValidator<CreateTimeEntryRequest>
{
    public CreateTimeEntryRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.WorkDate).NotEmpty();
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(TimeEntry.MaxHoursPerEntry);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(TimeEntry.MaxDescriptionLength);
    }
}

public sealed class UpdateTimeEntryRequestValidator : AbstractValidator<UpdateTimeEntryRequest>
{
    public UpdateTimeEntryRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.WorkDate).NotEmpty();
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(TimeEntry.MaxHoursPerEntry);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(TimeEntry.MaxDescriptionLength);
        RuleFor(x => x.ConcurrencyStamp).NotEmpty();
    }
}

public sealed class TimeEntryQueryValidator : AbstractValidator<TimeEntryQuery>
{
    public TimeEntryQueryValidator()
    {
        RuleFor(x => x)
            .Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithName("From")
            .WithMessage("'From' must be before or equal to 'To'.");

        RuleFor(x => x)
            .Must(q => q.From is null || q.To is null || q.To.Value.DayNumber - q.From.Value.DayNumber <= 366)
            .WithName("To")
            .WithMessage("The date range can't be longer than one year.");
    }
}

public sealed class RejectTimeEntryRequestValidator : AbstractValidator<RejectTimeEntryRequest>
{
    public RejectTimeEntryRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(TimeEntry.MaxRejectionReasonLength);
    }
}

internal static class TimeEntryMappings
{
    public static TimeEntryDto ToDto(this TimeEntryDetails details)
    {
        var entry = details.Entry;

        return new TimeEntryDto(
            entry.Id,
            entry.UserId,
            details.UserFullName,
            entry.ProjectId,
            details.ProjectCode,
            details.ProjectName,
            entry.WorkDate,
            entry.Hours,
            entry.Description,
            entry.Status,
            entry.RejectionReason,
            entry.UpdatedAtUtc,
            entry.ConcurrencyStamp);
    }
}
