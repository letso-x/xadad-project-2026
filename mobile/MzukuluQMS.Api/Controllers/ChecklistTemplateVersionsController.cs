using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Controllers;

[ApiController]
[Route("api/checklist-template-versions")]
public sealed class ChecklistTemplateVersionsController : ControllerBase
{
    private readonly IChecklistTemplateService _service;

    public ChecklistTemplateVersionsController(
        IChecklistTemplateService service)
    {
        _service = service;
    }

    [HttpGet("{versionId:long}")]
    [ProducesResponseType(
        typeof(ChecklistTemplateVersionDefinitionDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChecklistTemplateVersionDefinitionDto>>
        GetVersionDefinition(
            long versionId,
            CancellationToken cancellationToken)
    {
        var definition =
            await _service.GetVersionDefinitionAsync(
                versionId,
                cancellationToken);

        if (definition is null)
        {
            return NotFound();
        }

        return Ok(definition);
    }
}