using Microsoft.Extensions.Configuration;
using Npgsql;
var config=new ConfigurationBuilder().AddUserSecrets<MzukuluQMS.Api.Data.NpgsqlConnectionFactory>().AddEnvironmentVariables().Build();
try
{
 await using var connection=new NpgsqlConnection(config.GetConnectionString("QmsDatabase"));await connection.OpenAsync();
 await using var tx=await connection.BeginTransactionAsync();
 await using(var readOnly=new NpgsqlCommand("SET TRANSACTION READ ONLY",connection,tx))await readOnly.ExecuteNonQueryAsync();
 const string sql="""
 SELECT DISTINCT f."FieldType"::text, f."Options"::text
 FROM public."ChecklistField" f
 JOIN public."ChecklistSection" s ON s."ChecklistSectionID"=f."ChecklistSectionID"
 JOIN public."ChecklistTemplateVersion" v ON v."ChecklistTemplateVersionID"=s."ChecklistTemplateVersionID"
 JOIN public."ChecklistTemplate" t ON t."ChecklistTemplateID"=v."ChecklistTemplateID"
 WHERE t."TemplateNumber"='MZT-QC-306' AND f."FieldType"::text='Dropdown'
 """;
 await using(var query=new NpgsqlCommand(sql,connection,tx))await using(var reader=await query.ExecuteReaderAsync())
 while(await reader.ReadAsync())Console.WriteLine("TYPE "+reader.GetString(0)+" OPTIONS "+(reader.IsDBNull(1)?"null":reader.GetString(1)));
 await tx.RollbackAsync();Console.WriteLine("READ ONLY — no records changed");
}
catch(Exception error){Console.WriteLine("Probe failed: "+error.GetType().Name+"; cause "+error.InnerException?.GetType().Name);if(error is PostgresException p)Console.WriteLine("SQLSTATE "+p.SqlState);Environment.ExitCode=2;}