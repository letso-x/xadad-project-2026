using MzukuluQMS.Api.DTOs.Users;

namespace MzukuluQMS.Api.Repositories.Users;

public interface IUserRepository
{
    Task<CurrentUserDto?> GetByExternalAuthIdAsync(
        Guid externalAuthId,
        CancellationToken cancellationToken = default);
}