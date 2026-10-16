using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MzuApplication.Models;
namespace MzuApplication.Data
{
 internal static class NcrData
 {
  public static async Task LoadAsync()
  {
   var database=Repository.Db;
   var ncrs=new List<Ncr>();
   foreach(var form in database.Forms)
   {
    var rows=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/qc-forms/"+form.Id+"/ncrs").ConfigureAwait(false));
    foreach(var row in rows)
    {
     NcrSeverity severity;Enum.TryParse(Json.Text(row,"severity"),true,out severity);
     ncrs.Add(new Ncr{Id=Json.Text(row,"ncrID"),NcrNo=Json.Text(row,"ncrNumber"),FormId=form.Id,ProjectId=form.ProjectId,Title=Json.Text(row,"sourceFieldLabel"),Description=Json.Text(row,"classification"),Severity=severity,State=Json.Get(row,"closedAt")!=null?NcrState.Closed:Json.Get(row,"correctedByUserID")!=null?NcrState.AwaitingVerification:NcrState.Open,CorrectiveAction=Json.Text(row,"correctiveAction"),RaisedBy=Json.Text(row,"createdByName"),RaisedAt=Json.Date(row,"createdAt")??DateTime.MinValue,ClosedBy=Json.Text(row,"verifierName"),ClosedAt=Json.Date(row,"closedAt"),RemoteRecord=row});
    }
   }
   database.Ncrs=ncrs;
  }
  public static async Task VerifyAsync(Ncr ncr,bool passed,string comments)
  {
   var form=await FormData.LoadAsync(ncr.FormId).ConfigureAwait(false);
   await FormData.WriteAsync(form,"POST","/ncrs/"+ncr.Id+"/verify",new{passed,comments,expectedRevision=Json.Id(ncr.RemoteRecord,"revision")}).ConfigureAwait(false);
   await LoadAsync().ConfigureAwait(false);
  }
  public static async Task CorrectAsync(Ncr ncr,string severity,string classification,bool blocking,string disposition,string action,byte[] image,string name,string contentType)
  {
   var form=await FormData.LoadAsync(ncr.FormId).ConfigureAwait(false);
   var evidence=await FormData.UploadAsync(form,image,name,contentType,"Photo",Json.Text(ncr.RemoteRecord,"qcFormFieldID")).ConfigureAwait(false);
   await FormData.WriteAsync(form,"POST","/ncrs/"+ncr.Id+"/correct",new{severity,classification,isBlocking=blocking,disposition,correctiveAction=action,correctiveEvidenceID=Json.Id(evidence,"evidenceID"),expectedRevision=Json.Id(ncr.RemoteRecord,"revision")}).ConfigureAwait(false);
   await LoadAsync().ConfigureAwait(false);
  }
 }
}
