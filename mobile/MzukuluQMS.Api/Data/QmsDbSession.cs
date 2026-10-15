using Npgsql;
namespace MzukuluQMS.Api.Data;

// One request-owned transaction; repository leases never dispose its connection.
public sealed class QmsDbSession(IDbConnectionFactory factory) : IAsyncDisposable
{
    public NpgsqlConnection? Connection { get; private set; }
    public NpgsqlTransaction? Transaction { get; private set; }
    public async Task BeginAsync(CancellationToken ct)
    {
        if (Connection is not null) throw new InvalidOperationException("A QMS transaction is already active.");
        try
        {
            Connection = await factory.CreateOpenConnectionAsync(ct);
            Transaction = await Connection.BeginTransactionAsync(ct);
            await using var timezone = new NpgsqlCommand("SET LOCAL TIME ZONE 'UTC'", Connection, Transaction);
            await timezone.ExecuteNonQueryAsync(ct);
        }
        catch { await DisposeAsync(); throw; }
    }
    public async Task<ConnectionLease> OpenAsync(CancellationToken ct) =>
        Connection is not null ? new(Connection, false) : new(await factory.CreateOpenConnectionAsync(ct), true);
    public async Task CommitAsync(CancellationToken ct)
    {
        await Transaction!.CommitAsync(ct);
        await DisposeAsync();
    }
    public async ValueTask DisposeAsync()
    {
        var transaction=Transaction;var connection=Connection;
        Transaction=null;Connection=null;
        try {if(transaction is not null)await transaction.DisposeAsync();}
        finally {if(connection is not null)await connection.DisposeAsync();}
    }
}
public sealed class ConnectionLease(NpgsqlConnection connection, bool owns) : IAsyncDisposable
{
    public NpgsqlConnection Connection => connection;
    public ValueTask DisposeAsync() => owns ? connection.DisposeAsync() : ValueTask.CompletedTask;
}
