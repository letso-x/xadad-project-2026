using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
namespace MzukuluQMS.Mobile.Core;

public sealed record PendingOperation(Guid OperationId, Guid EntityId, string Kind, string Payload, long? ExpectedRevision, int Attempts, Guid? DependsOn);
public sealed record LocalRecord(Guid LocalId, long? ServerId, string Json, bool Dirty);
public sealed record SyncConflict(Guid OperationId,string LocalJson,string ServerJson,string Reason);
public interface ILocalKeyStore { Task<byte[]> GetKeyAsync(Guid owner, CancellationToken ct); }

// SQLite metadata is NOT encrypted. Payload/file encryption is a defence in depth;
// verified SQLCipher DB encryption remains a release requirement.
public sealed class OfflineStore(string directory, Guid owner, ILocalKeyStore keys) : IAsyncDisposable
{
    public Guid Owner => owner;
    private readonly SemaphoreSlim gate=new(1,1);
    private SqliteConnection? connection;
    private byte[]? key;
    public async Task InitialiseAsync(CancellationToken ct=default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if(connection is not null) return;
            SQLitePCL.Batteries_V2.Init();
            Directory.CreateDirectory(directory);
            key=await keys.GetKeyAsync(owner,ct);
            if(key.Length!=32) throw new InvalidOperationException("Local key must contain 256 bits.");
            connection=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=Path.Combine(directory,$"qms-{owner:N}.db"),ForeignKeys=true}.ToString());
            await connection.OpenAsync(ct);
            await using var version=connection.CreateCommand(); version.CommandText="PRAGMA user_version";
            var current=Convert.ToInt32(await version.ExecuteScalarAsync(ct));
            if(current>1) throw new InvalidOperationException("Local database is newer than this application. Preserve it and upgrade the app.");
            using var tx=connection.BeginTransaction();
            await ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS Entity(LocalId TEXT PRIMARY KEY,ServerId INTEGER,Payload BLOB NOT NULL,Dirty INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS Operation(OperationId TEXT PRIMARY KEY,EntityId TEXT NOT NULL REFERENCES Entity(LocalId),Kind TEXT NOT NULL,Payload BLOB NOT NULL,ExpectedRevision INTEGER,Status TEXT NOT NULL,Attempts INTEGER NOT NULL DEFAULT 0,CreatedAt INTEGER NOT NULL,NextAttempt INTEGER NOT NULL DEFAULT 0,LastError TEXT,DependsOn TEXT REFERENCES Operation(OperationId));
                CREATE TABLE IF NOT EXISTS Conflict(OperationId TEXT PRIMARY KEY REFERENCES Operation(OperationId),LocalPayload BLOB NOT NULL,ServerPayload BLOB NOT NULL,Reason TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS Evidence(LocalId TEXT PRIMARY KEY,EntityId TEXT NOT NULL REFERENCES Entity(LocalId),Path TEXT NOT NULL,ContentType TEXT NOT NULL,FileName TEXT NOT NULL);
                PRAGMA user_version=1;
                UPDATE Operation SET Status='Pending' WHERE Status='Processing';
                """,tx,ct);
            tx.Commit();
        }
        catch
        {
            if(connection is not null) await connection.DisposeAsync();
            connection=null;
            // Never delete/reset a failed or newer local database.
            throw;
        }
        finally {gate.Release();}
    }
    public async Task CacheAsync(Guid id,long? serverId,string json,CancellationToken ct=default)
    {
        await Locked(async()=>{await ExecuteAsync("INSERT INTO Entity VALUES($id,$server,$payload,0) ON CONFLICT(LocalId) DO UPDATE SET ServerId=excluded.ServerId,Payload=excluded.Payload WHERE Entity.Dirty=0",null,ct,("$id",id.ToString()),("$server",serverId),("$payload",Encrypt(json)));},ct);
    }
    public async Task<Guid> SaveAndEnqueueAsync(Guid entityId,string localJson,string kind,string payload,long? expectedRevision,Guid? dependsOn=null,CancellationToken ct=default)
    {
        var operationId=Guid.NewGuid();
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            await ExecuteAsync("INSERT INTO Entity VALUES($id,NULL,$payload,1) ON CONFLICT(LocalId) DO UPDATE SET Payload=excluded.Payload,Dirty=1",tx,ct,("$id",entityId.ToString()),("$payload",Encrypt(localJson)));
            await ExecuteAsync("INSERT INTO Operation(OperationId,EntityId,Kind,Payload,ExpectedRevision,Status,CreatedAt,DependsOn) VALUES($op,$entity,$kind,$payload,$revision,'Pending',$now,$depends)",tx,ct,("$op",operationId.ToString()),("$entity",entityId.ToString()),("$kind",kind),("$payload",Encrypt(payload)),("$revision",expectedRevision),("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),("$depends",dependsOn?.ToString()));
            tx.Commit();
        },ct);return operationId;
    }
    public async Task<LocalRecord?> GetAsync(Guid id,CancellationToken ct=default)
    {
        LocalRecord? record=null;await Locked(async()=>{
            await using var cmd=Command("SELECT ServerId,Payload,Dirty FROM Entity WHERE LocalId=$id",null,("$id",id.ToString()));await using var r=await cmd.ExecuteReaderAsync(ct);
            if(await r.ReadAsync(ct))record=new(id,r.IsDBNull(0)?null:r.GetInt64(0),Decrypt((byte[])r[1]),r.GetInt64(2)!=0);
        },ct);return record;
    }
    public async Task<PendingOperation?> ClaimAsync(CancellationToken ct=default)
    {
        PendingOperation? result=null;await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            await using var cmd=Command("""
                SELECT o.OperationId,o.EntityId,o.Kind,o.Payload,o.ExpectedRevision,o.Attempts,o.DependsOn FROM Operation o
                WHERE o.Status='Pending' AND o.NextAttempt<=$now
                 AND (o.DependsOn IS NULL OR EXISTS(SELECT 1 FROM Operation p WHERE p.OperationId=o.DependsOn AND p.Status='Completed'))
                 AND NOT EXISTS(SELECT 1 FROM Operation prior WHERE prior.EntityId=o.EntityId AND prior.rowid<o.rowid AND prior.Status<>'Completed')
                ORDER BY o.rowid LIMIT 1
                """,tx,("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            await using(var r=await cmd.ExecuteReaderAsync(ct))if(await r.ReadAsync(ct))result=new(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.GetString(2),Decrypt((byte[])r[3]),r.IsDBNull(4)?null:r.GetInt64(4),r.GetInt32(5),r.IsDBNull(6)?null:Guid.Parse(r.GetString(6)));
            if(result is not null)await ExecuteAsync("UPDATE Operation SET Status='Processing',Attempts=Attempts+1 WHERE OperationId=$id",tx,ct,("$id",result.OperationId.ToString()));
            tx.Commit();
        },ct);return result;
    }
    public async Task CompleteAsync(PendingOperation op,long? serverId,long? revision,CancellationToken ct=default)
    {
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            await ExecuteAsync("UPDATE Operation SET Status='Completed',LastError=NULL WHERE OperationId=$id",tx,ct,("$id",op.OperationId.ToString()));
            await ExecuteAsync("UPDATE Entity SET ServerId=COALESCE($server,ServerId),Dirty=EXISTS(SELECT 1 FROM Operation WHERE EntityId=$entity AND Status<>'Completed') WHERE LocalId=$entity",tx,ct,("$server",serverId),("$entity",op.EntityId.ToString()));
            // Acknowledgement advances only dependent unsent requests, never overwrites a recorded conflict.
            if(revision.HasValue)await ExecuteAsync("UPDATE Operation SET ExpectedRevision=$revision WHERE EntityId=$entity AND Status='Pending'",tx,ct,("$revision",revision),("$entity",op.EntityId.ToString()));
            tx.Commit();
        },ct);
    }
    public async Task FailAsync(PendingOperation op,string reason,bool transient,bool conflict=false,string? serverJson=null,CancellationToken ct=default)
    {
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            var state=conflict?"Conflict":transient?"Pending":"Failed";
            var next=DateTimeOffset.UtcNow.AddSeconds(Math.Min(300,Math.Pow(2,Math.Min(op.Attempts+1,8)))).ToUnixTimeMilliseconds();
            await ExecuteAsync("UPDATE Operation SET Status=$status,LastError=$reason,NextAttempt=$next WHERE OperationId=$id",tx,ct,("$status",state),("$reason",reason),("$next",next),("$id",op.OperationId.ToString()));
            if(conflict)await ExecuteAsync("INSERT INTO Conflict VALUES($id,$local,$server,$reason) ON CONFLICT(OperationId) DO UPDATE SET ServerPayload=excluded.ServerPayload,Reason=excluded.Reason",tx,ct,("$id",op.OperationId.ToString()),("$local",Encrypt(op.Payload)),("$server",Encrypt(serverJson ?? "null")),("$reason",reason));
            tx.Commit();
        },ct);
    }
    public async Task<IReadOnlyList<SyncConflict>> ConflictsAsync(CancellationToken ct=default)
    {
        var rows=new List<SyncConflict>();await Locked(async()=>{await using var cmd=Command("SELECT OperationId,LocalPayload,ServerPayload,Reason FROM Conflict",null);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)) rows.Add(new(Guid.Parse(r.GetString(0)),Decrypt((byte[])r[1]),Decrypt((byte[])r[2]),r.GetString(3)));},ct);return rows;
    }
    public async Task<IReadOnlyDictionary<string,int>> CountsAsync(CancellationToken ct=default)
    {
        var rows=new Dictionary<string,int>();await Locked(async()=>{await using var cmd=Command("SELECT Status,COUNT(*) FROM Operation GROUP BY Status",null);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(r.GetString(0),r.GetInt32(1));},ct);return rows;
    }
    public async Task<Guid> SaveEvidenceAsync(Guid entityId,Stream source,string contentType,string fileName,CancellationToken ct=default)
    {
        await InitialiseAsync(ct);
        // Copy/flush first; an interrupted DB write leaves a recoverable orphan, never a false saved intent.
        var id=Guid.NewGuid();var evidenceDirectory=Path.Combine(directory,$"evidence-{owner:N}");Directory.CreateDirectory(evidenceDirectory);
        var destination=Path.Combine(evidenceDirectory,id.ToString("N")+".bin");
        using var buffer=new MemoryStream();var chunk=new byte[8192];int count;
        while((count=await source.ReadAsync(chunk,ct))>0)
        {
            if(buffer.Length+count>20*1024*1024)throw new ArgumentException("Evidence exceeds 20 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0,count),ct);
        }
        var encrypted=EncryptBytes(buffer.ToArray());
        await using(var file=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,FileOptions.WriteThrough)){await file.WriteAsync(encrypted,ct);file.Flush(true);}
        await Locked(async()=>{await ExecuteAsync("INSERT INTO Evidence VALUES($id,$entity,$path,$type,$name)",null,ct,("$id",id.ToString()),("$entity",entityId.ToString()),("$path",destination),("$type",contentType),("$name",Path.GetFileName(fileName)));},ct);return id;
    }
    public async Task<byte[]> ReadEvidenceAsync(Guid id,CancellationToken ct=default)
    {
        string? file=null;await Locked(async()=>{await using var cmd=Command("SELECT Path FROM Evidence WHERE LocalId=$id",null,("$id",id.ToString()));file=(string?)await cmd.ExecuteScalarAsync(ct);},ct);
        if(file is null)throw new KeyNotFoundException("Local evidence not found.");return DecryptBytes(await File.ReadAllBytesAsync(file,ct));
    }
    private async Task Locked(Func<Task> action,CancellationToken ct)
    {
        await InitialiseAsync(ct);await gate.WaitAsync(ct);try {await action();}finally {gate.Release();}
    }
    private SqliteCommand Command(string sql,SqliteTransaction? tx,params (string,object?)[] parameters)
    {
        var cmd=connection!.CreateCommand();cmd.CommandText=sql;cmd.Transaction=tx;foreach(var (name,value) in parameters)cmd.Parameters.AddWithValue(name,value??DBNull.Value);return cmd;
    }
    private async Task ExecuteAsync(string sql,SqliteTransaction? tx,CancellationToken ct,params (string,object?)[] parameters)
    {await using var cmd=Command(sql,tx,parameters);await cmd.ExecuteNonQueryAsync(ct);}
    private byte[] Encrypt(string json)=>EncryptBytes(Encoding.UTF8.GetBytes(json));
    private string Decrypt(byte[] bytes)=>Encoding.UTF8.GetString(DecryptBytes(bytes));
    private byte[] EncryptBytes(byte[] plaintext)
    {
        var output=new byte[28+plaintext.Length];RandomNumberGenerator.Fill(output.AsSpan(0,12));
        using var aes=new AesGcm(key!,16);aes.Encrypt(output.AsSpan(0,12),plaintext,output.AsSpan(28),output.AsSpan(12,16),owner.ToByteArray());return output;
    }
    private byte[] DecryptBytes(byte[] bytes)
    {
        if(bytes.Length<28)throw new CryptographicException("Invalid local payload.");var plaintext=new byte[bytes.Length-28];
        using var aes=new AesGcm(key!,16);aes.Decrypt(bytes.AsSpan(0,12),bytes.AsSpan(28),bytes.AsSpan(12,16),plaintext,owner.ToByteArray());return plaintext;
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();try {if(connection is not null)await connection.DisposeAsync();connection=null;if(key is not null)CryptographicOperations.ZeroMemory(key);key=null;}finally{gate.Release();}
    }
}
