using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using MzukuluQMS.Api.Data;
namespace MzukuluQMS.Api.Security;
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
public sealed class PermissionHandler(IDbConnectionFactory factory) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if(!MfaPolicies.IsAssured(context.User) || !Guid.TryParse(context.User.FindFirstValue("sub"),out var subject)) return;
        await using var connection=await factory.CreateOpenConnectionAsync();
        if(await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM public."User" u JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text
              WHERE u."ExternalAuthID"=@subject AND u."IsActive" AND rp."Permission"=@permission)
            """,new { subject, permission=requirement.Permission }))) context.Succeed(requirement);
    }
}
