using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs.Evidence;
using MzukuluQMS.Api.Services.Evidence;

namespace MzukuluQMS.Api.Controllers;

[Authorize(Policy = "QmsUser")]
[ApiController]
[Route("api/qc-forms/{qcFormId:long}/evidence")]
public sealed class EvidenceController : ControllerBase
{
    private readonly IEvidenceService _service;
    private readonly EvidenceContentService _content;

    public EvidenceController(
        IEvidenceService service, EvidenceContentService content)
    {
        _service = service;
        _content = content;
    }

    [HttpGet("{evidenceId:long}/content")]
    public async Task<IActionResult> Download(long qcFormId,long evidenceId,CancellationToken ct)
    {
        var file=await _content.DownloadAsync(qcFormId,evidenceId,ct);
        Response.Headers.CacheControl="no-store";Response.Headers["X-Content-Type-Options"]="nosniff";
        return File(file.Bytes,file.ContentType,file.FileName);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(EvidenceDto),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvidenceDto>> UploadEvidence(
        long qcFormId,
        [FromForm] UploadEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var evidence =
            await _service.UploadAsync(
                qcFormId,
                request,
                cancellationToken);

        return Created(
            $"/api/qc-forms/{qcFormId}/evidence/{evidence.EvidenceID}",
            evidence);
    }
}