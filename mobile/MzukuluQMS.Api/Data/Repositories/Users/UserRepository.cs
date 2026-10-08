using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.DTOs.Users;
using MzukuluQMS.Api.Repositories.Users;

namespace MzukuluQMS.Api.Repositories.Users;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CurrentUserDto?> GetByExternalAuthIdAsync(
        Guid externalAuthId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "UserID",
                "ExternalAuthID",
                "FirstName",
                "LastName",
                "Email",
                "Role"::text AS "Role",
                "IsActive"
            FROM public."User"
            WHERE "ExternalAuthID" = @ExternalAuthID;
            """;

        await using var connection =
    await _connectionFactory.CreateOpenConnectionAsync(
        cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                ExternalAuthID = externalAuthId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<CurrentUserDto>(
            command);
    }
}