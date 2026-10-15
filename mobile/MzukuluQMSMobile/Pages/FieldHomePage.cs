using System.Text.Json.Nodes;
using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldHomePage : FlyoutPage
{
 public FieldHomePage(FieldSession session,InspectionWorkspace workspace)
 {
  Title="Mzukulu QC";
  var menu=new VerticalStackLayout{Padding=18,Spacing=12};menu.Add(FieldUi.Section("Mzukulu QC"));menu.Add(FieldUi.Category("Operations"));
  void Add(string name,Func<Page> factory){menu.Add(FieldUi.Action(name,()=>{Detail=new NavigationPage(factory()){BarBackgroundColor=FieldUi.Navy,BarTextColor=Colors.White};IsPresented=false;return Task.CompletedTask;},true));}
  if(workspace.Permissions.Contains("ReadReports"))Add("Dashboard",()=>new FieldDashboardPage(workspace));
  if(workspace.Permissions.Contains("ReadClients"))Add("Clients",()=>new FieldClientsPage(workspace));
  if(workspace.Permissions.Contains("ReadProjects"))Add("Projects",()=>new FieldProjectsPage(workspace));
  if(workspace.Permissions.Contains("ReadTemplates"))Add("Form Library",()=>new FieldLibraryPage(workspace));
  if(workspace.Permissions.Contains("ReadInspection"))Add("Notifications",()=>new FieldNotificationsPage(workspace));
  Add("Sync & conflicts",()=>new FieldSyncPage(workspace));
  if(workspace.Permissions.Contains("ManageTemplates"))Add("Checklist Builder",()=>new FieldTemplatesPage(workspace));
  Add("Account & security",()=>new FieldSecurityPage(session,workspace));
  menu.Add(FieldUi.Action("Sign out",async()=>{await workspace.DisposeAsync();await session.LogoutAsync();if(Window is not null)Window.Page=new NavigationPage(new FieldLoginPage(session,workspace));},true));
  Flyout=new ContentPage{Title="Mzukulu QC",BackgroundColor=FieldUi.Background,Content=new ScrollView{Content=menu}};
  Detail=new NavigationPage(workspace.Permissions.Contains("ReadReports")?new FieldDashboardPage(workspace):new FieldProjectsPage(workspace)){BarBackgroundColor=FieldUi.Navy,BarTextColor=Colors.White};
 }
}
public sealed class FieldDashboardPage : FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;
 public FieldDashboardPage(InspectionWorkspace workspace):this(workspace,new FieldListViewModel(workspace)){}
 private FieldDashboardPage(InspectionWorkspace workspace,FieldListViewModel model):base("Dashboard",model){this.workspace=workspace;this.model=model;}
 protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
 private async Task LoadAsync()
 {
  Rows.Clear();Heading("Overview","QC Control Centre");
  if(await model.LoadAsync(workspace.DashboardAsync))
  {
   Rows.Add(FieldUi.Text("As of "+BusinessText.Date(model.Data!["asOf"]?.ToString())));
   foreach(var (key,label) in new[]{("totalProjects","Accessible projects"),("activeProjects","Active projects"),("formsInProgress","Forms in progress"),("totalQcRecords","Total QC records"),("openNcrCount","Requiring attention / NCR"),("clients","Clients in accessible projects"),("activeTemplates","Active checklist templates"),("approvedForms","Approved forms")})
    Rows.Add(FieldUi.Card(FieldUi.Text(model.Data[key]?.ToString()??"Unavailable",26,true),FieldUi.Text(label)));
   var counts=await (await workspace.StoreAsync()).CountsAsync();Rows.Add(FieldUi.Card(FieldUi.Text((counts.GetValueOrDefault("Pending")+counts.GetValueOrDefault("Processing")).ToString(),26,true),FieldUi.Text("Pending sync")));
   Rows.Add(FieldUi.Section("Recent QC activity"));
   foreach(var form in model.Data["recentActivity"]!.AsArray()){var id=form!["qcFormID"]!.GetValue<long>();Rows.Add(FieldUi.Card(FieldUi.Text(form["formNumber"]!.ToString(),16,true),FieldUi.Badge(form["status"]!.ToString()),FieldUi.Action("Open record",()=>Navigation.PushAsync(new FieldFormPage(workspace,id)),true)));}
  }
  Rows.Add(FieldUi.Section("Quick actions"));Rows.Add(FieldUi.Action("Projects",()=>Navigation.PushAsync(new FieldProjectsPage(workspace))));Rows.Add(FieldUi.Action("Form Library",()=>Navigation.PushAsync(new FieldLibraryPage(workspace)),true));Rows.Add(FieldUi.Action("Sync & conflicts",()=>Navigation.PushAsync(new FieldSyncPage(workspace)),true));
 }
}
