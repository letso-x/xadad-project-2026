using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
using MzukuluQMS.Mobile.Core;
using System.Text.Json.Nodes;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldSyncPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldViewModel model;
 public FieldSyncPage(InspectionWorkspace workspace):this(workspace,new FieldViewModel()){}
 private FieldSyncPage(InspectionWorkspace workspace,FieldViewModel model):base("Sync & conflicts",model){this.workspace=workspace;this.model=model;}
 protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
 private async Task LoadAsync()
 {
  await model.RunAsync(async()=>{
   Rows.Clear();Heading("Saved work","Sync & conflicts");Rows.Add(FieldUi.Action("Reconnect & sync",async()=>{await model.RunAsync(async()=>model.Message=await workspace.SyncAsync());await LoadAsync();}));
   var local=await workspace.StoreAsync();var counts=await local.CountsAsync();foreach(var count in counts)Rows.Add(FieldUi.Card(FieldUi.Text(BusinessText.Status(count.Key)+": "+count.Value,18,true)));
   var outbox=await local.OutboxAsync();
   foreach(var entry in outbox.Where(o=>o.Status!="Completed"))
   {
    var card=FieldUi.Card(FieldUi.Text(BusinessText.Action(entry.Operation.Kind),16,true),FieldUi.Badge(entry.Status),FieldUi.Text(entry.LastError=="Authentication required"?"Sign in again to continue syncing.":entry.Status is "Failed" or "Conflict"?ApiFailureMessage.Describe(System.Net.HttpStatusCode.BadRequest,entry.FailureJson):"Your saved work is waiting to sync."));
    var record=await local.GetAsync(entry.Operation.EntityId);JsonNode? form=null;try{form=JsonNode.Parse(record?.Json??"null");}catch(System.Text.Json.JsonException){}
    if(form?["formNumber"] is not null)((VerticalStackLayout)card.Content!).Add(FieldUi.Text(form["formNumber"]!.ToString(),14,true));
    if(form?["qcFormID"] is JsonValue value&&value.TryGetValue<long>(out var id))((VerticalStackLayout)card.Content!).Add(FieldUi.Action("Open QC record",()=>Navigation.PushAsync(new FieldFormPage(workspace,id)),true));
    Rows.Add(card);
   }
   foreach(var entry in outbox.Where(o=>(o.Status is "Failed" or "Conflict") && (o.Operation.Kind is "SubmitQCForm" or "ReturnForCorrection" or "ApproveQCForm" or "CreateSignOff")))
    Rows.Add(FieldUi.Action("Withdraw action: "+BusinessText.Action(entry.Operation.Kind),async()=>{
     var reason=await DisplayPromptAsync("Withdraw unsuccessful action","This withdraws the retry, without undoing an accepted action. Your answers, attachments and history are retained. Explain why you are withdrawing it.",maxLength:2000);
     if(reason is not null)await model.RunAsync(()=>local.WithdrawRejectedWorkflowAsync(entry.Operation.OperationId,reason));await LoadAsync();
    },true));
   foreach(var conflict in await local.ConflictsAsync())Rows.Add(FieldUi.Card(FieldUi.Warning(conflict.Status=="Cancelled"?"This unsuccessful action was withdrawn. Both versions remain in the record history.":"This record changed while you were offline. Review the latest record with an authorised reviewer before continuing."),FieldUi.Badge(conflict.Status)));
   Rows.Add(FieldUi.Text("Both versions are retained for review. Your answers and attachments are never automatically replaced to resolve a conflict."));
   Rows.Add(FieldUi.Text("Signing out keeps your saved work on this device for your account."));
  });
 }
}
public sealed class FieldTemplatesPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;
 public FieldTemplatesPage(InspectionWorkspace workspace):this(workspace,new FieldListViewModel(workspace)){}
 private FieldTemplatesPage(InspectionWorkspace workspace,FieldListViewModel model):base("Checklist templates",model){this.workspace=workspace;this.model=model;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();Rows.Clear();Heading("Administration","Checklist templates");Rows.Add(FieldUi.Action("+ Create Checklist Template",()=>Navigation.PushAsync(new FieldTemplateBuilderPage(workspace))));
  if(await model.LoadAsync(workspace.TemplatesAsync))foreach(var template in model.Data!.AsArray())Rows.Add(FieldUi.Card(FieldUi.Text(template?["templateNumber"]?.ToString()??"",12,true),FieldUi.Text(template?["templateName"]?.ToString()??"",18,true),FieldUi.Text("Revision "+template?["revision"]),FieldUi.Badge(template?["isActive"]?.GetValue<bool>()==true?"Active":"Inactive")));
 }
}
