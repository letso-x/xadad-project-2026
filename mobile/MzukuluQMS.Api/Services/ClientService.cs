using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Services;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _repository;

    public ClientService(IClientRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ClientDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<ClientDto?> GetByIdAsync(
        long clientId,
        CancellationToken cancellationToken = default)
    {
        if (clientId <= 0)
        {
            throw new ArgumentException(
                "ClientID must be greater than zero.",
                nameof(clientId));
        }

        return _repository.GetByIdAsync(
            clientId,
            cancellationToken);
    }

    public Task<ClientDto> CreateAsync(
        CreateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName))
        {
            throw new ArgumentException(
                "ClientName is required.",
                nameof(request.ClientName));
        }

        var normalizedRequest =
            new CreateClientRequest
            {
                ClientName =
                    request.ClientName.Trim(),

                ClientCode =
                    NormalizeOptional(
                        request.ClientCode),

                ContactName =
                    NormalizeOptional(
                        request.ContactName),

                ContactEmail =
                    NormalizeOptional(
                        request.ContactEmail),

                ContactPhone =
                    NormalizeOptional(
                        request.ContactPhone),

                AddressLine1 =
                    NormalizeOptional(
                        request.AddressLine1),

                AddressLine2 =
                    NormalizeOptional(
                        request.AddressLine2),

                City =
                    NormalizeOptional(
                        request.City),

                Province =
                    NormalizeOptional(
                        request.Province),

                PostalCode =
                    NormalizeOptional(
                        request.PostalCode),

                IsActive = request.IsActive
            };

        return _repository.CreateAsync(
            normalizedRequest,
            cancellationToken);
    }

    public Task<ClientDto?> UpdateAsync(
        long clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (clientId <= 0)
        {
            throw new ArgumentException(
                "ClientID must be greater than zero.",
                nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(request.ClientName))
        {
            throw new ArgumentException(
                "ClientName is required.",
                nameof(request.ClientName));
        }

        var normalizedRequest =
            new UpdateClientRequest
            {
                ClientName =
                    request.ClientName.Trim(),

                ClientCode =
                    NormalizeOptional(
                        request.ClientCode),

                ContactName =
                    NormalizeOptional(
                        request.ContactName),

                ContactEmail =
                    NormalizeOptional(
                        request.ContactEmail),

                ContactPhone =
                    NormalizeOptional(
                        request.ContactPhone),

                AddressLine1 =
                    NormalizeOptional(
                        request.AddressLine1),

                AddressLine2 =
                    NormalizeOptional(
                        request.AddressLine2),

                City =
                    NormalizeOptional(
                        request.City),

                Province =
                    NormalizeOptional(
                        request.Province),

                PostalCode =
                    NormalizeOptional(
                        request.PostalCode),

                IsActive =
                    request.IsActive
            };

        return _repository.UpdateAsync(
            clientId,
            normalizedRequest,
            cancellationToken);
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}