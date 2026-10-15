namespace MzukuluQMS.Api.DTOs;

public sealed class CreateClientRequest
{
    public string ClientName { get; init; } = string.Empty;

    public string? ClientCode { get; init; }

    public string? ContactName { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public string? AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    public string? City { get; init; }

    public string? Province { get; init; }

    public string? PostalCode { get; init; }

    public bool IsActive { get; init; } = true;
}