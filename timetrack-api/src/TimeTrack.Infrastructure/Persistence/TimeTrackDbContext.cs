using Microsoft.EntityFrameworkCore;
using TimeTrack.Domain.Projects;
using TimeTrack.Domain.TimeEntries;
using TimeTrack.Domain.Users;

namespace TimeTrack.Infrastructure.Persistence;

public sealed class TimeTrackDbContext(DbContextOptions<TimeTrackDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeTrackDbContext).Assembly);
    }
}
