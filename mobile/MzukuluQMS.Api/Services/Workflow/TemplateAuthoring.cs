using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services.Users;
namespace MzukuluQMS.Api.Services.Workflow;
public sealed class TemplateAuthoring(QmsDbSession session,ICurrentUserService users)
{
 public static void Validate(CreateChecklistTemplateRequest r)
 {
  if(new[]{r.DocumentNumber,r.DocumentName,r.Revision}.Any(s=>string.IsNullOrWhiteSpace(s)||s.Length>200))throw new ArgumentException("Document number, name and revision are required (max 200 characters).");
  if(r.Sections is null||r.Sections.Count is <1 or >100||r.ProjectFields is null||r.ProjectFields.Count>100)throw new ArgumentException("Provide 1–100 checklist sections and at most 100 project fields.");
  if(r.Sections.Any(s=>s.Fields is null||s.Fields.Count==0)||r.Sections.Sum(s=>s.Fields.Count)>1000)throw new ArgumentException("Every section needs actual checklist fields; at most 1000 total fields are allowed.");
  if(r.RequiredSignOffTypes is null||r.RequiredSignOffTypes.Count==0||r.RequiredSignOffTypes.Distinct().Count()!=r.RequiredSignOffTypes.Count||r.RequiredSignOffTypes.Any(t=>t is not ("Inspector" or "Technician" or "Supervisor" or "Client")))throw new ArgumentException("Configure distinct required signing capacities explicitly.");
  var keys=new HashSet<string>(StringComparer.Ordinal);
  foreach(var field in r.ProjectFields.Concat(r.Sections.SelectMany(s=>s.Fields)))
  {
   if(field.Key is null||!Regex.IsMatch(field.Key,"^[a-z][a-z0-9_]{0,63}$")||!keys.Add(field.Key)||string.IsNullOrWhiteSpace(field.Label)||field.Label.Length>500)throw new ArgumentException("Field keys must be unique lowercase identifiers and labels are required.");
   if(field.Type is not ("Text" or "LongText" or "Number" or "Decimal" or "Boolean" or "YesNo" or "Date" or "DateTime" or "Dropdown" or "MultiSelect" or "Photo" or "Signature" or "Reference"))throw new ArgumentException("Unsupported field type.");
   if(field.Type is "Dropdown" or "MultiSelect")
   {
    if(field.Options is not {ValueKind:JsonValueKind.Array} options||options.GetArrayLength()==0||options.EnumerateArray().Any(o=>o.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(o.GetString())))throw new ArgumentException("Choice fields need a nonempty array of string options.");
    if(options.EnumerateArray().Select(o=>o.GetString()).Distinct(StringComparer.Ordinal).Count()!=options.GetArrayLength())throw new ArgumentException("Choice options must be unique.");
   }
   if(field.ValidationRules is {ValueKind:not (JsonValueKind.Object or JsonValueKind.Null)})throw new ArgumentException("Validation rules must be a JSON object.");
  }
  if(r.ProjectFields.Any(f=>f.Type is "Photo" or "Signature"||f.RequiresPhoto||f.RequiresSignature))throw new ArgumentException("Project information fields cannot use QC-field evidence; place evidence fields in a checklist section.");
  foreach(var field in r.ProjectFields)if(Item(field) is not null)throw new ArgumentException("Inspection item relationships belong to section fields, not project headers.");
  foreach(var section in r.Sections)
  {
   var results=section.Fields.Where(f=>Item(f)?.Role=="Result").ToDictionary(f=>f.Key,StringComparer.Ordinal);
   foreach(var field in section.Fields)
   {
    if(field.ValidationRules is {ValueKind:JsonValueKind.Object} rules&&rules.TryGetProperty("unit",out var unit)&&
     (unit.ValueKind!=JsonValueKind.String||unit.GetString()!.Length>50))throw new ArgumentException("Units must be text of at most 50 characters.");
    var item=Item(field);if(item is null)continue;
    if(item.Value.Role=="Result")
    {
     if(item.Value.Key!=field.Key||field.Type!="Dropdown"||field.Options is not {ValueKind:JsonValueKind.Array} choices||
      !choices.EnumerateArray().Select(o=>o.GetString()).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"A","R","N/A"}))
      throw new ArgumentException("Each inspection item needs its own A/R/N/A result field and matching stable key.");
    }
    else if(item.Value.Role!="Additional"||!results.ContainsKey(item.Value.Key))throw new ArgumentException("Additional fields must reference a result item in the same section.");
   }
  }
 }
 private static (string Key,string Role)? Item(TemplateFieldInput field)
 {
  if(field.ValidationRules is not {ValueKind:JsonValueKind.Object} rules||!rules.TryGetProperty("inspectionItem",out var item))return null;
  if(item.ValueKind!=JsonValueKind.Object||!item.TryGetProperty("key",out var key)||key.ValueKind!=JsonValueKind.String||
   !item.TryGetProperty("role",out var role)||role.ValueKind!=JsonValueKind.String)throw new ArgumentException("Invalid inspection-item relationship metadata.");
  return (key.GetString()!,role.GetString()!);
 }
 public async Task<object> CreateAsync(CreateChecklistTemplateRequest r,CancellationToken ct)
 {
  Validate(r);var actor=await users.GetCurrentUserAsync(ct);await session.BeginAsync(ct);
  try
  {
   // Serialises competing document-number creation without depending on unknown legacy uniqueness constraints.
   await session.Connection!.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@number,0))",new {number=r.DocumentNumber.Trim().ToLowerInvariant()},session.Transaction,cancellationToken:ct));
   if(await session.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS(SELECT 1 FROM public.\"ChecklistTemplate\" WHERE lower(\"TemplateNumber\")=lower(@number))",new {number=r.DocumentNumber.Trim()},session.Transaction,cancellationToken:ct)))throw new InvalidOperationException("Document number already exists. Existing templates are not overwritten.");
   var templateId=await session.Connection!.QuerySingleAsync<long>(new CommandDefinition("""
    INSERT INTO public."ChecklistTemplate" ("TemplateNumber","TemplateName","Subtitle","IsActive","CreatedByUserID")
    VALUES(@number,@name,@Subtitle,@Active,@actor) RETURNING "ChecklistTemplateID"
    """,new {number=r.DocumentNumber.Trim(),name=r.DocumentName.Trim(),r.Subtitle,r.Active,actor=actor.UserID},session.Transaction,cancellationToken:ct));
   var versionId=await session.Connection!.QuerySingleAsync<long>(new CommandDefinition("""
    INSERT INTO public."ChecklistTemplateVersion" ("ChecklistTemplateID","Revision","IssueDate","EffectiveDate","Status","Scope","AcceptanceCriteria","CreatedByUserID")
    VALUES(@templateId,@Revision,@date,@date,'Active',@Scope,@AcceptanceCriteria,@actor) RETURNING "ChecklistTemplateVersionID"
    """,new {templateId,r.Revision,date=r.IssueDate.ToDateTime(TimeOnly.MinValue),r.Scope,r.AcceptanceCriteria,actor=actor.UserID},session.Transaction,cancellationToken:ct));
   var order=0;foreach(var field in r.ProjectFields)await InsertFieldAsync(field,versionId,++order,true,ct);
   order=0;foreach(var section in r.Sections)
   {
    var sectionId=await session.Connection!.QuerySingleAsync<long>(new CommandDefinition("""
     INSERT INTO public."ChecklistSection" ("ChecklistTemplateVersionID","SectionName","DisplayOrder","IsRequired")
     VALUES(@versionId,@title,@order,@Required) RETURNING "ChecklistSectionID"
     """,new {versionId,title=section.Title??"",order=++order,section.Required},session.Transaction,cancellationToken:ct));
    var fieldOrder=0;foreach(var field in section.Fields)await InsertFieldAsync(field,sectionId,++fieldOrder,false,ct);
   }
   foreach(var type in r.RequiredSignOffTypes)await session.Connection!.ExecuteAsync(new CommandDefinition("""
    INSERT INTO qms_security."TemplateSignatureRequirement" ("ChecklistTemplateVersionID","SignOffType") VALUES(@versionId,@type)
    """,new {versionId,type},session.Transaction,cancellationToken:ct));
   await session.Connection!.ExecuteAsync(new CommandDefinition("""
    INSERT INTO qms_security."AuditEvent" ("ActorUserID","Action","EntityType","EntityID","Details")
    VALUES(@actor,'ChecklistTemplateCreated','ChecklistTemplateVersion',@versionId,jsonb_build_object('templateID',@templateId))
    """,new {actor=actor.UserID,versionId,templateId},session.Transaction,cancellationToken:ct));
   await session.CommitAsync(ct);return new {checklistTemplateID=templateId,checklistTemplateVersionID=versionId};
  }
  finally{await session.DisposeAsync();}
 }
 private async Task InsertFieldAsync(TemplateFieldInput field,long parent,int order,bool project,CancellationToken ct)
 {
  var data=new Dictionary<string,object?>{["FieldKey"]=field.Key,["FieldLabel"]=field.Label,["FieldType"]=field.Type,["IsRequired"]=field.Required,["RequiresPhoto"]=field.RequiresPhoto,["RequiresSignature"]=field.RequiresSignature,["HelpText"]=field.HelpText,["Placeholder"]=field.Placeholder,["Options"]=field.Options,["ValidationRules"]=field.ValidationRules,["DisplayOrder"]=order,["IsActive"]=true};
  // Populate the repository table's composite type, so enum conversion uses its actual FieldType rather than a guessed enum name.
  var sql=project?"""
   INSERT INTO public."ChecklistTemplateProjectField" ("ChecklistTemplateVersionID","FieldKey","FieldLabel","FieldType","IsRequired","HelpText","Placeholder","Options","ValidationRules","DisplayOrder","IsActive")
   SELECT @parent,f."FieldKey",f."FieldLabel",f."FieldType",f."IsRequired",f."HelpText",f."Placeholder",f."Options",f."ValidationRules",f."DisplayOrder",f."IsActive"
   FROM jsonb_populate_record(NULL::public."ChecklistTemplateProjectField",CAST(@json AS jsonb)) f
   """:"""
   INSERT INTO public."ChecklistField" ("ChecklistSectionID","FieldKey","FieldLabel","FieldType","IsRequired","RequiresPhoto","RequiresSignature","HelpText","Placeholder","Options","ValidationRules","DisplayOrder","IsActive")
   SELECT @parent,f."FieldKey",f."FieldLabel",f."FieldType",f."IsRequired",f."RequiresPhoto",f."RequiresSignature",f."HelpText",f."Placeholder",f."Options",f."ValidationRules",f."DisplayOrder",f."IsActive"
   FROM jsonb_populate_record(NULL::public."ChecklistField",CAST(@json AS jsonb)) f
   """;
  await session.Connection!.ExecuteAsync(new CommandDefinition(sql,new {parent,json=JsonSerializer.Serialize(data)},session.Transaction,cancellationToken:ct));
 }
}
