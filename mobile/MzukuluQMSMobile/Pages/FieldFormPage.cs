using System.Globalization;
using System.Text.Json.Nodes;
using MzukuluQMSMobile.Services;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldFormPage : ContentPage
{
 private readonly InspectionWorkspace workspace;private readonly long formId;private readonly VerticalStackLayout rows=new(){Padding=20,Spacing=12};private readonly Label message=new();private JsonNode? form;
 public FieldFormPage(InspectionWorkspace workspace,long formId){this.workspace=workspace;this.formId=formId;Title="Inspection";Content=new ScrollView{Content=rows};ToolbarItems.Add(new ToolbarItem("Sync",null,async()=>{try{message.Text=await workspace.SyncAsync();}catch{message.Text="Sync unavailable. Work retained.";}}));}
 protected override async void OnAppearing(){base.OnAppearing();try{form=await workspace.FormAsync(formId);Render();}catch{message.Text="Unable to open this record. Cached data is retained.";if(!rows.Contains(message))rows.Add(message);}}
 private void Render()
 {
  rows.Clear();rows.Add(new Label{Text=form!["formNumber"]+" · "+form["status"],FontSize=24});rows.Add(message);
  var editable=form["status"]!.GetValue<string>() is "Draft" or "InProgress" or "ReturnedForCorrection";
  if(!editable)rows.Add(new Label{Text="Read-only record. Historical signatures remain on the server."});
  if(form["status"]!.GetValue<string>()=="ReturnedForCorrection")rows.Add(new Label{Text="Returned for correction. Review the audit reason, correct values, resubmit and provide fresh signing consent."});
  foreach(var field in form["projectFields"]!.AsArray())AddField(field!,true,editable);
  foreach(var section in form["sections"]!.AsArray()){rows.Add(new Label{Text=section!["sectionName"]!.GetValue<string>(),FontSize=22});foreach(var field in section["fields"]!.AsArray())AddField(field!,false,editable);}
  if(editable){var submit=new Button{Text="Queue submission"};submit.Clicked+=async(_,_)=>{submit.IsEnabled=false;try{await workspace.QueueSubmitAsync(form);message.Text="Submission queued. Required fields/evidence will be checked by the server.";}catch{message.Text="Submission was not saved. Local values retained.";}finally{submit.IsEnabled=true;}};rows.Add(submit);}
 }
 private void AddField(JsonNode field,bool projectField,bool editable)
 {
  var type=field["fieldType"]!.GetValue<string>();var required=field["isRequired"]?.GetValue<bool>()==true;
  rows.Add(new Label{Text=field["fieldLabel"]+(required?" *":""),FontAttributes=FontAttributes.Bold});
  if(field["helpText"] is not null)rows.Add(new Label{Text=field["helpText"]!.GetValue<string>()});
  if(type is "Photo" or "Signature"){rows.Add(new Label{Text=type+" evidence is required where indicated. Capture/signing UI is not yet available."});return;}
  Func<JsonNode?> value;View input;
  if(type is "Boolean" or "YesNo")
  {
   var picker=new Picker{Title="Choose",ItemsSource=new[]{"Unanswered","Yes","No"},SelectedIndex=field["value"] is null?0:field["value"]!.GetValue<bool>()?1:2};input=picker;value=()=>picker.SelectedIndex<=0?null:JsonValue.Create(picker.SelectedIndex==1);
  }
  else if(type=="Dropdown")
  {
   var options=field["options"]?.AsArray().Select(o=>o is JsonValue?o.GetValue<string>():o?["value"]?.GetValue<string>()??"").ToList()??new();
   var picker=new Picker{Title="Choose",ItemsSource=options,SelectedItem=field["value"]?.GetValue<string>()};input=picker;value=()=>picker.SelectedItem is string selected?JsonValue.Create(selected):null;
  }
  else if(type is "Text" or "LongText" or "Reference" or "Number" or "Decimal" or "Date" or "DateTime" or "MultiSelect")
  {
   var entry=new Editor{Text=field["value"] is JsonValue j?j.ToString():field["value"]?.ToJsonString(),Placeholder=field["placeholder"]?.GetValue<string>()??(type=="MultiSelect"?"JSON array of selected options":type),AutoSize=EditorAutoSizeOption.TextChanges};input=entry;
   value=()=>type switch {"Number" or "Decimal"=>JsonValue.Create(decimal.Parse(entry.Text??"",CultureInfo.InvariantCulture)),"MultiSelect"=>JsonNode.Parse(entry.Text??"[]"),_=>string.IsNullOrEmpty(entry.Text)?null:JsonValue.Create(entry.Text)};
  }
  else{rows.Add(new Label{Text="Unsupported field type: "+type});return;}
  input.IsEnabled=editable;rows.Add(input);
  if(editable){var save=new Button{Text="Save locally"};save.Clicked+=async(_,_)=>{save.IsEnabled=false;try{await workspace.SaveValueAsync(form!,field,value(),projectField);message.Text="Saved locally · pending sync. Server validation applies on sync.";}catch{message.Text="Could not save this value. Check its format; previous durable work is retained.";}finally{save.IsEnabled=true;}};rows.Add(save);}
 }
}
