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

    public EvidenceController(
        IEvidenceService service)
    {
        _service = service;
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