using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Services.Users;
namespace MzukuluQMS.Api.Controllers;
[ApiController,Authorize(Policy="QmsUser"),Route("api/auth/permissions")]
public sealed class MobileContextController(QmsDbSession session,ICurrentUserService users) : ControllerBase
{
 [HttpGet] public async Task<IActionResult> Permissions(CancellationToken ct)
 {
  var actor=await users.GetCurrentUserAsync(ct);
  await using var lease=await session.OpenAsync(ct);
  var permissions=await lease.Connection.QueryAsync<string>(new CommandDefinition("""
   SELECT rp."Permission" FROM qms_security."RolePermission" rp JOIN public."User" u ON u."Role"::text=rp."RoleName"
   WHERE u."UserID"=@id AND u."IsActive" ORDER BY rp."Permission"
   """,new {id=actor.UserID},cancellationToken:ct));
  return Ok(new {userID=actor.UserID,permissions=permissions.AsList(),asOf=DateTimeOffset.UtcNow});
 }
}
