using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Controllers;

[ApiController]
[Route("api/qc-forms")]
public sealed class QCFormsController : ControllerBase
{
    private readonly IQCFormService _service;

    public QCFormsController(
        IQCFormService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(QCFormDto),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QCFormDto>> CreateQCForm(
        [FromBody] CreateQCFormRequest request,
        CancellationToken cancellationToken)
    {
        var form =
            await _service.CreateAsync(
                request,
                cancellationToken);

        return Created(
            $"/api/qc-forms/{form.QCFormID}",
            form);
    }

    [HttpGet("{qcFormId:long}")]
    [ProducesResponseType(
    typeof(QCFormDetailsDto),
    StatusCodes.Status200OK)]
    [ProducesResponseType(
    StatusCodes.Status404NotFound)]
    [ProducesResponseType(
    StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QCFormDetailsDto>> GetQCForm(
    long qcFormId,
    CancellationToken cancellationToken)
    {
        var form =
            await _service.GetByIdAsync(
                qcFormId,
                cancellationToken);

        if (form is null)
        {
            return NotFound();
        }

        return Ok(form);
    }
    [HttpPut("{qcFormId:long}/responses/{qcResponseId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResponse(
    long qcFormId,
    long qcResponseId,
    [FromBody] UpdateQCResponseRequest request,
    CancellationToken cancellationToken)
    {
        await _service.UpdateResponseAsync(
            qcFormId,
            qcResponseId,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpPut("{qcFormId:long}/project-fields/{qcFormProjectFieldId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProjectField(
    long qcFormId,
    long qcFormProjectFieldId,
    [FromBody] UpdateQCFormProjectFieldRequest request,
    CancellationToken cancellationToken)
    {
        await _service.UpdateProjectFieldAsync(
            qcFormId,
            qcFormProjectFieldId,
            request,
            cancellationToken);

        return NoContent();
    }

}