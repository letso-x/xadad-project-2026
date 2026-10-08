using Dapper;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ClientRepository(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ClientDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "ClientID",
                "ClientName",
                "ClientCode",
                "ContactName",
                "ContactEmail",
                "ContactPhone",
                "AddressLine1",
                "AddressLine2",
                "City",
                "Province",
                "PostalCode",
                "IsActive",
                "CreatedAt",
                "UpdatedAt"
            FROM public."Client"
            ORDER BY
                "ClientName",
                "ClientID";
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            cancellationToken: cancellationToken);

        var clients =
            await connection.QueryAsync<ClientDto>(command);

        return clients.AsList();
    }

    public async Task<ClientDto?> GetByIdAsync(
        long clientId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "ClientID",
                "ClientName",
                "ClientCode",
                "ContactName",
                "ContactEmail",
                "ContactPhone",
                "AddressLine1",
                "AddressLine2",
                "City",
                "Province",
                "PostalCode",
                "IsActive",
                "CreatedAt",
                "UpdatedAt"
            FROM public."Client"
            WHERE "ClientID" = @ClientID;
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                ClientID = clientId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ClientDto>(
            command);
    }
    public async Task<ClientDto> CreateAsync(
    CreateClientRequest request,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO public."Client"
        (
            "ClientName",
            "ClientCode",
            "ContactName",
            "ContactEmail",
            "ContactPhone",
            "AddressLine1",
            "AddressLine2",
            "City",
            "Province",
            "PostalCode",
            "IsActive",
        )
        VALUES
        (
            @ClientName,
            @ClientCode,
            @ContactName,
            @ContactEmail,
            @ContactPhone,
            @AddressLine1,
            @AddressLine2,
            @City,
            @Province,
            @PostalCode,
            @IsActive
        )
        RETURNING
            "ClientID",
            "ClientName",
            "ClientCode",
            "ContactName",
            "ContactEmail",
            "ContactPhone",
            "AddressLine1",
            "AddressLine2",
            "City",
            "Province",
            "PostalCode",
            "IsActive",
            "CreatedAt",
            "UpdatedAt";
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            request,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<ClientDto>(
            command);
    }
    public async Task<ClientDto?> UpdateAsync(
    long clientId,
    UpdateClientRequest request,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE public."Client"
        SET
            "ClientName" = @ClientName,
            "ClientCode" = @ClientCode,
            "ContactName" = @ContactName,
            "ContactEmail" = @ContactEmail,
            "ContactPhone" = @ContactPhone,
            "AddressLine1" = @AddressLine1,
            "AddressLine2" = @AddressLine2,
            "City" = @City,
            "Province" = @Province,
            "PostalCode" = @PostalCode,
            "IsActive" = @IsActive,
            "UpdatedAt" = now()
        WHERE "ClientID" = @ClientID
        RETURNING
            "ClientID",
            "ClientName",
            "ClientCode",
            "ContactName",
            "ContactEmail",
            "ContactPhone",
            "AddressLine1",
            "AddressLine2",
            "City",
            "Province",
            "PostalCode",
            "IsActive",
            "CreatedAt",
            "UpdatedAt";
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            ClientID = clientId,
            request.ClientName,
            request.ClientCode,
            request.ContactName,
            request.ContactEmail,
            request.ContactPhone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Province,
            request.PostalCode,
            request.IsActive
        };

        var command = new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ClientDto>(
            command);
    }

}