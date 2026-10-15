using System.Globalization;
using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;

public sealed class FieldCableRegistersPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly long projectId;private readonly FieldListViewModel model;
 public FieldCableRegistersPage(InspectionWorkspace w,long project):this(w,project,new FieldListViewModel(w)){}
 private FieldCableRegistersPage(InspectionWorkspace w,long project,FieldListViewModel m):base("Cable registers",m){workspace=w;projectId=project;model=m;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();Rows.Clear();Heading("MZT-QC-203","Cable registers");
  if(workspace.Permissions.Contains("PerformInspection"))Rows.Add(FieldUi.Action("+ New cable register",()=>Navigation.PushAsync(new FieldCableEditorPage(workspace,projectId,null,null,null))));
  if(!await model.LoadAsync(()=>workspace.CableRegistersAsync(projectId)))return;
  foreach(var r in model.Data!.AsArray())
  {
   var id=r!["cableRegisterID"]!.GetValue<long>();Rows.Add(FieldUi.Card(FieldUi.Text(r["registerNumber"]!.ToString(),18,true),FieldUi.Text(r["description"]?.ToString()??"Cable installation register"),FieldUi.Text($"{r["total"]} cables · {r["pulledCount"]} pulled · {r["glandedCount"]} glanded · {r["terminatedCount"]} terminated · {r["completeCount"]} complete"),FieldUi.Action("Open register",()=>Navigation.PushAsync(new FieldCableRegisterPage(workspace,projectId,id)),true)));
  }
  if(model.Data.AsArray().Count==0)Rows.Add(FieldUi.Text("No cable registers for this project."));
 }
}
public sealed class FieldCableRegisterPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly long projectId,registerId;private readonly FieldListViewModel model;
 public FieldCableRegisterPage(InspectionWorkspace w,long project,long register):this(w,project,register,new FieldListViewModel(w)){}
 private FieldCableRegisterPage(InspectionWorkspace w,long project,long register,FieldListViewModel m):base("Cable register",m){workspace=w;projectId=project;registerId=register;model=m;}
 protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
 private async Task LoadAsync()
 {
  Rows.Clear();Heading("MZT-QC-203","Cable schedule & installation register");if(!await model.LoadAsync(()=>workspace.CableRegisterAsync(projectId,registerId)))return;
  var r=model.Data!["register"]!;var rows=model.Data["rows"]!.AsArray();
  foreach(var (key,label) in new[]{("registerNumber","QC record / register number"),("registerRevision","Register revision"),("projectName","Project"),("projectNumber","Project number"),("clientName","Client"),("contractOrderNumber","Contract / order"),("areaLocation","Area / location"),("drawingReference","Drawing number & revision"),("scheduleReference","Cable schedule & revision"),("notes","Notes"),("maintainedByName","Register maintained by"),("verifiedByName","Verified by")})Rows.Add(FieldUi.Card(FieldUi.Text(label,11,true),FieldUi.Text(r[key]?.ToString()??"Not recorded")));
  if(r["verifiedAt"] is not null)Rows.Add(FieldUi.Text("Verified "+BusinessText.Date(r["verifiedAt"]!.ToString())));
  Rows.Add(FieldUi.Text($"{rows.Count} cables · {Count("pulled")} pulled · {Count("glanded")} glanded · {Count("terminated")} terminated · {Count("complete")} complete",16,true));
  int Count(string key)=>rows.Count(c=>c?[key]?.GetValue<bool>()==true);
  if(workspace.Permissions.Contains("PerformInspection"))
  {
   Rows.Add(FieldUi.Action("Edit register header",()=>Navigation.PushAsync(new FieldCableEditorPage(workspace,projectId,registerId,r,null)),true));
   Rows.Add(FieldUi.Action("+ Add cable",()=>Navigation.PushAsync(new FieldCableEditorPage(workspace,projectId,registerId,r,new JsonObject())),true));
  }
  foreach(var cable in rows)
  {
   var card=new VerticalStackLayout{Spacing=10};card.Add(FieldUi.Text(cable!["cableNumber"]!.ToString(),18,true));
   foreach(var(key,label) in new[]{("fromLocation","From"),("toLocation","To"),("cableType","Type"),("cableSize","Size"),("cores","Cores"),("drumNumber","Drum"),("length","Length (m)"),("irCertificateNumber","IR certificate"),("continuityCertificateNumber","Continuity certificate")})card.Add(FieldUi.Text(label+": "+(cable[key]?.ToString()??"Not recorded")));
   foreach(var(key,label) in new[]{("pulled","Pulled"),("glanded","Glanded"),("terminated","Terminated"),("complete","Complete")})card.Add(FieldUi.Text(label+": "+(cable[key] is null?"Not recorded":cable[key]!.GetValue<bool>()?"Yes":"No")));
   if(workspace.Permissions.Contains("PerformInspection"))card.Add(FieldUi.Action("Edit cable",()=>Navigation.PushAsync(new FieldCableEditorPage(workspace,projectId,registerId,r,cable)),true));Rows.Add(FieldUi.Card(card));
  }
  if(workspace.Permissions.Contains("ReviewInspection"))Rows.Add(FieldUi.Action("Verify completed register",async()=>{
   if(!await DisplayAlertAsync("Verify register","Confirm independent review of all completed cables and referenced certificates? Future changes require fresh verification.","Verify","Cancel"))return;
   if(await model.RunAsync(async()=>{await workspace.VerifyCableRegisterAsync(projectId,registerId,r["revision"]!.GetValue<long>());},"Register verified."))await LoadAsync();
  }));
 }
}
public sealed class FieldCableEditorPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldViewModel model;private readonly long projectId;private readonly long? registerId;private readonly JsonNode? header,cable;
 private JsonNode draft=new JsonObject();private bool loaded,draftSaveFailed;private readonly bool row;private string DraftKey=>"cable-editor:"+projectId+":"+(registerId?.ToString()??"new")+":"+(row?cable?["cableRecordID"]?.ToString()??"new-row":"header");
 public FieldCableEditorPage(InspectionWorkspace w,long project,long? register,JsonNode? header,JsonNode? cable):this(w,project,register,header,cable,new FieldViewModel()){}
 private FieldCableEditorPage(InspectionWorkspace w,long project,long? register,JsonNode? h,JsonNode? c,FieldViewModel m):base(c is null?"Register details":"Cable details",m){workspace=w;projectId=project;registerId=register;header=h?.DeepClone();cable=c?.DeepClone();row=c is not null;model=m;model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy))Rows.IsEnabled=!model.Busy;};}
 protected override async void OnAppearing()
 {
  base.OnAppearing();if(loaded)return;loaded=true;await model.RunAsync(async()=>{
   draft=await workspace.InputDraftAsync(projectId,DraftKey)??(row?cable:header)?.DeepClone()??new JsonObject();
   if(draft["expectedRevision"] is null)draft["expectedRevision"]=draft["revision"]?.DeepClone()??JsonValue.Create(0L);
   if(draft["registerRevisionGuard"] is null)draft["registerRevisionGuard"]=header?["revision"]?.DeepClone()??JsonValue.Create(0L);
   Render();
  });
 }
 private async Task PersistAsync(){try{await workspace.SaveInputDraftAsync(projectId,DraftKey,draft);draftSaveFailed=false;}catch{draftSaveFailed=true;model.Message="The draft could not be saved. Keep this page open and try again.";}}
 private void Render()
 {
  Rows.Clear();Heading("MZT-QC-203",row?"Cable details":"Register details");Rows.Add(FieldUi.Text("Draft changes are retained on this device. Connect to save. Uploaded evidence and verification history are not deleted."));
  var fields=row?new[]{("cableNumber","Cable number *"),("fromLocation","From"),("toLocation","To"),("cableType","Type"),("cableSize","Size"),("cores","Cores"),("drumNumber","Drum number"),("irCertificateNumber","IR certificate number"),("continuityCertificateNumber","Continuity certificate number")}:new[]{("registerNumber","QC record / register number *"),("description","Description"),("registerRevision","Register revision"),("drawingReference","Drawing number & revision"),("scheduleReference","Cable schedule reference & revision"),("areaLocation","Area / location"),("notes","Notes")};
  foreach(var(key,label) in fields){var input=FieldUi.Entry(label);input.Text=draft[key]?.ToString();input.MaxLength=key is "registerNumber" or "cableNumber"?100:key=="notes"?4000:row?200:500;input.TextChanged+=async(_,_)=>{draft[key]=input.Text;await PersistAsync();};Rows.Add(FieldUi.Card(FieldUi.Text(label,12,true),input));}
  if(row)
  {
   var length=FieldUi.Entry("Length in metres",Keyboard.Numeric);length.Text=draft["lengthInput"]?.ToString()??draft["length"]?.ToString();length.TextChanged+=async(_,_)=>{draft["lengthInput"]=length.Text;await PersistAsync();};Rows.Add(FieldUi.Card(FieldUi.Text("Length (m)",12,true),length));
   foreach(var(key,label) in new[]{("pulled","Pulled"),("glanded","Glanded"),("terminated","Terminated"),("complete","Complete")})
   {
    var selection=new FieldChoice(label,new[]{("unknown","Not recorded"),("yes","Yes"),("no","No")},draft[key] is null?"unknown":draft[key]!.GetValue<bool>()?"yes":"no");selection.SelectionChanged+=async(_,_)=>{draft[key]=selection.SelectedValue=="unknown"?null:JsonValue.Create(selection.SelectedValue=="yes");await PersistAsync();};Rows.Add(FieldUi.Card(FieldUi.Text(label,12,true),selection));
   }
  }
  Rows.Add(FieldUi.Action("Save",async()=>{
   if(await model.RunAsync(async()=>{
    await PersistAsync();if(draftSaveFailed)throw new InvalidOperationException("The draft could not be saved. Keep this page open and try again before sending changes.");
    if(row)
    {
     var text=draft["lengthInput"]?.ToString()??draft["length"]?.ToString();
     if(string.IsNullOrWhiteSpace(text))draft["length"]=null;else if(decimal.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out var value)&&value>=0)draft["length"]=value;else throw new ArgumentException("Enter a valid nonnegative cable length.");
     await workspace.SaveCableAsync(projectId,registerId!.Value,cable?["cableRecordID"]?.GetValue<long>(),draft["registerRevisionGuard"]!.GetValue<long>(),draft);
    }
    else await workspace.SaveCableRegisterAsync(projectId,registerId,draft);
    await workspace.SaveInputDraftAsync(projectId,DraftKey,null);
   },"Saved."))await Navigation.PopAsync();
  }));
 }
}
