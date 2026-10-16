using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
namespace MzuApplication.Views
{
 public sealed class ItpView:ViewBase
 {
  private readonly string project;private List<Dictionary<string,object>> plans=new List<Dictionary<string,object>>();private bool loaded;private string message="Loading inspection and test plans…";
  public ItpView(MainForm shell,string projectId):base(shell){project=projectId;HandleCreated+=async(s,e)=>{if(!loaded){loaded=true;await ReloadAsync();}};}
  public override string RouteKey{get{return "projects";}}
  private async Task ReloadAsync(){try{plans=Json.Rows(await ItpData.ListAsync(project));message="Plans and progress from the shared service.";}catch(Exception ex){message=ex.Message;}if(!IsDisposed)Rebuild();}
  protected override void BuildContent()
  {
   AddCrumbs("Project",()=>Shell.Navigate(new ProjectDetailView(Shell,project)),"Inspection & Test Plans");
   var create=new FlatButton{Text="Create ITP",Width=140};create.Click+=(s,e)=>Create();
   var refresh=new FlatButton{Text="Refresh",Width=100};refresh.Click+=async(s,e)=>await ReloadAsync();
   AddPageHeader("QUALITY PLANNING","Inspection & Test Plans",message,create,refresh);
   var table=new DataGridView{Location=new Point(Left1,Y),Width=InnerWidth,Height=Math.Max(140,plans.Count*32+40),ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.White};
   foreach(var label in new[]{"Number","Title","Revision","Status","Current","Progress"})table.Columns.Add(label,label);
   foreach(var plan in plans){var i=table.Rows.Add(Json.Text(plan,"number"),Json.Text(plan,"title"),Json.Text(plan,"revision"),Json.Text(plan,"status"),Json.Bool(plan,"isCurrent")?"Current":"",Json.Text(Json.Get(plan,"progress"),"percentageComplete")+"%");table.Rows[i].Tag=plan;}
   table.CellDoubleClick+=async(s,e)=>{if(e.RowIndex>=0)await OpenAsync(Json.Id(table.Rows[e.RowIndex].Tag,"id"));};Controls.Add(table);Y=table.Bottom+16;
  }
  private void Create()
  {
   using(var dialog=new Dialog("Create inspection and test plan",540))
   {
    var number=dialog.AddTextField("Number","");var title=dialog.AddTextField("Title","");
    dialog.AddAsyncAction("Create draft",async()=>{var result=await ItpData.CreateAsync(project,new{number=number.Value,title=title.Value,activities=new object[0]});dialog.Tag=Json.Id(result,"id");dialog.DialogResult=DialogResult.OK;});
    if(dialog.ShowDialog(Shell)==DialogResult.OK)OpenCreated((long)dialog.Tag);
   }
  }
  private async void OpenCreated(long version){await OpenAsync(version);await ReloadAsync();}
  private async Task OpenAsync(long id)
  {
   object result;try{result=await ItpData.GetAsync(project,id);}catch(Exception ex){Notify(ex.Message,true);return;}
   var version=Json.Get(result,"version");var definition=Json.Clone(Json.Get(version,"definition"));var activities=Json.Rows(definition,"activities");var stamp=Json.Id(version,"stamp");var draft=Json.Text(version,"status")=="Draft";
   using(var dialog=new Dialog(Json.Text(definition,"number")+" · "+Json.Text(version,"status"),900))
   {
    var title=dialog.AddTextField("Title",Json.Text(definition,"title"));title.Enabled=draft;
    dialog.AddReadOnlyField("Progress",Json.Text(Json.Get(result,"progress"),"percentageComplete")+"% complete");
    var table=new DataGridView{Height=260,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
    foreach(var name in new[]{"Order","Code","Activity","Point","Status"})table.Columns.Add(name,name);
    Action fill=()=>{table.Rows.Clear();foreach(var activity in activities.OrderBy(a=>Json.Id(a,"sequence"))){var runtime=Json.Rows(result,"activities").FirstOrDefault(a=>Json.Id(Json.Get(a,"definition"),"id")==Json.Id(activity,"id"));var i=table.Rows.Add(Json.Text(activity,"sequence"),Json.Text(activity,"code"),Json.Text(activity,"title"),Json.Text(activity,"pointType"),runtime==null?"Draft":Json.Text(runtime,"status"));table.Rows[i].Tag=activity;}};fill();dialog.AddControl(table);
    dialog.AddNote("Double-click an activity to edit a draft or record an execution action. Hold release, dependencies, linked QC and authority are checked on the server.");
    table.CellDoubleClick+=(s,e)=>{if(e.RowIndex<0)return;var activity=Json.Map(table.Rows[e.RowIndex].Tag);if(draft){EditActivity(activity,activities);fill();}else Execute(id,stamp,activity,result,dialog);};
    if(draft)
    {
     var add=new Button{Text="Add activity",Height=30};add.Click+=(s,e)=>{var activity=new Dictionary<string,object>{{"id",activities.Count==0?1:activities.Max(a=>Json.Id(a,"id"))+1},{"sequence",activities.Count+1},{"code",""},{"title",""},{"description",""},{"criteria",""},{"pointType","None"},{"dependsOn",new object[0]},{"witnessRequired",true},{"independentHoldRelease",true}};if(EditActivity(activity,activities)){activities.Add(activity);fill();}};dialog.AddControl(add);
     dialog.AddAsyncAction("Save draft",async()=>{definition["title"]=title.Value;definition["activities"]=activities;await ItpData.SaveAsync(project,id,stamp,definition);dialog.DialogResult=DialogResult.OK;});
    }
    var action=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Height=30};action.Items.AddRange(new object[]{"submit","approve","revise"});dialog.AddControl(action);
    dialog.AddAsyncAction("Apply action",async()=>{if(action.SelectedItem==null)return;var applied=await ItpData.ActAsync(project,id,stamp,Convert.ToString(action.SelectedItem),null,"Desktop plan action",null);dialog.Tag=Json.Id(applied,"id");dialog.DialogResult=DialogResult.Yes;});
    dialog.AddAsyncAction("Audit",async()=>{var audit=Json.Rows(await ItpData.AuditAsync(project,id));using(var history=new Dialog("ITP audit",700)){foreach(var row in audit)history.AddReadOnlyField(Json.Text(row,"actorName"),Json.Text(row,"action")+" · "+Json.Text(row,"occurredAt"));history.ShowDialog(dialog);}});
    var outcome=dialog.ShowDialog(Shell);if(outcome==DialogResult.OK||outcome==DialogResult.Yes){await ReloadAsync();await OpenAsync(outcome==DialogResult.Yes?(long)dialog.Tag:id);}
   }
  }
  private bool EditActivity(Dictionary<string,object> activity,List<Dictionary<string,object>> all)
  {
   using(var dialog=new Dialog("ITP activity",650))
   {
    var order=dialog.AddTextField("Order",Json.Text(activity,"sequence"));var code=dialog.AddTextField("Code",Json.Text(activity,"code"));var title=dialog.AddTextField("Title",Json.Text(activity,"title"));var description=dialog.AddTextArea("Description",Json.Text(activity,"description"));var criteria=dialog.AddTextArea("Acceptance criteria",Json.Text(activity,"criteria"));
    var point=dialog.AddDropdown("Point type",new[]{"None","Hold","Witness","Review"},Json.Text(activity,"pointType"));
    var witness=dialog.AddCheckField("Witness required",Json.Bool(activity,"witnessRequired"));var independent=dialog.AddCheckField("Independent hold release",Json.Bool(activity,"independentHoldRelease"));
    var requireQc=dialog.AddCheckField("Requires linked QC",Json.Bool(activity,"requireQc"));
    var templates=Repository.Db.Templates;var template=dialog.AddDropdown("Linked checklist revision",new[]{"None"}.Concat(templates.Select(t=>t.DocNumber+" · "+t.Revision)),templates.Where(t=>t.VersionId==Json.Id(activity,"checklistTemplateVersionId")).Select(t=>t.DocNumber+" · "+t.Revision).FirstOrDefault()??"None");
    var dependencies=new CheckedListBox{Height=100};var others=all.Where(a=>Json.Id(a,"id")!=Json.Id(activity,"id")).ToList();var existing=Json.Array(Json.Get(activity,"dependsOn"));foreach(var other in others)dependencies.Items.Add(Json.Text(other,"code")+" · "+Json.Text(other,"title"),existing.Any(x=>Convert.ToInt64(x)==Json.Id(other,"id")));dialog.AddControl(dependencies);
    dialog.AddPrimaryAction("Keep changes",()=>{int sequence;if(!int.TryParse(order.Value,out sequence))return;activity["sequence"]=sequence;activity["code"]=code.Value;activity["title"]=title.Value;activity["description"]=description.Value;activity["criteria"]=criteria.Value;activity["pointType"]=point.SelectedItem;activity["witnessRequired"]=witness.Checked;activity["independentHoldRelease"]=independent.Checked;activity["requireQc"]=requireQc.Checked;activity["checklistTemplateVersionId"]=template.SelectedIndex<=0?(object)null:templates[template.SelectedIndex-1].VersionId;activity["dependsOn"]=dependencies.CheckedIndices.Cast<int>().Select(i=>Json.Id(others[i],"id")).ToArray();dialog.DialogResult=DialogResult.OK;});
    return dialog.ShowDialog(Shell)==DialogResult.OK;
   }
  }
  private void Execute(long version,long stamp,Dictionary<string,object> activity,object result,Dialog owner)
  {
   using(var dialog=new Dialog(Json.Text(activity,"code")+" · "+Json.Text(activity,"title"),650))
   {
    dialog.AddReadOnlyField("Criteria",Json.Text(activity,"criteria"));var state=Json.Rows(result,"activities").FirstOrDefault(a=>Json.Id(Json.Get(a,"definition"),"id")==Json.Id(activity,"id"));dialog.AddReadOnlyField("Availability",Json.Text(state,"status")+" · "+Json.Text(state,"blockedReason"));
    var execution=Json.Get(state,"execution");var actors=Json.Map(Json.Get(result,"actorNames"));
    foreach(var key in new[]{"completedBy","releasedBy","witnessedBy","reviewedBy"}){object name;var who=Json.Text(execution,key);dialog.AddReadOnlyField(key.Replace("By"," by"),actors.TryGetValue(who,out name)?Convert.ToString(name):"Pending");}
    var action=dialog.AddDropdown("Action",new[]{"complete","release","witness","review","link-qc"},null);var comments=dialog.AddTextArea("Comments","");
    var forms=Repository.FormsForProject(project);var qc=dialog.AddDropdown("QC record (link action)",new[]{"None"}.Concat(forms.Select(f=>f.QcRecordNo)),"None");
    dialog.AddAsyncAction("Record action",async()=>{if(action.SelectedItem==null)return;await ItpData.ActAsync(project,version,stamp,Convert.ToString(action.SelectedItem),Json.Id(activity,"id"),comments.Value,qc.SelectedIndex<=0?(long?)null:long.Parse(forms[qc.SelectedIndex-1].Id));dialog.DialogResult=DialogResult.OK;});
    if(dialog.ShowDialog(owner)==DialogResult.OK)owner.DialogResult=DialogResult.OK;
   }
  }
 }
}
