using MzukuluQMSMobile.Services;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldLoginPage : ContentPage
{
 private readonly FieldSession session;private readonly InspectionWorkspace workspace;
 private readonly Entry email=new(){Placeholder="Email",Keyboard=Keyboard.Email};private readonly Entry password=new(){Placeholder="Password",IsPassword=true};
 private readonly Label message=new();private bool busy;
 public FieldLoginPage(FieldSession session,InspectionWorkspace workspace)
 {
  this.session=session;this.workspace=workspace;Title="Mzukulu QMS";
  var login=new Button{Text="Sign in"};login.Clicked+=async(_,_)=>{if(busy)return;busy=true;login.IsEnabled=false;try{await session.LoginAsync(email.Text??"",password.Text??"");password.Text="";await EnterAsync();}catch(Exception){message.Text="Unable to sign in. Check your credentials, linked QMS account and API connection.";}finally{busy=false;login.IsEnabled=true;}};
  Content=new VerticalStackLayout{Padding=24,Spacing=16,Children={new Label{Text="Field inspections",FontSize=28},email,password,login,message}};
 }
 protected override async void OnAppearing(){base.OnAppearing();try{if(await session.RestoreAsync())await EnterAsync();}catch{message.Text="Saved session could not be restored. Local work is retained.";}}
 private async Task EnterAsync(){var page=new FieldProjectsPage(session,workspace);Navigation.InsertPageBefore(page,this);await Navigation.PopAsync();}
}
public sealed class FieldProjectsPage : ContentPage
{
 private readonly FieldSession session;private readonly InspectionWorkspace workspace;private readonly VerticalStackLayout rows=new(){Spacing=12,Padding=20};private readonly Label status=new();
 public FieldProjectsPage(FieldSession session,InspectionWorkspace workspace)
 {
  this.session=session;this.workspace=workspace;Title="Projects";Content=new ScrollView{Content=rows};
  ToolbarItems.Add(new ToolbarItem("Sync",null,async()=>{try{status.Text=await workspace.SyncAsync();}catch{status.Text="Sync unavailable; work retained.";}}));
  ToolbarItems.Add(new ToolbarItem("Sign out",null,async()=>{await workspace.DisposeAsync();session.Logout();var login=new FieldLoginPage(session,workspace);Navigation.InsertPageBefore(login,this);await Navigation.PopAsync();}));
 }
 protected override async void OnAppearing(){base.OnAppearing();try{rows.Clear();rows.Add(status);var projects=await workspace.ProjectsAsync();foreach(var project in projects.AsArray()){var id=project!["projectID"]!.GetValue<long>();var button=new Button{Text=project["projectName"]!.GetValue<string>()};button.Clicked+=async(_,_)=>await Navigation.PushAsync(new FieldFormsPage(workspace,id));rows.Add(button);}if(rows.Count==1)rows.Add(new Label{Text="No accessible projects are cached or available."});}catch{status.Text="Unable to load projects. Check your session, permissions and API connection.";}}
}
public sealed class FieldFormsPage : ContentPage
{
 private readonly InspectionWorkspace workspace;private readonly long projectId;private readonly VerticalStackLayout rows=new(){Padding=20,Spacing=12};
 public FieldFormsPage(InspectionWorkspace workspace,long projectId){this.workspace=workspace;this.projectId=projectId;Title="Inspection records";Content=new ScrollView{Content=rows};}
 protected override async void OnAppearing(){base.OnAppearing();try{rows.Clear();var forms=await workspace.FormsAsync(projectId);foreach(var form in forms.AsArray()){var id=form!["qcFormID"]!.GetValue<long>();var button=new Button{Text=form["formNumber"]+" · "+form["status"]};button.Clicked+=async(_,_)=>await Navigation.PushAsync(new FieldFormPage(workspace,id));rows.Add(button);}if(rows.Count==0)rows.Add(new Label{Text="No inspection records available. Start inspections through the authorised API client."});}catch{rows.Add(new Label{Text="Unable to load records. Cached work is retained."});}}
}
