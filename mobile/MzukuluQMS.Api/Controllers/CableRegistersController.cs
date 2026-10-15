using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Controllers;
[ApiController,Authorize(Policy="QmsUser"),Route("api/projects/{projectId:long}/cable-registers")]
public sealed class CableRegistersController(CableRegisters service):ControllerBase
{
 [HttpGet]public async Task<IActionResult> List(long projectId,CancellationToken ct)=>Ok(await service.ListAsync(projectId,ct));
 [HttpGet("{registerId:long}")]public async Task<IActionResult> Get(long projectId,long registerId,CancellationToken ct)=>Ok(await service.GetAsync(projectId,registerId,ct));
 [HttpPost]public async Task<IActionResult> Create(long projectId,CableRegisterInput request,CancellationToken ct)=>StatusCode(201,await service.SaveRegisterAsync(projectId,null,request,ct));
 [HttpPut("{registerId:long}")]public async Task<IActionResult> Update(long projectId,long registerId,CableRegisterInput request,CancellationToken ct)=>Ok(await service.SaveRegisterAsync(projectId,registerId,request,ct));
 [HttpPost("{registerId:long}/cables")]public async Task<IActionResult> Add(long projectId,long registerId,[FromQuery]long registerRevision,CableInput request,CancellationToken ct){await service.SaveCableAsync(projectId,registerId,null,registerRevision,request,ct);return NoContent();}
 [HttpPut("{registerId:long}/cables/{cableId:long}")]public async Task<IActionResult> UpdateCable(long projectId,long registerId,long cableId,[FromQuery]long registerRevision,CableInput request,CancellationToken ct){await service.SaveCableAsync(projectId,registerId,cableId,registerRevision,request,ct);return NoContent();}
 [HttpPost("{registerId:long}/verify")]public async Task<IActionResult> Verify(long projectId,long registerId,[FromQuery]long revision,CancellationToken ct){await service.VerifyAsync(projectId,registerId,revision,ct);return NoContent();}
}
