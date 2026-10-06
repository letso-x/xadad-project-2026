using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetProjects(
        CancellationToken cancellationToken)
    {
        var projects =
            await _projectService.GetAllAsync(cancellationToken);

        return Ok(projects);
    }

    [HttpGet("{projectId:long:min(1)}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> GetProject(
        long projectId,
        CancellationToken cancellationToken)
    {
        var project =
            await _projectService.GetByIdAsync(
                projectId,
                cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        return Ok(project);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDto>> CreateProject(
    [FromBody] CreateProjectRequest request,
    CancellationToken cancellationToken)
    {
            var project =
                await _projectService.CreateAsync(
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetProject),
                new { projectId = project.ProjectID },
                project);
        
            
        
    }
}