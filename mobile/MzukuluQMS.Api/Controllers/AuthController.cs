using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Repositories.Users;

namespace MzukuluQMS.Api.Controllers;


[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public AuthController(
        IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var subject =
            User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(subject) ||
            !Guid.TryParse(subject, out var externalAuthId))
        {
            return Unauthorized();
        }

        var user =
            await _userRepository.GetByExternalAuthIdAsync(
                externalAuthId,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new
            {
                Message =
                    "The authenticated Supabase user is not linked to a QMS user."
            });
        }

        if (!user.IsActive)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Message =
                        "The QMS user account is inactive."
                });
        }

        return Ok(user);
    }
}