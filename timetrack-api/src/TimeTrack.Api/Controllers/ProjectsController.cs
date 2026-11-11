using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Security;
using TimeTrack.Application.Common.Models;
using TimeTrack.Application.Projects;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
[Produces("application/json")]
public sealed class ProjectsController(IProjectService projectService) : ControllerBase
{
    /// <summary>Searches projects by code, name or client (used by the Angular search box).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProjectDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ProjectDto>> Search(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        projectService.SearchAsync(new ProjectQuery(search, activeOnly, page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProjectDto> Get(Guid id, CancellationToken cancellationToken) =>
        projectService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = AuthConstants.ManagerPolicy)]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }
}
