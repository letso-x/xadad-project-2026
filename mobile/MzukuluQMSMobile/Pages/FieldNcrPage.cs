using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;

public sealed class FieldNcrPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly NcrViewModel model;
 public FieldNcrPage(InspectionWorkspace workspace,long formId,JsonNode ncr):this(workspace,new NcrViewModel(workspace,formId,ncr["ncrID"]?.GetValue<long>()??0,ncr["publicNCRID"]?.GetValue<long>())){}
 private FieldNcrPage(InspectionWorkspace workspace,NcrViewModel model):base("Non-conformance",model)
 {this.workspace=workspace;this.model=model;model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy))Rows.IsEnabled=!model.Busy;};ToolbarItems.Add(new ToolbarItem("Sync",null,async()=>{if(await model.SyncAsync())Render();}));}
 protected override async void OnAppearing(){base.OnAppearing();if(await model.LoadAsync())Render();else{Rows.Clear();Heading("Non-conformance","Case unavailable");Rows.Add(Status);}}
 private void Detail(string label,string? value)=>Rows.Add(FieldUi.Card(FieldUi.Text(label.ToUpperInvariant(),11,true),FieldUi.Text(string.IsNullOrWhiteSpace(value)?"Not recorded":value)));
 private void Render()
 {
  var n=model.Ncr!;Rows.Clear();Heading("Non-conformance",n["ncrNumber"]?.ToString()??"Historical NCR");
  Rows.Add(FieldUi.Badge(NcrInteraction.IsClosed(n)?"Closed":"Open"));Rows.Add(FieldUi.Text(NcrInteraction.Status(n),16,true));Rows.Add(Status);
  Detail("Project",n["projectName"]?.ToString()??model.Form?["documentSnapshot"]?["project"]?.ToString());
  Detail("Project number",n["projectNumber"]?.ToString()??model.Form?["documentSnapshot"]?["projectNumber"]?.ToString());
  Detail("QC record",n["qcFormNumber"]?.ToString()??model.Form?["formNumber"]?.ToString());
  var source=model.Form?["sections"]?.AsArray().SelectMany(s=>s?["fields"]?.AsArray()??[]).FirstOrDefault(f=>f?["qcFormFieldID"] is not null&&JsonNode.DeepEquals(f["qcFormFieldID"],n["qcFormFieldID"]));
  Detail("Inspection requirement",n["sourceFieldLabel"]?.ToString()??source?["fieldLabel"]?.ToString());
  Detail("Raised by",n["createdByName"]?.ToString());Detail("Raised",BusinessText.Date(n["createdAt"]?.ToString()));
  Detail("Severity",n["severity"]?.ToString());Detail("Classification",n["classification"]?.ToString());
  Detail("Approval impact",n["isBlocking"] is null?"Unclassified — approval remains blocked":n["isBlocking"]!.GetValue<bool>()?"Blocks approval until resolved":"Does not block approval");
  Detail("Disposition",n["disposition"]?.ToString());Detail("Corrective action",n["correctiveAction"]?.ToString());
  Detail("Corrective evidence",n["correctiveEvidenceFileName"]?.ToString());
  if(n["correctiveEvidenceID"]?.GetValue<long>() is >0)Rows.Add(FieldUi.Action("View corrective evidence",()=>model.RunAsync(async()=>await Navigation.PushAsync(new FieldEvidencePreviewPage(await model.PreviewAsync(n["correctiveEvidenceID"]!.GetValue<long>())))),true));
  Detail("Corrected by",n["correctedByName"]?.ToString());Detail("Correction date",n["correctedAt"] is null?"Not recorded":BusinessText.Date(n["correctedAt"]!.ToString()));
  Detail("Verifier",n["verifierName"]?.ToString());Detail("Verification",n["verificationPassed"] is null?"Not yet verified":n["verificationPassed"]!.GetValue<bool>()?"Passed — closed":"Failed — remains open");
  Detail("Verification comments",n["verificationComments"]?.ToString());Detail("Verification date",n["verifiedAt"] is null?"Not yet verified":BusinessText.Date(n["verifiedAt"]!.ToString()));
  if(NcrInteraction.IsHistorical(n))Rows.Add(FieldUi.Warning("This historical case is retained for traceability. Ask an authorised reviewer to reconcile it before making changes."));
  else if(!NcrInteraction.CanChangeForForm(model.Form?["status"]?.ToString()))Rows.Add(FieldUi.Warning("This QC record is protected. NCR details and evidence remain available for review."));
  else if(!NcrInteraction.IsClosed(n))
  {
   if(model.Can("CorrectNcr"))AddCorrection();
   if(model.Can("VerifyNcr"))AddVerification();
  }
  Rows.Add(FieldUi.Action("Sync & inspect conflicts",()=>Navigation.PushAsync(new FieldSyncPage(workspace)),true));
 }
 private void AddCorrection()
 {
  Rows.Add(FieldUi.Section("Corrective action"));Rows.Add(FieldUi.Text("Draft changes are saved on this device. The original failed inspection and previous corrections remain in history."));
  AddText("severity","Severity");AddText("classification","Classification");AddText("disposition","Disposition");AddText("correctiveAction","Corrective action",true);
  var blocking=new FieldChoice("Approval impact",new[]{("true","Blocks approval"),("false","Does not block approval")},model.Correction["isBlocking"]?.GetValue<bool>() is {} value?value?"true":"false":null);
  blocking.SelectionChanged+=async(_,_)=>{model.Correction["isBlocking"]=blocking.SelectedValue=="true";await model.RetainDraftAsync();};Rows.Add(blocking);
  var options=model.Evidence.Where(e=>e?["evidenceType"]?.ToString()=="Photo").Select(e=>(e!["evidenceID"]!.ToString(),e["fileName"]?.ToString()??"Uploaded photo")).ToArray();
  var evidence=new FieldChoice("Choose uploaded corrective photo",options,model.Correction["correctiveEvidenceID"]?.ToString());
  evidence.SelectionChanged+=async(_,_)=>{if(long.TryParse(evidence.SelectedValue,out var id))model.Correction["correctiveEvidenceID"]=id;await model.RetainDraftAsync();};Rows.Add(evidence);
  if(options.Length==0)Rows.Add(FieldUi.Warning("Upload corrective photo evidence for this QC record before saving the corrective action."));
  Rows.Add(FieldUi.Action("Preview selected evidence",async()=>{
   if(model.Correction["correctiveEvidenceID"]?.GetValue<long>() is not >0){model.Message="Choose an uploaded photo first.";return;}
   await model.RunAsync(async()=>await Navigation.PushAsync(new FieldEvidencePreviewPage(await model.PreviewAsync(model.Correction["correctiveEvidenceID"]!.GetValue<long>()))));
  },true));
  if(model.Can("PerformInspection")&&FormInput.Editable(model.Form!["status"]!.ToString()))
  {
   Rows.Add(FieldUi.Action("Camera — corrective photo",async()=>{if(await model.CaptureEvidenceAsync(true))Render();},true));
   Rows.Add(FieldUi.Action("Choose corrective photo",async()=>{if(await model.CaptureEvidenceAsync(false))Render();},true));
  }
  else Rows.Add(FieldUi.Text("New photos can be added only while the QC record is editable. Use existing uploaded evidence, or ask the reviewer to return the record for correction."));
  Rows.Add(FieldUi.Action("Save corrective action",async()=>{if(await model.CorrectAsync())Render();}));
 }
 private void AddText(string key,string label,bool multiline=false)
 {
  Rows.Add(FieldUi.Text(label,12,true));
  if(multiline){var input=new Editor{Text=model.Correction[key]?.ToString()??"",AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=100,MaxLength=4000};input.TextChanged+=async(_,_)=>{model.Correction[key]=input.Text??"";await model.RetainDraftAsync();};Rows.Add(input);}
  else{var input=FieldUi.Entry(label);input.Text=model.Correction[key]?.ToString()??"";input.MaxLength=4000;input.TextChanged+=async(_,_)=>{model.Correction[key]=input.Text??"";await model.RetainDraftAsync();};Rows.Add(input);}
 }
 private void AddVerification()
 {
  Rows.Add(FieldUi.Section("Independent verification"));
  var problem=NcrInteraction.VerificationProblem(model.Ncr!,workspace.CurrentActor??Guid.Empty);
  if(problem is not null){Rows.Add(FieldUi.Warning(problem));return;}
  Rows.Add(FieldUi.Text("Review the saved corrective action and evidence before making a decision. Failed verification keeps this NCR open."));
  var decision=new FieldChoice("Verification decision",new[]{("pass","Pass — close NCR"),("fail","Fail — keep NCR open")});Rows.Add(decision);
  var comments=new Editor{Placeholder="Verification comments (required)",MaxLength=2000,AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=100};Rows.Add(comments);
  Rows.Add(FieldUi.Action("Verify corrective action",async()=>{
   if(decision.SelectedValue is null){model.Message="Choose Pass or Fail before verification.";return;}
   if(!await DisplayAlertAsync("Verify corrective action",decision.SelectedValue=="pass"?"Confirm independent verification and close this NCR?":"Record failed verification and leave this NCR open?","Confirm","Cancel"))return;
   if(await model.VerifyAsync(decision.SelectedValue=="pass",comments.Text??""))Render();
  }));
 }
}

public sealed class FieldEvidencePreviewPage:ContentPage
{
 public FieldEvidencePreviewPage(byte[] bytes)
 {Title="Evidence photo";BackgroundColor=FieldUi.Background;Content=new ScrollView{Content=new Image{Source=ImageSource.FromStream(()=>new MemoryStream(bytes)),Aspect=Aspect.AspectFit,MinimumHeightRequest=300}};}
}
