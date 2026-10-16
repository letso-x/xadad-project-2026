using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;
namespace MzuApplication.Views
{
 public class FormFillView:ViewBase
 {
  private readonly string id;private QcForm form;private string error="Loading QC record…";private bool loading,busy,conflict;
  private Dictionary<string,object> pending=new Dictionary<string,object>();
  private readonly Timer timer=new Timer{Interval=30000};
  public FormFillView(MainForm shell,string formId):base(shell){id=formId;HandleCreated+=async(s,e)=>{if(loading)return;loading=true;await LoadAsync();};timer.Tick+=async(s,e)=>{if(pending.Count>0&&!busy&&!conflict)await SaveAsync();};timer.Start();}
  public override string RouteKey{get{return "library";}}
  private async Task LoadAsync()
  {
   try
   {
    var remote=await FormData.LoadAsync(id);var cached=LocalDrafts.Read(id);form=remote;
    if(cached!=null){pending=Json.Map(Json.Get(cached,"values"));if(pending.Count>0){var baseline=Json.Id(cached,"revision");conflict=baseline!=form.Revision;form.Revision=baseline;}}
    error=conflict?"Saved local changes refer to an older server revision. Review them before discarding or reconciling.":pending.Count>0?"Restored unsent changes. Save to send them to the service.":"";
   }
   catch(Exception ex){form=null;error="Could not load this record or its encrypted draft. Any existing draft is retained. "+ex.Message;}
   if(!IsDisposed)Rebuild();
  }
  protected override void BuildContent()
  {
   if(form==null){AddPageHeader("QC RECORD","QC record",error);return;}
   var project=Repository.GetProject(form.ProjectId);
   AddCrumbs(project==null?"Projects":project.Name,()=>Shell.Navigate(new ProjectDetailView(Shell,form.ProjectId)),form.QcRecordNo);
   AddPageHeader("QC RECORD",form.QcRecordNo,form.ServerStatus+(error.Length>0?" · "+error:""));
   Buttons(new[]{"Save Draft","Submit","Refresh","Export CSV","Back to Project"},new Action[]{async()=>await SaveAsync(),async()=>await ActionAsync("/submit"),async()=>await RefreshAsync(),()=>ExportCsv(),()=>Shell.Navigate(new ProjectDetailView(Shell,form.ProjectId))});
   Group("PROJECT INFORMATION",body=>
   {
    if(project!=null)body.Controls.Add(Ui.Body(project.Name+" · "+project.Number+" · "+project.Site));
    foreach(var field in Json.Rows(form.RemoteDetails,"projectFields"))AddField(body,field,true);
   });
   var snapshot=Json.Get(form.RemoteDetails,"documentSnapshot");
   Group("SCOPE & ACCEPTANCE CRITERIA",body=>{body.Controls.Add(Ui.Wrapped(Json.Text(snapshot,"scope"),Theme.Small,Theme.Navy950,InnerWidth-60));body.Controls.Add(Ui.Wrapped(Json.Text(snapshot,"acceptanceCriteria"),Theme.SmallBold,Theme.Navy950,InnerWidth-60));});
   // Always render the QC execution snapshot. Never substitute a newer live template.
   foreach(var section in Json.Rows(form.RemoteDetails,"sections"))Group(Json.Text(section,"sectionName"),body=>{foreach(var field in Json.Rows(section,"fields"))AddField(body,field,false);});
   Group("REMARKS / OBSERVATIONS / DEVIATIONS",body=>
   {
    var remarks=new TextInput{Multiline=true,Height=Dpi.S(80),Width=InnerWidth-Dpi.S(60),Value=Convert.ToString(Value("remarks",form.Remarks))};remarks.ReadOnlyValue=!Writable();remarks.ValueChanged+=(s,e)=>Changed("remarks",remarks.Value,false);body.Controls.Add(remarks);
   });
   Group("SIGN-OFF",body=>
   {
    body.Controls.Add(Ui.Wrapped("Submit first, then sign with your own account. Historical signatures remain on the server; approval checks current signatures.",Theme.Small,Theme.Slate700,InnerWidth-60));
    var capacities=Json.Get(form.RemoteDetails,"requiredSignOffTypes") as object[]??new object[0];
    foreach(var capacity in capacities){var name=Convert.ToString(capacity);var sign=new FlatButton{Text="Sign as "+name,Width=220,Enabled=form.ServerStatus=="Submitted"};sign.Click+=async(s,e)=>await SignAsync(name,null);body.Controls.Add(sign);}
    var history=new FlatButton{Text="Sign-off history",Width=220};history.Click+=async(s,e)=>await ShowHistoryAsync();body.Controls.Add(history);
   });
   Buttons(new[]{"Approve","Return for correction","NCRs"},new Action[]{async()=>await ActionAsync("/approve"),()=>ReturnForCorrection(),()=>Shell.Navigate(new NcrView(Shell))});
  }
  private bool Writable(){return form.ServerStatus=="Draft"||form.ServerStatus=="InProgress"||form.ServerStatus=="ReturnedForCorrection";}
  private object Value(string key,object original){object value;return pending.TryGetValue(key,out value)?value:original;}
  private void Changed(string key,object value,bool immediate)
  {
   pending[key]=value;
   try{LocalDrafts.Save(id,form.Revision,pending);}catch(Exception ex){Notify("Could not cache unsent input: "+ex.Message,true);}
   if(immediate&&!busy&&!conflict)SaveOutcome();
  }
  private async void SaveOutcome(){await SaveAsync();}
  private void AddField(FlowLayoutPanel body,Dictionary<string,object> field,bool header)
  {
   var key=(header?"header:":"response:")+Json.Text(field,header?"qcFormProjectFieldID":"qcResponseID");
   body.Controls.Add(Ui.Wrapped(Json.Text(field,"fieldLabel")+(Json.Bool(field,"isRequired")?" *":""),Theme.SmallBold,Theme.Navy950,InnerWidth-60));
   var type=Json.Text(field,"fieldType");
   if(type!="Photo"&&type!="Signature")
   {
    var editor=ApiFieldEditor.Create(field,Value(key,Json.Get(field,"value")),value=>Changed(key,value,type=="Dropdown"));editor.Width=Math.Min(editor.Width,InnerWidth-Dpi.S(60));editor.Enabled=Writable();body.Controls.Add(editor);
   }
   else body.Controls.Add(Ui.Body(type=="Photo"?"Attach photographic evidence below.":"Signature evidence is added after submission."));
   if(!header && (type=="Photo"||Json.Bool(field,"requiresPhoto")))
   {
    var photo=new FlatButton{Text="Attach photo",Width=180,Enabled=Writable()};photo.Click+=async(s,e)=>await PhotoAsync(Json.Text(field,"qcFormFieldID"));body.Controls.Add(photo);
   }
   if(!header && (type=="Signature"||Json.Bool(field,"requiresSignature")))
   {
    var sign=new FlatButton{Text="Sign this requirement",Width=240,Enabled=form.ServerStatus=="Submitted"};sign.Click+=(s,e)=>ChooseSignature(Json.Text(field,"qcFormFieldID"));body.Controls.Add(sign);
   }
   var help=Json.Text(field,"helpText");if(help.Length>0)body.Controls.Add(Ui.Wrapped(help,Theme.Small,Theme.Slate500,InnerWidth-60));
  }
  private void Group(string title,Action<FlowLayoutPanel> fill)
  {
   var card=AddPanelCard(title,30);var body=new FlowLayoutPanel{Location=new Point(Dpi.S(18),card.Controls[0].Bottom+Dpi.S(12)),Width=InnerWidth-Dpi.S(36),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};card.Controls.Add(body);fill(body);body.PerformLayout();card.Height=body.Bottom+Dpi.S(18);Y=card.Bottom+Dpi.S(16);
  }
  private void Buttons(string[] names,Action[] actions)
  {
   var row=new FlowLayoutPanel{Location=new Point(Left1,Y),Width=InnerWidth,Height=Dpi.S(78),WrapContents=true};
   for(int i=0;i<names.Length;i++){var action=actions[i];var button=new FlatButton{Text=names[i],Width=Dpi.S(Math.Max(130,names[i].Length*9)),Height=Dpi.S(34)};button.Click+=(s,e)=>action();row.Controls.Add(button);}Controls.Add(row);Y=row.Bottom+12;
  }
  private async Task<bool> SaveAsync()
  {
   if(busy)return false;if(conflict){Notify("Reload/reconcile the conflicting record before saving.",true);return false;}
   busy=true;Enabled=false;
   try
   {
    foreach(var entry in pending.ToList())
    {
     await FormData.SaveValueAsync(form,entry.Key,entry.Value);
     pending.Remove(entry.Key);LocalDrafts.Save(id,form.Revision,pending);
    }
    error="Saved to the shared service.";return true;
   }
   catch(ApiException ex){conflict=ex.IsRevisionConflict;error=ex.Message;Notify(error,true);return false;}
   catch(Exception ex){error=ex.Message;Notify(error,true);return false;}
   finally{busy=false;if(!IsDisposed){Enabled=true;Rebuild();}}
  }
  private async Task ActionAsync(string suffix,object body=null)
  {
   if(busy)return;if(pending.Count>0 && !await SaveAsync())return;
   await RunAsync(async()=>{await FormData.WriteAsync(form,"POST",suffix,body);form=await FormData.LoadAsync(id);LocalDrafts.Save(id,form.Revision,pending);error="Saved to the shared service.";});
  }
  private async Task RunAsync(Func<Task> action)
  {
   if(busy)return;busy=true;Enabled=false;
   try{await action();}catch(ApiException ex){conflict=ex.IsRevisionConflict;error=ex.Message;Notify(error,true);}catch(Exception ex){error=ex.Message;Notify(error,true);}finally{busy=false;if(!IsDisposed){Enabled=true;Rebuild();}}
  }
  private async Task RefreshAsync()
  {
   if(pending.Count>0){if(MessageBox.Show(Shell,"Discard the cached unsent changes and reload the server version?","Reload QC record",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;pending.Clear();}
   await RunAsync(async()=>{form=await FormData.LoadAsync(id);conflict=false;LocalDrafts.Save(id,form.Revision,pending);error="Reloaded from the service.";});
  }
  private async Task PhotoAsync(string field)
  {
   using(var picker=new OpenFileDialog{Filter="Photos|*.jpg;*.jpeg;*.png"})
   {
    if(picker.ShowDialog(Shell)!=DialogResult.OK)return;
    await RunAsync(async()=>{await FormData.UploadAsync(form,File.ReadAllBytes(picker.FileName),Path.GetFileName(picker.FileName),Path.GetExtension(picker.FileName).ToLowerInvariant()==".png"?"image/png":"image/jpeg","Photo",field);error="Photo uploaded.";LocalDrafts.Save(id,form.Revision,pending);});
   }
  }
  private void ChooseSignature(string field)
  {
   using(var dialog=new Dialog("Signing capacity",480)){var type=dialog.AddDropdown("Sign as",new[]{"Inspector","Technician","Supervisor","Client"},null);dialog.AddAsyncAction("Continue",async()=>{if(type.SelectedItem==null)return;await SignAsync(Convert.ToString(type.SelectedItem),field);dialog.DialogResult=DialogResult.OK;});dialog.ShowDialog(Shell);}
  }
  private async Task SignAsync(string capacity,string field)
  {
   using(var ink=new SignatureForm())
   {
    if(ink.ShowDialog(Shell)!=DialogResult.OK)return;
    await RunAsync(async()=>{var evidence=await FormData.UploadAsync(form,ink.Png,"signature.png","image/png","Signature",field);await FormData.WriteAsync(form,"POST","/sign-offs",new{signOffType=capacity,signatureEvidenceID=Json.Id(evidence,"evidenceID"),comments="Signed in the desktop application"});error="Signature recorded.";LocalDrafts.Save(id,form.Revision,pending);});
   }
  }
  private void ReturnForCorrection()
  {
   using(var dialog=new Dialog("Return for correction",520)){var reason=dialog.AddTextArea("Reason","");dialog.AddAsyncAction("Return",async()=>{await ActionAsync("/return-for-correction",new{reason=reason.Value});dialog.DialogResult=DialogResult.OK;});dialog.ShowDialog(Shell);}
  }
  private async Task ShowHistoryAsync()
  {
   await RunAsync(async()=>{var history=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/qc-forms/"+id+"/sign-offs"));using(var dialog=new Dialog("Sign-off history",600)){foreach(var item in history)dialog.AddReadOnlyField(Json.Text(item,"signerName")+" · "+Json.Text(item,"signOffType"),Json.Text(item,"signedAt")+(Json.Bool(item,"matchesCurrentContentAndSubmission")?" · Current content":" · Historical content"));dialog.ShowDialog(Shell);}});
  }
  private async void ExportCsv()
  {
   if(busy)return;if(pending.Count>0&&!await SaveAsync())return;
   using(var save=new SaveFileDialog{Filter="CSV file (*.csv)|*.csv",FileName=Exporter.SafeFileName(form.QcRecordNo)+".csv"})
   {
    if(save.ShowDialog(Shell)!=DialogResult.OK)return;
    await RunAsync(async()=>{form=await FormData.LoadAsync(id);Exporter.ExportForm(form,save.FileName);error="Exported the server execution snapshot.";});
   }
  }
  protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();}base.Dispose(disposing);}
 }
}
