using Dapper;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services.Users;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentUserService _currentUsers;

    public ClientRepository(
        IDbConnectionFactory connectionFactory, ICurrentUserService currentUsers)
    {
        _connectionFactory = connectionFactory;
        _currentUsers = currentUsers;
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
            "IsActive"
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

        var actor = await _currentUsers.GetCurrentUserAsync(cancellationToken);
        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            request, transaction,
            cancellationToken: cancellationToken);

        var result = await connection.QuerySingleAsync<ClientDto>(command);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO qms_security."AuditEvent" ("ActorUserID","Action","EntityType","EntityID","Details")
            VALUES (@actor,'ClientCreated','Client',@id,jsonb_build_object('isActive',CAST(@active AS boolean)))
            """,new { actor=actor.UserID,id=result.ClientID,active=result.IsActive },transaction,cancellationToken:cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return result;
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

        var actor = await _currentUsers.GetCurrentUserAsync(cancellationToken);
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

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            parameters, transaction,
            cancellationToken: cancellationToken);

        var result = await connection.QuerySingleOrDefaultAsync<ClientDto>(command);
        if(result is null)return null;
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO qms_security."AuditEvent" ("ActorUserID","Action","EntityType","EntityID","Details")
            VALUES (@actor,'ClientUpdated','Client',@id,jsonb_build_object('isActive',CAST(@active AS boolean)))
            """,new { actor=actor.UserID,id=clientId,active=result.IsActive },transaction,cancellationToken:cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

}
