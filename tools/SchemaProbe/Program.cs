using Microsoft.Extensions.Configuration;
using Npgsql;
var config = new ConfigurationBuilder().AddUserSecrets<MzukuluQMS.Api.Data.NpgsqlConnectionFactory>().AddEnvironmentVariables().Build();
var cs = config.GetConnectionString("QmsDatabase");
if (string.IsNullOrWhiteSpace(cs)) { Console.WriteLine("BLOCKED: QmsDatabase not configured in User Secrets/environment."); return; }
try {
await using var c = new NpgsqlConnection(cs); await c.OpenAsync();
await using var tx = await c.BeginTransactionAsync();
await using var cmd = new NpgsqlCommand("SET TRANSACTION READ ONLY; SELECT table_name, column_name, data_type FROM information_schema.columns WHERE table_schema='public' ORDER BY table_name,ordinal_position",c,tx);
await using (var r = await cmd.ExecuteReaderAsync()) { while(await r.ReadAsync()) Console.WriteLine($"{r.GetString(0)}.{r.GetString(1)} {r.GetString(2)}"); }
await using var detail = new NpgsqlCommand("SELECT 'enum',t.typname,e.enumlabel FROM pg_enum e JOIN pg_type t ON t.oid=e.enumtypid UNION ALL SELECT 'fixture',\"QCFormID\"::text,\"Status\"::text FROM public.\"QCForm\" WHERE \"QCFormID\" IN (8,9)",c,tx);
await using (var r=await detail.ExecuteReaderAsync()) { while(await r.ReadAsync()) Console.WriteLine($"{r.GetString(0)} {r.GetString(1)} {r.GetString(2)}"); }
await tx.RollbackAsync();
} catch(Exception e) { Console.WriteLine("BLOCKED: read-only schema probe failed ("+e.GetType().Name+"). No credentials emitted."); }
