using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldLoginPage : FieldScreen
{
 private readonly FieldSession session;private readonly InspectionWorkspace workspace;private readonly FieldLoginViewModel model;
 private bool entering;
 public FieldLoginPage(FieldSession session,InspectionWorkspace workspace,PasswordRecovery recovery):this(session,workspace,recovery,new FieldLoginViewModel(session)){}
 private FieldLoginPage(FieldSession session,InspectionWorkspace workspace,PasswordRecovery recovery,FieldLoginViewModel model):base("Mzukulu QC",model)
 {
  this.session=session;this.workspace=workspace;this.model=model;BackgroundColor=FieldUi.Navy;
  var email=FieldUi.Entry("Email",Keyboard.Email);email.SetBinding(Entry.TextProperty,nameof(model.Email));
  var password=FieldUi.Entry("Password");password.IsPassword=true;password.SetBinding(Entry.TextProperty,nameof(model.Password));
  Rows.Add(new BoxView{HeightRequest=60,Color=Colors.Transparent});
  Rows.Add(FieldUi.Section("MZUKULU QC · QUALITY CONTROL"));
  Rows.Add(FieldUi.Card(FieldUi.Category("Field access"),FieldUi.Text("Sign in",28,true),FieldUi.Text("Use your linked QMS account to access authorised projects."),email,password,
   FieldUi.Action("Sign in",async()=>{if(await model.LoginAsync())await EnterAsync();}),FieldUi.Action("Forgot password?",()=>Navigation.PushAsync(new FieldForgotPasswordPage(recovery)),true),Status));
 }
 protected override async void OnAppearing()
 {
  base.OnAppearing();
#if WINDOWS
  MzukuluQMSMobile.WinUI.StartupTrace.Mark("Login OnAppearing begin");
#endif
  if(entering||RecoveryLinks.Active)return;await model.RunAsync(async()=>{if(await model.RestoreAsync())await EnterAsync();});
#if WINDOWS
  MzukuluQMSMobile.WinUI.StartupTrace.Mark("Login OnAppearing complete");
#endif
 }
 private async Task EnterAsync()
 {
  if(entering||RecoveryLinks.Active)return;entering=true;
  try{if(!session.IsMfaVerified){if(session.HasPendingSignIn)await Navigation.PushAsync(new FieldMfaPage(session,workspace,OpenHomeAsync));return;}await OpenHomeAsync();}
  finally{entering=false;}
 }
 public void SetNotice(string notice)=>model.Message=notice;
 private async Task OpenHomeAsync(){if(RecoveryLinks.Active)return;if(!session.IsMfaVerified)throw new InvalidOperationException("Complete two-step verification to continue.");await workspace.LoadPermissionsAsync();if(RecoveryLinks.Active)return;if(Window is not null)Window.Page=new FieldHomePage(session,workspace);}
}
