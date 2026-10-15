using Microsoft.AspNetCore.Authorization;
﻿using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Controllers;

[Authorize(Policy = "QmsUser")]
[Authorize(Policy = "ReadTemplates")]
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

    [HttpPost,Authorize(Policy="ManageTemplates")]
    public async Task<IActionResult> Create([FromBody] CreateChecklistTemplateRequest request,[FromServices] MzukuluQMS.Api.Services.Workflow.TemplateAuthoring authoring,CancellationToken ct)
      =>StatusCode(201,await authoring.CreateAsync(request,ct));

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