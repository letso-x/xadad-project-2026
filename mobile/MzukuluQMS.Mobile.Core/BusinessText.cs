using System.Globalization;
namespace MzukuluQMS.Mobile.Core;
public static class BusinessText
{
 public static string Status(string? status)=>status switch
 {
  "Draft"=>"Draft","InProgress"=>"In Progress","Submitted"=>"Submitted",
  "ReturnedForCorrection"=>"Returned for Correction","Approved"=>"Approved",
  "Cancelled"=>"Cancelled","Active"=>"Active","Inactive"=>"Inactive","Planning"=>"Planning","OnHold"=>"On Hold",
  "Completed"=>"Complete","Closed"=>"Closed","Open"=>"Open NCR",
  "Pending" or "Pending sync"=>"Pending sync","Processing" or "Syncing"=>"Syncing",
  "Synced"=>"Synced","Conflict"=>"Needs review","Failed" or "Sync failed"=>"Sync failed",
  "Rejected"=>"Rejected","Current signature"=>"Current signature","Historical signature"=>"Historical signature",
  _=>"Status unavailable"
 };
 public static string Action(string? action)=>action switch
 {
  "CreateQCForm" or "QCFormCreated"=>"QC record created",
  "UpdateQCResponse" or "QCResponseUpdated"=>"Inspection answer updated",
  "UpdateQCFormProjectField" or "QCFormProjectFieldUpdated"=>"Project information updated",
  "UpdateRemarks" or "QCFormRemarksUpdated"=>"Remarks updated",
  "UploadEvidence" or "EvidenceUploaded"=>"Attachment added",
  "SubmitQCForm" or "QCFormSubmitted"=>"QC record submitted",
  "ReturnForCorrection" or "QCFormReturnedForCorrection"=>"Returned for correction",
  "ApproveQCForm" or "QCFormApproved"=>"QC record approved",
  "CreateSignOff" or "QCSignOffCreated"=>"Signature recorded",
  _=>"Record updated"
 };
 public static string SignOff(string? type)=>type switch
 {
  "Inspector" or "Technician"=>"Inspected / Tested By",
  "Supervisor"=>"Reviewed By","Client"=>"Client Sign-off",_=>"Sign-off"
 };
 public static string PhotoStatus(string? status)=>status switch
 {"Completed"=>"Uploaded","Failed" or "Conflict"=>"Upload failed","Cancelled"=>"Withdrawn attachment",_=>"Pending upload"};
 public static string Date(string? value)=>DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date.ToLocalTime().ToString("dd MMM yyyy · HH:mm",CultureInfo.CurrentCulture):"Date unavailable";
}
