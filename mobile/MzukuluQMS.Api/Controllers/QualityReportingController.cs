using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Controllers;
[Authorize(Policy="QmsUser")][ApiController][Route("api/quality")]
public sealed class QualityReportingController(QualityReporting service) : ControllerBase
{
 [HttpGet("projects/{projectId:long}/dashboard")]public async Task<IActionResult> Dashboard(long projectId,CancellationToken ct)=>Ok(await service.DashboardAsync(projectId,ct));
 [HttpGet("projects/{projectId:long}/handover-readiness")]public async Task<IActionResult> Readiness(long projectId,CancellationToken ct)=>Ok(await service.ReadinessAsync(projectId,ct));
 [HttpGet("notifications")]public async Task<IActionResult> Notifications(CancellationToken ct)=>Ok(await service.NotificationsAsync(ct));
 [HttpPost("notifications/{notificationId:long}/read")]public async Task<IActionResult> Read(long notificationId,CancellationToken ct){await service.MarkReadAsync(notificationId,ct);return NoContent();}
}
