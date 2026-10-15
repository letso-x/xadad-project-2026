using Npgsql;

namespace MzukuluQMS.Api.Data;

public interface IDbConnectionFactory
{
    Task<NpgsqlConnection> CreateOpenConnectionAsync(
        CancellationToken cancellationToken = default);
}