using Microsoft.Extensions.Logging.Abstractions;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Exceptions;
using TimeTrack.Application.Common.Models;
using TimeTrack.Application.TimeEntries;
using TimeTrack.Domain.Common;
using TimeTrack.Domain.Projects;
using TimeTrack.Domain.TimeEntries;

namespace TimeTrack.Application.Tests;

public sealed class TimeEntryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 9);

    private readonly ITimeEntryRepository _timeEntries = Substitute.For<ITimeEntryRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Project _project = Project.Create("CLD-2026-021", "Azure Cloud Migration", "Fabrikam Bank", 900);
    private readonly TimeEntryService _sut;

    public TimeEntryServiceTests()
    {
        _currentUser.UserId.Returns(_userId);
        _projects.GetByIdAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(_project);

        _sut = new TimeEntryService(
            _timeEntries,
            _projects,
            _unitOfWork,
            _currentUser,
            new FixedTimeProvider(Now),
            new TimeEntryQueryValidator(),
            new CreateTimeEntryRequestValidator(),
            new UpdateTimeEntryRequestValidator(),
            NullLogger<TimeEntryService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_AddsEntryAndSaves()
    {
        TimeEntry? added = null;
        _timeEntries.When(r => r.Add(Arg.Any<TimeEntry>())).Do(call => added = call.Arg<TimeEntry>());
        _timeEntries.GetDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => new TimeEntryDetails(added!, _project.Code, _project.Name, "Felipe Urueta"));

        var result = await _sut.CreateAsync(
            new CreateTimeEntryRequest(_project.Id, Today, 3, "Landing zone design"),
            CancellationToken.None);

        result.Status.ShouldBe(TimeEntryStatus.Draft);
        result.Hours.ShouldBe(3);
        result.ProjectCode.ShouldBe("CLD-2026-021");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenDailyLimitWouldBeExceeded_ThrowsAndDoesNotSave()
    {
        // Arg.Is for the null argument: mixing a literal default value with Arg.Any is ambiguous for NSubstitute.
        _timeEntries
            .GetLoggedHoursAsync(_userId, Today, Arg.Is<Guid?>(id => id == null), Arg.Any<CancellationToken>())
            .Returns(10m);

        var exception = await Should.ThrowAsync<DomainException>(() => _sut.CreateAsync(
            new CreateTimeEntryRequest(_project.Id, Today, 2.5m, "Late workshop"),
            CancellationToken.None));

        exception.Code.ShouldBe("TimeEntry.DailyLimitExceeded");
        _timeEntries.DidNotReceive().Add(Arg.Any<TimeEntry>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ForInactiveProject_Throws()
    {
        _project.Deactivate();

        var exception = await Should.ThrowAsync<DomainException>(() => _sut.CreateAsync(
            new CreateTimeEntryRequest(_project.Id, Today, 2, "Training"),
            CancellationToken.None));

        exception.Code.ShouldBe("TimeEntry.InactiveProject");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ThrowsValidationException()
    {
        await Should.ThrowAsync<FluentValidation.ValidationException>(() => _sut.CreateAsync(
            new CreateTimeEntryRequest(Guid.Empty, Today, 0, string.Empty),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithStaleConcurrencyStamp_ThrowsConflict()
    {
        var entry = TimeEntry.Create(_userId, _project.Id, Today, 2, "Original", Now);
        _timeEntries.GetByIdAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(entry);

        var staleStamp = Guid.NewGuid();

        await Should.ThrowAsync<ConcurrencyConflictException>(() => _sut.UpdateAsync(
            entry.Id,
            new UpdateTimeEntryRequest(_project.Id, Today, 3, "Changed", staleStamp),
            CancellationToken.None));

        entry.Hours.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_EntryOfAnotherUser_ThrowsNotFound()
    {
        var someoneElsesEntry = TimeEntry.Create(Guid.NewGuid(), _project.Id, Today, 2, "Not mine", Now);
        _timeEntries.GetByIdAsync(someoneElsesEntry.Id, Arg.Any<CancellationToken>()).Returns(someoneElsesEntry);

        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateAsync(
            someoneElsesEntry.Id,
            new UpdateTimeEntryRequest(_project.Id, Today, 3, "Hijack", someoneElsesEntry.ConcurrencyStamp),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_ExcludesTheEditedEntryFromTheDailyTotal()
    {
        var entry = TimeEntry.Create(_userId, _project.Id, Today, 6, "Original", Now);
        _timeEntries.GetByIdAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(entry);
        _timeEntries.GetLoggedHoursAsync(_userId, Today, entry.Id, Arg.Any<CancellationToken>()).Returns(4m);
        _timeEntries.GetDetailsAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(_ => new TimeEntryDetails(entry, _project.Code, _project.Name, "Felipe Urueta"));

        // 4 other hours + 8 = 12: exactly at the limit, so it must be accepted.
        var result = await _sut.UpdateAsync(
            entry.Id,
            new UpdateTimeEntryRequest(_project.Id, Today, 8, "Extended", entry.ConcurrencyStamp),
            CancellationToken.None);

        result.Hours.ShouldBe(8);
    }
}
