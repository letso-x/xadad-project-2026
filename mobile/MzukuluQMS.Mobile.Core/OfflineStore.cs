using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
namespace MzukuluQMS.Mobile.Core;

public sealed record PendingOperation(Guid OperationId, Guid EntityId, string Kind, string Payload, long? ExpectedRevision, int Attempts, Guid? DependsOn);
public sealed record LocalRecord(Guid LocalId, long? ServerId, string Json, bool Dirty);
public sealed record OutboxEntry(PendingOperation Operation,string Status,string? LastError,string? FailureJson=null);
public sealed record SyncConflict(Guid OperationId,string LocalJson,string ServerJson,string Reason,string Status="Conflict");
public interface ILocalKeyStore { Task<byte[]> GetKeyAsync(Guid owner, CancellationToken ct); }

// SQLite metadata is NOT encrypted. Payload/file encryption is a defence in depth;
// verified SQLCipher DB encryption remains a release requirement.
public sealed partial class OfflineStore(string directory, Guid owner, ILocalKeyStore keys) : IAsyncDisposable
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
            connection=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=Path.Combine(directory,$"qms-{owner:N}.db"),ForeignKeys=true,Pooling=false}.ToString());
            await connection.OpenAsync(ct);
            await using var version=connection.CreateCommand(); version.CommandText="PRAGMA user_version";
            var current=Convert.ToInt32(await version.ExecuteScalarAsync(ct));
            if(current>4) throw new InvalidOperationException("Local database is newer than this application. Preserve it and upgrade the app.");
            using var tx=connection.BeginTransaction();
            await ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS Entity(LocalId TEXT PRIMARY KEY,ServerId INTEGER,Payload BLOB NOT NULL,Dirty INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS Operation(OperationId TEXT PRIMARY KEY,EntityId TEXT NOT NULL REFERENCES Entity(LocalId),Kind TEXT NOT NULL,Payload BLOB NOT NULL,ExpectedRevision INTEGER,Status TEXT NOT NULL,Attempts INTEGER NOT NULL DEFAULT 0,CreatedAt INTEGER NOT NULL,NextAttempt INTEGER NOT NULL DEFAULT 0,LastError TEXT,DependsOn TEXT REFERENCES Operation(OperationId));
                CREATE TABLE IF NOT EXISTS Conflict(OperationId TEXT PRIMARY KEY REFERENCES Operation(OperationId),LocalPayload BLOB NOT NULL,ServerPayload BLOB NOT NULL,Reason TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS Evidence(LocalId TEXT PRIMARY KEY,EntityId TEXT NOT NULL REFERENCES Entity(LocalId),Path TEXT NOT NULL,ContentType TEXT NOT NULL,FileName TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS OperationResult(OperationId TEXT PRIMARY KEY REFERENCES Operation(OperationId),Payload BLOB NOT NULL);
                CREATE TABLE IF NOT EXISTS OperationDecision(OperationId TEXT PRIMARY KEY REFERENCES Operation(OperationId),Reason BLOB NOT NULL,DecidedAt INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS OperationFailure(OperationId TEXT PRIMARY KEY REFERENCES Operation(OperationId),Payload BLOB NOT NULL);
                PRAGMA user_version=4;
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
    public async Task RestoreFormMetadataAsync(Guid entityId,JsonNode definition,CancellationToken ct=default)
    {
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            await using var query=Command("SELECT Payload FROM Entity WHERE LocalId=$id",tx,("$id",entityId.ToString()));
            var bytes=await query.ExecuteScalarAsync(ct) as byte[]??throw new InvalidOperationException("Saved record not found.");
            var source=JsonNode.Parse(Decrypt(bytes))!;var repaired=OfflineFormIdentity.RestoreMetadata(source,definition);
            if(!JsonNode.DeepEquals(source,repaired))await ExecuteAsync("UPDATE Entity SET Payload=$payload WHERE LocalId=$id",tx,ct,("$payload",Encrypt(repaired.ToJsonString())),("$id",entityId.ToString()));
            tx.Commit();
        },ct);
    }
    public async Task<Guid> SaveAndEnqueueAsync(Guid entityId,string localJson,string kind,string payload,long? expectedRevision,Guid? dependsOn=null,CancellationToken ct=default)
    {
        var operationId=Guid.NewGuid();
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            (localJson,expectedRevision)=await PreserveLocalIdentityAsync(entityId,localJson,expectedRevision,tx,ct);
            await ExecuteAsync("INSERT INTO Entity VALUES($id,NULL,$payload,1) ON CONFLICT(LocalId) DO UPDATE SET Payload=excluded.Payload,Dirty=1",tx,ct,("$id",entityId.ToString()),("$payload",Encrypt(localJson)));
            await ExecuteAsync("INSERT INTO Operation(OperationId,EntityId,Kind,Payload,ExpectedRevision,Status,CreatedAt,DependsOn) VALUES($op,$entity,$kind,$payload,$revision,'Pending',$now,$depends)",tx,ct,("$op",operationId.ToString()),("$entity",entityId.ToString()),("$kind",kind),("$payload",Encrypt(payload)),("$revision",expectedRevision),("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),("$depends",dependsOn?.ToString()));
            tx.Commit();
        },ct);return operationId;
    }
    public async Task<string?> OperationResultAsync(Guid operationId,CancellationToken ct=default)
    {
        string? result=null;await Locked(async()=>{await using var cmd=Command("SELECT Payload FROM OperationResult WHERE OperationId=$id",null,("$id",operationId.ToString()));var bytes=await cmd.ExecuteScalarAsync(ct) as byte[];if(bytes is not null)result=Decrypt(bytes);},ct);return result;
    }
    public async Task<IReadOnlyList<LocalRecord>> RecordsAsync(CancellationToken ct=default)
    {
        var rows=new List<LocalRecord>();await Locked(async()=>{await using var cmd=Command("SELECT LocalId,ServerId,Payload,Dirty FROM Entity",null);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(new(Guid.Parse(r.GetString(0)),r.IsDBNull(1)?null:r.GetInt64(1),Decrypt((byte[])r[2]),r.GetInt64(3)!=0));},ct);return rows;
    }
    public async Task<bool> HasPendingTransitionAsync(Guid entityId,CancellationToken ct=default)
    {
        var pending=false;await Locked(async()=>{await using var cmd=Command("SELECT EXISTS(SELECT 1 FROM Operation WHERE EntityId=$id AND Status NOT IN('Completed','Cancelled') AND Kind IN('SubmitQCForm','ReturnForCorrection','ApproveQCForm','CreateSignOff'))",null,("$id",entityId.ToString()));pending=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))!=0;},ct);return pending;
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
                 AND NOT EXISTS(SELECT 1 FROM Operation prior WHERE prior.EntityId=o.EntityId AND prior.rowid<o.rowid AND prior.Status NOT IN('Completed','Cancelled'))
                ORDER BY o.rowid LIMIT 1
                """,tx,("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            await using(var r=await cmd.ExecuteReaderAsync(ct))if(await r.ReadAsync(ct))result=new(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.GetString(2),Decrypt((byte[])r[3]),r.IsDBNull(4)?null:r.GetInt64(4),r.GetInt32(5),r.IsDBNull(6)?null:Guid.Parse(r.GetString(6)));
            if(result is not null)await ExecuteAsync("UPDATE Operation SET Status='Processing',Attempts=Attempts+1 WHERE OperationId=$id",tx,ct,("$id",result.OperationId.ToString()));
            tx.Commit();
        },ct);return result;
    }
    public async Task CompleteAsync(PendingOperation op,long? serverId,long? revision,CancellationToken ct=default,string? resultJson=null,string? serverSnapshot=null)
    {
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            if(resultJson is not null)await ExecuteAsync("INSERT INTO OperationResult VALUES($id,$payload) ON CONFLICT(OperationId) DO NOTHING",tx,ct,("$id",op.OperationId.ToString()),("$payload",Encrypt(resultJson)));
            await ExecuteAsync("UPDATE Operation SET Status='Completed',LastError=NULL WHERE OperationId=$id",tx,ct,("$id",op.OperationId.ToString()));
            await ExecuteAsync("UPDATE Entity SET ServerId=COALESCE($server,ServerId),Dirty=EXISTS(SELECT 1 FROM Operation WHERE EntityId=$entity AND Status NOT IN('Completed','Cancelled')) WHERE LocalId=$entity",tx,ct,("$server",serverId),("$entity",op.EntityId.ToString()));
            // Persist acknowledged revision even while later local edits keep the entity dirty.
            await using(var payloadCommand=Command("SELECT Payload FROM Entity WHERE LocalId=$entity",tx,("$entity",op.EntityId.ToString())))
            {
                var payload=await payloadCommand.ExecuteScalarAsync(ct) as byte[];
                if(payload is not null && (revision.HasValue||op.Kind=="CreateQCForm"))
                {
                    try {
                        var node=JsonNode.Parse(Decrypt(payload));
                        if(op.Kind=="CreateQCForm"&&node?["qcFormID"] is not null&&long.TryParse(node["qcFormID"]!.ToString(),out var localFormId)&&localFormId<0)
                            node=OfflineFormIdentity.Acknowledge(node,JsonNode.Parse(serverSnapshot??throw new InvalidOperationException("The created record needs to be reloaded before dependent work can sync."))!);

                        if(node is JsonObject obj) {
                            if(revision.HasValue)obj["revision"]=revision.Value;
                            if(op.Kind=="SubmitQCForm")obj["status"]="Submitted";
                            if(op.Kind=="ReturnForCorrection")obj["status"]="ReturnedForCorrection";
                            if(op.Kind=="ApproveQCForm")obj["status"]="Approved";
                            await ExecuteAsync("UPDATE Entity SET Payload=$payload WHERE LocalId=$entity",tx,ct,("$entity",op.EntityId.ToString()),("$payload",Encrypt(obj.ToJsonString())));
                        }
                    }catch(JsonException) when(op.Kind!="CreateQCForm"||serverSnapshot is null){ /* Earlier non-JSON synthetic cache entries remain intact. */ }
                }
            }
            // Never alter an already-attempted retry's fingerprint after a lost response.
            if(revision.HasValue)await ExecuteAsync("UPDATE Operation SET ExpectedRevision=$revision WHERE EntityId=$entity AND Status='Pending' AND Attempts=0",tx,ct,("$revision",revision),("$entity",op.EntityId.ToString()));
            tx.Commit();
        },ct);
    }
    public async Task FailAsync(PendingOperation op,string reason,bool transient,bool conflict=false,string? serverJson=null,CancellationToken ct=default,string? failureJson=null)
    {
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            if(failureJson is not null)await ExecuteAsync("INSERT INTO OperationFailure VALUES($id,$payload) ON CONFLICT(OperationId) DO UPDATE SET Payload=excluded.Payload",tx,ct,("$id",op.OperationId.ToString()),("$payload",Encrypt(failureJson)));
            var state=conflict?"Conflict":transient?"Pending":"Failed";
            var next=DateTimeOffset.UtcNow.AddSeconds(Math.Min(300,Math.Pow(2,Math.Min(op.Attempts+1,8)))).ToUnixTimeMilliseconds();
            await ExecuteAsync("UPDATE Operation SET Status=$status,LastError=$reason,NextAttempt=$next WHERE OperationId=$id",tx,ct,("$status",state),("$reason",reason),("$next",next),("$id",op.OperationId.ToString()));
            if(conflict)await ExecuteAsync("INSERT INTO Conflict VALUES($id,$local,$server,$reason) ON CONFLICT(OperationId) DO UPDATE SET ServerPayload=excluded.ServerPayload,Reason=excluded.Reason",tx,ct,("$id",op.OperationId.ToString()),("$local",Encrypt(op.Payload)),("$server",Encrypt(serverJson ?? "null")),("$reason",reason));
            tx.Commit();
        },ct);
    }
    public async Task WithdrawRejectedWorkflowAsync(Guid operationId,string reason,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>2000)throw new ArgumentException("A withdrawal reason is required (max 2000).");
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            string? entity=null;
            await using(var cmd=Command("SELECT EntityId FROM Operation WHERE OperationId=$id AND Status IN('Failed','Conflict') AND Kind IN('SubmitQCForm','ReturnForCorrection','ApproveQCForm','CreateSignOff')",tx,("$id",operationId.ToString())))entity=await cmd.ExecuteScalarAsync(ct) as string;
            if(entity is null)throw new InvalidOperationException("Only a failed/conflicting workflow action can be withdrawn. Pending work and value/evidence edits remain retained.");
            await ExecuteAsync("INSERT INTO OperationDecision VALUES($id,$reason,$now)",tx,ct,("$id",operationId.ToString()),("$reason",Encrypt(reason)),("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            await ExecuteAsync("UPDATE Operation SET Status='Cancelled' WHERE OperationId=$id",tx,ct,("$id",operationId.ToString()));
            await ExecuteAsync("UPDATE Entity SET Dirty=EXISTS(SELECT 1 FROM Operation WHERE EntityId=$entity AND Status NOT IN('Completed','Cancelled')) WHERE LocalId=$entity",tx,ct,("$entity",entity));
            tx.Commit();
        },ct);
    }
    public async Task<IReadOnlyList<SyncConflict>> ConflictsAsync(CancellationToken ct=default)
    {
        var rows=new List<SyncConflict>();await Locked(async()=>{await using var cmd=Command("SELECT c.OperationId,c.LocalPayload,c.ServerPayload,c.Reason,o.Status FROM Conflict c JOIN Operation o ON o.OperationId=c.OperationId",null);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)) rows.Add(new(Guid.Parse(r.GetString(0)),Decrypt((byte[])r[1]),Decrypt((byte[])r[2]),r.GetString(3),r.GetString(4)));},ct);return rows;
    }
    public async Task<IReadOnlyList<OutboxEntry>> OutboxAsync(CancellationToken ct=default)
    {
        var rows=new List<OutboxEntry>();await Locked(async()=>{await using var cmd=Command("SELECT o.OperationId,o.EntityId,o.Kind,o.Payload,o.ExpectedRevision,o.Attempts,o.DependsOn,o.Status,o.LastError,f.Payload FROM Operation o LEFT JOIN OperationFailure f ON f.OperationId=o.OperationId ORDER BY o.rowid",null);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(new(new(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.GetString(2),Decrypt((byte[])r[3]),r.IsDBNull(4)?null:r.GetInt64(4),r.GetInt32(5),r.IsDBNull(6)?null:Guid.Parse(r.GetString(6))),r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),r.IsDBNull(9)?null:Decrypt((byte[])r[9])));},ct);return rows;
    }
    public async Task<IReadOnlyDictionary<string,int>> CountsAsync(CancellationToken ct=default)
    {
        var rows=new Dictionary<string,int>();await Locked(async()=>{await using var cmd=Command("SELECT Status,COUNT(*) FROM Operation GROUP BY Status",null);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))rows.Add(r.GetString(0),r.GetInt32(1));},ct);return rows;
    }
    public async Task<Guid> SaveEvidenceAsync(Guid entityId,Stream source,string contentType,string fileName,CancellationToken ct=default,Func<Guid,SyncRequest>? uploadRequest=null,string? localJson=null,long? expectedRevision=null,SyncRequest? signOffRequest=null)
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
        await Locked(async()=>{
            using var tx=connection!.BeginTransaction();
            await ExecuteAsync("INSERT INTO Evidence VALUES($id,$entity,$path,$type,$name)",tx,ct,("$id",id.ToString()),("$entity",entityId.ToString()),("$path",destination),("$type",contentType),("$name",Path.GetFileName(fileName)));
            if(uploadRequest is not null)
            {
                var operation=Guid.NewGuid();
                (localJson,expectedRevision)=await PreserveLocalIdentityAsync(entityId,localJson??throw new ArgumentException("Local snapshot is required."),expectedRevision,tx,ct);
                await ExecuteAsync("UPDATE Entity SET Payload=$payload,Dirty=1 WHERE LocalId=$entity",tx,ct,("$payload",Encrypt(localJson??throw new ArgumentException("Local snapshot is required."))),("$entity",entityId.ToString()));
                await ExecuteAsync("INSERT INTO Operation(OperationId,EntityId,Kind,Payload,ExpectedRevision,Status,CreatedAt) VALUES($op,$entity,'UploadEvidence',$payload,$revision,'Pending',$now)",tx,ct,("$op",operation.ToString()),("$entity",entityId.ToString()),("$payload",Encrypt(JsonSerializer.Serialize(uploadRequest(id)))),("$revision",expectedRevision),("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                if(signOffRequest is not null)
                    await ExecuteAsync("INSERT INTO Operation(OperationId,EntityId,Kind,Payload,ExpectedRevision,Status,CreatedAt,DependsOn) VALUES($op,$entity,'CreateSignOff',$payload,$revision,'Pending',$now,$depends)",tx,ct,("$op",Guid.NewGuid().ToString()),("$entity",entityId.ToString()),("$payload",Encrypt(JsonSerializer.Serialize(signOffRequest with {EvidenceOperationId=operation}))),("$revision",expectedRevision),("$now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),("$depends",operation.ToString()));
            }
            tx.Commit();
        },ct);return id;
    }
    public async Task<byte[]> ReadEvidenceAsync(Guid id,CancellationToken ct=default)
    {
        string? file=null;await Locked(async()=>{await using var cmd=Command("SELECT Path FROM Evidence WHERE LocalId=$id",null,("$id",id.ToString()));file=(string?)await cmd.ExecuteScalarAsync(ct);},ct);
        if(file is null)throw new KeyNotFoundException("Local evidence not found.");return DecryptBytes(await File.ReadAllBytesAsync(file,ct));
    }
    private async Task<(string Json,long? Revision)> PreserveLocalIdentityAsync(Guid entityId,string json,long? revision,SqliteTransaction tx,CancellationToken ct)
    {
        JsonNode? incoming;try{incoming=JsonNode.Parse(json);}catch(JsonException){return(json,revision);}
        if(incoming is not JsonObject form||!long.TryParse(form["qcFormID"]?.ToString(),out var id)||id>=0)return(json,revision);
        await using var cmd=Command("SELECT Payload FROM Entity WHERE LocalId=$id",tx,("$id",entityId.ToString()));
        var bytes=await cmd.ExecuteScalarAsync(ct) as byte[];if(bytes is null)return(json,revision);
        var saved=JsonNode.Parse(Decrypt(bytes));if(saved?["serverIdMap"] is null)return(json,revision);
        foreach(var key in new[]{"serverIdMap","serverFormID","formNumber","localCreate","revision","status"})form[key]=saved[key]?.DeepClone();
        return(form.ToJsonString(),long.TryParse(saved["revision"]?.ToString(),out var acknowledged)?acknowledged:revision);
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
