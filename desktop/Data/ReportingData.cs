using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MzuApplication.Models;
namespace MzuApplication.Data
{
 internal static class ReportingData
 {
  public static string ActionText(string action)
  {
   return System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace((action??"").Replace(':',' '),"([A-Z]+)([A-Z][a-z])","$1 $2"),"([a-z0-9])([A-Z])","$1 $2");
  }
  public static Dictionary<string,object> Portfolio {get{return Repository.Db.Portfolio;}}
  public static Task<object> DashboardAsync(string project){return ApiClient.Shared.SendAsync("GET","api/quality/projects/"+project+"/dashboard");}
  public static Task<object> ReadinessAsync(string project){return ApiClient.Shared.SendAsync("GET","api/quality/projects/"+project+"/handover-readiness");}
  public static async Task LoadPortfolioAsync(){var database=Repository.Db;database.Portfolio=Json.Map(await ApiClient.Shared.SendAsync("GET","api/quality/dashboard").ConfigureAwait(false));}
  public static async Task LoadAuditAsync()
  {
   var database=Repository.Db;
   var entries=new List<AuditEntry>();
   foreach(var form in database.Forms)
   {
    long after=0;
    while(true)
    {
     var rows=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/qc-forms/"+form.Id+"/audit?afterId="+after+"&limit=100").ConfigureAwait(false));
     foreach(var row in rows)entries.Add(MapAudit(row,form,"core"));
     if(rows.Count<100)break;var next=rows.Max(r=>Json.Id(r,"auditEventID"));if(next<=after)throw new InvalidOperationException("The audit cursor did not advance.");after=next;
    }
    var legacy=Json.Rows(await ApiClient.Shared.SendAsync("GET","api/qc-forms/"+form.Id+"/audit-history").ConfigureAwait(false));
    entries.AddRange(legacy.Select(row=>MapAudit(row,form,"historical")));
   }
   database.Audit=entries.OrderByDescending(e=>e.At).ToList();
  }
  private static AuditEntry MapAudit(object row,QcForm form,string source)
  {
   return new AuditEntry{Id=source+":"+form.Id+":"+Json.Text(row,"auditEventID"),EntityId=form.Id,EntityLabel=form.QcRecordNo,EntityType="Form",At=Json.Date(row,"occurredAt")??DateTime.MinValue,User=string.IsNullOrWhiteSpace(Json.Text(row,"actorName"))?"Historical user unavailable":Json.Text(row,"actorName"),ServerAction=Json.Text(row,"action"),Detail=source=="historical"?"Historical audit record":"Shared service audit record"};
  }
  public static Task<object> NotificationsAsync(){return ApiClient.Shared.SendAsync("GET","api/quality/notifications");}
  public static Task<object> ReadNotificationAsync(long id){return ApiClient.Shared.SendAsync("POST","api/quality/notifications/"+id+"/read");}
 }
}
