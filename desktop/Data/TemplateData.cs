using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MzuApplication.Models;
namespace MzuApplication.Data
{
 internal static class TemplateData
 {
  public static async Task LoadAsync()
  {
   var database=Repository.Db;
   var rows=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/checklist-templates").ConfigureAwait(false));
   var templates=new List<ChecklistTemplate>();
   foreach(var row in rows)
   {
    var id=Json.Id(row,"activeVersionID");
    if(id==0)continue;
    var definition=await ApiClient.Shared.SendAsync("GET","api/checklist-template-versions/"+id).ConfigureAwait(false);
    templates.Add(Map(definition));
   }
   database.Templates=templates;
  }
  public static ChecklistTemplate Map(object row)
  {
   var template=new ChecklistTemplate{Id=Json.Text(row,"checklistTemplateID"),VersionId=Json.Id(row,"checklistTemplateVersionID"),RemoteDefinition=Json.Map(row),DocNumber=Json.Text(row,"templateNumber"),DocName=Json.Text(row,"templateName"),Description=Json.Text(row,"subtitle"),Revision=Json.Text(row,"revision"),IssueDate=Json.Date(row,"issueDate")??DateTime.Today,Active=Json.Bool(row,"isTemplateActive"),Scope=Json.Text(row,"scope"),Acceptance=Json.Text(row,"acceptanceCriteria")};
   template.ProjectFields=Json.Rows(row,"projectFields").Select(f=>Json.Text(f,"fieldLabel")).ToList();
   template.RequiredSignOffTypes=(Json.Get(row,"requiredSignOffTypes") as object[]??new object[0]).Select(Convert.ToString).ToList();
   foreach(var section in Json.Rows(row,"sections"))
   {
    var target=new TemplateSection{Id=Json.Text(section,"checklistSectionID"),Title=Json.Text(section,"sectionName")};
    foreach(var field in Json.Rows(section,"fields"))target.Items.Add(new TemplateItem{Id=Json.Text(field,"fieldKey"),Text=Json.Text(field,"fieldLabel")});
    template.Sections.Add(target);
   }
   return template;
  }
  // The legacy editor can safely edit its own inspection-item schema. Other dynamic
  // definitions remain usable in the form renderer, but cannot be flattened on save.
  public static bool CanEdit(ChecklistTemplate template)
  {
   if(template.RemoteDefinition==null)return true;
   return Json.Rows(template.RemoteDefinition,"projectFields").All(f=>Json.Text(f,"fieldType")=="Text") &&
    Json.Rows(template.RemoteDefinition,"sections").All(s=>string.IsNullOrEmpty(Json.Text(s,"description"))&&Json.Rows(s,"fields").All(f=>Json.Text(f,"fieldType")=="Dropdown" && IsInspectionResult(f)));
  }
  private static bool IsInspectionResult(object f){return Json.Text(Json.Get(Json.Get(f,"validationRules"),"inspectionItem"),"role")=="Result";}
  public static async Task SaveAsync(ChecklistTemplate draft)
  {
   var sections=new List<object>();var fieldIndex=0;
   foreach(var section in draft.Sections)
   {
    var fields=new List<object>();
    foreach(var item in section.Items)
    {
     fieldIndex++;
     var original=Json.Rows(draft.RemoteDefinition,"sections").SelectMany(s=>Json.Rows(s,"fields")).FirstOrDefault(f=>Json.Text(f,"fieldKey")==item.Id);
     var key=original==null?"item_"+Guid.NewGuid().ToString("N"):Json.Text(original,"fieldKey");
     if(original==null)fields.Add(new {key,label=item.Text,type="Dropdown",required=true,requiresPhoto=false,requiresSignature=false,options=new[]{"A","R","N/A"},validationRules=new {inspectionItem=new {key,role="Result"}}});
     else fields.Add(new{key,label=item.Text,type=Json.Text(original,"fieldType"),required=Json.Bool(original,"isRequired"),requiresPhoto=Json.Bool(original,"requiresPhoto"),requiresSignature=Json.Bool(original,"requiresSignature"),helpText=Json.Get(original,"helpText"),placeholder=Json.Get(original,"placeholder"),options=Json.Get(original,"options"),validationRules=Json.Get(original,"validationRules")});
     if(item.HasRef)fields.Add(new {key=key+"_ref",label=item.RefLabel,type="Text",required=item.RefRequired,requiresPhoto=false,requiresSignature=false,validationRules=new {inspectionItem=new {key,role="Additional"}}});
    }
    var originalSection=Json.Rows(draft.RemoteDefinition,"sections").FirstOrDefault(s=>Json.Text(s,"checklistSectionID")==section.Id);
    sections.Add(new {title=section.Title,required=originalSection==null||Json.Bool(originalSection,"isRequired"),fields});
   }
   var priorHeaders=Json.Rows(draft.RemoteDefinition,"projectFields");
   var projectFields=draft.ProjectFields.Select((label,i)=>
   {
    var original=i<priorHeaders.Count?priorHeaders[i]:null;
    return new {key=original==null?"header_"+Guid.NewGuid().ToString("N"):Json.Text(original,"fieldKey"),label,type="Text",required=Json.Bool(original,"isRequired"),requiresPhoto=false,requiresSignature=false,helpText=Json.Get(original,"helpText"),placeholder=Json.Get(original,"placeholder"),options=Json.Get(original,"options"),validationRules=Json.Get(original,"validationRules")};
   }).ToArray();
   var body=new {documentNumber=draft.DocNumber,documentName=draft.DocName,subtitle=draft.Description,revision=draft.Revision,issueDate=draft.IssueDate.ToString("yyyy-MM-dd"),active=draft.Active,scope=draft.Scope,acceptanceCriteria=draft.Acceptance,projectFields,sections,requiredSignOffTypes=draft.RequiredSignOffTypes};
   var saved=await ApiClient.Shared.SendAsync("POST",draft.Id==null?"api/checklist-templates":"api/checklist-templates/"+draft.Id+"/revisions?expectedVersionId="+draft.VersionId,body).ConfigureAwait(false);
   draft.Id=Json.Text(saved,"checklistTemplateID");draft.VersionId=Json.Id(saved,"checklistTemplateVersionID");
   if(string.IsNullOrEmpty(draft.Id)||draft.VersionId==0)throw new InvalidOperationException("Publishing was not fully acknowledged. Refresh the template list before trying again.");
   var database=Repository.Db;
   var definition=await ApiClient.Shared.SendAsync("GET","api/checklist-template-versions/"+draft.VersionId).ConfigureAwait(false);
   var mapped=Map(definition);database.Templates=database.Templates.Where(t=>t.Id!=mapped.Id).Concat(new[]{mapped}).ToList();
  }
 }
}
