using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;
using MzukuluQMS.Api.Services.Workflow;

namespace MzukuluQMS.Api.Controllers;

[Authorize(Policy = "QmsUser")]
[ApiController]
[Route("api/qc-forms")]
public sealed class QCFormsController : ControllerBase
{
    private readonly IQCFormService _service;
    private readonly QCFormQueries _queries;

    public QCFormsController(
        IQCFormService service, QCFormQueries queries)
    {
        _service = service;
        _queries = queries;
    }

    [HttpGet("{qcFormId:long}/evidence")]
    public async Task<IActionResult> Evidence(long qcFormId,CancellationToken ct)=>Ok(await _queries.EvidenceAsync(qcFormId,ct));
    [HttpPut("{qcFormId:long}/remarks")]
    public async Task<IActionResult> Remarks(long qcFormId,[FromBody] RemarksRequest request,CancellationToken ct)
    {await _queries.UpdateRemarksAsync(qcFormId,request.Remarks,ct);return NoContent();}
    public sealed record RemarksRequest(string Remarks);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long projectId, CancellationToken cancellationToken, [FromQuery] int page=1, [FromQuery] int pageSize=50, [FromQuery] string? status=null) =>
        Ok(await _queries.ListAsync(projectId,page,pageSize,status,cancellationToken));

    [HttpGet("{qcFormId:long}/sign-offs")]
    public async Task<IActionResult> SignOffHistory(long qcFormId,CancellationToken cancellationToken) =>
        Ok(await _queries.SignOffsAsync(qcFormId,cancellationToken));

    [HttpGet("{qcFormId:long}/audit-history")]
    public async Task<IActionResult> PublicAuditHistory(long qcFormId,CancellationToken ct)=>Ok(await _queries.PublicAuditAsync(qcFormId,ct));

    [HttpGet("{qcFormId:long}/audit")]
    public async Task<IActionResult> AuditHistory(long qcFormId,CancellationToken cancellationToken,[FromQuery] long afterId=0,[FromQuery] int limit=50) =>
        Ok(await _queries.AuditAsync(qcFormId,afterId,limit,cancellationToken));

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

        Response.Headers.ETag = "\"" + form.Revision + "\"";
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

    [HttpPost("{qcFormId:long}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitQCForm(
    long qcFormId,
    CancellationToken cancellationToken)
    {
        await _service.SubmitAsync(
            qcFormId,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{qcFormId:long}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveQCForm(
    long qcFormId,
    CancellationToken cancellationToken)
    {
        await _service.ApproveAsync(
            qcFormId,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{qcFormId:long}/return-for-correction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnForCorrection(
    long qcFormId,
    [FromBody] ReturnForCorrectionRequest request,
    CancellationToken cancellationToken)
    {
        await _service.ReturnForCorrectionAsync(
            qcFormId,
            request.Reason,
            cancellationToken);

        return NoContent();
    }

}