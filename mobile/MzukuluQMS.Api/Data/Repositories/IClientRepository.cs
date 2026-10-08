using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public interface IClientRepository
{
    Task<IReadOnlyList<ClientDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ClientDto?> GetByIdAsync(
        long clientId,
        CancellationToken cancellationToken = default);

    Task<ClientDto> CreateAsync(
    CreateClientRequest request,
    CancellationToken cancellationToken = default);

    Task<ClientDto?> UpdateAsync(
        long clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default);
}