using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Domain.Projects;
using TimeTrack.Domain.TimeEntries;
using TimeTrack.Domain.Users;

namespace TimeTrack.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public const string DemoPassword = "Demo123!";

    /// <summary>
    /// Creates the schema and loads demo data. If EF migrations exist, it applies them;
    /// otherwise (first run, no migrations generated yet) it creates the schema directly.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, bool seedDemoData, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TimeTrackDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (!seedDemoData || await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await SeedAsync(db, hasher, timeProvider.GetUtcNow(), cancellationToken);

        logger.LogInformation("DatabaseInitializer.InitializeAsync: demo data seeded");
    }

    private static async Task SeedAsync(
        TimeTrackDbContext db,
        IPasswordHasher hasher,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var manager = CreateUser("manager@timetrack.dev", "Laura Bennett", UserRole.Manager, hasher);
        var felipe = CreateUser("consultant@timetrack.dev", "Felipe Urueta", UserRole.Consultant, hasher);
        var priya = CreateUser("priya@timetrack.dev", "Priya Sharma", UserRole.Consultant, hasher);
        db.Users.AddRange(manager, felipe, priya);

        var audit = Project.Create("AUD-2026-014", "Annual Financial Audit", "Northwind Retail", 1200);
        var cloud = Project.Create("CLD-2026-021", "Azure Cloud Migration", "Fabrikam Bank", 900);
        var tax = Project.Create("TAX-2026-007", "Tax Advisory", "Contoso Energy", 400);
        var data = Project.Create("DAI-2026-003", "Data & AI Strategy", "Adventure Works", 650);
        var training = Project.Create("INT-2025-099", "Internal Training", "TimeTrack", 200);
        training.Deactivate();
        db.Projects.AddRange(audit, cloud, tax, data, training);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var lastMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7) - 7);

        // Felipe: a full previous week with every status, plus today's drafts.
        // (The Angular demo mode seeds exactly the same data.)
        db.TimeEntries.AddRange(
            Approved(felipe, cloud, lastMonday, 8, "Sprint planning and landing zone backlog grooming"),
            Approved(felipe, cloud, lastMonday.AddDays(1), 6, "Designed the landing zone and network topology"),
            Approved(felipe, data, lastMonday.AddDays(1), 2, "Stakeholder interviews for the AI use-case inventory"),
            Approved(felipe, audit, lastMonday.AddDays(2), 7.5m, "Walkthrough of the revenue recognition controls"),
            Approved(felipe, cloud, lastMonday.AddDays(3), 4, "Bicep modules for the hub-and-spoke network"),
            Rejected(felipe, audit, lastMonday.AddDays(3), 4, "Client meeting", "Please describe which audit area the meeting covered."),
            Submitted(felipe, cloud, lastMonday.AddDays(4), 7.5m, "Migrated the reporting API to Azure App Service"),
            Submitted(felipe, data, lastMonday.AddDays(4), 1, "Prepared the data governance workshop agenda"),
            TimeEntry.Create(felipe.Id, data.Id, today, 3, "Workshop prep: data governance maturity assessment", now),
            TimeEntry.Create(felipe.Id, cloud.Id, today, 2.5m, "Code review of the Angular time-entry module", now),
            // Priya: submitted entries so the manager has a pending queue.
            Submitted(priya, tax, lastMonday.AddDays(4), 8, "Prepared the transfer pricing documentation"),
            Submitted(priya, audit, today, 5.25m, "Tested revenue recognition controls"));

        await db.SaveChangesAsync(cancellationToken);

        TimeEntry Submitted(User user, Project project, DateOnly date, decimal hours, string description)
        {
            var entry = TimeEntry.Create(user.Id, project.Id, date, hours, description, now);
            entry.Submit(now);
            return entry;
        }

        TimeEntry Approved(User user, Project project, DateOnly date, decimal hours, string description)
        {
            var entry = Submitted(user, project, date, hours, description);
            entry.Approve(manager.Id, now);
            return entry;
        }

        TimeEntry Rejected(User user, Project project, DateOnly date, decimal hours, string description, string reason)
        {
            var entry = Submitted(user, project, date, hours, description);
            entry.Reject(manager.Id, reason, now);
            return entry;
        }
    }

    private static User CreateUser(string email, string fullName, UserRole role, IPasswordHasher hasher)
    {
        var user = User.Create(email, fullName, role);
        user.SetPasswordHash(hasher.Hash(user, DemoPassword));
        return user;
    }
}
