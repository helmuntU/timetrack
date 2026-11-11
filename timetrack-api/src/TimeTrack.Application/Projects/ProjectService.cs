using FluentValidation;
using Microsoft.Extensions.Logging;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Exceptions;
using TimeTrack.Application.Common.Models;
using TimeTrack.Domain.Common;
using TimeTrack.Domain.Projects;

namespace TimeTrack.Application.Projects;

public sealed record ProjectDto(Guid Id, string Code, string Name, string ClientName, decimal BudgetHours, bool IsActive);

public sealed record ProjectQuery(string? Search, bool ActiveOnly = true, int Page = 1, int PageSize = 10);

public sealed record CreateProjectRequest(string Code, string Name, string ClientName, decimal BudgetHours);

public sealed class ProjectQueryValidator : AbstractValidator<ProjectQuery>
{
    public ProjectQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(Project.MaxCodeLength).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Project.MaxNameLength);
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(Project.MaxClientNameLength);
        RuleFor(x => x.BudgetHours).GreaterThan(0).LessThanOrEqualTo(100_000);
    }
}

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken);

    Task<ProjectDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);
}

public sealed class ProjectService(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<ProjectQuery> queryValidator,
    IValidator<CreateProjectRequest> createValidator,
    ILogger<ProjectService> logger) : IProjectService
{
    public async Task<PagedResult<ProjectDto>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);

        var result = await projects.SearchAsync(
            query.Search?.Trim(),
            query.ActiveOnly,
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<ProjectDto>(
            result.Items.Select(p => p.ToDto()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), id);

        return project.ToDto();
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        // The controller already applies the "Manager" policy; checking again here keeps the
        // rule safe even if this service is reused from another entry point (defense in depth).
        if (!currentUser.IsManager)
        {
            throw new ForbiddenAccessException("Only managers can create projects.");
        }

        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var project = Project.Create(request.Code, request.Name, request.ClientName, request.BudgetHours);

        if (await projects.CodeExistsAsync(project.Code, cancellationToken))
        {
            throw new DomainException("Project.DuplicateCode", $"A project with code {project.Code} already exists.");
        }

        projects.Add(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProjectService.CreateAsync: project {ProjectCode} created by {UserId}",
            project.Code,
            currentUser.UserId);

        return project.ToDto();
    }
}

internal static class ProjectMappings
{
    public static ProjectDto ToDto(this Project project) =>
        new(project.Id, project.Code, project.Name, project.ClientName, project.BudgetHours, project.IsActive);
}
