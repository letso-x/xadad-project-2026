using System.Text.Json;
using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Exceptions;
using MzukuluQMS.Api.Services.Users;
namespace MzukuluQMS.Api.Services.Workflow;

public sealed record CableRegisterInput(string RegisterNumber,string? Description,string? RegisterRevision,string? DrawingReference,string? ScheduleReference,string? AreaLocation,string? Notes,long ExpectedRevision);
public sealed record CableInput(string CableNumber,string? FromLocation,string? ToLocation,string? CableType,string? CableSize,string? Cores,string? DrumNumber,decimal? Length,bool? Pulled,bool? Glanded,bool? Terminated,string? IrCertificateNumber,string? ContinuityCertificateNumber,bool? Complete,long ExpectedRevision);
public static class CableRules
{
 public static void Validate(CableRegisterInput r)
 {
  if(string.IsNullOrWhiteSpace(r.RegisterNumber)||r.RegisterNumber.Length>100||r.ExpectedRevision<0)throw new ArgumentException("Register number is required (maximum 100 characters).");
  if(new[]{r.Description,r.RegisterRevision,r.DrawingReference,r.ScheduleReference,r.AreaLocation}.Any(s=>s?.Length>500)||r.Notes?.Length>4000)throw new ArgumentException("Check register text lengths (500 characters; notes 4000).");
 }
 public static void Validate(CableInput r)
 {
  if(string.IsNullOrWhiteSpace(r.CableNumber)||r.CableNumber.Length>100||r.ExpectedRevision<0)throw new ArgumentException("Cable number is required (maximum 100 characters).");
  if(new[]{r.FromLocation,r.ToLocation,r.CableType,r.CableSize,r.Cores,r.DrumNumber,r.IrCertificateNumber,r.ContinuityCertificateNumber}.Any(s=>s?.Length>200)||r.Length is <0 or >1000000)throw new ArgumentException("Check cable details (maximum 200 characters) and nonnegative length.");
  if(r.Complete==true&&(r.Pulled!=true||r.Glanded!=true||r.Terminated!=true||string.IsNullOrWhiteSpace(r.IrCertificateNumber)||string.IsNullOrWhiteSpace(r.ContinuityCertificateNumber)))throw new ArgumentException("A complete cable needs pulled, glanded and terminated confirmation, plus IR and continuity certificate numbers.");
 }
}
public sealed class CableRegisters(QmsDbSession session,IQCWorkflow workflow,ICurrentUserService users)
{
 private async Task RequireSchemaAsync(CancellationToken ct)
 {
  var count=await session.Connection!.ExecuteScalarAsync<int>(new CommandDefinition("""
   SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND
    ((table_name='CableRegister' AND column_name IN ('Revision','RegisterRevision','DrawingReference','ScheduleReference','AreaLocation','Notes','MaintainedByUserID','VerifiedByUserID','VerifiedAt'))
    OR (table_name='CableRecord' AND column_name IN ('Revision','CableSize','Cores','DrumNumber','Pulled','Glanded','Terminated','IrCertificateNumber','ContinuityCertificateNumber','Complete')))
   """,transaction:session.Transaction,cancellationToken:ct));
  if(count!=19)throw new InvalidOperationException("Cable registers need administrator setup. QC records and saved work are unaffected.");
 }
 public async Task<object> ListAsync(long projectId,CancellationToken ct)
 {
  await using var access=await workflow.BeginAsync(null,projectId,"ReadInspection",ct);await RequireSchemaAsync(ct);
  return (await session.Connection!.QueryAsync(new CommandDefinition("""
   SELECT r.*,count(c."CableRecordID") AS "Total",count(*) FILTER(WHERE c."Pulled") AS "PulledCount",
    count(*) FILTER(WHERE c."Glanded") AS "GlandedCount",count(*) FILTER(WHERE c."Terminated") AS "TerminatedCount",count(*) FILTER(WHERE c."Complete") AS "CompleteCount"
   FROM public."CableRegister" r LEFT JOIN public."CableRecord" c ON c."CableRegisterID"=r."CableRegisterID"
   WHERE r."ProjectID"=@projectId GROUP BY r."CableRegisterID" ORDER BY r."CableRegisterID" DESC
   """,new {projectId},session.Transaction,cancellationToken:ct))).Select(BusinessRow).ToList();
 }
 public async Task<object> GetAsync(long projectId,long registerId,CancellationToken ct)
 {
  await using var access=await workflow.BeginAsync(null,projectId,"ReadInspection",ct);await RequireSchemaAsync(ct);
  var register=await session.Connection!.QuerySingleOrDefaultAsync(new CommandDefinition("""
   SELECT r.*,p."ProjectName",p."ProjectNumber",p."ContractOrderNumber",p."SiteLocation",c."ClientName",
    NULLIF(btrim(concat_ws(' ',m."FirstName",m."LastName")),'') AS "MaintainedByName",
    NULLIF(btrim(concat_ws(' ',v."FirstName",v."LastName")),'') AS "VerifiedByName"
   FROM public."CableRegister" r JOIN public."Project" p ON p."ProjectID"=r."ProjectID" JOIN public."Client" c ON c."ClientID"=p."ClientID"
   LEFT JOIN public."User" m ON m."UserID"=r."MaintainedByUserID" LEFT JOIN public."User" v ON v."UserID"=r."VerifiedByUserID"
   WHERE r."CableRegisterID"=@registerId AND r."ProjectID"=@projectId
   """,new {projectId,registerId},session.Transaction,cancellationToken:ct))??throw new KeyNotFoundException("Cable register was not found.");
  var rows=(await session.Connection!.QueryAsync(new CommandDefinition("SELECT * FROM public.\"CableRecord\" WHERE \"CableRegisterID\"=@registerId ORDER BY \"CableNumber\",\"CableRecordID\"",new {registerId},session.Transaction,cancellationToken:ct))).AsList();
  return new {register=BusinessRow(register),rows=rows.Select(BusinessRow).ToList()};
 }
 private async Task LockAsync(long projectId,long registerId,long revision,CancellationToken ct)
 {
  var actual=await session.Connection!.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT \"Revision\" FROM public.\"CableRegister\" WHERE \"CableRegisterID\"=@registerId AND \"ProjectID\"=@projectId FOR UPDATE",new {projectId,registerId},session.Transaction,cancellationToken:ct))??throw new KeyNotFoundException("Cable register was not found.");
  if(actual!=revision)throw new ConflictException("This register changed. Your draft is retained; refresh before saving.");
 }
 private async Task AuditAsync(Guid actor,string action,long id,object details,CancellationToken ct)
 {
  await session.Connection!.ExecuteAsync(new CommandDefinition("INSERT INTO qms_security.\"AuditEvent\"(\"ActorUserID\",\"Action\",\"EntityType\",\"EntityID\",\"Details\") VALUES(@actor,@action,'CableRegister',@id,CAST(@json AS jsonb))",new {actor,action,id,json=JsonSerializer.Serialize(details)},session.Transaction,cancellationToken:ct));await session.CommitAsync(ct);
 }
 public async Task<object> SaveRegisterAsync(long projectId,long? registerId,CableRegisterInput r,CancellationToken ct)
 {
  CableRules.Validate(r);await using var access=await workflow.BeginAsync(null,projectId,"PerformInspection",ct);await RequireSchemaAsync(ct);var actor=(await users.GetCurrentUserAsync(ct)).UserID;
  await session.Connection!.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended('cable-register:'||CAST(@projectId AS text),0))",new{projectId},session.Transaction,cancellationToken:ct));
  if(await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM public.\"CableRegister\" WHERE \"ProjectID\"=@projectId AND lower(btrim(\"RegisterNumber\"))=lower(btrim(@number)) AND \"CableRegisterID\"<>COALESCE(CAST(@id AS bigint),0))",new{projectId,number=r.RegisterNumber,id=registerId},session.Transaction,cancellationToken:ct)))throw new ConflictException("This register number already exists. Refresh the register list before creating another.");
  r=r with {RegisterNumber=r.RegisterNumber.Trim()};
  object? previous=null;long id;
  if(registerId.HasValue)
  {
   await LockAsync(projectId,registerId.Value,r.ExpectedRevision,ct);previous=await session.Connection!.QuerySingleAsync(new CommandDefinition("SELECT * FROM public.\"CableRegister\" WHERE \"CableRegisterID\"=@id",new{id=registerId.Value},session.Transaction,cancellationToken:ct));
   id=await session.Connection!.QuerySingleAsync<long>(new CommandDefinition("""
    UPDATE public."CableRegister" SET "RegisterNumber"=@RegisterNumber,"Description"=@Description,"RegisterRevision"=@RegisterRevision,
     "DrawingReference"=@DrawingReference,"ScheduleReference"=@ScheduleReference,"AreaLocation"=@AreaLocation,"Notes"=@Notes,
     "MaintainedByUserID"=@actor,"VerifiedByUserID"=NULL,"VerifiedAt"=NULL,"Revision"="Revision"+1,"UpdatedAt"=clock_timestamp()
    WHERE "CableRegisterID"=@id RETURNING "CableRegisterID"
    """,Parameters(r,new{actor,id=registerId.Value}),session.Transaction,cancellationToken:ct));
  }
  else id=await session.Connection!.QuerySingleAsync<long>(new CommandDefinition("""
   INSERT INTO public."CableRegister"("ProjectID","RegisterNumber","Description","RegisterRevision","DrawingReference","ScheduleReference","AreaLocation","Notes","MaintainedByUserID")
   VALUES(@projectId,@RegisterNumber,@Description,@RegisterRevision,@DrawingReference,@ScheduleReference,@AreaLocation,@Notes,@actor) RETURNING "CableRegisterID"
   """,Parameters(r,new{actor,projectId}),session.Transaction,cancellationToken:ct));
  await AuditAsync(actor,registerId.HasValue?"CableRegisterUpdated":"CableRegisterCreated",id,new{previous,change=r},ct);return new{cableRegisterID=id};
 }
 public async Task SaveCableAsync(long projectId,long registerId,long? cableId,long registerRevision,CableInput r,CancellationToken ct)
 {
  CableRules.Validate(r);await using var access=await workflow.BeginAsync(null,projectId,"PerformInspection",ct);await RequireSchemaAsync(ct);await LockAsync(projectId,registerId,registerRevision,ct);var actor=(await users.GetCurrentUserAsync(ct)).UserID;
  if(await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM public.\"CableRecord\" WHERE \"CableRegisterID\"=@registerId AND lower(btrim(\"CableNumber\"))=lower(btrim(@number)) AND \"CableRecordID\"<>COALESCE(CAST(@id AS bigint),0))",new{registerId,number=r.CableNumber,id=cableId},session.Transaction,cancellationToken:ct)))throw new ConflictException("This cable number already exists in the register. Refresh before adding it again.");
  r=r with {CableNumber=r.CableNumber.Trim()};
  object? previous=null;
  if(cableId.HasValue)previous=await session.Connection!.QuerySingleOrDefaultAsync(new CommandDefinition("SELECT * FROM public.\"CableRecord\" WHERE \"CableRecordID\"=@cableId AND \"CableRegisterID\"=@registerId FOR UPDATE",new{cableId,registerId},session.Transaction,cancellationToken:ct))??throw new KeyNotFoundException("Cable was not found.");
  var parameters=Parameters(r,new{cableId,registerId});
  var sql=cableId.HasValue?"""
   UPDATE public."CableRecord" SET "CableNumber"=@CableNumber,"FromLocation"=@FromLocation,"ToLocation"=@ToLocation,"CableType"=@CableType,"CableSize"=@CableSize,"Cores"=@Cores,"DrumNumber"=@DrumNumber,"Length"=@Length,
    "Pulled"=@Pulled,"Glanded"=@Glanded,"Terminated"=@Terminated,"IrCertificateNumber"=@IrCertificateNumber,"ContinuityCertificateNumber"=@ContinuityCertificateNumber,"Complete"=@Complete,"Revision"="Revision"+1,"UpdatedAt"=clock_timestamp()
   WHERE "CableRecordID"=@cableId AND "CableRegisterID"=@registerId AND "Revision"=@ExpectedRevision
   """:"""
   INSERT INTO public."CableRecord"("CableRegisterID","CableNumber","FromLocation","ToLocation","CableType","CableSize","Cores","DrumNumber","Length","Pulled","Glanded","Terminated","IrCertificateNumber","ContinuityCertificateNumber","Complete")
   VALUES(@registerId,@CableNumber,@FromLocation,@ToLocation,@CableType,@CableSize,@Cores,@DrumNumber,@Length,@Pulled,@Glanded,@Terminated,@IrCertificateNumber,@ContinuityCertificateNumber,@Complete)
   """;
  if(await session.Connection!.ExecuteAsync(new CommandDefinition(sql,parameters,session.Transaction,cancellationToken:ct))!=1)throw new ConflictException("This cable changed. Your draft is retained; refresh before saving.");
  await session.Connection!.ExecuteAsync(new CommandDefinition("UPDATE public.\"CableRegister\" SET \"Revision\"=\"Revision\"+1,\"MaintainedByUserID\"=@actor,\"VerifiedByUserID\"=NULL,\"VerifiedAt\"=NULL,\"UpdatedAt\"=clock_timestamp() WHERE \"CableRegisterID\"=@registerId",new{actor,registerId},session.Transaction,cancellationToken:ct));
  await AuditAsync(actor,cableId.HasValue?"CableUpdated":"CableAdded",registerId,new{cableId,previous,change=r},ct);
 }
 private static DynamicParameters Parameters(object input,object extra){var result=new DynamicParameters(input);result.AddDynamicParams(extra);return result;}
 private static IDictionary<string,object?> BusinessRow(object row)=>((IDictionary<string,object>)row).ToDictionary(p=>char.ToLowerInvariant(p.Key[0])+p.Key[1..],p=>(object?)p.Value);
 public async Task VerifyAsync(long projectId,long registerId,long revision,CancellationToken ct)
 {
  await using var access=await workflow.BeginAsync(null,projectId,"ReviewInspection",ct);await RequireSchemaAsync(ct);await LockAsync(projectId,registerId,revision,ct);
  var actor=(await users.GetCurrentUserAsync(ct)).UserID;
  var previous=await session.Connection!.QuerySingleAsync(new CommandDefinition("SELECT * FROM public.\"CableRegister\" WHERE \"CableRegisterID\"=@registerId",new{registerId},session.Transaction,cancellationToken:ct));
  var maintainer=(Guid?)previous.MaintainedByUserID;
  if(maintainer is null||maintainer==actor)throw new ForbiddenException("A different authorised reviewer must verify this register.");
  var complete=await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT count(*)>0 AND bool_and(\"Complete\" IS TRUE AND \"Pulled\" IS TRUE AND \"Glanded\" IS TRUE AND \"Terminated\" IS TRUE AND NULLIF(btrim(\"IrCertificateNumber\"),'') IS NOT NULL AND NULLIF(btrim(\"ContinuityCertificateNumber\"),'') IS NOT NULL) FROM public.\"CableRecord\" WHERE \"CableRegisterID\"=@registerId",new{registerId},session.Transaction,cancellationToken:ct));
  if(!complete)throw new InvalidOperationException("Every cable must be complete before final register verification.");
  await session.Connection!.ExecuteAsync(new CommandDefinition("UPDATE public.\"CableRegister\" SET \"VerifiedByUserID\"=@actor,\"VerifiedAt\"=clock_timestamp(),\"Revision\"=\"Revision\"+1,\"UpdatedAt\"=clock_timestamp() WHERE \"CableRegisterID\"=@registerId",new{actor,registerId},session.Transaction,cancellationToken:ct));
  await AuditAsync(actor,"CableRegisterVerified",registerId,new{previous},ct);
 }
}
