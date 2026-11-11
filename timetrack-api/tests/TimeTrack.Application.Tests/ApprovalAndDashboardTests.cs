using Microsoft.Extensions.Logging.Abstractions;
using TimeTrack.Application.Approvals;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Exceptions;
using TimeTrack.Application.Common.Models;
using TimeTrack.Application.Dashboard;
using TimeTrack.Application.TimeEntries;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Application.Tests;

public sealed class ApprovalServiceTests
{
    private readonly ITimeEntryRepository _timeEntries = Substitute.For<ITimeEntryRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    [Fact]
    public async Task ListPendingAsync_AsConsultant_ThrowsForbidden()
    {
        _currentUser.IsManager.Returns(false);
        var sut = CreateSut();

        await Should.ThrowAsync<ForbiddenAccessException>(() => sut.ListPendingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RejectAsync_WithEmptyReason_ThrowsValidationException()
    {
        _currentUser.IsManager.Returns(true);
        var sut = CreateSut();

        await Should.ThrowAsync<FluentValidation.ValidationException>(
            () => sut.RejectAsync(Guid.NewGuid(), new RejectTimeEntryRequest(string.Empty), CancellationToken.None));
    }

    private ApprovalService CreateSut() => new(
        _timeEntries,
        Substitute.For<IUnitOfWork>(),
        _currentUser,
        TimeProvider.System,
        new RejectTimeEntryRequestValidator(),
        NullLogger<ApprovalService>.Instance);
}

public sealed class DashboardServiceTests
{
    [Theory]
    [InlineData("2026-11-09", "2026-11-09")] // Monday stays Monday
    [InlineData("2026-11-12", "2026-11-09")] // Thursday
    [InlineData("2026-11-15", "2026-11-09")] // Sunday goes back, not forward
    public void GetMonday_ReturnsStartOfIsoWeek(string date, string expectedMonday)
    {
        DashboardService.GetMonday(DateOnly.Parse(date)).ShouldBe(DateOnly.Parse(expectedMonday));
    }

    [Fact]
    public async Task GetWeeklySummaryAsync_AggregatesByDayStatusAndProject()
    {
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var monday = TimeEntry.Create(userId, projectA, new DateOnly(2026, 11, 9), 6, "A", now);
        var tuesday = TimeEntry.Create(userId, projectB, new DateOnly(2026, 11, 10), 4, "B", now);
        tuesday.Submit(now);

        var repository = Substitute.For<ITimeEntryRepository>();
        repository
            .ListForUserAsync(
                userId,
                new DateOnly(2026, 11, 9),
                new DateOnly(2026, 11, 15),
                Arg.Is<TimeEntryStatus?>(status => status == null),
                Arg.Any<CancellationToken>())
            .Returns(new List<TimeEntryDetails>
            {
                new(monday, "AUD-1", "Audit", "Felipe"),
                new(tuesday, "CLD-1", "Cloud", "Felipe"),
            });

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);

        var sut = new DashboardService(repository, currentUser, new FixedTimeProvider(now));

        var summary = await sut.GetWeeklySummaryAsync(weekOf: null, CancellationToken.None);

        summary.WeekStart.ShouldBe(new DateOnly(2026, 11, 9));
        summary.TotalHours.ShouldBe(10);
        summary.DraftHours.ShouldBe(6);
        summary.SubmittedHours.ShouldBe(4);
        summary.DailyHours.Count.ShouldBe(7);
        summary.DailyHours[0].Hours.ShouldBe(6);
        summary.HoursByProject[0].ProjectCode.ShouldBe("AUD-1");
    }
}
