using Dapper;
using Microsoft.Extensions.Configuration;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Services.Workflow;
using Npgsql;
using System.Text.RegularExpressions;
var config=new ConfigurationBuilder().AddUserSecrets<NpgsqlConnectionFactory>().AddEnvironmentVariables().Build();
try
{
 await using var c=new NpgsqlConnection(config.GetConnectionString("QmsDatabase"));await c.OpenAsync();
 await using var tx=await c.BeginTransactionAsync();await c.ExecuteAsync("SET TRANSACTION READ ONLY",transaction:tx);
 foreach(var row in await c.QueryAsync<Column>("SELECT table_name AS \"Table\",column_name AS \"Name\",data_type AS \"Type\",is_nullable AS \"Nullable\",column_default AS \"Default\",character_maximum_length AS \"Max\",is_identity AS \"Identity\" FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('CableRegister','CableRecord','ChecklistTemplateVersion','ChecklistTemplate','ChecklistField','ChecklistTemplateProjectField') ORDER BY table_name,ordinal_position",transaction:tx))Console.WriteLine($"COLUMN {row.Table}.{row.Name} {row.Type} nullable={row.Nullable} default={row.Default} max={row.Max} identity={row.Identity}");
 foreach(var value in await c.QueryAsync<string>("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid IN ('public.\"ChecklistTemplateVersion\"'::regclass,'public.\"CableRegister\"'::regclass,'public.\"CableRecord\"'::regclass)",transaction:tx))Console.WriteLine("CONSTRAINT "+value);
 var source=await File.ReadAllTextAsync("mobile/MzukuluQMS.Api/Services/Workflow/NcrService.cs");
 var list=source[source.IndexOf(" ListAsync(",StringComparison.Ordinal)..source.IndexOf(" CorrectAsync(",StringComparison.Ordinal)];
 foreach(Match query in Regex.Matches(list,"\"\"\"(.*?)\"\"\"",RegexOptions.Singleline))
 {
  var rows=(await c.QueryAsync<NcrRow>(query.Groups[1].Value,new{formId=0L},tx)).AsList();
  Console.WriteLine("PASS NCR enriched SQL parsed/read without database mutations.");
 }
 foreach(var row in await c.QueryAsync<Permission>("SELECT \"RoleName\" AS \"Role\",\"Permission\" AS \"Name\" FROM qms_security.\"RolePermission\" WHERE \"Permission\" IN ('ReadNcr','CorrectNcr','VerifyNcr','ReadInspection','PerformInspection','ManageTemplates','ReadReports') ORDER BY \"RoleName\",\"Permission\"",transaction:tx))Console.WriteLine($"PERMISSION {row.Role}: {row.Name}");
 var authoring=await File.ReadAllTextAsync("mobile/MzukuluQMS.Api/Services/Workflow/TemplateAuthoring.cs");
 var parameters=new {id=0L,templateId=0L,versionId=0L,number="read-only-probe",name="Probe",Subtitle=(string?)null,Active=true,actor=Guid.Empty,Revision="Probe",revision="Probe",date=DateTime.SpecifyKind(DateTime.Today,DateTimeKind.Unspecified),Scope=(string?)null,AcceptanceCriteria=(string?)null,title="Probe",order=1,Required=true,type="Inspector",action="Probe",previous=(long?)null,parent=0L,json="{}"};
 foreach(Match query in Regex.Matches(authoring,"\"\"\"(.*?)\"\"\"",RegexOptions.Singleline))
 {
  var sql=query.Groups[1].Value.Trim();if(sql.Contains(';'))throw new InvalidOperationException("EXPLAIN probe accepts one statement only.");
  await c.ExecuteAsync("EXPLAIN "+sql,parameters,tx);Console.WriteLine("PASS template SQL EXPLAIN only (not executed).");
 }
 var reporting=await File.ReadAllTextAsync("mobile/MzukuluQMS.Api/Services/Workflow/QualityReporting.cs");
 var notifications=reporting[reporting.IndexOf(" NotificationsAsync(",StringComparison.Ordinal)..reporting.IndexOf(" MarkReadAsync(",StringComparison.Ordinal)];
 foreach(Match query in Regex.Matches(notifications,"\"\"\"(.*?)\"\"\"",RegexOptions.Singleline)){await c.QueryAsync<NotificationRow>(query.Groups[1].Value,new{actor=Guid.Empty},tx);Console.WriteLine("PASS notification names/project-access query read-only.");}
 var histories=await File.ReadAllTextAsync("mobile/MzukuluQMS.Api/Services/Workflow/QCFormQueries.cs");
 var signs=histories[histories.IndexOf(" SignOffsAsync(",StringComparison.Ordinal)..histories.IndexOf(" PublicAuditAsync(",StringComparison.Ordinal)];
 foreach(Match query in Regex.Matches(signs,"\"\"\"(.*?)\"\"\"",RegexOptions.Singleline)){await c.QueryAsync<SignOffHistoryRow>(query.Groups[1].Value,new{formId=0L,currentHash=new string('a',64)},tx);Console.WriteLine("PASS signature requirement/name query read-only.");}
 await tx.RollbackAsync();Console.WriteLine("PASS metadata/contract probe finished read-only; no users, grants, templates or records changed.");
}
catch(Exception e){Environment.ExitCode=2;Console.WriteLine("Probe failure type: "+e.GetType().Name);if(e is PostgresException pg)Console.WriteLine("SQLSTATE: "+pg.SqlState);}
record Column(string Table,string Name,string Type,string Nullable,string? Default,int? Max,string Identity);
record Permission(string Role,string Name);
