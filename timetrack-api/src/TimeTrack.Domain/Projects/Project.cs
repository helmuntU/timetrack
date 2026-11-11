using System.Text.RegularExpressions;
using TimeTrack.Domain.Common;

namespace TimeTrack.Domain.Projects;

public sealed partial class Project : Entity
{
    public const int MaxNameLength = 150;
    public const int MaxClientNameLength = 150;
    public const int MaxCodeLength = 20;

    private Project()
    {
    }

    /// <summary>Business code, for example "AUD-2026-014".</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string ClientName { get; private set; } = string.Empty;

    public decimal BudgetHours { get; private set; }

    public bool IsActive { get; private set; }

    public static Project Create(string code, string name, string clientName, decimal budgetHours)
    {
        var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedCode.Length is 0 or > MaxCodeLength || !CodePattern().IsMatch(normalizedCode))
        {
            throw new DomainException(
                "Project.InvalidCode",
                "The project code must contain only letters, numbers and dashes (max 20 characters).");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            throw new DomainException("Project.InvalidName", "The project name is required (max 150 characters).");
        }

        if (string.IsNullOrWhiteSpace(clientName) || clientName.Trim().Length > MaxClientNameLength)
        {
            throw new DomainException("Project.InvalidClient", "The client name is required (max 150 characters).");
        }

        if (budgetHours <= 0)
        {
            throw new DomainException("Project.InvalidBudget", "The budget must be greater than zero hours.");
        }

        return new Project
        {
            Code = normalizedCode,
            Name = name.Trim(),
            ClientName = clientName.Trim(),
            BudgetHours = budgetHours,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex CodePattern();
}
