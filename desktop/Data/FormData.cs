using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MzuApplication.Models;
namespace MzuApplication.Data
{
 internal static class FormData
 {
  public static async Task LoadListAsync(string projectId)
  {
   var database=Repository.Db;
   var forms=new List<QcForm>();
   for(var page=1;;page++)
   {
    var rows=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/qc-forms?projectId="+projectId+"&page="+page+"&pageSize=100").ConfigureAwait(false));
    forms.AddRange(rows.Select(Map));if(rows.Count<100)break;
   }
   database.Forms=database.Forms.Where(f=>f.ProjectId!=projectId).Concat(forms).ToList();
  }
  public static QcForm Map(object row)
  {
   var state=Json.Text(row,"status");
   var status=state=="Approved"?RecordStatus.CompleteAccepted:state=="Submitted"?RecordStatus.Submitted:state=="ReturnedForCorrection"?RecordStatus.ReturnedForCorrection:state=="Cancelled"?RecordStatus.Cancelled:RecordStatus.InProgress;
   return new QcForm{Id=Json.Text(row,"qcFormID"),ProjectId=Json.Text(row,"projectID"),TemplateId=Json.Text(row,"checklistTemplateID"),QcRecordNo=Json.Text(row,"formNumber"),ServerStatus=state,Revision=Json.Id(row,"revision"),Status=status,CreatedAt=Json.Date(row,"createdAt")??DateTime.MinValue,UpdatedAt=Json.Date(row,"updatedAt")??DateTime.MinValue,Remarks=Json.Text(row,"remarks"),RemoteDetails=Json.Map(row)};
  }
  public static async Task<QcForm> LoadAsync(string id)
  {
   var database=Repository.Db;
   var form=Map(await ApiClient.Shared.SendAsync("GET","api/qc-forms/"+id).ConfigureAwait(false));
   database.Forms=database.Forms.Where(f=>f.Id!=id).Concat(new[]{form}).ToList();return form;
  }
  public static async Task<QcForm> CreateAsync(ChecklistTemplate template,Project project)
  {
   var database=Repository.Db;
   var result=await ApiClient.Shared.SendAsync("POST","api/qc-forms",new{ProjectID=long.Parse(project.Id),ChecklistTemplateID=long.Parse(template.Id),ChecklistTemplateVersionID=template.VersionId}).ConfigureAwait(false);
   var form=Map(result);if(string.IsNullOrEmpty(form.Id))throw new InvalidOperationException("The create response did not include a form identity. Refresh before creating another record.");
   database.Forms=database.Forms.Where(f=>f.Id!=form.Id).Concat(new[]{form}).ToList();return form;
  }
  public static async Task SaveValueAsync(QcForm form,string key,object value)
  {
   var suffix=key=="remarks"?"/remarks":key.StartsWith("header:",StringComparison.Ordinal)?"/project-fields/"+long.Parse(key.Substring(7)):"/responses/"+long.Parse(key.Substring(9));
   object body=key=="remarks"?(object)new{remarks=Convert.ToString(value)}:new{value};
   await WriteAsync(form,"PUT",suffix,body).ConfigureAwait(false);
   if(key=="remarks")form.Remarks=Convert.ToString(value);
   else
   {
    var header=key.StartsWith("header:",StringComparison.Ordinal);
    var fields=header?Json.Rows(form.RemoteDetails,"projectFields"):Json.Rows(form.RemoteDetails,"sections").SelectMany(s=>Json.Rows(s,"fields")).ToList();
    var field=fields.FirstOrDefault(f=>Json.Text(f,header?"qcFormProjectFieldID":"qcResponseID")==key.Substring(key.IndexOf(':')+1));
    if(field!=null)field["value"]=value;
   }
  }
  public static async Task<object> WriteAsync(QcForm form,string method,string suffix,object body=null)
  {
   var result=await ApiClient.Shared.SendAsync(method,"api/qc-forms/"+form.Id+suffix,body,form.Revision.ToString()).ConfigureAwait(false);
   var revision=Json.Id(result,"__revision");if(revision<=0)throw new InvalidOperationException("The server did not confirm the updated revision. Reload this record before further edits.");
   form.Revision=revision;return result;
  }
  public static async Task<object> UploadAsync(QcForm form,byte[] bytes,string name,string type,string evidenceType,string fieldId)
  {
   var result=await ApiClient.Shared.UploadAsync(form.Id,bytes,name,type,evidenceType,fieldId,form.Revision.ToString()).ConfigureAwait(false);
   var revision=Json.Id(result,"__revision");if(revision<=0)throw new InvalidOperationException("Reload this record to confirm the uploaded evidence.");
   form.Revision=revision;return result;
  }
 }
}
