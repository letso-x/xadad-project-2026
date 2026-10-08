using MzukuluQMS.Api.DTOs.Users;

namespace MzukuluQMS.Api.Services.Users;

public interface ICurrentUserService
{
    Task<CurrentUserDto> GetCurrentUserAsync(
        CancellationToken cancellationToken = default);
}