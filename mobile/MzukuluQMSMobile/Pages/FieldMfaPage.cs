using MzukuluQMS.Mobile.Core;
using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldMfaPage : FieldScreen
{
 private readonly FieldSession session;private readonly InspectionWorkspace workspace;private readonly Func<Task> complete;private readonly bool backup;private readonly FieldViewModel model;
 private readonly List<(Button Button,Func<bool>? Enabled)> actions=[];
 private readonly Entry code=FieldUi.Entry("6-digit code",Keyboard.Numeric);
 private FieldChoice? factorChoice;private Entry? secret;private WebView? qr;private VerticalStackLayout? verification;private Border? setupCard;private string? selected,copiedKey;private bool loaded;
 public FieldMfaPage(FieldSession session,InspectionWorkspace workspace,Func<Task> complete,bool backup=false):this(session,workspace,complete,backup,new FieldViewModel()){}
 private FieldMfaPage(FieldSession session,InspectionWorkspace workspace,Func<Task> complete,bool backup,FieldViewModel model):base(backup?"Add authenticator":"Two-step verification",model,showMenu:false)
 {
  this.session=session;this.workspace=workspace;this.complete=complete;this.backup=backup;this.model=model;
  NavigationPage.SetHasBackButton(this,false);
  code.MaxLength=6;code.FontSize=22;code.IsTextPredictionEnabled=false;SemanticProperties.SetDescription(code,"Six-digit verification code from your authenticator app");
  code.TextChanged+=(_,e)=>{var clean=new string((e.NewTextValue??"").Where(c=>c>='0'&&c<='9').Take(6).ToArray());if(code.Text!=clean)code.Text=clean;UpdateEnabled();};
  code.Completed+=async(_,_)=>{if(!model.Busy&&selected is not null)await model.RunAsync(VerifyCoreAsync);};
  model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FieldViewModel.Busy))UpdateEnabled();};
 }
 protected override async void OnAppearing(){base.OnAppearing();if(loaded)return;loaded=true;if(!await model.RunAsync(LoadAsync))RenderRetry();}
 private void UpdateEnabled(){foreach(var item in actions)item.Button.IsEnabled=!model.Busy&&(item.Enabled?.Invoke()??true);code.IsEnabled=!model.Busy&&selected is not null;if(factorChoice is not null)factorChoice.IsEnabled=!model.Busy;}
 private Button Action(string label,Func<Task> action,Func<bool>? enabled=null,bool secondary=false)
 {
  var button=new Button{Text=label,MinimumHeightRequest=48,CornerRadius=7,Padding=new Thickness(12,8),BackgroundColor=secondary?Colors.White:FieldUi.Navy,TextColor=secondary?FieldUi.Navy:Colors.White,BorderColor=FieldUi.Line,BorderWidth=secondary?1:0};
  SemanticProperties.SetDescription(button,label);actions.Add((button,enabled));
  button.Clicked+=async(_,_)=>{if(!button.IsEnabled||model.Busy)return;await model.RunAsync(action);UpdateEnabled();};UpdateEnabled();return button;
 }
 private async Task LoadAsync()
 {
  if(!session.HasPendingSignIn)throw new InvalidOperationException("Sign in before setting up your authenticator.");
  var factors=await session.LoadFactorsAsync();actions.Clear();Rows.Clear();factorChoice=null;selected=null;code.Text="";
  var enrolling=backup||factors.Count==0;Title=enrolling?"Secure your account":"Two-step verification";
  Heading("Account security",Title);
  var loading=new ActivityIndicator{Color=FieldUi.Navy,HeightRequest=24};loading.SetBinding(ActivityIndicator.IsRunningProperty,nameof(FieldViewModel.Busy));loading.SetBinding(ActivityIndicator.IsVisibleProperty,nameof(FieldViewModel.Busy));Rows.Add(loading);
  if(backup&&!session.IsMfaVerified)throw new InvalidOperationException("Complete two-step verification before adding a backup authenticator.");
  if(enrolling)
  {
   Rows.Add(FieldUi.Text("Set up an authenticator app to protect your account."));
   Rows.Add(Action("Set up authenticator",async()=>{
    AuthenticatorSetup setup;
    try{setup=await session.EnrollAsync(backup);}
    catch(AuthenticatorAlreadyEnrolledException){if(backup)await complete();else await LoadAsync();return;}
    await ShowSetupAsync(setup);
   },()=>session.Setup is null));
   setupCard=FieldUi.Card();setupCard.IsVisible=false;Rows.Add(setupCard);
  }
  else
  {
   Rows.Add(FieldUi.Text("Enter the 6-digit code from your authenticator app."));selected=factors[0].Id;
   if(factors.Count>1){factorChoice=new FieldChoice("Choose authenticator",factors.Select(f=>(f.Id,f.Name)).ToArray(),selected);var choice=factorChoice;choice.SelectionChanged+=async(_,_)=>{if(model.Busy)return;selected=choice.SelectedValue;code.Text="";await model.RunAsync(async()=>{await session.ChallengeAsync(selected!);model.Message="Enter the current code from the selected authenticator.";});};Rows.Add(FieldUi.Card(FieldUi.Text("AUTHENTICATOR APP",11,true),choice));}
  }
  verification=new VerticalStackLayout{Spacing=12,IsVisible=!enrolling};
  if(enrolling)verification.Add(FieldUi.Text("Enter the 6-digit code from your authenticator app."));
  verification.Add(FieldUi.Card(FieldUi.Text("VERIFICATION CODE",11,true),code));
  verification.Add(Action(enrolling?"Verify and continue":"Verify",VerifyCoreAsync,()=>selected is not null&&((code.Text?.Length??0)==6||session.IsMfaVerified&&session.Setup is null)));
  verification.Add(Action("Request a new verification attempt",async()=>{await session.ChallengeAsync(selected!);code.Text="";model.Message="Enter the current code from your authenticator app.";code.Focus();},()=>selected is not null,true));Rows.Add(verification);
  Rows.Add(Action(backup?"Cancel":"Cancel sign-in",CancelAsync,secondary:true));UpdateEnabled();
  if(enrolling&&session.Setup is { } pending)await ShowSetupAsync(pending);
  else if(!enrolling){await session.ChallengeAsync(selected!);code.Focus();}
 }
 private async Task ShowSetupAsync(AuthenticatorSetup setup)
 {
  selected=setup.FactorId;var area=new VerticalStackLayout{Spacing=12};setupCard!.Content=area;setupCard.IsVisible=true;verification!.IsVisible=true;
  area.Add(FieldUi.Text("Add this account to your authenticator app, then return here and enter its 6-digit code."));
  area.Add(Action("Open authenticator app",async()=>{
   var current=session.Setup;if(current is null)return;
   model.Message=await AuthenticatorConvenience.OpenAsync(current,new PlatformAuthenticatorLauncher())?"Return here and enter the code from your authenticator app.":AuthenticatorConvenience.Fallback;
  },()=>session.Setup is not null));
  var qrArea=new VerticalStackLayout{Spacing=8,IsVisible=false};
  area.Add(Action("Show QR code",()=>{var current=session.Setup;if(current is not null&&qr is null)AddQr(current,qrArea);qrArea.IsVisible=true;return Task.CompletedTask;},()=>session.Setup is not null,true));area.Add(qrArea);
  area.Add(Action("Copy setup key",CopySetupKeyAsync,()=>session.Setup is not null,true));
  area.Add(FieldUi.Text("Add this key manually in your authenticator app if automatic setup is unavailable."));
  var manual=new VerticalStackLayout{Spacing=8,IsVisible=false};secret=FieldUi.Entry("");secret.IsReadOnly=true;
  area.Add(Action("Show setup key",()=>{secret.Text=session.Setup?.Secret??"";manual.IsVisible=true;return Task.CompletedTask;},()=>session.Setup is not null,true));
  manual.Add(FieldUi.Text("Setup key",12,true));manual.Add(secret);
  manual.Add(Action("Hide setup key",()=>{secret.Text="";manual.IsVisible=false;return Task.CompletedTask;},secondary:true));
  manual.Add(FieldUi.Text("Keep this key private. It is not saved by this application.",12));area.Add(manual);
  UpdateEnabled();await session.ChallengeAsync(selected);await Scroller.ScrollToAsync(setupCard,ScrollToPosition.Start,true);
 }
 private async Task CopySetupKeyAsync()
 {
  var key=session.Setup?.Secret;if(string.IsNullOrEmpty(key))return;
  try{await Clipboard.Default.SetTextAsync(key);copiedKey=key;model.Message="Setup key copied. Keep it private.";}
  catch(Exception error) when(error is not OutOfMemoryException){model.Message="The setup key could not be copied. Use Show setup key to enter it manually.";}
 }
 private void AddQr(AuthenticatorSetup setup,VerticalStackLayout area)
 {
  area.Add(FieldUi.Text("Scan this QR code with your authenticator app."));
  try
  {
   var html=AuthenticatorQr.Html(setup.QrCode);
   qr=new WebView{HeightRequest=280,WidthRequest=280,HorizontalOptions=LayoutOptions.Center,BackgroundColor=Colors.White};
   qr.Navigating+=(_,e)=>{if(!e.Url.StartsWith("about:blank",StringComparison.OrdinalIgnoreCase)&&!e.Url.StartsWith("data:text/html",StringComparison.OrdinalIgnoreCase))e.Cancel=true;};
   var imageError=FieldUi.Text("The setup image could not load. Copy the setup key to continue.");imageError.IsVisible=false;
   qr.Navigated+=(_,e)=>imageError.IsVisible=e.Result!=WebNavigationResult.Success;
   qr.Source=new HtmlWebViewSource{Html=html};area.Add(qr);area.Add(imageError);
  }
  catch(Exception error) when(error is InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException)
  {qr=null;area.Add(FieldUi.Text("The setup image is unavailable. Copy or reveal the setup key to continue."));}
 }
 private async Task VerifyCoreAsync()
 {
  if(selected is null){model.Message="Set up or choose an authenticator app first.";return;}
  try{await session.VerifyMfaAsync(selected,code.Text??"");await ClearSecretAsync();await complete();}
  finally{code.Text="";if(session.Setup is null)await ClearSecretAsync();}
 }
 private void RenderRetry(){if(!Rows.Contains(Status))Heading("Account security",Title);Rows.Add(Action("Try again",LoadAsync));Rows.Add(Action(backup?"Cancel":"Cancel sign-in",CancelAsync,secondary:true));}
 private async Task CancelAsync(){await ClearSecretAsync();if(!backup){await session.LogoutAsync();await workspace.DisposeAsync();}await Navigation.PopAsync();}
 private async Task ClearSecretAsync()
 {
  if(secret is not null)secret.Text="";try{if(qr is not null)qr.Source=new HtmlWebViewSource{Html=""};}catch(System.Runtime.InteropServices.COMException){}session.ClearSetupSecret();
  var copied=copiedKey;copiedKey=null;
  if(copied is not null){try{if(await Clipboard.Default.GetTextAsync()==copied)await Clipboard.Default.SetTextAsync("");}catch{/* Clipboard cleanup must not prevent logout or navigation. */}}
 }
 // Switching to an authenticator must not destroy the active enrollment or create another factor on return.
 protected override void OnDisappearing(){base.OnDisappearing();}
 protected override bool OnBackButtonPressed()=>true;
}

public sealed class FieldSecurityPage : FieldScreen
{
 private readonly FieldSession session;private readonly InspectionWorkspace workspace;private readonly FieldViewModel model;
 public FieldSecurityPage(FieldSession session,InspectionWorkspace workspace):this(session,workspace,new FieldViewModel()){}
 private FieldSecurityPage(FieldSession session,InspectionWorkspace workspace,FieldViewModel model):base("Account & security",model){this.session=session;this.workspace=workspace;this.model=model;}
 protected override async void OnAppearing(){base.OnAppearing();await model.RunAsync(async()=>{
  var factors=await session.LoadFactorsAsync();Rows.Clear();Heading("Account security","Two-step verification");
  if(!session.IsMfaVerified){Rows.Add(FieldUi.Warning("Complete two-step verification before continuing."));Rows.Add(FieldUi.Action("Complete verification",()=>Navigation.PushAsync(new FieldMfaPage(session,workspace,async()=>{await workspace.LoadPermissionsAsync();if(Window is not null)Window.Page=new FieldHomePage(session,workspace);} ))));return;}
  Rows.Add(FieldUi.Card(FieldUi.Text("Enabled",18,true),FieldUi.Text("An authenticator app is required to access your QC work.")));
  foreach(var factor in factors)Rows.Add(FieldUi.Card(FieldUi.Text(factor.Name,16,true),FieldUi.Text("Verified authenticator app")));
  Rows.Add(FieldUi.Action("Add backup authenticator",()=>Navigation.PushAsync(new FieldMfaPage(session,workspace,()=>Navigation.PopAsync(),true))));
  Rows.Add(FieldUi.Text("Keep a backup authenticator in case your primary app is unavailable. If you lose all authenticators, contact your administrator."));Rows.Add(FieldUi.Text("Removing authenticators requires administrator assistance. Two-step verification cannot be disabled here."));
 });}
}
