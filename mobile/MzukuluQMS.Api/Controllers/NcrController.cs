using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Controllers;
[Authorize(Policy="QmsUser")][ApiController][Route("api/qc-forms/{formId:long}/ncrs")]
public sealed class NcrController(NcrService service) : ControllerBase
{
 [HttpGet] public async Task<IActionResult> List(long formId,CancellationToken ct)=>Ok(await service.ListAsync(formId,ct));
 [HttpPost("{ncrId:long}/correct")] public async Task<IActionResult> Correct(long formId,long ncrId,CorrectNcrRequest request,CancellationToken ct){await service.CorrectAsync(formId,ncrId,request,ct);return NoContent();}
 [HttpPost("{ncrId:long}/verify")] public async Task<IActionResult> Verify(long formId,long ncrId,VerifyNcrRequest request,CancellationToken ct){await service.VerifyAsync(formId,ncrId,request,ct);return NoContent();}
}
