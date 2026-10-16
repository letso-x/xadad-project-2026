using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldForgotPasswordPage:FieldScreen
{
 public FieldForgotPasswordPage(PasswordRecovery recovery):this(recovery,new FieldViewModel()){}
 private FieldForgotPasswordPage(PasswordRecovery recovery,FieldViewModel model):base("Reset password",model,showMenu:false)
 {
  Heading("Account recovery","Forgot password?");Rows.Add(FieldUi.Text("Enter your email. Open the latest reset link on this same device within 30 minutes."));
  var email=FieldUi.Entry("Email",Keyboard.Email);email.IsTextPredictionEnabled=false;Rows.Add(email);
  var send=FieldUi.Action("Send reset link",async()=>{await model.RunAsync(async()=>model.Message=await recovery.RequestAsync(email.Text??""));});Rows.Add(send);
  model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy)){send.IsEnabled=!model.Busy;email.IsEnabled=!model.Busy;}};
 }
}
public sealed class FieldPasswordRecoveryPage:FieldScreen
{
 private readonly PasswordRecovery recovery;private readonly Uri callback;private readonly Func<bool,Task> finish;private readonly FieldViewModel model;
 private readonly Entry password=FieldUi.Entry("New password"),confirm=FieldUi.Entry("Confirm password");private readonly VerticalStackLayout form=new(){Spacing=12};private bool loaded;
 public FieldPasswordRecoveryPage(PasswordRecovery recovery,Uri callback,Func<bool,Task> finish):this(recovery,callback,finish,new FieldViewModel()){}
 private FieldPasswordRecoveryPage(PasswordRecovery recovery,Uri callback,Func<bool,Task> finish,FieldViewModel model):base("Change password",model,showMenu:false)
 {
  this.recovery=recovery;this.callback=callback;this.finish=finish;this.model=model;NavigationPage.SetHasBackButton(this,false);
  Heading("Account recovery","Change password");Rows.Add(FieldUi.Text("Use 12–128 characters. Your authenticator will still be required at sign-in if it is already enabled."));
  password.IsPassword=confirm.IsPassword=true;password.IsTextPredictionEnabled=confirm.IsTextPredictionEnabled=false;password.MaxLength=confirm.MaxLength=128;
  form.IsVisible=false;form.Add(password);form.Add(confirm);form.Add(FieldUi.Action("Update password",async()=>{
   if(await model.RunAsync(async()=>model.Message=await recovery.UpdatePasswordAsync(password.Text??"",confirm.Text??""))){password.Text=confirm.Text="";await finish(true);}
  }));Rows.Add(form);
  var cancel=FieldUi.Action("Return to sign in",async()=>{password.Text=confirm.Text="";await recovery.CancelAsync();await finish(false);},true);Rows.Add(cancel);
  model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy)){form.IsEnabled=!model.Busy;cancel.IsEnabled=!model.Busy;}};
 }
 protected override async void OnAppearing(){base.OnAppearing();if(loaded)return;loaded=true;if(await model.RunAsync(()=>recovery.AcceptCallbackAsync(callback)))form.IsVisible=recovery.CanChangePassword;}
 protected override bool OnBackButtonPressed()=>true;
}