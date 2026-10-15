using System.Security.Claims;
using MzukuluQMS.Api.DTOs.Users;

namespace MzukuluQMS.Api.Services.Users;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<CurrentUserDto> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var principal =
            _httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException(
                "No authenticated request context is available.");

        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(
                "The request is not authenticated.");
        }

        var userIdClaim =
            principal.FindFirstValue("qms_user_id");

        var role =
            principal.FindFirstValue("qms_role");

        var externalAuthIdClaim =
            principal.FindFirstValue("sub");

        var email =
            principal.FindFirstValue("email")
            ?? string.Empty;

        if (!Guid.TryParse(
                userIdClaim,
                out var userId))
        {
            throw new UnauthorizedAccessException(
                "The authenticated user is not linked to an active QMS account.");
        }

        if (!Guid.TryParse(
                externalAuthIdClaim,
                out var externalAuthId))
        {
            throw new UnauthorizedAccessException(
                "The authenticated token does not contain a valid subject.");
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new UnauthorizedAccessException(
                "The authenticated user does not have a QMS role.");
        }

        var user = new CurrentUserDto
        {
            UserID = userId,
            ExternalAuthID = externalAuthId,
            Email = email,
            Role = role,
            IsActive = true
        };

        return Task.FromResult(user);
    }
}