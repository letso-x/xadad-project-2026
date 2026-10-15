using System.Text.Json.Nodes;
namespace MzukuluQMS.Mobile.Core;

public static class NcrInteraction
{
 public static bool CanChangeForForm(string? status)=>status is "Draft" or "InProgress" or "ReturnedForCorrection" or "Submitted";
 private static long? Number(JsonNode? value)=>long.TryParse(value?.ToString(),System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var number)?number:null;
 public static bool IsHistorical(JsonNode ncr)=>ncr["isLegacyOnly"]?.GetValue<bool>()==true||Number(ncr["ncrID"]) is not >0;
 public static bool IsClosed(JsonNode ncr)=>ncr["closedAt"] is not null;
 public static string Status(JsonNode ncr)=>IsClosed(ncr)?"Closed":ncr["verificationPassed"]?.GetValue<bool>()==false?"Verification failed — open":ncr["correctedByUserID"] is not null?"Awaiting independent verification":"Open";
 public static string? VerificationProblem(JsonNode ncr,Guid actor)
 {
  if(IsHistorical(ncr))return "This historical case is read-only. Ask an authorised reviewer to reconcile it.";
  if(IsClosed(ncr))return "This NCR is closed. Its history remains available.";
  if(ncr["createdByUserID"] is null)return "Creator identity is unavailable. Independent verification cannot be established.";
  if(ncr["correctedByUserID"] is null||string.IsNullOrWhiteSpace(ncr["correctiveAction"]?.ToString())||ncr["correctiveEvidenceID"] is null||ncr["isBlocking"] is null)
   return "Record corrective action, classification and uploaded evidence before verification.";
  if(ncr["createdByUserID"]!.ToString().Equals(actor.ToString(),StringComparison.OrdinalIgnoreCase)||ncr["correctedByUserID"]!.ToString().Equals(actor.ToString(),StringComparison.OrdinalIgnoreCase))
   return "A different authorised person must verify this NCR. The creator and corrector cannot verify their own work.";
  return null;
 }
 public static void ValidateCorrection(JsonNode ncr,JsonNode draft)
 {
  if(IsHistorical(ncr)||IsClosed(ncr))throw new InvalidOperationException("This NCR is read-only.");
  foreach(var (key,label) in new[]{("severity","Severity"),("classification","Classification"),("disposition","Disposition"),("correctiveAction","Corrective action")})
   if(string.IsNullOrWhiteSpace(draft[key]?.ToString())||draft[key]!.ToString().Length>4000)throw new ArgumentException(label+" is required (maximum 4000 characters).");
  if(draft["isBlocking"] is not JsonValue blocking||!blocking.TryGetValue<bool>(out _))throw new ArgumentException("Choose whether this NCR blocks approval.");
  if(Number(draft["correctiveEvidenceID"]) is not >0)throw new ArgumentException("Choose uploaded corrective evidence from this QC record.");
  if(Number(draft["expectedRevision"]) is not >=0)throw new ArgumentException("Reload this case before saving corrective action.");
 }
 public static void ValidateVerification(JsonNode ncr,Guid actor,string comments)
 {
  if(VerificationProblem(ncr,actor) is {} problem)throw new InvalidOperationException(problem);
  if(string.IsNullOrWhiteSpace(comments)||comments.Length>2000)throw new ArgumentException("Verification comments are required (maximum 2000 characters).");
 }
}
