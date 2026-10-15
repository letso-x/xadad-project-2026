using Microsoft.Extensions.Configuration;
using MzukuluQMS.Api.Configuration;
using Npgsql;
var config=new ConfigurationBuilder().AddUserSecrets<MzukuluQMS.Api.Data.NpgsqlConnectionFactory>().AddEnvironmentVariables().Build();
var keys=new[]{"ConnectionStrings:QmsDatabase","SupabaseAuth:MetadataAddress","SupabaseAuth:Audience","SupabaseStorage:BaseUrl","SupabaseStorage:SecretKey","SupabaseStorage:Bucket"};
foreach(var key in keys){var provider=config.Providers.Reverse().FirstOrDefault(p=>p.TryGet(key,out _));Console.WriteLine($"{key}: configured={!string.IsNullOrWhiteSpace(config[key])}; source={provider?.GetType().Name??"none"}");}
try
{
 var db=new NpgsqlConnectionStringBuilder(config.GetConnectionString("QmsDatabase"));
 Console.WriteLine($"Database SSL mode: {db.SslMode}; explicit CA configured={!string.IsNullOrWhiteSpace(db.RootCertificate)}; CA file exists={File.Exists(db.RootCertificate)}; local Windows CA path={db.RootCertificate?.Contains(":\\")==true}");
 if(args.Contains("--bundle-ca")){db.RootCertificate=Path.GetFullPath("mobile/MzukuluQMS.Api/certs/supabase-db-ca.crt");Console.WriteLine("Using bundled public CA for this read-only connection probe only.");}
 if(db.SslMode!=SslMode.VerifyFull)throw new InvalidOperationException();
 await using var connection=new NpgsqlConnection(db.ConnectionString);await connection.OpenAsync();
 // Read the inspected internal diagnostic flag in pinned Npgsql10.0.3 only;
 // application code does not depend on this reflection or alter TLS validation.
 var encrypted=typeof(NpgsqlConnection).GetProperty("IsSslEncrypted",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(connection) as bool?;
 Console.WriteLine("Client-to-pooler TLS encryption="+encrypted+"; certificate/hostname validation=VerifyFull.");if(encrypted!=true)throw new InvalidOperationException();
 await using var command=new NpgsqlCommand("SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()",connection);
 var tls=await command.ExecuteScalarAsync();Console.WriteLine($"Read-only PostgreSQL query PASS; pg_stat_ssl backend SSL={tls} (pooler-to-database leg, not client-to-pooler TLS).");
}
catch(Exception error)
{
 Environment.ExitCode=2;
 for(Exception? cause=error;cause is not null;cause=cause.InnerException)
 {
  Console.WriteLine("Connection failure type: "+cause.GetType().Name);
  if(cause is PostgresException pg)Console.WriteLine("SQLSTATE: "+pg.SqlState);
  if(cause is System.Net.Sockets.SocketException socket)Console.WriteLine("Socket category: "+socket.SocketErrorCode);
  if(cause is System.Security.Authentication.AuthenticationException)Console.WriteLine("TLS validation failed; validation was not disabled.");
 }
}
