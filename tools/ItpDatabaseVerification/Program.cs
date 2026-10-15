using System.Text.Json;
using Dapper;
using Npgsql;
using Microsoft.AspNetCore.Http;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.DTOs.Users;
using MzukuluQMS.Api.Exceptions;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Workflow;

// Integration assertions against a NEW, ephemeral, LOCAL database only. Never reads API User Secrets.
var configured=Environment.GetEnvironmentVariable("QMS_ITP_TEST_ADMIN_CONNECTION");
if(string.IsNullOrWhiteSpace(configured)){Console.WriteLine("NOT RUN: set QMS_ITP_TEST_ADMIN_CONNECTION for a local PostgreSQL administrator. No database changes made.");return 2;}
var settings=new NpgsqlConnectionStringBuilder(configured);
if(settings.Host is not ("localhost" or "127.0.0.1" or "::1")){Console.WriteLine("REFUSED: only loopback PostgreSQL is allowed. No database changes made.");return 2;}
var database="qms_itp_test_"+Guid.NewGuid().ToString("N");
await using var admin=new NpgsqlConnection(settings.ConnectionString);await admin.OpenAsync();
await admin.ExecuteAsync($"CREATE DATABASE \"{database}\"");settings.Database=database;
var assertions=0;var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);var first=Guid.NewGuid();var second=Guid.NewGuid();var outsider=Guid.NewGuid();
try
{
 await using var fixture=new NpgsqlConnection(settings.ConnectionString);await fixture.OpenAsync();
 await fixture.ExecuteAsync("""
  CREATE SCHEMA qms_security;CREATE SCHEMA qms_core;
  CREATE TABLE public."User"("UserID" uuid PRIMARY KEY,"ExternalAuthID" uuid,"Role" text,"IsActive" boolean,"FirstName" text,"LastName" text);
  CREATE TABLE public."Project"("ProjectID" bigint PRIMARY KEY,"ProjectName" text);
  CREATE TABLE public."ChecklistTemplateVersion"("ChecklistTemplateVersionID" bigint PRIMARY KEY);
  CREATE TABLE public."QCForm"("QCFormID" bigint PRIMARY KEY,"ProjectID" bigint,"ChecklistTemplateVersionID" bigint,"FormNumber" text,"Status" text,"SubmittedAt" timestamptz);
  CREATE TABLE public."NCR"("NCRID" bigint PRIMARY KEY,"QCFormID" bigint,"ClosedAt" timestamptz);
  CREATE TABLE qms_core."Ncr"("QCFormID" bigint,"LegacyNCRID" bigint,"ClosedAt" timestamptz,"IsBlocking" boolean);
  CREATE TABLE qms_security."RolePermission"("RoleName" text,"Permission" text,PRIMARY KEY("RoleName","Permission"));
  CREATE TABLE qms_security."ProjectAccess"("UserID" uuid,"ProjectID" bigint,PRIMARY KEY("UserID","ProjectID"));
  CREATE TABLE qms_security."AuditEvent"("AuditEventID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,"ActorUserID" uuid,"Action" text,"EntityType" text,"EntityID" bigint,"OccurredAt" timestamptz DEFAULT clock_timestamp(),"Details" jsonb NOT NULL);
  CREATE TABLE qms_security."FormSignatureRequirement"("QCFormID" bigint,"SignOffType" text);
  CREATE TABLE public."QCFormSection"("QCFormSectionID" bigint,"QCFormID" bigint);
  CREATE TABLE public."QCFormField"("QCFormFieldID" bigint,"QCFormSectionID" bigint,"RequiresSignature" boolean,"FieldType" text,"IsRequired" boolean);
  CREATE TABLE public."Evidence"("EvidenceID" bigint,"QCFormID" bigint,"QCFormFieldID" bigint,"EvidenceType" text,"UploadedByUserID" uuid,"CreatedAt" timestamptz);
  CREATE TABLE public."QCSignOff"("QCSignOffID" bigint,"QCFormID" bigint,"UserID" uuid,"SignOffType" text,"HashVersion" integer,"ContentHash" text,"SignedAt" timestamptz,"SignatureEvidenceID" bigint);
  INSERT INTO public."Project" VALUES(1,'Synthetic project'),(2,'Inaccessible project');INSERT INTO public."ChecklistTemplateVersion" VALUES(10);
  INSERT INTO public."QCForm" VALUES(1,1,10,'QC-LOCAL','Approved',clock_timestamp()-interval '1 hour');
  """);
 var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"mobile","MzukuluQMS.Api","Migrations","007_itp.sql")))root=root.Parent;
 if(root is null)throw new InvalidOperationException("Run the probe from the repository checkout.");
 await fixture.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(root.FullName,"mobile","MzukuluQMS.Api","Migrations","007_itp.sql")));
 foreach(var id in new[]{first,second,outsider})await fixture.ExecuteAsync("INSERT INTO public.\"User\" VALUES(@id,@id,@role,true,'Synthetic','Verifier');INSERT INTO qms_security.\"ProjectAccess\" VALUES(@id,1)",new{id,role=id==outsider?"Reader":"Executor"});
 var permissions=new[]{"ReadItp","ManageItp","SubmitItp","ApproveItp","ExecuteItp","ReleaseItpHold","WitnessItp","ReviewItp","SignInspection","SignInspection:Inspector"};
 foreach(var permission in permissions)await fixture.ExecuteAsync("INSERT INTO qms_security.\"RolePermission\" VALUES('Executor',@permission)",new{permission});
 await fixture.ExecuteAsync("INSERT INTO qms_security.\"RolePermission\" VALUES('Reader','ReadItp');INSERT INTO qms_security.\"FormSignatureRequirement\" VALUES(1,'Inspector');INSERT INTO public.\"Evidence\" VALUES(1,1,NULL,'Signature',@first,clock_timestamp()-interval '30 minutes');INSERT INTO public.\"QCSignOff\" VALUES(1,1,@first,'Inspector',2,@hash,clock_timestamp()-interval '20 minutes',1)",new{first,hash=Hash.Value});
 async Task<JsonElement> Call(Guid actor,Func<ItpService,Task<object>> action)
 {
  await using var db=new QmsDbSession(new Factory(settings.ConnectionString));var current=new Actor(actor);var workflow=new QCWorkflow(db,current,new HttpContextAccessor());var service=new ItpService(db,workflow,current,new Hash());
  return JsonSerializer.SerializeToElement(await action(service),json);
 }
 async Task<ItpVersion> Get(long id)=>JsonSerializer.Deserialize<ItpVersion>((await Call(first,s=>s.Get(1,id,default))).GetProperty("version"),json)!;
 void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);assertions++;}
 async Task Denied<T>(Func<Task> action) where T:Exception {try{await action();}catch(T){assertions++;return;}throw new InvalidOperationException("Expected "+typeof(T).Name);}
 ItpStep Step(long id,string point,params long[] dependencies)=>new(id,(int)id,"A"+id,"Activity "+id,"Installation","Criteria confirmed",point,dependencies.ToList());
 var definition=new ItpDefinition("ITP-LOCAL","Local integration plan",[Step(1,"Hold"),Step(2,"Witness",1),Step(3,"Review",2),Step(4,"None",3),Step(5,"None",4)]);
 var created=await Call(first,s=>s.Create(1,definition,default));var versionId=created.GetProperty("id").GetInt64();
 Check((await fixture.ExecuteScalarAsync<int>("SELECT count(*) FROM qms_security.\"AuditEvent\" WHERE \"EntityID\"=@versionId AND \"Action\"='ItpCreated'",new{versionId}))==1,"Create audit missing.");
 await Denied<ForbiddenException>(()=>Call(outsider,s=>s.Act(1,versionId,"release",new(0,1,"Denied"),default)));
 await Denied<ForbiddenException>(()=>Call(first,s=>s.List(2,default)));
 var v=await Get(versionId);Check(v.Definition.Activities.Count==5&&v.Executions.Count==5,"Dapper definition/execution mapping failed.");
 await Call(first,s=>s.Save(1,versionId,new(v.Stamp,definition with {Title="Edited"}),default));
 await Denied<ConflictException>(()=>Call(first,s=>s.Save(1,versionId,new(v.Stamp,definition),default)));
 v=await Get(versionId);
 var edited=definition with {Activities=definition.Activities.Select(a=>a.Id==4?a with {Sequence=5}:a.Id==5?a with {Sequence=4,DependsOn=[3,4]}:a).Append(Step(6,"None",5)).ToList()};
 await Call(first,s=>s.Save(1,versionId,new(v.Stamp,edited),default));v=await Get(versionId);await Call(first,s=>s.Save(1,versionId,new(v.Stamp,definition),default));
 async Task Act(string action,long? activity=null,Guid? actor=null,long? qc=null)
 {var latest=await Get(versionId);await Call(actor??first,s=>s.Act(1,versionId,action,new(latest.Stamp,activity,"Criteria checked",qc),default));}
 await Act("submit");v=await Get(versionId);await Denied<InvalidOperationException>(()=>Call(first,s=>s.Save(1,versionId,new(v.Stamp,definition),default)));await Act("approve",actor:second);
 await Denied<InvalidOperationException>(()=>Act("complete",2));await Act("complete",1);await Denied<InvalidOperationException>(()=>Act("complete",2));await Denied<InvalidOperationException>(()=>Act("release",1));await Act("release",1,second);await Act("complete",2);await Denied<InvalidOperationException>(()=>Act("complete",3));await Act("witness",2,second);await Act("complete",3);await Denied<InvalidOperationException>(()=>Act("complete",4));await Act("review",3,second);await Act("complete",4);await Act("complete",5);
 v=await Get(versionId);Check(ItpRules.Progress(v).PercentageComplete==100,"Execution progress mismatch.");Check(v.Executions[0].ReleasedBy==second&&v.Executions[1].WitnessedBy==second&&v.Executions[2].ReviewedBy==second,"Execution traceability missing.");
 var revision=(await Call(first,s=>s.Act(1,versionId,"revise",new(v.Stamp),default))).GetProperty("id").GetInt64();var newer=await Get(revision);Check(newer.Revision==2&&newer.Status=="Draft"&&newer.Executions.All(e=>e.CompletedAt is null),"New revision carried execution.");
 await Call(first,s=>s.Act(1,revision,"submit",new(newer.Stamp),default));newer=await Get(revision);await Call(second,s=>s.Act(1,revision,"approve",new(newer.Stamp),default));Check(!(await Get(versionId)).IsCurrent&&(await Get(revision)).IsCurrent,"Current version was not swapped.");await Denied<InvalidOperationException>(()=>Act("complete",1));
 await Denied<PostgresException>(()=>fixture.ExecuteAsync("UPDATE qms_core.\"ItpActivity\" SET \"Title\"='Forbidden' WHERE \"VersionId\"=@versionId",new{versionId}));
 // Inject audit failure and prove the new plan rolls back with its audit.
 await fixture.ExecuteAsync("CREATE FUNCTION qms_security.itp_test_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Injected audit failure'; END $$;CREATE TRIGGER itp_test_audit_failure BEFORE INSERT ON qms_security.\"AuditEvent\" FOR EACH ROW EXECUTE FUNCTION qms_security.itp_test_audit_failure()");
 await Denied<PostgresException>(()=>Call(first,s=>s.Create(1,definition with {Number="ITP-ROLLBACK"},default)));Check(await fixture.ExecuteScalarAsync<int>("SELECT count(*) FROM qms_core.\"ItpPlan\" WHERE \"Number\"='ITP-ROLLBACK'")==0,"Audit failure committed a plan.");
 var beforeFailure=await Get(revision);await Denied<PostgresException>(()=>Call(first,s=>s.Act(1,revision,"complete",new(beforeFailure.Stamp,1,"Injected failure"),default)));
 var afterFailure=await Get(revision);Check(afterFailure.Stamp==beforeFailure.Stamp&&afterFailure.Executions[0].CompletedAt is null,"Audit failure committed execution or revision stamp.");await fixture.ExecuteAsync("DROP TRIGGER itp_test_audit_failure ON qms_security.\"AuditEvent\"");
 // Real existing signature/NCR workflow protects configured QC completion and Hold release.
 versionId=(await Call(first,s=>s.Create(1,new("ITP-QC","QC integration",[Step(1,"Hold") with {ChecklistTemplateVersionId=10,RequireQc=true}]),default))).GetProperty("id").GetInt64();await Act("submit");await Act("approve",actor:second);await Act("link-qc",1,qc:1);
 await fixture.ExecuteAsync("INSERT INTO qms_core.\"Ncr\" VALUES(1,NULL,NULL,true)");await Denied<InvalidOperationException>(()=>Act("complete",1));await fixture.ExecuteAsync("UPDATE qms_core.\"Ncr\" SET \"ClosedAt\"=clock_timestamp()");await Act("complete",1);Check((await Get(versionId)).Executions[0].ReleasedAt is null,"QC silently released Hold.");await Act("release",1,second);
 var auditActions=(await fixture.QueryAsync<string>("SELECT DISTINCT \"Action\" FROM qms_security.\"AuditEvent\" WHERE \"EntityType\"='ITP'")).ToArray();foreach(var action in new[]{"ItpCreated","ItpDraftUpdated","ItpActivityAdded","ItpActivityEdited","ItpActivityDeleted","ItpActivityReordered","ItpDependencyChanged","ItpRevisionCreated","Itp:submit","Itp:approve","Itp:complete","Itp:release","Itp:witness","Itp:review","Itp:link-qc"})Check(auditActions.Contains(action),"Missing audit action "+action);
 Console.WriteLine(JsonSerializer.Serialize(new{status="pass",assertions,scope="Local ephemeral PostgreSQL; actual 007 migration, Dapper service, security, signature/NCR checks, audit rollback and history"}));return 0;
}
catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{status="fail",failureType=e.GetType().Name,assertions}));return 1;}
finally
{
 NpgsqlConnection.ClearAllPools();
 // Database name generated above, never from caller input. No existing DB is dropped.
 await admin.ExecuteAsync($"DROP DATABASE \"{database}\" WITH (FORCE)");
}
sealed class Factory(string value):IDbConnectionFactory {public async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken=default){var c=new NpgsqlConnection(value);await c.OpenAsync(cancellationToken);return c;}}
sealed class Actor(Guid id):ICurrentUserService {public Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new CurrentUserDto{UserID=id,ExternalAuthID=id,IsActive=true});}
sealed class Hash:IQCFormContentHashService {public static string Value=>new('a',64);public Task<string> GenerateHashAsync(long qcFormId,CancellationToken cancellationToken=default)=>Task.FromResult(Value);}
