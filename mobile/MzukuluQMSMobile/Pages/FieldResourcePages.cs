using System.Text.Json.Nodes;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldClientsPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;
 public FieldClientsPage(InspectionWorkspace workspace):this(workspace,new FieldListViewModel(workspace)){}
 private FieldClientsPage(InspectionWorkspace workspace,FieldListViewModel model):base("Clients",model){this.workspace=workspace;this.model=model;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();Rows.Clear();Heading("Clients","Clients");
  if(workspace.Permissions.Contains("ManageProjects"))Rows.Add(FieldUi.Action("+ Add Client",()=>Navigation.PushAsync(new FieldCreateResourcePage(workspace,false))));
  if(!await model.LoadAsync(workspace.ClientsAsync))return;
  JsonArray? projects=null;try{if(workspace.Permissions.Contains("ReadProjects"))projects=(await workspace.ProjectsAsync()).AsArray();}catch{model.Message+=" Project counts unavailable.";}
  foreach(var client in model.Data!.AsArray())
  {
   var id=client!["clientID"]!.GetValue<long>();var count=projects?.Count(p=>p?["clientID"]?.GetValue<long>()==id);
   Rows.Add(FieldUi.Card(FieldUi.Text(client["clientName"]!.ToString(),18,true),FieldUi.Text(string.Join(" · ",new[]{client["contactName"]?.ToString(),client["contactEmail"]?.ToString(),client["contactPhone"]?.ToString()}.Where(s=>!string.IsNullOrWhiteSpace(s)))),FieldUi.Text(count.HasValue?count+" accessible projects":"Project count unavailable"),FieldUi.Badge(client["isActive"]!.GetValue<bool>()?"Active":"Inactive"),FieldUi.Action("View projects",()=>Navigation.PushAsync(new FieldProjectsPage(workspace,id)),true)));
  }
  if(model.Data.AsArray().Count==0)Rows.Add(FieldUi.Text("No clients available."));
 }
}
public sealed class FieldProjectsPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;private readonly long? clientId;
 public FieldProjectsPage(InspectionWorkspace workspace,long? clientId=null):this(workspace,clientId,new FieldListViewModel(workspace)){}
 private FieldProjectsPage(InspectionWorkspace workspace,long? clientId,FieldListViewModel model):base("Projects",model){this.workspace=workspace;this.clientId=clientId;this.model=model;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();Rows.Clear();Heading("Projects","Projects");
  if(workspace.Permissions.Contains("ManageProjects"))Rows.Add(FieldUi.Action("+ Add Project",()=>Navigation.PushAsync(new FieldCreateResourcePage(workspace,true))));
  if(!await model.LoadAsync(workspace.ProjectsAsync))return;
  foreach(var project in model.Data!.AsArray().Where(p=>clientId is null||p?["clientID"]?.GetValue<long>()==clientId))
  {
   var id=project!["projectID"]!.GetValue<long>();Rows.Add(FieldUi.Card(FieldUi.Text(project["projectName"]!.ToString(),18,true),FieldUi.Text(project["projectNumber"]?.ToString()??""),FieldUi.Text(project["clientName"]?.ToString()??"Client unavailable"),FieldUi.Text(project["qcFormCount"] is null?"QC count unavailable":project["qcFormCount"]+" QC records"),FieldUi.Text(project["siteLocation"]?.ToString()??project["siteName"]?.ToString()??""),FieldUi.Badge(project["status"]?.ToString()??(project["isActive"]?.GetValue<bool>()==true?"Active":"Inactive")),FieldUi.Action("Open project / QC records",()=>Navigation.PushAsync(new FieldFormsPage(workspace,id,project)),true)));
  }
  if(model.Data.AsArray().Count==0)Rows.Add(FieldUi.Text("No accessible projects. Request project access from your administrator."));
 }
}
public sealed class FieldFormsPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;private readonly long projectId;private readonly JsonNode? project;
 public FieldFormsPage(InspectionWorkspace workspace,long projectId,JsonNode? project=null):this(workspace,projectId,project,new FieldListViewModel(workspace)){}
 private FieldFormsPage(InspectionWorkspace workspace,long projectId,JsonNode? project,FieldListViewModel model):base("Project QC records",model){this.workspace=workspace;this.projectId=projectId;this.project=project;this.model=model;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();Rows.Clear();Heading("Project",project?["projectName"]?.ToString()??"QC records");
  if(project is not null)Rows.Add(FieldUi.Card(FieldUi.Text(project["projectNumber"]?.ToString()??""),FieldUi.Text(project["contractOrderNumber"]?.ToString()??""),FieldUi.Text(project["siteLocation"]?.ToString()??"")));
  if(workspace.Permissions.Contains("ReadTemplates"))Rows.Add(FieldUi.Action("+ Start from Form Library",()=>Navigation.PushAsync(new FieldLibraryPage(workspace,projectId))));
  if(workspace.Permissions.Contains("ReadInspection"))Rows.Add(FieldUi.Action("Cable registers · MZT-QC-203",()=>Navigation.PushAsync(new FieldCableRegistersPage(workspace,projectId)),true));
  if(await model.LoadAsync(()=>workspace.FormsAsync(projectId)))
  {
   AddForms(model.Data!.AsArray());if(model.Data.AsArray().Count==0)Rows.Add(FieldUi.Text("No QC records available."));
   var page=1;Button? more=null;more=FieldUi.Action("Load more records",async()=>{page++;if(await model.LoadAsync(()=>workspace.CachedGetAsync("forms-page-"+page,projectId,$"api/qc-forms?projectId={projectId}&page={page}&pageSize=100"))){AddForms(model.Data!.AsArray());if(model.Data.AsArray().Count<100)more!.IsVisible=false;}else page--;},true);more.IsVisible=model.Data.AsArray().Count==100;Rows.Add(more);
  }
 }
 private void AddForms(JsonArray forms){foreach(var form in forms){var id=form!["qcFormID"]!.GetValue<long>();Rows.Add(FieldUi.Card(FieldUi.Text(form["formNumber"]!.ToString(),18,true),FieldUi.Badge(form["status"]!.ToString()),FieldUi.Text("Updated "+BusinessText.Date(form["updatedAt"]?.ToString())),FieldUi.Action("Open / resume",()=>Navigation.PushAsync(new FieldFormPage(workspace,id)),true)));}}
}
public sealed class FieldLibraryPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;private readonly ProjectSelection selection;private bool loading;
 public FieldLibraryPage(InspectionWorkspace workspace,long? projectId=null):this(workspace,projectId,new FieldListViewModel(workspace)){}
 private FieldLibraryPage(InspectionWorkspace workspace,long? projectId,FieldListViewModel model):base("Form Library",model){this.workspace=workspace;selection=new ProjectSelection(projectId);this.model=model;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();await LoadAsync();
 }
 private async Task LoadAsync()
 {
  if(loading||model.Busy)return;loading=true;
  try
  {
  Rows.Clear();Heading("Form Library","Start a QC record");
  Rows.Add(FieldUi.Action("Refresh projects and templates",LoadAsync,true));
  JsonArray? projects=null;await model.RunAsync(async()=>projects=(await workspace.ProjectsAsync()).AsArray());if(projects is null)return;
  selection.Refresh(projects.Select(p=>new ProjectChoice(p!["projectID"]!.GetValue<long>(),p["projectName"]!.ToString(),p["projectNumber"]?.ToString())));
  var selectedLabel=FieldUi.Text(selection.Selected is not null?"New QC records will use this project.":"Choose a project to enable Create Form.",12);
  var choices=new VerticalStackLayout{Spacing=8,IsVisible=false};
  var createButtons=new List<Button>();
  Button? selector=null;
  string SelectorText()=> (selection.Selected?.DisplayName??"Choose project")+(choices.IsVisible?"  \u25B4":"  \u25BE");
  selector=FieldUi.Action(SelectorText(),()=>{
   choices.IsVisible=!choices.IsVisible;selector!.Text=SelectorText();
   SemanticProperties.SetDescription(selector,choices.IsVisible?"Project dropdown expanded. Choose a project below.":"Project dropdown collapsed. "+(selection.Selected?.DisplayName??"No project selected."));
   return Task.CompletedTask;
  },true);
  selector.AutomationId="ProjectDropdown";
  foreach(var project in selection.Projects)
  {
   choices.Add(FieldUi.Action(project.DisplayName,()=>{
    selection.Select(project.Id);selectedLabel.Text="New QC records will use this project.";choices.IsVisible=false;selector.Text=SelectorText();
    SemanticProperties.SetDescription(selector,"Project dropdown. Selected: "+project.DisplayName);
    foreach(var button in createButtons)button.IsEnabled=workspace.Permissions.Contains("PerformInspection");
    model.Message="Project selected. Choose a checklist below.";return Task.CompletedTask;
   },true));
  }
  if(selection.Projects.Count==0){selector.IsEnabled=false;selectedLabel.Text="No accessible projects. Request project access from your administrator, then refresh.";}
  Rows.Add(FieldUi.Card(FieldUi.Text("PROJECT FOR THE NEW FORM",12,true),selector,selectedLabel,choices));
  if(!await model.LoadAsync(workspace.TemplatesAsync))return;
  if(!workspace.Permissions.Contains("PerformInspection"))Rows.Add(FieldUi.Text("You can browse templates; starting inspections requires explicit permission."));
  foreach(var template in model.Data!.AsArray().Where(t=>t?["isActive"]?.GetValue<bool>()==true&&t["activeVersionID"] is not null))
  {
   var create=FieldUi.Action("+ Create Form",async()=>{
    if(!workspace.Permissions.Contains("PerformInspection")){model.Message="Inspection creation is not permitted for this account.";return;}
    if(selection.Selected is not { } project){model.Message="Choose a project first.";return;}var id=await model.CreateFormAsync(project.Id,template!);if(id.HasValue)await Navigation.PushAsync(new FieldFormPage(workspace,id.Value));
   });
   create.IsEnabled=selection.Selected is not null&&workspace.Permissions.Contains("PerformInspection");createButtons.Add(create);
   Rows.Add(FieldUi.Card(FieldUi.Text(template!["templateNumber"]?.ToString()??"",12,true),FieldUi.Text(template["templateName"]!.ToString(),18,true),FieldUi.Text(template["subtitle"]?.ToString()??""),FieldUi.Text("Revision "+template["revision"]),create));
   var version=template["activeVersionID"]!.GetValue<long>();Rows.Add(FieldUi.Action("Save checklist for offline use",()=>model.RunAsync(async()=>{await workspace.TemplateAsync(version);},"Checklist saved on this device. You can start it offline with a saved accessible project."),true));
  }
  }
  finally{loading=false;}
 }
}
public sealed class FieldCreateResourcePage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;private readonly bool project;
 public FieldCreateResourcePage(InspectionWorkspace workspace,bool project):this(workspace,project,new FieldListViewModel(workspace)){}
 private FieldCreateResourcePage(InspectionWorkspace workspace,bool project,FieldListViewModel model):base(project?"New Project":"New Client",model)
 {this.workspace=workspace;this.model=model;this.project=project;}
 protected override async void OnAppearing()
 {
  base.OnAppearing();await LoadAsync();
 }
 private async Task LoadAsync()
 {
  if(Rows.Count>0)return;Heading(project?"Projects":"Clients",Title!);
  var fields=new Dictionary<string,Entry>();FieldChoice? client=null;JsonArray? clients=null;
  if(project)
  {
   if(!await model.RunAsync(async()=>clients=(await workspace.ClientsAsync()).AsArray())){Rows.Add(FieldUi.Action("Try again",async()=>{Rows.Clear();await LoadAsync();},true));return;}
   clients=new JsonArray(clients!.Where(c=>c?["isActive"]?.GetValue<bool>()==true).Select(c=>c!.DeepClone()).ToArray());
   var options=clients.Select(c=>(Value:c!["clientID"]!.ToString(),Label:c["clientName"]!.ToString()+(string.IsNullOrWhiteSpace(c["clientCode"]?.ToString())?"":" · "+c["clientCode"]))).ToArray();
   client=new FieldChoice("Choose active client",options){AutomationId="NewProjectClientDropdown",IsEnabled=options.Length>0};
   Rows.Add(FieldUi.Card(FieldUi.Text("CLIENT *",12,true),client));
   if(options.Length==0)Rows.Add(FieldUi.Warning("No active clients are available. Add or activate a client before creating a project."));
  }
  foreach(var (key,label) in project?new[]{("projectName","Project name *"),("projectNumber","Project number"),("contractOrderNumber","Contract / order number"),("siteName","Site name"),("siteLocation","Area / location"),("description","Description")}:new[]{("clientName","Client name *"),("clientCode","Client code"),("contactName","Contact name"),("contactEmail","Contact email"),("contactPhone","Contact phone")}){var entry=FieldUi.Entry(label);fields[key]=entry;Rows.Add(FieldUi.Card(FieldUi.Text(label,12,true),entry));}
  var active=new Switch{IsToggled=true};if(!project)Rows.Add(FieldUi.Card(FieldUi.Text("Active"),active));
  Rows.Add(FieldUi.Action("Save",async()=>{
   var values=new JsonObject();foreach(var (key,entry) in fields)values[key]=string.IsNullOrWhiteSpace(entry.Text)?null:entry.Text.Trim();
   if(project){var selected=clients!.FirstOrDefault(c=>c?["clientID"]?.ToString()==client!.SelectedValue);if(selected is null){model.Message="Choose an active client.";return;}values["clientID"]=selected["clientID"]!.GetValue<long>();}
   else values["isActive"]=active.IsToggled;
   var success=project?await model.CreateProjectAsync(values):await model.CreateClientAsync(values);if(success)await Navigation.PopAsync();
  }));Rows.Add(FieldUi.Text("Connect to create a new entry. If the connection is interrupted, check the register before trying again."));
 }
}
