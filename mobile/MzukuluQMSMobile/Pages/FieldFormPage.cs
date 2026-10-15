using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Maui.Graphics;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldFormPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldFormViewModel model;private readonly long formId;
 private readonly LocalEditQueue edits=new();
 private readonly List<InputState> inputs=[];
 private readonly Dictionary<string,JsonNode?> drafts=new();
 private Func<Task>? saveRemarks;
 private bool remarksSaveFailed;
 private sealed class InputState
 {
  public required JsonNode Field;public bool Project;public required string Key;
  public Func<JsonNode?>? GetValue;public Func<string?>? RawText;public VisualElement? FocusTarget;
  public required Border Border;public required Label Error;public bool ShowErrors;public bool SaveFailed;
 }
 private static string InputKey(JsonNode field,bool project)=>(project?"project:":"answer:")+field[project?"qcFormProjectFieldID":"qcResponseID"];
 public FieldFormPage(InspectionWorkspace workspace,long formId):this(workspace,formId,new FieldFormViewModel(workspace,formId)){}
 private FieldFormPage(InspectionWorkspace workspace,long formId,FieldFormViewModel model):base("QC record",model)
 {
  this.workspace=workspace;this.formId=formId;this.model=model;
  ToolbarItems.Add(new ToolbarItem("Sync",null,async()=>{await FlushAsync();await model.SyncAsync();await LoadAsync();}));
 }
 protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
 private async Task LoadAsync(){await FlushAsync();if(inputs.Any(i=>i.SaveFailed)||remarksSaveFailed)return;if(await model.LoadAsync())await model.RunAsync(RenderAsync);else if(Rows.Count==0){Heading("QC record","Inspection unavailable");}}
 private async Task RenderAsync()
 {
  inputs.Clear();drafts.Clear();saveRemarks=null;var form=model.Form!;
  if(model.Editable)
  {
   foreach(var field in form["projectFields"]?.AsArray()??[])if(field is not null)drafts[InputKey(field,true)]=await workspace.InputDraftAsync(formId,InputKey(field,true));
   foreach(var section in form["sections"]?.AsArray()??[])foreach(var field in section?["fields"]?.AsArray()??[])if(field is not null)drafts[InputKey(field,false)]=await workspace.InputDraftAsync(formId,InputKey(field,false));
  }
  Rows.Clear();var snapshot=form["documentSnapshot"];Heading("Quality record",snapshot?["checklistName"]?.ToString()??form["formNumber"]!.ToString());
  Rows.Add(FieldUi.Card(FieldUi.Text((snapshot?["documentNumber"]?.ToString()??"Document information unavailable")+" · "+(snapshot?["revision"]?.ToString()??"Revision unavailable"),12,true),FieldUi.Text(form["formNumber"]!.ToString(),20,true),FieldUi.Text(snapshot?["project"]?.ToString()??"Project unavailable"),FieldUi.Badge(form["status"]!.ToString())));
  if(snapshot is null||snapshot["metadataUnavailable"]?.GetValue<bool>()==true)Rows.Add(FieldUi.Warning("This historical record has no captured document header. Current template labels will not be substituted."));
  if(form["status"]!.ToString()=="ReturnedForCorrection")
  {
   var returned=model.Audit.OrderBy(a=>a?["occurredAt"]?.ToString()).LastOrDefault(a=>a?["action"]?.ToString()=="QCFormReturnedForCorrection");string? reason=null;
   try{reason=JsonNode.Parse(returned?["detailsJson"]?.ToString()??"{}")?["reason"]?.ToString();}catch(System.Text.Json.JsonException){}
   Rows.Add(FieldUi.Warning("RETURNED FOR CORRECTION\n"+(reason??"Reason unavailable. Open audit history or contact the reviewer.")+"\nCorrect, resubmit and provide a fresh signature. Historical signatures remain retained."));
  }
  if(!model.Editable)Rows.Add(FieldUi.Text("This record is read-only. Editing depends on its status and your permissions."));
  Rows.Add(FieldUi.Section("Project information"));
  if(snapshot is not null)foreach(var (key,label) in new[]{("client","Client"),("project","Project"),("projectNumber","Project number"),("contractOrderNumber","Contract / order number"),("siteLocation","Area / location")})Rows.Add(FieldUi.Card(FieldUi.Text(label.ToUpperInvariant(),11,true),FieldUi.Text(snapshot[key]?.ToString()??"Not provided")));
  foreach(var field in form["projectFields"]?.AsArray()??[])if(field is not null)AddField(field,true);
  Rows.Add(FieldUi.Section("Scope & acceptance criteria"));Rows.Add(FieldUi.Card(FieldUi.Text(snapshot?["scope"]?.ToString()??"Scope not recorded."),FieldUi.Text(snapshot?["acceptanceCriteria"]?.ToString()??"Acceptance criteria not recorded.")));
  var itemNumber=0;foreach(var section in form["sections"]?.AsArray()??[])
  {
   Rows.Add(FieldUi.Section(section?["sectionName"]?.ToString()??"Inspection / test requirements"));
   if(section?["description"] is not null)Rows.Add(FieldUi.Text(section["description"]!.ToString()));
   foreach(var group in ChecklistItems.Groups(section?["fields"]?.AsArray()??new JsonArray()))
   {
    if(!group.IsInspectionItem){AddField(group.Primary,false);continue;}
    var item=new VerticalStackLayout{Spacing=12};item.Add(FieldUi.Text((++itemNumber)+". "+group.Primary["fieldLabel"],16,true));
    AddField(group.Primary,false,item,true);foreach(var additional in group.Fields.Skip(1))AddField(additional,false,item);
    Rows.Add(FieldUi.Card(item));
   }
  }
  if(form["remarks"] is not null||model.Editable)
  {
   var draft=model.Editable?await workspace.InputDraftAsync(formId,"remarks"):null;
   Rows.Add(FieldUi.Section("Remarks / observations / deviations"));var notes=new Editor{TextColor=FieldUi.Navy,BackgroundColor=Colors.White,PlaceholderColor=FieldUi.Muted,Text=draft?["text"]?.ToString()??form["remarks"]?.ToString()??"",AutoSize=EditorAutoSizeOption.TextChanges,IsReadOnly=!model.Editable,MinimumHeightRequest=100};Rows.Add(FieldUi.Card(notes));
   if(model.Editable)
   {
    async Task SaveNotes(bool commit)
    {
     var text=notes.Text??"";
     try{await edits.Enqueue(async()=>{await workspace.SaveInputDraftAsync(formId,"remarks",new JsonObject{["text"]=text});if(commit){if(form["remarks"]?.ToString()!=text)await workspace.SaveRemarksAsync(form,text);await workspace.SaveInputDraftAsync(formId,"remarks",null);}});remarksSaveFailed=false;}
     catch{remarksSaveFailed=true;model.Message="Remarks could not be saved. Keep this record open and try again.";}
    }
    notes.TextChanged+=async(_,_)=>await SaveNotes(false);notes.Unfocused+=async(_,_)=>await SaveNotes(true);saveRemarks=()=>SaveNotes(true);
   }
  }
  var failed=form["sections"]?.AsArray().SelectMany(s=>s?["fields"]?.AsArray()??[]).Any(f=>f is not null&&f["fieldType"]?.ToString()=="Dropdown"&&ChecklistItems.IsResultOptions(Options(f))&&f["value"]?.ToString() is "Fail" or "R" or "Rejected")==true;
  if(failed||model.Ncrs.Count>0)
  {
   Rows.Add(FieldUi.Section("Non-conformance"));Rows.Add(FieldUi.Warning("Rejected inspection items require attention. Sync to see linked NCRs. Accepting an item later does not close an existing NCR."));
   if(model.Ncrs.Count==0)Rows.Add(FieldUi.Text("No linked NCR loaded yet. Check pending sync and NCR access."));
   foreach(var ncr in model.Ncrs)
   {
    var source=form["sections"]?.AsArray().SelectMany(s=>s?["fields"]?.AsArray()??[]).FirstOrDefault(f=>f?["qcFormFieldID"] is not null&&JsonNode.DeepEquals(f["qcFormFieldID"],ncr?["qcFormFieldID"]));
    Rows.Add(FieldUi.Card(FieldUi.Text(ncr?["ncrNumber"]?.ToString()??"NCR number unavailable",16,true),FieldUi.Text(ncr?["classification"]?.ToString()??"Unclassified"),FieldUi.Badge(ncr?["closedAt"] is null?"Open":"Closed"),FieldUi.Text(source?["fieldLabel"]?.ToString()??"Historical inspection requirement unavailable"),
     FieldUi.Action("Open NCR",async()=>{await FlushAsync();await Navigation.PushAsync(new FieldNcrPage(workspace,formId,ncr!));},true)));
   }
  }
  Rows.Add(FieldUi.Section("Sign-off history"));Rows.Add(FieldUi.Text("Required signing capacities: "+string.Join(", ",form["requiredSignOffTypes"]?.AsArray().Select(t=>t!.ToString())??[])));
  if(model.SignOffs.Count==0)Rows.Add(FieldUi.Text("No signatures recorded. Sign after submitting this record."));
  foreach(var sign in model.SignOffs)Rows.Add(FieldUi.Card(FieldUi.Text(BusinessText.SignOff(sign?["signOffType"]?.ToString()),16,true),FieldUi.Text(sign?["signerName"]?.ToString()??"Signer name unavailable"),FieldUi.Text(sign?["signedRequirement"]?.ToString()??"Record sign-off"),FieldUi.Text(sign?["signOffType"]?.ToString()??"Signing capacity unavailable"),FieldUi.Text("Signed "+BusinessText.Date(sign?["signedAt"]?.ToString())),FieldUi.Badge(sign?["matchesCurrentContentAndSubmission"]?.GetValue<bool>()==true?"Current signature":"Historical signature")));
  if(form["status"]?.ToString()=="Approved")
  {
   var approved=model.Audit.OrderBy(a=>a?["occurredAt"]?.ToString()).LastOrDefault(a=>a?["action"]?.ToString()=="QCFormApproved");
   Rows.Add(FieldUi.Card(FieldUi.Text("Approved By",16,true),FieldUi.Text(approved?["actorName"]?.ToString()??"Authorised approver"),FieldUi.Badge("Approved"),FieldUi.Text(BusinessText.Date(approved?["occurredAt"]?.ToString()))));
  }
  if(form["status"]!.ToString()=="Submitted"&&model.Can("SignInspection"))Rows.Add(FieldUi.Action("Draw fresh signature",()=>Navigation.PushAsync(new FieldSignaturePage(workspace,model,null))));
  if(model.Editable&&model.Can("SubmitInspection"))Rows.Add(FieldUi.Action(form["status"]!.ToString()=="ReturnedForCorrection"?"Resubmit corrected record":"Submit QC record",SubmitAsync));
  if(form["status"]!.ToString()=="Submitted")
  {
   if(model.Can("ReviewInspection"))Rows.Add(FieldUi.Action("Return for correction",async()=>{var reason=await DisplayPromptAsync("Return for correction","Explain the correction required",maxLength:2000);if(reason is not null){await model.ReviewAsync(false,reason);await LoadAsync();}},true));
   if(model.Can("ApproveInspection"))Rows.Add(FieldUi.Action("Approve record",async()=>{if(await DisplayAlertAsync("Approve record","Approval requires current authorised signatures and resolved blocking NCRs.","Approve","Cancel")){await model.ReviewAsync(true,null);await LoadAsync();}}));
  }
  Rows.Add(FieldUi.Section("Audit trail"));Rows.Add(FieldUi.Text(model.HistoryMessage));
  foreach(var item in model.Audit)Rows.Add(FieldUi.Card(FieldUi.Text(BusinessText.Action(item?["action"]?.ToString()),14,true),FieldUi.Text(BusinessText.Date(item?["occurredAt"]?.ToString())),FieldUi.Text(item?["actorName"]?.ToString()??"Authorised user")));
  Rows.Add(FieldUi.Action("Sync & inspect conflicts",()=>Navigation.PushAsync(new FieldSyncPage(workspace)),true));
  await Task.CompletedTask;
 }
 private void AddField(JsonNode field,bool projectField,VerticalStackLayout? host=null,bool inspectionResult=false)
 {
  try{AddFieldControls(field,projectField,host,inspectionResult);}catch(Exception error) when(error is InvalidOperationException or FormatException or ArgumentException or OverflowException)
  {(host??Rows).Add(FieldUi.Card(FieldUi.Text(field["fieldLabel"]?.ToString()??"Field",16,true),FieldUi.Warning("Stored value or metadata has an unsupported format. It remains retained; request authorised review.")));}
 }
 private void AddFieldControls(JsonNode field,bool projectField,VerticalStackLayout? host,bool inspectionResult)
 {
  var type=field["fieldType"]!.ToString();var required=field["isRequired"]?.GetValue<bool>()==true;var controls=new VerticalStackLayout{Spacing=10};
  var key=InputKey(field,projectField);drafts.TryGetValue(key,out var draft);
  var initial=draft is JsonObject obj&&obj.ContainsKey("value")?draft["value"]:field["value"];
  InputState? state=null;Func<string?>? rawText=null;VisualElement? focus=null;
  async Task Changed(bool commit=true){if(state is not null&&model.Editable)await PersistAsync(state,commit);}
  var label=inspectionResult?"RESULT":field["fieldLabel"]?.ToString()??"Field";var unit=ChecklistItems.Unit(field);
  controls.Add(FieldUi.Text(label+(!string.IsNullOrWhiteSpace(unit)?" ("+unit+")":"")+(required?" *":""),16,true));if(field["helpText"] is not null)controls.Add(FieldUi.Text(field["helpText"]!.ToString()));
  var error=FieldUi.Text("");error.TextColor=FieldUi.Accent;Func<JsonNode?>? value=null;
  if(type is "Boolean" or "YesNo")
  {
   var picker=new FieldChoice("Choose answer",new[]{"Unanswered","Yes","No"},initial is null?"Unanswered":(type=="YesNo"?string.Equals(initial.ToString(),"Yes",StringComparison.OrdinalIgnoreCase):initial.GetValue<bool>())?"Yes":"No"){IsEnabled=model.Editable};controls.Add(picker);value=()=>picker.SelectedValue is null or "Unanswered"?null:type=="YesNo"?JsonValue.Create(picker.SelectedValue):JsonValue.Create(picker.SelectedValue=="Yes");focus=picker;picker.SelectionChanged+=async(_,_)=>await Changed();
  }
  else if(type=="Dropdown")
  {
   var options=Options(field);var selected=initial?.ToString();
   if(ChecklistItems.IsResultOptions(options))
   {
    var attention=FieldUi.Text("",12,true);attention.TextColor=FieldUi.Accent;
    void UpdateAttention()
    {
     var linked=model.Ncrs.FirstOrDefault(n=>n?["qcFormFieldID"] is not null&&JsonNode.DeepEquals(n["qcFormFieldID"],field["qcFormFieldID"]));
     var rejected=selected is "R" or "Fail" or "Rejected";var open=linked is not null&&linked["closedAt"] is null;
     attention.IsVisible=rejected||open;
     attention.Text=(rejected?"Requires attention":"Open NCR")+(linked?["ncrNumber"] is not null?" · "+linked["ncrNumber"]:rejected?" · Sync to check linked NCR":"");
    }
    var grid=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)},ColumnSpacing=6};
    var buttons=new List<Button>();var index=0;
    foreach(var option in options){var b=FieldUi.Action(option is "Pass" or "Accepted"?"A":option is "Fail" or "Rejected"?"R":option,async()=>{selected=option;foreach(var button in buttons)button.BackgroundColor=button.ClassId==selected?(selected is "A" or "Pass" or "Accepted"?Color.FromArgb("#176247"):selected=="N/A"?FieldUi.Muted:FieldUi.Accent):FieldUi.Navy;UpdateAttention();await Changed();});b.ClassId=option;b.IsEnabled=model.Editable;b.BackgroundColor=selected==option?(selected is "A" or "Pass" or "Accepted"?Color.FromArgb("#176247"):selected=="N/A"?FieldUi.Muted:FieldUi.Accent):FieldUi.Navy;SemanticProperties.SetDescription(b,option is "A" or "Pass" or "Accepted"?"Accepted":option is "R" or "Fail" or "Rejected"?"Rejected — requires attention":"Not applicable");buttons.Add(b);grid.Add(b,index++,0);}controls.Add(grid);UpdateAttention();controls.Add(attention);value=()=>selected is null?null:JsonValue.Create(selected);focus=buttons.FirstOrDefault();
   }
   else{var picker=new FieldChoice("Choose answer",options,selected){IsEnabled=model.Editable};controls.Add(picker);value=()=>picker.SelectedValue is string answer?JsonValue.Create(answer):null;focus=picker;picker.SelectionChanged+=async(_,_)=>await Changed();}
  }
  else if(type=="MultiSelect")
  {
   var selected=initial?.AsArray().Select(n=>n!.ToString()).ToHashSet()??new();
   foreach(var option in Options(field)){var check=new CheckBox{IsChecked=selected.Contains(option),IsEnabled=model.Editable,MinimumHeightRequest=48};check.CheckedChanged+=async(_,e)=>{if(e.Value)selected.Add(option);else selected.Remove(option);await Changed();};controls.Add(new HorizontalStackLayout{Spacing=8,Children={check,FieldUi.Text(option)}});focus??=check;}value=()=>new JsonArray(Options(field).Where(selected.Contains).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
  }
  else if(type is "Date" or "DateTime")
  {
   var answered=new Switch{IsToggled=initial is not null,IsEnabled=model.Editable};var date=new DatePicker{Date=DateTime.TryParse(initial?.ToString(),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var parsed)?parsed.Date:DateTime.Today,IsEnabled=model.Editable,MinimumHeightRequest=48};var time=new TimePicker{Time=parsed.TimeOfDay,IsEnabled=model.Editable,MinimumHeightRequest=48};
   controls.Add(new HorizontalStackLayout{Children={answered,FieldUi.Text("Answered")}});controls.Add(date);if(type=="DateTime")controls.Add(time);
   value=()=>!answered.IsToggled?null:JsonValue.Create(type=="Date"?Convert.ToDateTime(date.Date,CultureInfo.InvariantCulture).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):new DateTimeOffset(Convert.ToDateTime(date.Date,CultureInfo.InvariantCulture).Date.Add((TimeSpan?)time.Time??TimeSpan.Zero)).ToString("O"));
   focus=date;answered.Toggled+=async(_,_)=>await Changed();date.DateSelected+=async(_,_)=>await Changed();time.PropertyChanged+=async(_,e)=>{if(e.PropertyName==nameof(TimePicker.Time))await Changed();};
  }
  else if(type is "Text" or "LongText" or "Number" or "Decimal" or "Reference")
  {
   if(type=="LongText"){var input=new Editor{TextColor=FieldUi.Navy,BackgroundColor=Colors.White,PlaceholderColor=FieldUi.Muted,Text=draft?["text"]?.ToString()??initial?.ToString(),Placeholder=field["placeholder"]?.ToString(),AutoSize=EditorAutoSizeOption.TextChanges,IsReadOnly=!model.Editable,MinimumHeightRequest=96};controls.Add(input);value=()=>FormInput.Parse(type,input.Text);rawText=()=>input.Text;focus=input;input.TextChanged+=async(_,_)=>await Changed(false);input.Unfocused+=async(_,_)=>await Changed();}
   else{var input=FieldUi.Entry(field["placeholder"]?.ToString()??type,type is "Number" or "Decimal"?Keyboard.Numeric:Keyboard.Default);input.Text=draft?["text"]?.ToString()??(initial is JsonValue v&&v.TryGetValue<decimal>(out var number)&&type is "Number" or "Decimal"?number.ToString(CultureInfo.CurrentCulture):initial?.ToString());input.IsReadOnly=!model.Editable;controls.Add(input);value=()=>FormInput.Parse(type,input.Text);rawText=()=>input.Text;focus=input;input.TextChanged+=async(_,_)=>await Changed(false);input.Unfocused+=async(_,_)=>await Changed();}
  }
  else if(type is not ("Photo" or "Signature")){controls.Add(FieldUi.Warning("This answer format is not supported. Ask an administrator to review it. Saved answers are retained."));}
  if(!projectField&&(type=="Photo"||field["requiresPhoto"]?.GetValue<bool>()==true))
  {
   var id=field["qcFormFieldID"]!.GetValue<long>();controls.Add(FieldUi.Text("Photo evidence"+(required||field["requiresPhoto"]?.GetValue<bool>()==true?" *":""),12,true));
   foreach(var evidence in model.Evidence.Where(e=>e?["qcFormFieldID"]?.GetValue<long>()==id&&e["evidenceType"]?.ToString()=="Photo"))
   {
    var evidenceId=evidence!["evidenceID"]!.GetValue<long>();
    controls.Add(FieldUi.Action("View photo · "+evidence["fileName"],async()=>{await model.RunAsync(async()=>{var bytes=await model.UploadedEvidenceAsync(evidenceId);await Navigation.PushAsync(new FieldEvidencePreviewPage(bytes));});},true));
   }
   if(model.Editable){controls.Add(FieldUi.Action("Camera",()=>CaptureAsync(id,true,controls),true));controls.Add(FieldUi.Action("Choose from gallery",()=>CaptureAsync(id,false,controls),true));}
   _=AddLocalPreviewsAsync(id,controls);
  }
  if(!projectField&&(type=="Signature"||field["requiresSignature"]?.GetValue<bool>()==true))
  {
   controls.Add(FieldUi.Text("Sign this requirement after submitting the record. Corrected content requires fresh consent."));
   if(model.Form?["status"]?.ToString()=="Submitted"&&model.Can("SignInspection"))
   {
    var signatureField=field["qcFormFieldID"]!.GetValue<long>();
    controls.Add(FieldUi.Action("Sign this requirement",async()=>{await FlushAsync();await Navigation.PushAsync(new FieldSignaturePage(workspace,model,signatureField));},true));
   }
   if(model.Form!["status"]!.ToString()=="Submitted"&&model.Can("SignInspection")){var id=field["qcFormFieldID"]!.GetValue<long>();controls.Add(FieldUi.Action("Sign this field",()=>Navigation.PushAsync(new FieldSignaturePage(workspace,model,id))));}
  }
  if(!projectField)
  {
   var actor=field["answeredByName"]?.ToString();var at=field["answeredAt"]?.ToString();
   if(at is not null)controls.Add(FieldUi.Text("Last synced answer · "+(actor??"Name unavailable")+" · "+BusinessText.Date(at),11));
  }
  controls.Add(error);var border=FieldUi.Card(controls);
  state=new InputState{Field=field,Project=projectField,Key=key,GetValue=value,RawText=rawText,FocusTarget=focus,Border=border,Error=error};inputs.Add(state);
  if(host is null)Rows.Add(border);else host.Add(border);
 }
 private static List<string> Options(JsonNode field)=>field["options"] is JsonArray array?array.Select(o=>o is JsonValue?o.ToString():o?["value"]?.ToString()??"").ToList():[];
 private static string? ValueError(InputState input,out JsonNode? value)
 {
  value=null;if(input.GetValue is null)return null;
  try{value=input.GetValue();var error=FormInput.ValidationError(input.Field,value);return error is not null&&FormInput.Missing(value)?input.Field["fieldType"]?.ToString()=="Dropdown"&&ChecklistItems.IsResultOptions(Options(input.Field))?"Select a result.":"Required field":error;}
  catch(FormatException){return "Check the number or date format.";}
  catch(OverflowException){return "This number is too large.";}
  catch(ArgumentException e){return e.Message;}
 }
 private static void ShowError(InputState input,string? error)
 {
  input.Error.Text=error??"";input.Error.IsVisible=error is not null;
  input.Border.Stroke=error is null?FieldUi.Line:FieldUi.Accent;
  input.Border.StrokeThickness=error is null?1:2;
  SemanticProperties.SetDescription(input.Error,error??"");
 }
 private async Task PersistAsync(InputState input,bool commit)
 {
  var problem=ValueError(input,out var value);var captured=value?.DeepClone();
  var draft=new JsonObject{["value"]=captured?.DeepClone()};
  if(input.RawText is not null)draft["text"]=input.RawText();
  if(input.ShowErrors)ShowError(input,problem);
  var form=model.Form!;
  try
  {
   await edits.Enqueue(async()=>{
    await workspace.SaveInputDraftAsync(formId,input.Key,draft);
    if(commit&&problem is null){await workspace.SaveValueAsync(form,input.Field,captured,input.Project);await workspace.SaveInputDraftAsync(formId,input.Key,null);}
   });input.SaveFailed=false;
  }
  catch{input.SaveFailed=true;input.ShowErrors=true;ShowError(input,"Could not save this answer. Keep this record open and try again.");model.Message="Some changes could not be saved. Please try again before leaving this record.";}
 }
 private async Task FlushAsync()
 {
  await edits.DrainAsync();
  if(!model.Editable)return;
  if(await (await workspace.StoreAsync()).HasPendingTransitionAsync(InspectionWorkspace.LocalId("form",formId)))return;
  foreach(var input in inputs)if(input.GetValue is not null)await PersistAsync(input,true);
  if(saveRemarks is not null)await saveRemarks();
  await edits.DrainAsync();
 }
 private async Task<HashSet<long>> PhotoFieldsAsync()
 {
  var photos=model.Evidence.Where(e=>e?["evidenceType"]?.ToString()=="Photo"&&e["qcFormFieldID"] is not null).Select(e=>e!["qcFormFieldID"]!.GetValue<long>()).ToHashSet();
  foreach(var item in await model.LocalEvidenceAsync())if(item.Type=="Photo"&&item.Field.HasValue&&item.Status!="Cancelled")photos.Add(item.Field.Value);
  return photos;
 }
 private string? InputError(InputState input,IReadOnlySet<long> photos)
 {
  if(input.SaveFailed)return "Could not save this answer. Please try again.";
  var problem=ValueError(input,out _);
  if(problem is not null)return problem;
  if(!input.Project&&(input.Field["requiresPhoto"]?.GetValue<bool>()==true||input.Field["fieldType"]?.ToString()=="Photo"&&input.Field["isRequired"]?.GetValue<bool>()==true)&&!photos.Contains(input.Field["qcFormFieldID"]!.GetValue<long>()))return "A photo is required.";
  return null;
 }
 private async Task SubmitAsync()
 {
  await FlushAsync();var photos=await PhotoFieldsAsync();InputState? first=null;var count=0;
  if(remarksSaveFailed){model.Message="Remarks could not be saved. Please try again before submitting.";return;}
  foreach(var input in inputs){input.ShowErrors=true;var error=InputError(input,photos);ShowError(input,error);if(error is not null){first??=input;count++;}}
  if(first is not null){model.Message=$"{count} field{(count==1?"":"s")} need attention. Complete the highlighted fields.";first.FocusTarget?.Focus();await Scroller.ScrollToAsync(first.Border,ScrollToPosition.Start,true);return;}
  // Failed submissions leave the rendered controls and entered values intact.
  if(await model.SubmitAsync())await LoadAsync();
 }
 private async Task CaptureAsync(long fieldId,bool camera,VerticalStackLayout controls)
 {
  await FlushAsync();var id=await model.CaptureAsync(fieldId,camera);if(id.HasValue)await LoadAsync();
 }
 private async Task AddLocalPreviewsAsync(long fieldId,VerticalStackLayout controls)
 {
  try{foreach(var item in (await model.LocalEvidenceAsync()).Where(e=>e.Field==fieldId&&e.Type=="Photo"&&e.Status is not ("Cancelled" or "Completed"))){var bytes=await model.EvidenceBytesAsync(item.Id);controls.Add(FieldUi.Text(BusinessText.PhotoStatus(item.Status)));controls.Add(new Image{Source=ImageSource.FromStream(()=>new MemoryStream(bytes)),Aspect=Aspect.AspectFit,HeightRequest=180});
   if(model.Editable&&item.Status=="Pending")controls.Add(FieldUi.Action("Remove unsent photo",async()=>{await FlushAsync();if(await DisplayAlertAsync("Remove unsent photo","Remove this photo from the upload queue? Photos already sent to the server cannot be removed.","Remove","Keep")){if(await model.RemoveUnsentPhotoAsync(item.Id))await LoadAsync();}},true));
  }}catch{model.Message="Some photo previews are unavailable. Saved attachments are retained.";}
 }
}
public sealed class FieldSignaturePage : FieldScreen
{
 private sealed class InkDrawing(SignatureInk ink) : IDrawable
 {
  public void Draw(ICanvas canvas,RectF bounds){canvas.FillColor=Colors.White;canvas.FillRectangle(bounds);canvas.StrokeColor=FieldUi.Navy;canvas.StrokeSize=3;foreach(var stroke in ink.Strokes)for(var i=1;i<stroke.Count;i++)canvas.DrawLine(stroke[i-1].X*bounds.Width,stroke[i-1].Y*bounds.Height,stroke[i].X*bounds.Width,stroke[i].Y*bounds.Height);}
 }
 public FieldSignaturePage(InspectionWorkspace workspace,FieldFormViewModel form,long? fieldId):base("Fresh signoff",new FieldViewModel())
 {
  var model=(FieldViewModel)BindingContext;var ink=new SignatureInk();Heading("Electronic signature","Draw your signature");
  Rows.Add(FieldUi.Warning("I intend to sign the current submitted record under my authenticated account. An old signature will not be reused for corrected content."));
  if(fieldId.HasValue){var requirement=form.Form?["sections"]?.AsArray().SelectMany(s=>s?["fields"]?.AsArray()??[]).FirstOrDefault(f=>f?["qcFormFieldID"]?.GetValue<long>()==fieldId.Value);Rows.Add(FieldUi.Card(FieldUi.Text("Signing requirement",12,true),FieldUi.Text(requirement?["fieldLabel"]?.ToString()??"Selected inspection requirement",18,true)));}
  var pad=new GraphicsView{Drawable=new InkDrawing(ink),HeightRequest=240,BackgroundColor=Colors.White};
  void Point(TouchEventArgs e,bool begin){if(e.Touches.Length==0||pad.Width<=0||pad.Height<=0)return;var p=e.Touches[0];if(begin)ink.Begin((float)(p.X/pad.Width),(float)(p.Y/pad.Height));else ink.Move((float)(p.X/pad.Width),(float)(p.Y/pad.Height));pad.Invalidate();}
  pad.StartInteraction+=(_,e)=>Point(e,true);pad.DragInteraction+=(_,e)=>Point(e,false);pad.EndInteraction+=(_,e)=>Point(e,false);
  Rows.Add(FieldUi.Card(pad));Rows.Add(FieldUi.Action("Clear / redraw",()=>{ink.Clear();pad.Invalidate();return Task.CompletedTask;},true));
  var types=workspace.Permissions.Where(p=>p.StartsWith("SignInspection:",StringComparison.Ordinal)).Select(p=>p["SignInspection:".Length..]).Order().ToList();var picker=new Picker{Title="Signing capacity",ItemsSource=types};if(types.Count==1)picker.SelectedIndex=0;Rows.Add(picker);
  var comments=new Editor{TextColor=FieldUi.Navy,BackgroundColor=Colors.White,PlaceholderColor=FieldUi.Muted,Placeholder="Signing comments",AutoSize=EditorAutoSizeOption.TextChanges};Rows.Add(comments);
  var consent=new CheckBox();Rows.Add(new HorizontalStackLayout{Children={consent,FieldUi.Text("I confirm my intent to sign this submitted record.")}});
  Rows.Add(FieldUi.Action("Sign record",async()=>{
   if(!consent.IsChecked||picker.SelectedItem is not string type||!ink.HasInk){model.Message="Draw a signature, choose your capacity and confirm signing intent.";return;}
   if(await form.SignatureAsync(ink,fieldId,type,comments.Text,Math.Clamp((int)pad.Width,32,2000),Math.Clamp((int)pad.Height,32,2000))){await Navigation.PopAsync();}else model.Message=form.Message;
  }));
 }
}
