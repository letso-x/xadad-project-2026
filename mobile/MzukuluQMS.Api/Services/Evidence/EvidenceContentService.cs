using Dapper;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Services.Workflow;
namespace MzukuluQMS.Api.Services.Evidence;
public sealed class EvidenceContentService(QmsDbSession session,IQCWorkflow workflow,IEvidenceStorage storage)
{
 public async Task<EvidenceContent> DownloadAsync(long formId,long evidenceId,CancellationToken ct)
 {
  if(evidenceId<=0)throw new ArgumentException("Evidence ID must be positive.");
  Metadata metadata;
  await using(var access=await workflow.BeginAsync(formId,null,"ReadInspection",ct))
  {
   metadata=await session.Connection!.QuerySingleOrDefaultAsync<Metadata>(new CommandDefinition("""
    SELECT "StorageBucket","StoragePath","FileName","ContentType","FileSizeBytes" FROM public."Evidence"
    WHERE "EvidenceID"=@evidenceId AND "QCFormID"=@formId
    """,new{evidenceId,formId},session.Transaction,cancellationToken:ct))??throw new KeyNotFoundException("Evidence was not found for this form.");
  }
  if(metadata.FileSizeBytes>20*1024*1024)throw new InvalidOperationException("Evidence exceeds the supported download limit.");
  var bytes=await storage.DownloadAsync(metadata.StorageBucket,metadata.StoragePath,ct);
  var type=metadata.ContentType is "image/png" or "image/jpeg" or "application/pdf"?metadata.ContentType:"application/octet-stream";
  var name=Path.GetFileName(metadata.FileName);if(string.IsNullOrWhiteSpace(name))name="evidence";
  return new(bytes,type,name);
 }
 private sealed class Metadata {public string StorageBucket{get;init;}="";public string StoragePath{get;init;}="";public string FileName{get;init;}="";public string ContentType{get;init;}="";public long? FileSizeBytes{get;init;}}
}
public sealed record EvidenceContent(byte[] Bytes,string ContentType,string FileName);
