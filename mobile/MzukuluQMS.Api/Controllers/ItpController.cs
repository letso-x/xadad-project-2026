using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Controllers;
[ApiController,Authorize(Policy="QmsUser"),Route("api/projects/{projectId:long}/itps")]
public sealed class ItpController(ItpService service):ControllerBase
{
 [HttpGet]public async Task<IActionResult> List(long projectId,CancellationToken ct)=>Ok(await service.List(projectId,ct));
 [HttpGet("progress")]public async Task<IActionResult> Progress(long projectId,CancellationToken ct)=>Ok(await service.ProjectProgress(projectId,ct));
 [HttpGet("{versionId:long}")]public async Task<IActionResult> Get(long projectId,long versionId,CancellationToken ct)=>Ok(await service.Get(projectId,versionId,ct));
 [HttpGet("{versionId:long}/audit")]public async Task<IActionResult> Audit(long projectId,long versionId,CancellationToken ct)=>Ok(await service.AuditHistory(projectId,versionId,ct));
 [HttpPost]public async Task<IActionResult> Create(long projectId,ItpDefinition request,CancellationToken ct)=>StatusCode(201,await service.Create(projectId,request,ct));
 [HttpPut("{versionId:long}")]public async Task<IActionResult> Save(long projectId,long versionId,ItpWrite request,CancellationToken ct)=>Ok(await service.Save(projectId,versionId,request,ct));
 [HttpPost("{versionId:long}/{action}")]public async Task<IActionResult> Act(long projectId,long versionId,string action,ItpAction request,CancellationToken ct)=>Ok(await service.Act(projectId,versionId,action,request,ct));
}
