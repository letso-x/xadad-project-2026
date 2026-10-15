using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;

public sealed class FieldItpsPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly long project;private readonly FieldListViewModel model;
 public FieldItpsPage(InspectionWorkspace w,long p):this(w,p,new FieldListViewModel(w)){}
 private FieldItpsPage(InspectionWorkspace w,long p,FieldListViewModel m):base("Inspection & Test Plans",m){workspace=w;project=p;model=m;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();Rows.Clear();Heading("Project quality","Inspection & Test Plans");
  if(!await model.LoadAsync(()=>workspace.ItpsAsync(project)))return;var cached=workspace.IsCached;
  Rows.Add(FieldUi.Text(cached?"Saved plans · offline viewing only. Connect and refresh to confirm current status and authority.":"Approved plans control activity progression. Historical revisions remain available."));
  if(workspace.Permissions.Contains("ManageItp")&&!cached)Rows.Add(FieldUi.Action("+ Create ITP",()=>Navigation.PushAsync(new FieldItpEditorPage(workspace,project,null))));
  var current=model.Data!.AsArray().Where(x=>x?["isCurrent"]?.GetValue<bool>()==true).ToList();
  int Count(string key)=>current.Sum(x=>x!["progress"]![key]!.GetValue<int>());
  var total=Count("totalActivities");var complete=Count("completedActivities");
  Rows.Add(FieldUi.Card(FieldUi.Text("Current approved plans · project progress",16,true),FieldUi.Text($"{complete}/{total} activities complete · {(total==0?0:Math.Round(100m*complete/total,1))}% · {Count("blockedActivities")} blocked · {Count("openHolds")} open Holds · {Count("releasedHolds")} released · {Count("witnessPending")} Witness pending · {Count("reviewPending")} Review pending")));
  foreach(var item in ItpPresentation.List(model.Data))Rows.Add(FieldUi.Card(FieldUi.Text(item.Number+" · "+item.Title,18,true),FieldUi.Badge(item.Status),FieldUi.Text($"Revision {item.Revision}"+(item.Current?" · Current approved plan":"")),FieldUi.Text(item.Progress),FieldUi.Action("Open ITP",()=>Navigation.PushAsync(new FieldItpPage(workspace,project,item.Id)),true)));
  if(model.Data.AsArray().Count==0)Rows.Add(FieldUi.Text("No ITPs for this project."));
 }
}
public sealed class FieldItpPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly long project,id;private readonly FieldListViewModel model;
 public FieldItpPage(InspectionWorkspace w,long p,long i):this(w,p,i,new FieldListViewModel(w)){}
 private FieldItpPage(InspectionWorkspace w,long p,long i,FieldListViewModel m):base("ITP",m){workspace=w;project=p;id=i;model=m;model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy))Rows.IsEnabled=!model.Busy;};}
 protected override async void OnAppearing(){base.OnAppearing();await Load();}
 private async Task Load()
 {
  Rows.Clear();Heading("Controlled plan","Inspection & Test Plan");if(!await model.LoadAsync(()=>workspace.ItpAsync(project,id)))return;
  var data=model.Data!;var v=data["version"]!;var d=v["definition"]!;var cached=workspace.IsCached;
  Rows.Add(FieldUi.Text("Project: "+(data["projectName"]?.ToString()??"Saved accessible project")));
  Rows.Add(FieldUi.Text(d["number"]+" · "+d["title"],20,true));Rows.Add(FieldUi.Badge(v["status"]!.ToString()));Rows.Add(FieldUi.Text("Revision "+v["revision"]+(v["isCurrent"]?.GetValue<bool>()==true?" · Current":" · Historical / pending")));
  Rows.Add(FieldUi.Text(ItpPresentation.Progress(data["progress"]!)));
  Rows.Add(FieldUi.Text("Project plan · information as of "+BusinessText.Date(data["asOf"]?.ToString())));
  if(cached)Rows.Add(FieldUi.Warning("Saved snapshot · current server status is unconfirmed. All ITP changes and releases require a connection."));
  string Name(JsonNode? actor)=>actor is null?"Not recorded":data["actorNames"]?[actor.ToString()]?.ToString()??"Recorded user";
  foreach(var(key,label) in new[]{("created","Created"),("submitted","Submitted"),("approved","Approved")})
   if(v[key+"At"] is not null)Rows.Add(FieldUi.Text(label+" by "+Name(v[key+"By"])+" · "+BusinessText.Date(v[key+"At"]!.ToString())));
  bool Can(string action,JsonNode? activity=null)=>ItpPresentation.Can(action,v,activity,workspace.Permissions,cached);
  if(Can("edit"))Rows.Add(FieldUi.Action("Edit draft and activities",()=>Navigation.PushAsync(new FieldItpEditorPage(workspace,project,data)),true));
  foreach(var(action,label) in new[]{("submit","Submit for approval"),("approve","Approve controlled plan"),("revise","Create revision")})if(Can(action))
   Rows.Add(FieldUi.Action(label,async()=>{
    if(!await DisplayAlertAsync(label,"Confirm this controlled ITP action?","Continue","Cancel"))return;
    JsonNode? result=null;if(await model.RunAsync(async()=>{result=await workspace.ActItpAsync(project,id,action,v["stamp"]!.GetValue<long>());},"Saved."))
    {if(action=="revise")await Navigation.PushAsync(new FieldItpPage(workspace,project,result!["id"]!.GetValue<long>()));else await Load();}
   }));
  foreach(var activity in ItpPresentation.Ordered(data["activities"]!))
  {
   var s=activity["definition"]!;var e=activity["execution"]!;var activityId=s["id"]!.GetValue<long>();
   var card=new VerticalStackLayout{Spacing=10};card.Add(FieldUi.Text(s["code"]+" · "+s["title"],18,true));card.Add(FieldUi.Text(ItpPresentation.Point(s["pointType"]!.ToString()),14,true));
   card.Add(FieldUi.Text(s["description"]?.ToString()??""));card.Add(FieldUi.Text("Criteria: "+(s["criteria"]?.ToString()??"")));
   var predecessors=s["dependsOn"]!.AsArray().Select(x=>v["definition"]!["activities"]!.AsArray().Single(y=>y!["id"]!.ToString()==x!.ToString())!["code"]!.ToString());
   card.Add(FieldUi.Text("Depends on: "+(s["dependsOn"]!.AsArray().Count==0?"None":string.Join(", ",predecessors))));
   card.Add(activity["blockedReason"] is null?FieldUi.Text(ItpPresentation.Availability(activity)):FieldUi.Warning(ItpPresentation.Availability(activity)));
   if(s["requireQc"]?.GetValue<bool>()==true&&e["qcFormId"] is null)card.Add(FieldUi.Warning("Link and approve the required QC record before completion or release."));
   if(activity["linkedQc"] is JsonNode qc)
   {
    card.Add(FieldUi.Text("QC "+qc["number"]+" · "+BusinessText.Status(qc["status"]!.ToString())+" · "+qc["blockingNcrs"]+" blocking NCR(s)"));
    if(workspace.Permissions.Contains("ReadInspection"))card.Add(FieldUi.Action("Open QC record",()=>Navigation.PushAsync(new FieldFormPage(workspace,qc["id"]!.GetValue<long>())),true));
   }
   foreach(var(prefix,label) in new[]{("completed","Completed"),("released","Hold released"),("witnessed","Witnessed"),("reviewed","Reviewed")})
    if(e[prefix+"At"] is not null)card.Add(FieldUi.Text(label+" by "+Name(e[prefix+"By"])+" · "+BusinessText.Date(e[prefix+"At"]!.ToString())));
   foreach(var key in new[]{"completionComments","releaseComments","witnessComments","reviewComments"})if(e[key] is not null)card.Add(FieldUi.Text(e[key]!.ToString()));
   if(s["pointType"]?.ToString()=="Witness"&&e["witnessedAt"] is null)card.Add(FieldUi.Text(s["witnessRequired"]?.GetValue<bool>()==true?"Witness required · Pending":"Witness optional · Pending"));
   if(s["pointType"]?.ToString()=="Review"&&e["reviewedAt"] is null)card.Add(FieldUi.Text("Review acknowledgement · Pending"));
   foreach(var(action,label) in new[]{("complete","Complete activity"),("release","Release Hold"),("witness","Record Witness"),("review","Acknowledge Review")})if(Can(action,activity))
    card.Add(FieldUi.Action(label,async()=>{
     var comments=await DisplayPromptAsync(label,"Record how the criteria were satisfied. Hold release is a separate authorised action.","Save","Cancel",maxLength:4000);
     if(string.IsNullOrWhiteSpace(comments))return;
     if(await model.RunAsync(async()=>{await workspace.ActItpAsync(project,id,action,v["stamp"]!.GetValue<long>(),activityId,comments);},"Saved."))await Load();
    },true));
   if(Can("link-qc",activity)&&workspace.Permissions.Contains("ReadInspection"))card.Add(FieldUi.Action("Link an existing QC record",async()=>{
    await model.RunAsync(async()=>{
     var forms=(await workspace.FormsAsync(project)).AsArray().Where(f=>f?["qcFormID"]?.GetValue<long>()>0&&f?["checklistTemplateVersionID"]?.ToString()==s["checklistTemplateVersionId"]?.ToString()).ToList();
     if(forms.Count==0)throw new InvalidOperationException("No matching synced QC record is available. Start the configured checklist from Form Library, sync it, then link it here.");
     var labels=forms.Select(f=>f!["formNumber"]+" · "+BusinessText.Status(f["status"]!.ToString())).ToArray();
     var choice=await DisplayActionSheetAsync("Choose matching QC record","Cancel",null,labels);var index=Array.IndexOf(labels,choice);if(index<0)return;
     await workspace.ActItpAsync(project,id,"link-qc",v["stamp"]!.GetValue<long>(),activityId,"Linked QC record",forms[index]!["qcFormID"]!.GetValue<long>());
    });await Load();
   },true));
   Rows.Add(FieldUi.Card(card));
  }
  Rows.Add(FieldUi.Action("Audit history",async()=>{
   if(await model.RunAsync(async()=>{
    var audit=await workspace.ItpAuditAsync(project,id);var lines=audit.AsArray().Select(a=>a!["actorName"]+" · "+ItpPresentation.AuditAction(a["action"]!.ToString())+" · "+BusinessText.Date(a["occurredAt"]?.ToString()));
    await DisplayAlertAsync("Recent ITP audit history",string.Join("\n",lines),"Close");
   })){};
  },true));
 }
}
public sealed class FieldItpEditorPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly long project;private readonly JsonNode? source;private readonly FieldViewModel model;
 private readonly SemaphoreSlim persistence=new(1,1);private JsonNode draft=new JsonObject();private bool loaded;private JsonArray templates=new();
 private long? Version=>source?["version"]?["id"]?.GetValue<long>();private string Key=>"itp-editor:"+(Version?.ToString()??"new");
 public FieldItpEditorPage(InspectionWorkspace w,long p,JsonNode? data):this(w,p,data,new FieldViewModel()){}
 private FieldItpEditorPage(InspectionWorkspace w,long p,JsonNode? data,FieldViewModel m):base("Author ITP",m){workspace=w;project=p;source=data?.DeepClone();model=m;model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy))Rows.IsEnabled=!model.Busy;};}
 protected override async void OnAppearing()
 {
  base.OnAppearing();if(loaded)return;loaded=true;
  Rows.Clear();Heading("Controlled plan","Author ITP");
  await model.RunAsync(async()=>{
   draft=await workspace.InputDraftAsync(project,Key)??new JsonObject{["expectedStamp"]=source?["version"]?["stamp"]?.DeepClone()??JsonValue.Create(0L),["definition"]=source?["version"]?["definition"]?.DeepClone()??new JsonObject{["number"]="",["title"]="",["activities"]=new JsonArray()}};
   if(workspace.Permissions.Contains("ReadTemplates"))
    try{templates=(await workspace.TemplatesAsync()).AsArray();}
    catch(Exception e) when(e is HttpRequestException or TaskCanceledException or InvalidOperationException){model.Message="Checklist choices are unavailable. Existing pinned choices and your draft are retained; reconnect to choose a different checklist.";}
   Render();
  });
 }
 private async Task Persist()
 {
  var snapshot=draft.DeepClone();await persistence.WaitAsync();try{await workspace.SaveInputDraftAsync(project,Key,snapshot);}finally{persistence.Release();}
 }
 private async Task Remember(){try{await Persist();}catch{model.Message="Your draft could not be saved on this device. Keep this page open and retry before leaving.";}}
 private void Text(VerticalStackLayout target,JsonNode node,string key,string label,int max)
 {
  target.Add(FieldUi.Text(label,12,true));
  if(max>300)
  {
   var input=new Editor{Text=node[key]?.ToString(),Placeholder=label,MaxLength=max,AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=90,TextColor=FieldUi.Navy,BackgroundColor=Colors.White,PlaceholderColor=FieldUi.Muted};
   input.TextChanged+=async(_,_)=>{node[key]=input.Text??"";await Remember();};target.Add(input);
  }
  else
  {
   var input=FieldUi.Entry(label);input.MaxLength=max;input.Text=node[key]?.ToString();input.TextChanged+=async(_,_)=>{node[key]=input.Text??"";await Remember();};target.Add(input);
  }
 }
 private void Render()
 {
  Rows.Clear();Heading("Controlled plan","Author ITP");Rows.Add(FieldUi.Text("Draft changes stay on this device. Connect to save. Submitted and approved definitions cannot be edited."));
  var d=draft["definition"]!;var activities=d["activities"]!.AsArray();
  if(Version is null)Text(Rows,d,"number","ITP number *",100);else Rows.Add(FieldUi.Text(d["number"]!.ToString(),18,true));Text(Rows,d,"title","Title *",300);
  foreach(var s in activities.OrderBy(x=>x!["sequence"]!.GetValue<int>()).ToList())
  {
   var step=s!;var card=new VerticalStackLayout{Spacing=10};card.Add(FieldUi.Text("Activity "+step["sequence"],18,true));
   Text(card,step,"code","Activity code *",100);Text(card,step,"title","Activity title *",300);Text(card,step,"description","Description",4000);Text(card,step,"criteria","Completion / release criteria (required for Hold)",4000);
   var point=new FieldChoice("Point type",new[]{"None","Hold","Witness","Review"},step["pointType"]!.ToString());point.SelectionChanged+=async(_,_)=>{step["pointType"]=point.SelectedValue;await Remember();};card.Add(point);
   void Flag(string key,string label)
   {
    var check=new CheckBox{IsChecked=step[key]?.GetValue<bool>()==true,Color=FieldUi.Navy};check.CheckedChanged+=async(_,e)=>{step[key]=e.Value;await Remember();};card.Add(new HorizontalStackLayout{Children={check,FieldUi.Text(label)}});
   }
   Flag("witnessRequired","Witness required before successors may progress");Flag("independentHoldRelease","Hold releaser must differ from completer");
   var choices=new List<(string Value,string Label)>{("","No linked checklist")};
   choices.AddRange(templates.Where(t=>t?["activeVersionID"] is not null&&t["isActive"]?.GetValue<bool>()==true).Select(t=>(t!["activeVersionID"]!.ToString(),(t["templateNumber"]?.ToString()??"Checklist")+" · "+(t["templateName"]?.ToString()??"")+" · "+(t["revision"]?.ToString()??""))));
   var selected=step["checklistTemplateVersionId"]?.ToString()??"";if(selected!=""&&!choices.Any(c=>c.Value==selected))choices.Add((selected,"Existing pinned checklist revision"));
   var template=new FieldChoice("Checklist revision",choices,selected);template.SelectionChanged+=async(_,_)=>{step["checklistTemplateVersionId"]=long.TryParse(template.SelectedValue,out var value)?JsonValue.Create(value):null;await Remember();};card.Add(template);Flag("requireQc","Require approved linked QC before completion and release");
   card.Add(FieldUi.Text("Dependencies",14,true));
   foreach(var other in activities.Where(x=>x!["id"]!.ToString()!=step["id"]!.ToString()))
   {
    var predecessor=other!["id"]!.GetValue<long>();var deps=step["dependsOn"]!.AsArray();var check=new CheckBox{IsChecked=deps.Any(x=>x!.GetValue<long>()==predecessor),Color=FieldUi.Navy};
    check.CheckedChanged+=async(_,e)=>{var existing=deps.FirstOrDefault(x=>x!.GetValue<long>()==predecessor);if(e.Value&&existing is null)deps.Add(predecessor);else if(!e.Value&&existing is not null)deps.Remove(existing);await Remember();};card.Add(new HorizontalStackLayout{Children={check,FieldUi.Text(other["code"]+" · "+other["title"])}});
   }
   async Task Move(int direction)
   {
    var index=activities.IndexOf(step);var next=index+direction;if(next<0||next>=activities.Count)return;
    var clone=step.DeepClone();activities.RemoveAt(index);activities.Insert(next,clone);Renumber();await Remember();Render();
   }
   card.Add(FieldUi.Action("Move up",()=>Move(-1),true));card.Add(FieldUi.Action("Move down",()=>Move(1),true));
   card.Add(FieldUi.Action("Delete draft activity",async()=>{
    if(!await DisplayAlertAsync("Delete draft activity","Remove this activity and references to it from this draft?","Remove","Cancel"))return;
    var removed=step["id"]!.GetValue<long>();activities.Remove(step);foreach(var other in activities){var deps=other!["dependsOn"]!.AsArray();foreach(var dep in deps.Where(x=>x!.GetValue<long>()==removed).ToList())deps.Remove(dep);}Renumber();await Remember();Render();
   },true));Rows.Add(FieldUi.Card(card));
  }
  void Renumber(){for(var i=0;i<activities.Count;i++)activities[i]!["sequence"]=i+1;}
  Rows.Add(FieldUi.Action("+ Add activity",async()=>{
   if(activities.Count>=500){model.Message="Maximum 500 activities.";return;}
   activities.Add(new JsonObject{["id"]=Random.Shared.NextInt64(1,long.MaxValue),["sequence"]=activities.Count+1,["code"]="A"+(activities.Count+1),["title"]="",["description"]="",["criteria"]="",["pointType"]="None",["dependsOn"]=new JsonArray(),["checklistTemplateVersionId"]=null,["requireQc"]=false,["witnessRequired"]=true,["independentHoldRelease"]=true});await Remember();Render();
  },true));
  if(Version.HasValue)Rows.Add(FieldUi.Action("Review latest server draft",async()=>{
   await model.RunAsync(async()=>{
    var latest=await workspace.ItpAsync(project,Version.Value);
    if(workspace.IsCached)throw new InvalidOperationException("Connect before reviewing the latest draft. Your entered changes are retained.");
    var server=latest["version"]!;if(server["status"]!.ToString()!="Draft")throw new InvalidOperationException("This revision is no longer editable. Your entered draft is retained on this device.");
    var latestDefinition=server["definition"]!;
    string Summary(JsonNode definition)=>definition["title"]+"\n"+string.Join("\n\n",definition["activities"]!.AsArray().Select(s=>s!["sequence"]+". "+s["code"]+" · "+s["title"]+" · "+ItpPresentation.Point(s["pointType"]!.ToString())+"\n"+s["description"]+"\nCriteria: "+s["criteria"]+"\nDepends on: "+string.Join(", ",s["dependsOn"]!.AsArray().Select(x=>definition["activities"]!.AsArray().Single(a=>a!["id"]!.ToString()==x!.ToString())!["code"]!.ToString()))+"\nApproved QC required: "+s["requireQc"]+" · Witness required: "+s["witnessRequired"]+" · Independent Hold release: "+s["independentHoldRelease"]));
    if(!await DisplayAlertAsync("Review concurrent changes","Latest server draft:\n"+Summary(latestDefinition)+"\n\nYour entered draft:\n"+Summary(d)+"\n\nKeep your reviewed changes for the next Save? Saving will replace the latest draft definition; both versions remain in the audit history.","Keep my changes","Cancel"))return;
    draft["expectedStamp"]=server["stamp"]!.DeepClone();await Persist();model.Message="Latest revision checked. Review activities, dependencies and criteria before Save draft.";
   });
  },true));
  Rows.Add(FieldUi.Action("Save draft",async()=>{
   JsonNode? result=null;if(await model.RunAsync(async()=>{
    await Persist();result=await workspace.SaveItpAsync(project,Version,d,draft["expectedStamp"]!.GetValue<long>());await workspace.SaveInputDraftAsync(project,Key,null);
   },"ITP draft saved.")){await Navigation.PopAsync();if(Version is null)await Navigation.PushAsync(new FieldItpPage(workspace,project,result!["id"]!.GetValue<long>()));}
  }));
 }
}
