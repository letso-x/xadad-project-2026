using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using MzukuluQMS.Api.Repositories.Users;

namespace MzukuluQMS.Api.Security;

public sealed class QmsClaimsTransformation : IClaimsTransformation
{
    private readonly IUserRepository _userRepository;

    public QmsClaimsTransformation(
        IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<ClaimsPrincipal> TransformAsync(
        ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var subject =
            principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(subject) ||
            !Guid.TryParse(subject, out var externalAuthId))
        {
            return principal;
        }

        var user =
            await _userRepository.GetByExternalAuthIdAsync(
                externalAuthId);

        if (user is null || !user.IsActive)
        {
            return principal;
        }

        if (principal.Identity is not ClaimsIdentity identity)
        {
            return principal;
        }

        if (!identity.HasClaim(
                claim => claim.Type == "qms_user_id"))
        {
            identity.AddClaim(
                new Claim(
                    "qms_user_id",
                    user.UserID.ToString()));
        }

        if (!identity.HasClaim(
                claim => claim.Type == "qms_role"))
        {
            identity.AddClaim(
                new Claim(
                    "qms_role",
                    user.Role));
        }

        return principal;
    }
}