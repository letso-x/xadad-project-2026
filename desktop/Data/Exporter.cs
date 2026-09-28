using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Exports records to CSV so QC data can leave the app for reporting or handover
    /// packs. CSV is chosen over a bespoke format because every client already has
    /// something that opens it.
    /// </summary>
    internal static class Exporter
    {
        /// <summary>Escapes a field for CSV, quoting when needed.</summary>
        private static string Csv(string value)
        {
            if (value == null) return string.Empty;

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            string escaped = value.Replace("\"", "\"\"");

            return needsQuotes ? "\"" + escaped + "\"" : escaped;
        }

        private static string Row(params string[] cells)
        {
            return string.Join(",", cells.Select(Csv));
        }

        /// <summary>Writes the text to disk with a BOM so Excel reads UTF-8 correctly.</summary>
        private static void Write(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(true));
        }

        /// <summary>Exports one filled checklist, including every line result.</summary>
        public static void ExportForm(QcForm form, string path)
        {
            ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
            Project project = Repository.GetProject(form.ProjectId);
            Client client = project != null ? Repository.GetClient(project.ClientId) : null;

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(Row("Document", template != null ? template.DocNumber : string.Empty));
            sb.AppendLine(Row("Title", template != null ? template.DocName : string.Empty));
            sb.AppendLine(Row("Revision", template != null ? template.Revision : string.Empty));
            sb.AppendLine(Row("QC Record No.", form.QcRecordNo));
            sb.AppendLine(Row("Client", client != null ? client.Name : string.Empty));
            sb.AppendLine(Row("Project", project != null ? project.Name : string.Empty));
            sb.AppendLine(Row("Project No.", project != null ? project.Number : string.Empty));
            sb.AppendLine(Row("Status", Format.StatusText(form.Status)));
            sb.AppendLine(Row("NCR No.", form.NcrNo));
            sb.AppendLine(Row("Created", form.CreatedAt.ToString("yyyy-MM-dd HH:mm")));
            sb.AppendLine(Row("Last updated", form.UpdatedAt.ToString("yyyy-MM-dd HH:mm")));

            foreach (var kv in form.FieldValues)
            {
                sb.AppendLine(Row(kv.Key, kv.Value));
            }

            sb.AppendLine();
            sb.AppendLine(Row("No.", "Section", "Requirement", "Result", "Reference", "Initials", "Date"));

            int number = 0;
            if (template != null)
            {
                foreach (TemplateSection section in template.Sections)
                {
                    foreach (TemplateItem item in section.Items)
                    {
                        number++;
                        FormItemResult result = form.Items.FirstOrDefault(i => i.ItemId == item.Id);

                        sb.AppendLine(Row(
                            number.ToString(CultureInfo.InvariantCulture),
                            section.Title ?? string.Empty,
                            item.Text,
                            result != null ? Format.ResponseCode(result.Response) : string.Empty,
                            result != null ? result.Ref : string.Empty,
                            result != null ? result.Initials : string.Empty,
                            result != null ? Format.Iso(result.Date) : string.Empty));
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine(Row("Remarks", form.Remarks));
            sb.AppendLine();
            sb.AppendLine(Row("Sign-off", "Name", "Signature", "Date"));
            sb.AppendLine(Row("Inspected / Tested By", form.Inspected.Name,
                form.Inspected.Signature, Format.Iso(form.Inspected.Date)));
            sb.AppendLine(Row("Reviewed By", form.Reviewed.Name,
                form.Reviewed.Signature, Format.Iso(form.Reviewed.Date)));
            sb.AppendLine(Row("Approved By", form.Approved.Name,
                form.Approved.Signature, Format.Iso(form.Approved.Date)));

            Write(path, sb.ToString());

            Audit.Log(AuditAction.Exported, "Form", form.Id,
                (template != null ? template.DocNumber + " · " : string.Empty) + form.QcRecordNo,
                "Exported to CSV");
        }

        /// <summary>Exports the cable register grid.</summary>
        public static void ExportRegister(CableRegister register, string path)
        {
            Project project = Repository.GetProject(register.ProjectId);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Row("Document", CableRegister.DocNumber));
            sb.AppendLine(Row("Title", CableRegister.DocName));
            sb.AppendLine(Row("QC Record No.", register.QcRecordNo));
            sb.AppendLine(Row("Project", project != null ? project.Name : string.Empty));
            sb.AppendLine(Row("Status", Format.StatusText(register.Status)));

            foreach (var kv in register.FieldValues)
            {
                sb.AppendLine(Row(kv.Key, kv.Value));
            }

            sb.AppendLine();
            sb.AppendLine(Row("No.", "Cable No.", "From", "To", "Type / Size / Cores", "Drum No.",
                "Length (m)", "Pulled", "Glanded", "Terminated", "IR Cert.", "Cont. Cert.", "Complete"));

            foreach (CableRow cable in register.Cables)
            {
                sb.AppendLine(Row(
                    cable.No.ToString(CultureInfo.InvariantCulture),
                    cable.CableNo, cable.From, cable.To, cable.CableType, cable.DrumNo, cable.Length,
                    cable.Pulled ? "Yes" : "No",
                    cable.Glanded ? "Yes" : "No",
                    cable.Terminated ? "Yes" : "No",
                    cable.IrCert, cable.ContCert,
                    cable.Complete ? "Yes" : "No"));
            }

            sb.AppendLine();
            sb.AppendLine(Row("Notes", register.Notes));

            Write(path, sb.ToString());

            Audit.Log(AuditAction.Exported, "Register", register.Id,
                CableRegister.DocNumber + " · " + register.QcRecordNo, "Exported to CSV");
        }

        /// <summary>Exports every QC record for a project as a summary sheet.</summary>
        public static void ExportProjectRegister(Project project, string path)
        {
            StringBuilder sb = new StringBuilder();
            Client client = Repository.GetClient(project.ClientId);

            sb.AppendLine(Row("Project", project.Name));
            sb.AppendLine(Row("Project No.", project.Number));
            sb.AppendLine(Row("Client", client != null ? client.Name : string.Empty));
            sb.AppendLine(Row("Contract No.", project.ContractNo));
            sb.AppendLine(Row("Site", project.Site));
            sb.AppendLine(Row("Status", project.Status));
            sb.AppendLine(Row("Generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
            sb.AppendLine();

            sb.AppendLine(Row("Document", "Title", "QC Record No.", "Status",
                "Progress", "NCR No.", "Inspected By", "Last updated"));

            foreach (QcForm form in Repository.FormsForProject(project.Id)
                                              .OrderBy(f => f.QcRecordNo))
            {
                ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
                int total = template != null ? template.AllItems().Count : 0;
                string progress = total == 0
                    ? "—"
                    : form.AnsweredCount + "/" + total;

                sb.AppendLine(Row(
                    template != null ? template.DocNumber : string.Empty,
                    template != null ? template.DocName : string.Empty,
                    form.QcRecordNo,
                    Format.StatusText(form.Status),
                    progress,
                    form.NcrNo,
                    form.Inspected.Name,
                    form.UpdatedAt.ToString("yyyy-MM-dd")));
            }

            foreach (CableRegister register in Repository.RegistersForProject(project.Id))
            {
                int done = register.Cables.Count(c => c.Complete);
                sb.AppendLine(Row(
                    CableRegister.DocNumber,
                    CableRegister.DocName,
                    register.QcRecordNo,
                    Format.StatusText(register.Status),
                    done + "/" + register.Cables.Count,
                    string.Empty,
                    register.MaintainedBy.Name,
                    register.UpdatedAt.ToString("yyyy-MM-dd")));
            }

            Write(path, sb.ToString());

            Audit.Log(AuditAction.Exported, "Project", project.Id, project.Name,
                "Exported project QC register to CSV");
        }

        /// <summary>Exports the NCR log.</summary>
        public static void ExportNcrs(List<Ncr> ncrs, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Row("NCR No.", "Title", "Project", "Severity", "State", "Raised by",
                "Raised", "Assigned to", "Due", "Overdue", "Root cause", "Corrective action",
                "Closed by", "Closed"));

            foreach (Ncr ncr in ncrs)
            {
                Project project = Repository.GetProject(ncr.ProjectId);
                sb.AppendLine(Row(
                    ncr.NcrNo, ncr.Title,
                    project != null ? project.Name : string.Empty,
                    ncr.Severity.ToString(),
                    Format.NcrStateText(ncr.State),
                    ncr.RaisedBy,
                    ncr.RaisedAt.ToString("yyyy-MM-dd"),
                    ncr.AssignedTo,
                    Format.Iso(ncr.DueDate),
                    ncr.IsOverdue ? "Yes" : "No",
                    ncr.RootCause, ncr.CorrectiveAction,
                    ncr.ClosedBy, Format.Iso(ncr.ClosedAt)));
            }

            Write(path, sb.ToString());
            Audit.Log(AuditAction.Exported, "NCR", null, ncrs.Count + " NCRs", "Exported NCR log to CSV");
        }

        /// <summary>Exports the audit trail.</summary>
        public static void ExportAudit(List<AuditEntry> entries, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Row("Timestamp", "User", "Role", "Action", "Entity type", "Entity", "Detail"));

            foreach (AuditEntry entry in entries)
            {
                sb.AppendLine(Row(
                    entry.At.ToString("yyyy-MM-dd HH:mm:ss"),
                    entry.User,
                    Format.RoleName(entry.Role),
                    Audit.ActionText(entry.Action),
                    entry.EntityType,
                    entry.EntityLabel,
                    entry.Detail));
            }

            Write(path, sb.ToString());

            // Exporting the trail is itself a governance event worth recording.
            Audit.Log(AuditAction.Exported, "Audit", null,
                entries.Count + " entries", "Exported audit trail to CSV");
        }

        /// <summary>Suggests a filename that is safe on Windows.</summary>
        public static string SafeFileName(string basis)
        {
            if (string.IsNullOrWhiteSpace(basis)) return "export";

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(basis.Length);

            foreach (char c in basis)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '-' : c);
            }

            return sb.ToString().Trim();
        }
    }
}
