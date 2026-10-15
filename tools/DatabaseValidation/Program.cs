using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;
var mode=args.FirstOrDefault()??"inspect";
if(mode is not ("inspect" or "security" or "history" or "rehearse" or "apply"))throw new ArgumentException("Use inspect/security/history for checks; rehearse/apply only on an authorised disposable database.");
var config=new ConfigurationBuilder().AddUserSecrets<MzukuluQMS.Api.Data.NpgsqlConnectionFactory>().AddEnvironmentVariables().Build();
try {
await using var c=new NpgsqlConnection(config.GetConnectionString("QmsDatabase"));await c.OpenAsync();
if(mode=="security") {
await using var cmd=new NpgsqlCommand("""
 SELECT c.relname, c.relrowsecurity,
 has_table_privilege('anon',c.oid,'INSERT,UPDATE,DELETE') AS anon_write,
 has_table_privilege('authenticated',c.oid,'INSERT,UPDATE,DELETE') AS authenticated_write
 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind='r' ORDER BY c.relname
 """,c);
await using(var r=await cmd.ExecuteReaderAsync())while(await r.ReadAsync())Console.WriteLine($"Table {r.GetString(0)} RLS={r.GetBoolean(1)} anon_write={r.GetBoolean(2)} authenticated_write={r.GetBoolean(3)}");
await using var functions=new NpgsqlCommand("""
 SELECT p.proname,p.prosecdef,has_function_privilege('anon',p.oid,'EXECUTE'),has_function_privilege('authenticated',p.oid,'EXECUTE')
 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public' AND p.prokind='f' ORDER BY p.proname
 """,c);await using(var r=await functions.ExecuteReaderAsync())while(await r.ReadAsync())Console.WriteLine($"Function {r.GetString(0)} security_definer={r.GetBoolean(1)} anon_execute={r.GetBoolean(2)} authenticated_execute={r.GetBoolean(3)}");return;
}
if(mode=="history") {
var saved=JsonDocument.Parse(await File.ReadAllTextAsync(args.ElementAtOrDefault(1)??throw new ArgumentException("Pass the successful synthetic acceptance JSON")));var formId=saved.RootElement.GetProperty("state").GetProperty("formID").GetInt64();
var before=await Fingerprints(c,null);
foreach(var item in new[]{("signoff","UPDATE public.\"QCSignOff\" SET \"Comments\"=\"Comments\" WHERE \"QCFormID\"=@id"),("public audit","UPDATE public.\"AuditEvent\" SET \"Action\"=\"Action\" WHERE \"EntityType\"='QCForm' AND \"EntityID\"=CAST(@id AS text)"),("core audit","UPDATE qms_security.\"AuditEvent\" SET \"Action\"=\"Action\" WHERE \"EntityType\"='QCForm' AND \"EntityID\"=@id")}) {
await using var historyTx=await c.BeginTransactionAsync();await using var cmd=new NpgsqlCommand(item.Item2,c,historyTx);cmd.Parameters.AddWithValue("id",formId);var rejected=false;
try{await cmd.ExecuteNonQueryAsync();}catch(PostgresException p) when(p.SqlState=="P0001"){rejected=true;}finally{await historyTx.RollbackAsync();}
if(!rejected)throw new InvalidOperationException("Append-only guard did not reject "+item.Item1);Console.WriteLine("PASS append-only "+item.Item1+" guard on returned synthetic form ID.");}
var afterHistory=await Fingerprints(c,null);foreach(var item in before)if(afterHistory.GetValueOrDefault(item.Key)!=item.Value)throw new InvalidOperationException("History guard test changed public rows");Console.WriteLine("PASS all public rows unchanged after rolled-back guard tests.");return;
}
var baseline=await Fingerprints(c,null);
var schemas=await Scalar(c,null,"SELECT count(*) FROM pg_namespace WHERE nspname IN ('qms_security','qms_core')");
Console.WriteLine($"Connected with configured TLS. Existing QMS schemas: {schemas}. Public table fingerprints: {baseline.Count}.");
if(mode=="inspect") {Console.WriteLine(JsonSerializer.Serialize(baseline,new JsonSerializerOptions{WriteIndented=true}));return;}
if(Convert.ToInt64(schemas)!=0)throw new InvalidOperationException("Existing QMS schemas require reconciliation; automatic replay refused.");
var migrations=Directory.GetFiles("mobile/MzukuluQMS.Api/Migrations","*.sql").OrderBy(p=>p,StringComparer.Ordinal).ToArray();
if(migrations.Length!=5)throw new InvalidOperationException("Expected reviewed migrations 001 through 005.");
Directory.CreateDirectory("docs/codex/evidence");
if(mode=="apply")await File.WriteAllTextAsync("docs/codex/evidence/pre-migration-fingerprints-2026-10-14.json",JsonSerializer.Serialize(baseline,new JsonSerializerOptions{WriteIndented=true}));
await using var tx=await c.BeginTransactionAsync();
foreach(var file in migrations){var sql=Regex.Replace(await File.ReadAllTextAsync(file),@"(?m)^\s*(BEGIN|COMMIT);\s*$","");await using var cmd=new NpgsqlCommand(sql,c,tx){CommandTimeout=90};await cmd.ExecuteNonQueryAsync();Console.WriteLine($"Validated {Path.GetFileName(file)}");}
var after=await Fingerprints(c,tx);
foreach(var item in baseline)if(!after.TryGetValue(item.Key,out var value)||item.Value!=value)throw new InvalidOperationException("Historical fingerprint changed for "+item.Key);
Console.WriteLine("All pre-existing public table row counts and canonical row digests preserved (new additive columns excluded).");
if(mode=="apply"){await tx.CommitAsync();Console.WriteLine("COMMITTED migrations 001–005 atomically on authorised disposable target.");}
else {await tx.RollbackAsync();Console.WriteLine("REHEARSAL ROLLED BACK; no migration objects retained.");}
} catch(Exception e){Environment.ExitCode=2;Console.WriteLine("BLOCKED: "+e.GetType().Name);if(e is PostgresException p)Console.WriteLine($"SQLSTATE {p.SqlState}; position {p.Position}; schema {p.SchemaName}; table {p.TableName}; column {p.ColumnName}; constraint {p.ConstraintName}");else if(e is InvalidOperationException)Console.WriteLine(e.Message);}
static async Task<object?> Scalar(NpgsqlConnection c,NpgsqlTransaction? tx,string sql){await using var cmd=new NpgsqlCommand(sql,c,tx);return await cmd.ExecuteScalarAsync();}
static async Task<Dictionary<string,string>> Fingerprints(NpgsqlConnection c,NpgsqlTransaction? tx){
var tables=new List<string>();await using(var cmd=new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename",c,tx))await using(var r=await cmd.ExecuteReaderAsync())while(await r.ReadAsync())tables.Add(r.GetString(0));
var result=new Dictionary<string,string>();foreach(var table in tables){var quoted="\""+table.Replace("\"","\"\"")+"\"";var sql=$"SELECT count(*)::text||':'||md5(COALESCE(jsonb_agg(rowvalue ORDER BY rowvalue::text)::text,'[]')) FROM (SELECT to_jsonb(t)-'Revision'-'HashVersion'-'CoreAuditEventID' AS rowvalue FROM public.{quoted} t) data";result[table]=(string)(await Scalar(c,tx,sql))!;}return result;
}
