using System.Text.Json.Nodes;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
using MzukuluQMS.Mobile.Core;
using System.Globalization;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldTemplateBuilderPage : FieldScreen
{
 private readonly TemplateBuilderViewModel model;private bool loaded;private long? revisionVersion;
 public FieldTemplateBuilderPage(InspectionWorkspace workspace,long version):this(workspace){revisionVersion=version;}
 public FieldTemplateBuilderPage(InspectionWorkspace workspace):this(new TemplateBuilderViewModel(workspace)){}
 private FieldTemplateBuilderPage(TemplateBuilderViewModel model):base("Create checklist template",model){this.model=model;model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy))Rows.IsEnabled=!model.Busy;};}
 protected override async void OnAppearing(){base.OnAppearing();if(loaded)return;loaded=true;if(revisionVersion.HasValue){if(await model.StartRevisionAsync(revisionVersion.Value))Render();}else await model.RunAsync(async()=>{await model.LoadAsync();Render();});}
 private async Task SaveDraftAsync(){try{await model.PersistAsync();model.Message="Draft saved";}catch{model.Message="Draft could not be saved. Keep this page open and try again.";}}
 private void Render()
 {
  Rows.Clear();Heading("Administration",model.Draft["revisionTemplateID"] is null?"Create Checklist Template":"Create new revision");
  if(model.Draft["publishedTemplateID"] is null&&ChecklistFieldSettings.NeedsReferenceRepair(model.Draft))
  {
   Rows.Add(FieldUi.Warning("Some saved checklist settings need repair before editing. Your draft is retained."));
   Rows.Add(FieldUi.Action("Repair draft settings",async()=>{if(await model.RepairReferencesAsync())Render();}));return;
  }
  if(model.Draft["publishedTemplateID"] is not null)
  {
   Rows.Add(FieldUi.Card(FieldUi.Text(model.Draft["documentNumber"]+" · "+model.Draft["documentName"]+(model.Draft["active"]?.GetValue<bool>()==true?" is available in Form Library.":" is saved as inactive.")),FieldUi.Action("Start another template",async()=>{await model.ResetAsync();Render();})));return;
  }
  Rows.Add(FieldUi.Section("Document information"));
  foreach(var (key,label) in new[]{("documentNumber","Document number *"),("documentName","Document name *"),("subtitle","Description / subtitle"),("revision","Revision *")})
   if(model.Draft["revisionTemplateID"] is not null&&key!="revision")Rows.Add(FieldUi.Card(FieldUi.Text(label,12,true),FieldUi.Text(model.Draft[key]?.ToString()??"")));else AddText(model.Draft,key,label,Rows);
  var date=new DatePicker{Date=DateTime.TryParse(model.Draft["issueDate"]?.ToString(),out var value)?value:DateTime.Today};date.DateSelected+=async(_,_)=>{model.Draft["issueDate"]=Convert.ToDateTime(date.Date).ToString("yyyy-MM-dd");await SaveDraftAsync();};Rows.Add(FieldUi.Card(FieldUi.Text("ISSUE DATE",12,true),date));if(model.Draft["revisionTemplateID"] is null)AddFlag(model.Draft,"active","Active (visible in Form Library)",Rows);else Rows.Add(FieldUi.Text("Publishing makes this revision available for new forms. Existing QC records keep their saved checklist and signing requirements."));
  Rows.Add(FieldUi.Section("Project information fields"));Rows.Add(FieldUi.Text("Extra site-specific fields shown in the QC header."));
  var projects=model.Draft["projectFields"]!.AsArray();foreach(var field in projects)AddField(field!,projects,true);
  Rows.Add(FieldUi.Action("+ Add Field",async()=>{projects.Add(TemplateBuilderViewModel.NewField());await model.PersistAsync();Render();},true));
  Rows.Add(FieldUi.Section("Scope & acceptance criteria"));AddText(model.Draft,"scope","Scope",Rows,true);AddText(model.Draft,"acceptanceCriteria","Acceptance criteria",Rows,true);
  Rows.Add(FieldUi.Section("Checklist sections"));
  var sections=model.Draft["sections"]!.AsArray();var itemNumber=0;foreach(var section in sections)
  {
   AddText(section!,"title","Section title (optional)",Rows);AddFlag(section!,"required","Required section",Rows);
   AddReorder(sections,section!,Rows);
   Rows.Add(FieldUi.Action("Remove section",async()=>{sections.Remove(section);await model.PersistAsync();Render();},true));
   var fields=section!["fields"]!.AsArray();foreach(var group in ChecklistItems.Groups(fields))
   {
    if(!group.IsInspectionItem){AddField(group.Primary,fields,false);continue;}
    var item=new VerticalStackLayout{Spacing=10};item.Add(FieldUi.Text("ITEM "+(++itemNumber),16,true));
    AddField(group.Primary,fields,false,item,true);
    foreach(var additional in group.Fields.Skip(1))AddField(additional,fields,false,item);
    item.Add(FieldUi.Action("+ Add reading / reference / additional field",async()=>{ChecklistItems.AddAssociated(fields,group.Primary);await model.PersistAsync();Render();},true));
    var order=new HorizontalStackLayout{Spacing=8};
    foreach(var (label,direction) in new[]{("Move item up",-1),("Move item down",1)})order.Add(FieldUi.Action(label,async()=>{ChecklistItems.Move(fields,group,direction);await model.PersistAsync();Render();},true));
    item.Add(order);item.Add(FieldUi.Action("Remove item and its fields",async()=>{ChecklistItems.Remove(fields,group);await model.PersistAsync();Render();},true));Rows.Add(FieldUi.Card(item));
   }
   Rows.Add(FieldUi.Action("+ Add Checklist Item",async()=>{fields.Add(ChecklistItems.NewItem());await model.PersistAsync();Render();},true));
   Rows.Add(FieldUi.Action("+ Add standalone dynamic field",async()=>{fields.Add(TemplateBuilderViewModel.NewField());await model.PersistAsync();Render();},true));
  }
  Rows.Add(FieldUi.Action("+ Add Section",async()=>{sections.Add(new JsonObject{["title"]="",["required"]=true,["fields"]=new JsonArray()});await model.PersistAsync();Render();},true));
  Rows.Add(FieldUi.Section("Required signing capacities *"));Rows.Add(FieldUi.Text("Select at least one capacity before saving, such as Inspector. These requirements do not grant users permission to sign."));
  var types=model.Draft["requiredSignOffTypes"]!.AsArray();foreach(var type in new[]{"Inspector","Technician","Supervisor","Client"})
  {
   var check=new CheckBox{IsChecked=types.Any(t=>t?.ToString()==type)};check.CheckedChanged+=async(_,e)=>{var old=types.FirstOrDefault(t=>t?.ToString()==type);if(e.Value&&old is null)types.Add(type);else if(!e.Value&&old is not null)types.Remove(old);await SaveDraftAsync();};Rows.Add(new HorizontalStackLayout{Children={check,FieldUi.Text(type)}});
  }
  var publishStatus=FieldUi.Text("");publishStatus.SetBinding(Label.TextProperty,nameof(FieldViewModel.Message));Rows.Add(publishStatus);
  Rows.Add(FieldUi.Action("Save Template",async()=>{if(await model.PublishAsync())Render();}));Rows.Add(FieldUi.Text("Draft changes are saved automatically. Connect to publish the template. If the connection is interrupted, check Checklist Templates before trying again."));
 }
 private void AddField(JsonNode field,JsonArray parent,bool project,VerticalStackLayout? host=null,bool inspectionResult=false)
 {
  var card=new VerticalStackLayout{Spacing=10};Action? updateType=null;AddText(field,"label",inspectionResult?"Inspection / test requirement *":"Field label *",card);
  if(inspectionResult)
  {
   card.Add(FieldUi.Text("RESULT: A / R / N/A",12,true));
   if(field["validationError"]?.GetValue<bool>()==true){card.Add(FieldUi.Warning("Previous answer settings need review. The saved A/R/N/A configuration is retained."));card.Add(FieldUi.Action("Use saved A/R/N/A settings",async()=>{field["validationError"]=false;await SaveDraftAsync();Render();},true));}
  }
  else
  {
   var types=new[]{("Text","Short answer"),("LongText","Long answer"),("Number","Whole number"),("Decimal","Decimal number"),("Boolean","Yes / No"),("YesNo","Yes / No (text)"),("Date","Date"),("DateTime","Date and time"),("Dropdown","Single choice"),("MultiSelect","Multiple choices"),("Reference","Reference")}.Concat(project?Array.Empty<(string,string)>():new[]{("Photo","Photo"),("Signature","Signature")}).ToArray();var picker=new FieldChoice("Answer type",types,field["type"]?.ToString());picker.SelectionChanged+=async(_,_)=>{if(picker.SelectedValue is string type)field["type"]=type;updateType?.Invoke();await SaveDraftAsync();};card.Add(FieldUi.Text("ANSWER TYPE",11,true));card.Add(picker);
  }
  AddFlag(field,"required","Required",card);
  if(!project){AddFlag(field,"requiresPhoto","Requires photo evidence",card);AddFlag(field,"requiresSignature","Requires field signature",card);}
  AddText(field,"helpText","Help text",card);AddText(field,"placeholder","Placeholder",card);
  if(!inspectionResult)
  {
   var unit=FieldUi.Entry("e.g. Nm, Ω, MΩ");unit.Text=ChecklistItems.Unit(field);unit.TextChanged+=async(_,_)=>{field["validationRules"]!["unit"]=unit.Text??"";await SaveDraftAsync();};var units=FieldUi.Card(FieldUi.Text("UNIT (OPTIONAL)",11,true),unit);card.Add(units);
   var options=FieldUi.Entry("Separate choices with commas");options.Text=string.Join(", ",field["options"]!.AsArray().Select(o=>o!.ToString()));options.TextChanged+=async(_,_)=>{field["options"]=new JsonArray((options.Text??"").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(o=>(JsonNode?)JsonValue.Create(o)).ToArray());await SaveDraftAsync();};var choices=FieldUi.Card(FieldUi.Text("CHOICES",11,true),options);card.Add(choices);
   var limits=AddLimits(field,card);
   updateType=()=>{var type=field["type"]?.ToString();units.IsVisible=type is "Number" or "Decimal" or "Reference";choices.IsVisible=type is "Dropdown" or "MultiSelect";limits();};updateType();
  }
  if(!inspectionResult)card.Add(FieldUi.Action("Remove field",async()=>{parent.Remove(field);await model.PersistAsync();Render();},true));
  if(host is null){AddReorder(parent,field,card);Rows.Add(FieldUi.Card(card));}else host.Add(card);
 }
 private Action AddLimits(JsonNode field,VerticalStackLayout parent)
 {
  var numbers=new VerticalStackLayout{Spacing=8};var text=new VerticalStackLayout{Spacing=8};var entries=new Dictionary<string,Entry>();
  var error=FieldUi.Text(field["validationError"]?.GetValue<bool>()==true?"Review the saved limits, then apply them before publishing.":"");error.TextColor=FieldUi.Accent;Action? update=null;
  foreach(var (key,label,isLength) in new[]{("min","Minimum value",false),("max","Maximum value",false),("minLength","Minimum characters",true),("maxLength","Maximum characters",true)})
  {
   var entry=FieldUi.Entry("Optional",Keyboard.Numeric);var stored=field["validationRules"]?[key];
   entry.Text=field["limitInputs"] is JsonObject saved&&saved.ContainsKey(key)?saved[key]?.ToString()??"":stored is JsonValue value&&value.TryGetValue<decimal>(out var number)?number.ToString(CultureInfo.CurrentCulture):stored?.ToString();
   entries[key]=entry;(isLength?text:numbers).Add(FieldUi.Card(FieldUi.Text(label.ToUpperInvariant(),11,true),entry));
  }
  async Task ApplyAsync()
  {
   var inputs=entries.ToDictionary(e=>e.Key,e=>(string?)e.Value.Text);field["limitInputs"]=new JsonObject(inputs.Select(e=>new KeyValuePair<string,JsonNode?>(e.Key,JsonValue.Create(e.Value))));
   try{field["validationRules"]=ChecklistFieldSettings.Limits((field["validationRules"] as JsonObject)??new JsonObject(),inputs,CultureInfo.CurrentCulture);field["validationError"]=false;error.Text="";}
   catch(ArgumentException e){field["validationError"]=true;error.Text=e.Message;}
   update?.Invoke();await SaveDraftAsync();
  }
  foreach(var entry in entries.Values)entry.TextChanged+=async(_,_)=>await ApplyAsync();
  parent.Add(numbers);parent.Add(text);parent.Add(error);
  if(field["validationError"]?.GetValue<bool>()==true)parent.Add(FieldUi.Action("Apply displayed limits",ApplyAsync,true));
  update=()=>{var type=field["type"]?.ToString();var invalid=field["validationError"]?.GetValue<bool>()==true;numbers.IsVisible=invalid||type is "Number" or "Decimal";text.IsVisible=invalid||type is "Text" or "LongText" or "Reference";};return update;
 }
 private void AddReorder(JsonArray array,JsonNode node,VerticalStackLayout parent)
 {
  var controls=new HorizontalStackLayout{Spacing=8};foreach(var (label,direction) in new[]{("Move up",-1),("Move down",1)})controls.Add(FieldUi.Action(label,async()=>{var index=array.IndexOf(node);var target=index+direction;if(target>=0&&target<array.Count){array.RemoveAt(index);array.Insert(target,node);await model.PersistAsync();Render();}},true));parent.Add(controls);
 }
 private void AddText(JsonNode node,string key,string label,VerticalStackLayout parent,bool multiline=false)
 {
  parent.Add(FieldUi.Text(label.ToUpperInvariant(),11,true));
  if(multiline){var entry=new Editor{TextColor=FieldUi.Navy,BackgroundColor=Colors.White,PlaceholderColor=FieldUi.Muted,Text=node[key]?.ToString()??"",AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=90};entry.TextChanged+=async(_,_)=>{node[key]=entry.Text??"";await SaveDraftAsync();};parent.Add(entry);}
  else{var entry=FieldUi.Entry(label);entry.Text=node[key]?.ToString()??"";entry.TextChanged+=async(_,_)=>{
   if(key=="key"&&ChecklistItems.Role(node)=="Result")
   {
    var previous=node[key]?.ToString();foreach(var related in model.Draft["sections"]!.AsArray().SelectMany(s=>s!["fields"]!.AsArray()))
     if(related is not null&&ChecklistItems.ItemKey(related)==previous)related["validationRules"]!["inspectionItem"]!["key"]=entry.Text??"";
   }
   node[key]=entry.Text??"";await SaveDraftAsync();};parent.Add(entry);}
 }
 private void AddFlag(JsonNode node,string key,string label,VerticalStackLayout parent)
 {var check=new CheckBox{IsChecked=node[key]?.GetValue<bool>()==true};check.CheckedChanged+=async(_,e)=>{node[key]=e.Value;await SaveDraftAsync();};parent.Add(new HorizontalStackLayout{Children={check,FieldUi.Text(label)}});}
}
