using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Domain.Projects;
using TimeTrack.Domain.TimeEntries;
using TimeTrack.Domain.Users;

namespace TimeTrack.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();

        // Enums as strings: readable in the database and safe if the enum order changes.
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(u => u.IsManager);
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code).HasMaxLength(Project.MaxCodeLength).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Name).HasMaxLength(Project.MaxNameLength).IsRequired();
        builder.Property(p => p.ClientName).HasMaxLength(Project.MaxClientNameLength).IsRequired();
        builder.Property(p => p.BudgetHours).HasPrecision(9, 2);
    }
}

internal sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("TimeEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Hours).HasPrecision(5, 2);
        builder.Property(e => e.Description).HasMaxLength(TimeEntry.MaxDescriptionLength).IsRequired();
        builder.Property(e => e.RejectionReason).HasMaxLength(TimeEntry.MaxRejectionReasonLength);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        // EF adds "WHERE ConcurrencyStamp = @original" to every UPDATE/DELETE.
        // If no row matches, it throws DbUpdateConcurrencyException (handled in UnitOfWork).
        builder.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // Covers the two hottest queries: "my entries in a date range" and "daily total".
        builder.HasIndex(e => new { e.UserId, e.WorkDate });

        // Covers the manager's "pending approvals" queue.
        builder.HasIndex(e => e.Status);

        builder.Ignore(e => e.CanBeEdited);
    }
}
