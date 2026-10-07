using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Controllers;

[ApiController]
[Route("api/checklist-templates")]
public sealed class ChecklistTemplatesController : ControllerBase
{
    private readonly IChecklistTemplateService _service;

    public ChecklistTemplatesController(
        IChecklistTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<ChecklistTemplateDto>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChecklistTemplateDto>>> GetTemplates(
        CancellationToken cancellationToken)
    {
        var templates =
            await _service.GetAllAsync(cancellationToken);

        return Ok(templates);
    }
}