using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs.SignOffs;
using MzukuluQMS.Api.Services.SignOffs;

namespace MzukuluQMS.Api.Controllers;

[Authorize(Policy = "QmsUser")]
[ApiController]
[Route("api/qc-forms/{qcFormId:long}/sign-offs")]
public sealed class QCSignOffController : ControllerBase
{
    private readonly IQCSignOffService _service;

    public QCSignOffController(
        IQCSignOffService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(QCSignOffDto),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QCSignOffDto>> CreateSignOff(
        long qcFormId,
        [FromBody] CreateQCSignOffRequest request,
        CancellationToken cancellationToken)
    {
        var signOff =
            await _service.CreateAsync(
                qcFormId,
                request,
                cancellationToken);

        return Created(
            $"/api/qc-forms/{qcFormId}/sign-offs/{signOff.QCSignOffID}",
            signOff);
    }
}