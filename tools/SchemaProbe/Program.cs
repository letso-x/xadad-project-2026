#nullable enable
using Microsoft.Extensions.Configuration;
using Npgsql;
var config = new ConfigurationBuilder().AddUserSecrets<MzukuluQMS.Api.Data.NpgsqlConnectionFactory>().AddEnvironmentVariables().Build();
var cs = config.GetConnectionString("QmsDatabase");
if (string.IsNullOrWhiteSpace(cs)) { Console.WriteLine("BLOCKED: QmsDatabase not configured in User Secrets/environment."); Environment.ExitCode=2; return; }
try {
await using var c = new NpgsqlConnection(cs); await c.OpenAsync();
await using var tx = await c.BeginTransactionAsync();
await using var cmd = new NpgsqlCommand("SET TRANSACTION READ ONLY; SELECT table_name, column_name, data_type FROM information_schema.columns WHERE table_schema='public' ORDER BY table_name,ordinal_position",c,tx);
await using (var r = await cmd.ExecuteReaderAsync()) { while(await r.ReadAsync()) Console.WriteLine($"{r.GetString(0)}.{r.GetString(1)} {r.GetString(2)}"); }
await using var detail = new NpgsqlCommand("SELECT 'enum',t.typname,e.enumlabel FROM pg_enum e JOIN pg_type t ON t.oid=e.enumtypid UNION ALL SELECT 'fixture',\"QCFormID\"::text,\"Status\"::text FROM public.\"QCForm\" WHERE \"QCFormID\" IN (8,9)",c,tx);
await using (var r=await detail.ExecuteReaderAsync()) { while(await r.ReadAsync()) Console.WriteLine($"{r.GetString(0)} {r.GetString(1)} {r.GetString(2)}"); }
await tx.RollbackAsync();
} catch(Exception e) { Environment.ExitCode=2; Console.WriteLine("BLOCKED: read-only schema probe failed ("+e.GetType().Name+"). No credentials emitted.");
for (Exception? cause=e;cause is not null;cause=cause.InnerException)
{
 Console.WriteLine("Cause type: "+cause.GetType().Name);
 if(cause is System.Net.Sockets.SocketException socket) Console.WriteLine("Socket category: "+socket.SocketErrorCode);
 else if(cause is PostgresException postgres) {
  Console.WriteLine("PostgreSQL SQLSTATE: "+postgres.SqlState);
  if(postgres.SqlState=="28P01") Console.WriteLine("Database authentication rejected. Verify pooler connection parameters and database password locally; do not use the Supabase application-account password.");
 }
 else if(cause is System.Security.Authentication.AuthenticationException)
 {
  Console.WriteLine("TLS authentication failed. TLS validation was not bypassed.");
  foreach(var category in new[]{"UntrustedRoot","PartialChain","NotTimeValid","RemoteCertificateNameMismatch","RemoteCertificateChainErrors"})
   if(cause.Message.Contains(category,StringComparison.OrdinalIgnoreCase))Console.WriteLine("TLS category: "+category);
 }
 else if(cause is TimeoutException) Console.WriteLine("Connection timed out.");
} }
