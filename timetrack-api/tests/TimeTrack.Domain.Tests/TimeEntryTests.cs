using TimeTrack.Domain.Common;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Domain.Tests;

public sealed class TimeEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly Guid ConsultantId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_StartsAsDraft()
    {
        var entry = CreateEntry(hours: 7.5m);

        entry.Status.ShouldBe(TimeEntryStatus.Draft);
        entry.Hours.ShouldBe(7.5m);
        entry.UserId.ShouldBe(ConsultantId);
        entry.CanBeEdited.ShouldBeTrue();
        entry.ConcurrencyStamp.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(8.25)]
    [InlineData(12.0)]
    public void Create_WithHoursOutOfRange_Throws(double hours)
    {
        // xUnit attributes can't hold decimals, so the cases arrive as double.
        var exception = Should.Throw<DomainException>(() => CreateEntry(hours: (decimal)hours));

        exception.Code.ShouldBe("TimeEntry.InvalidHours");
    }

    [Theory]
    [InlineData(1.1)]
    [InlineData(2.3)]
    [InlineData(0.1)]
    public void Create_WithHoursNotInQuarterIncrements_Throws(double hours)
    {
        var exception = Should.Throw<DomainException>(() => CreateEntry(hours: (decimal)hours));

        exception.Code.ShouldBe("TimeEntry.InvalidIncrement");
    }

    [Fact]
    public void Create_ForFutureDate_Throws()
    {
        var exception = Should.Throw<DomainException>(() => CreateEntry(workDate: Today.AddDays(1)));

        exception.Code.ShouldBe("TimeEntry.FutureDate");
    }

    [Fact]
    public void Create_WithoutDescription_Throws()
    {
        var exception = Should.Throw<DomainException>(() => CreateEntry(description: "   "));

        exception.Code.ShouldBe("TimeEntry.DescriptionRequired");
    }

    [Fact]
    public void Create_TrimsDescription()
    {
        var entry = CreateEntry(description: "  Code review  ");

        entry.Description.ShouldBe("Code review");
    }

    [Fact]
    public void Submit_FromDraft_MovesToSubmitted()
    {
        var entry = CreateEntry();

        entry.Submit(Now);

        entry.Status.ShouldBe(TimeEntryStatus.Submitted);
        entry.CanBeEdited.ShouldBeFalse();
    }

    [Fact]
    public void Submit_WhenAlreadySubmitted_Throws()
    {
        var entry = CreateEntry();
        entry.Submit(Now);

        var exception = Should.Throw<DomainException>(() => entry.Submit(Now));

        exception.Code.ShouldBe("TimeEntry.InvalidTransition");
    }

    [Fact]
    public void Approve_SubmittedEntry_RecordsReviewer()
    {
        var entry = CreateEntry();
        entry.Submit(Now);

        entry.Approve(ManagerId, Now);

        entry.Status.ShouldBe(TimeEntryStatus.Approved);
        entry.ReviewedBy.ShouldBe(ManagerId);
        entry.ReviewedAtUtc.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public void Approve_DraftEntry_Throws()
    {
        var entry = CreateEntry();

        var exception = Should.Throw<DomainException>(() => entry.Approve(ManagerId, Now));

        exception.Code.ShouldBe("TimeEntry.NotSubmitted");
    }

    [Fact]
    public void Approve_OwnEntry_Throws()
    {
        var entry = CreateEntry();
        entry.Submit(Now);

        var exception = Should.Throw<DomainException>(() => entry.Approve(ConsultantId, Now));

        exception.Code.ShouldBe("TimeEntry.SelfReview");
    }

    [Fact]
    public void Reject_WithoutReason_Throws()
    {
        var entry = CreateEntry();
        entry.Submit(Now);

        var exception = Should.Throw<DomainException>(() => entry.Reject(ManagerId, " ", Now));

        exception.Code.ShouldBe("TimeEntry.ReasonRequired");
    }

    [Fact]
    public void UpdateDetails_OnRejectedEntry_ReturnsItToDraftAndClearsReview()
    {
        var entry = CreateEntry();
        entry.Submit(Now);
        entry.Reject(ManagerId, "Add more detail", Now);

        entry.UpdateDetails(ProjectId, Today, 4, "Audit meeting: revenue controls walkthrough", Now);

        entry.Status.ShouldBe(TimeEntryStatus.Draft);
        entry.RejectionReason.ShouldBeNull();
        entry.ReviewedBy.ShouldBeNull();
    }

    [Fact]
    public void UpdateDetails_OnApprovedEntry_Throws()
    {
        var entry = CreateEntry();
        entry.Submit(Now);
        entry.Approve(ManagerId, Now);

        var exception = Should.Throw<DomainException>(
            () => entry.UpdateDetails(ProjectId, Today, 2, "Changed", Now));

        exception.Code.ShouldBe("TimeEntry.NotEditable");
    }

    [Fact]
    public void EveryChange_RegeneratesConcurrencyStamp()
    {
        var entry = CreateEntry();
        var created = entry.ConcurrencyStamp;

        entry.UpdateDetails(ProjectId, Today, 2, "Updated", Now);
        var updated = entry.ConcurrencyStamp;

        entry.Submit(Now);
        var submitted = entry.ConcurrencyStamp;

        new[] { created, updated, submitted }.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void EnsureCanBeDeleted_WhenSubmitted_Throws()
    {
        var entry = CreateEntry();
        entry.Submit(Now);

        var exception = Should.Throw<DomainException>(entry.EnsureCanBeDeleted);

        exception.Code.ShouldBe("TimeEntry.NotDeletable");
    }

    private static TimeEntry CreateEntry(
        decimal hours = 4,
        DateOnly? workDate = null,
        string description = "Designed the landing zone") =>
        TimeEntry.Create(ConsultantId, ProjectId, workDate ?? Today, hours, description, Now);
}
