using System.Text.Json.Nodes;
using MzukuluQMSMobile.Services;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMSMobile.ViewModels;
public sealed class TemplateBuilderViewModel(InspectionWorkspace workspace) : FieldViewModel
{
 private readonly SemaphoreSlim persistence=new(1,1);private bool hasSavedDraft;
 public JsonNode Draft {get;private set;}=NewDraft();
 private static JsonNode NewDraft()=>new JsonObject{["documentNumber"]="",["documentName"]="",["subtitle"]="",["revision"]="Rev 1",["issueDate"]=DateTime.Today.ToString("yyyy-MM-dd"),["active"]=true,["scope"]="",["acceptanceCriteria"]="",["projectFields"]=new JsonArray(),["sections"]=new JsonArray(),["requiredSignOffTypes"]=new JsonArray()};
 public async Task LoadAsync(){var saved=await (await workspace.StoreAsync()).GetAsync(InspectionWorkspace.LocalId("builderDraft",0));hasSavedDraft=saved is not null;if(saved is not null)Draft=JsonNode.Parse(saved.Json)!;}
 public async Task PersistAsync()
 {
  await persistence.WaitAsync();try{var json=Draft.ToJsonString();await (await workspace.StoreAsync()).CacheAsync(InspectionWorkspace.LocalId("builderDraft",0),null,json);}finally{persistence.Release();}
 }
 public async Task ResetAsync(){Draft=NewDraft();await PersistAsync();}
 public async Task<bool> StartRevisionAsync(long version)=>await RunAsync(async()=>{
  await LoadAsync();
  if(hasSavedDraft&&Draft["publishedTemplateID"] is null)
  {
   if(Draft["expectedVersionID"]?.ToString()==version.ToString())return;
   throw new InvalidOperationException("Another checklist draft is saved on this device. Open Create Checklist Template to finish that draft before starting a revision.");
  }
  var definition=await workspace.TemplateAsync(version);Draft=TemplateRevisionDraft.FromDefinition(definition);await PersistAsync();
 },"Revision draft saved. Choose a new revision label before publishing.");
 public Task<bool> RepairReferencesAsync()=>RunAsync(async()=>{Draft=ChecklistFieldSettings.RepairReferences(Draft);await PersistAsync();},"Draft settings repaired. Review the checklist before saving.");
 public static JsonObject NewField()=>ChecklistItems.NewField();
 public async Task<bool> PublishAsync()=>await RunAsync(async()=>{
  await PersistAsync();var errors=ChecklistDraftValidation.Errors(Draft);if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
  var result=await workspace.CreateTemplateAsync(Draft);Draft["publishedTemplateID"]=result["checklistTemplateID"]!.DeepClone();await PersistAsync();Message=Draft["active"]?.GetValue<bool>()==true?"Template published. It is available in Form Library.":"Template saved as inactive. It will not appear in Form Library.";
 });
}
