namespace MzukuluQMS.Mobile.Core;

public static class RecordSyncState
{
 public static string Describe(LocalRecord? record,IEnumerable<OutboxEntry> outbox)
 {
  if(record is null)return "Saved locally";
  var active=outbox.Where(e=>e.Operation.EntityId==record.LocalId&&e.Status is not ("Completed" or "Cancelled")).ToArray();
  if(active.Any(e=>e.Status=="Conflict"))return "Conflict";
  if(active.Any(e=>e.Status=="Failed"))return "Sync failed";
  if(record.Dirty||active.Length>0)return "Pending sync";
  var status=System.Text.Json.Nodes.JsonNode.Parse(record.Json)?["status"]?.ToString();
  if(status=="Submitted")return "Submitted";
  return record.ServerId.HasValue?"Saved centrally":"Saved locally";
 }

 // Check the specific intent, rather than the result of unrelated queued work.
 public static bool SubmissionConfirmed(Guid operationId,IEnumerable<OutboxEntry> outbox)
  =>OperationConfirmed(operationId,"SubmitQCForm",outbox);
 public static bool OperationConfirmed(Guid operationId,string kind,IEnumerable<OutboxEntry> outbox)
  =>outbox.Any(e=>e.Operation.OperationId==operationId&&e.Operation.Kind==kind&&e.Status=="Completed");
}
