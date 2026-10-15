using MzukuluQMSMobile.Services;
using MzukuluQMSMobile.ViewModels;
using MzukuluQMS.Mobile.Core;
namespace MzukuluQMSMobile.Pages;
public sealed class FieldNotificationsPage:FieldScreen
{
 private readonly InspectionWorkspace workspace;private readonly FieldListViewModel model;
 public FieldNotificationsPage(InspectionWorkspace w):this(w,new FieldListViewModel(w)){}
 private FieldNotificationsPage(InspectionWorkspace w,FieldListViewModel m):base("Notifications",m){workspace=w;model=m;}
 protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
 private async Task LoadAsync()
 {
  Rows.Clear();Heading("Your work","Notifications");Rows.Add(FieldUi.Action("Refresh",LoadAsync,true));if(!await model.LoadAsync(workspace.NotificationsAsync))return;
  foreach(var n in model.Data!.AsArray())
  {
   var form=n!["qcFormID"]!.GetValue<long>();var notification=n["notificationID"]!.GetValue<long>();
   var card=new VerticalStackLayout{Spacing=10};card.Add(FieldUi.Text(n["kind"]?.ToString()=="NCRCreated"?"Inspection item needs corrective action":"QC record returned for correction",16,true));
   card.Add(FieldUi.Text(n["formNumber"]?.ToString()??"QC record"));card.Add(FieldUi.Text(n["projectName"]?.ToString()??""));card.Add(FieldUi.Text(BusinessText.Date(n["createdAt"]?.ToString())));card.Add(FieldUi.Badge(n["readAt"] is null?"Unread":"Read"));
   card.Add(FieldUi.Action("Open QC record",()=>Navigation.PushAsync(new FieldFormPage(workspace,form)),true));
   if(n["readAt"] is null)card.Add(FieldUi.Action("Mark as read",async()=>{if(await model.RunAsync(async()=>{await workspace.ReadNotificationAsync(notification);}))await LoadAsync();},true));Rows.Add(FieldUi.Card(card));
  }
  if(model.Data.AsArray().Count==0)Rows.Add(FieldUi.Text("No notifications for your accessible QC records."));
 }
}
