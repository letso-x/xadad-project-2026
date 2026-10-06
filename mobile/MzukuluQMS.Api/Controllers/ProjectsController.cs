using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Data.Repositories;

namespace MzukuluQMS.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;

    public ProjectsController(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects(
        CancellationToken cancellationToken)
    {
        var projects =
            await _projectRepository.GetAllAsync(cancellationToken);

        return Ok(projects);
    }
} 