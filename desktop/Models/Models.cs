using System;
using System.Collections.Generic;
using System.Linq;

namespace MzuApplication.Models
{
    /// <summary>Access level of the signed-in demo user.</summary>
    public enum UserRole
    {
        None = 0,
        Admin = 1,
        SiteUser = 2
    }

    /// <summary>Result recorded against a single checklist line.</summary>
    public enum ItemResponse
    {
        /// <summary>Not yet answered.</summary>
        Unanswered = 0,

        /// <summary>Accepted / conforms.</summary>
        Accepted = 1,

        /// <summary>Rejected — requires an NCR.</summary>
        Rejected = 2,

        /// <summary>Not applicable.</summary>
        NotApplicable = 3
    }

    /// <summary>Lifecycle state of a QC form or register.</summary>
    public enum RecordStatus
    {
        InProgress = 0,
        CompleteAccepted = 1,
        CompleteNcrRaised = 2
    }

    public class Client
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Contact { get; set; }
        public bool Active { get; set; }
    }

    public class Project
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public string ContractNo { get; set; }
        public string Site { get; set; }
        public string Description { get; set; }

        /// <summary>Planning, Active, On Hold or Complete.</summary>
        public string Status { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>One line of a checklist template.</summary>
    public class TemplateItem
    {
        public string Id { get; set; }
        public string Text { get; set; }

        /// <summary>Label for an optional reference / reading input, blank if none.</summary>
        public string RefLabel { get; set; }

        /// <summary>True when the reference must be supplied for an accepted line.</summary>
        public bool RefRequired { get; set; }

        public bool HasRef
        {
            get { return !string.IsNullOrWhiteSpace(RefLabel); }
        }
    }

    /// <summary>A named (or unnamed) group of checklist items.</summary>
    public class TemplateSection
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<TemplateItem> Items { get; set; }

        public TemplateSection()
        {
            Items = new List<TemplateItem>();
        }
    }

    /// <summary>A configurable MZT-QC-3xx checklist definition.</summary>
    public class ChecklistTemplate
    {
        public string Id { get; set; }
        public string DocNumber { get; set; }
        public string DocName { get; set; }
        public string Description { get; set; }
        public string Revision { get; set; }
        public DateTime IssueDate { get; set; }
        public bool Active { get; set; }

        /// <summary>Extra site-specific header fields, e.g. "Enclosure / Cabinet N°".</summary>
        public List<string> ProjectFields { get; set; }

        public string Scope { get; set; }
        public string Acceptance { get; set; }
        public List<TemplateSection> Sections { get; set; }

        public ChecklistTemplate()
        {
            ProjectFields = new List<string>();
            Sections = new List<TemplateSection>();
        }

        /// <summary>All items across every section, in display order.</summary>
        public List<TemplateItem> AllItems()
        {
            return Sections.SelectMany(s => s.Items).ToList();
        }

        public TemplateItem FindItem(string itemId)
        {
            return Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Id == itemId);
        }
    }

    /// <summary>The answer captured against one checklist item on a filled form.</summary>
    public class FormItemResult
    {
        public string ItemId { get; set; }
        public ItemResponse Response { get; set; }
        public string Ref { get; set; }
        public string Initials { get; set; }
        public DateTime? Date { get; set; }
    }

    /// <summary>A single name / signature / date sign-off block.</summary>
    public class SignoffEntry
    {
        public string Name { get; set; }
        public string Signature { get; set; }
        public DateTime? Date { get; set; }

        public bool IsSigned
        {
            get { return !string.IsNullOrWhiteSpace(Name); }
        }
    }

    /// <summary>A completed (or in-progress) instance of a checklist template.</summary>
    public class QcForm
    {
        public string Id { get; set; }
        public string TemplateId { get; set; }
        public string ProjectId { get; set; }
        public string QcRecordNo { get; set; }

        /// <summary>Values for the template's extra header fields, keyed by field label.</summary>
        public Dictionary<string, string> FieldValues { get; set; }

        public RecordStatus Status { get; set; }
        public List<FormItemResult> Items { get; set; }
        public string Remarks { get; set; }
        public string NcrNo { get; set; }

        public SignoffEntry Inspected { get; set; }
        public SignoffEntry Reviewed { get; set; }
        public SignoffEntry Approved { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; }

        public QcForm()
        {
            FieldValues = new Dictionary<string, string>();
            Items = new List<FormItemResult>();
            Inspected = new SignoffEntry();
            Reviewed = new SignoffEntry();
            Approved = new SignoffEntry();
        }

        public bool HasRejected
        {
            get { return Items.Any(i => i.Response == ItemResponse.Rejected); }
        }

        public int AnsweredCount
        {
            get { return Items.Count(i => i.Response != ItemResponse.Unanswered); }
        }
    }

    /// <summary>One cable line in the MZT-QC-203 register.</summary>
    public class CableRow
    {
        public string Id { get; set; }
        public int No { get; set; }
        public string CableNo { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string CableType { get; set; }
        public string DrumNo { get; set; }
        public string Length { get; set; }
        public bool Pulled { get; set; }
        public bool Glanded { get; set; }
        public bool Terminated { get; set; }
        public string IrCert { get; set; }
        public string ContCert { get; set; }
        public bool Complete { get; set; }
    }

    /// <summary>The MZT-QC-203 Cable Schedule &amp; Installation Register.</summary>
    public class CableRegister
    {
        public const string DocNumber = "MZT-QC-203";
        public const string DocName = "Cable Schedule & Installation Register";

        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string QcRecordNo { get; set; }
        public Dictionary<string, string> FieldValues { get; set; }
        public List<CableRow> Cables { get; set; }
        public string Notes { get; set; }
        public SignoffEntry MaintainedBy { get; set; }
        public SignoffEntry VerifiedBy { get; set; }
        public RecordStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; }

        public CableRegister()
        {
            FieldValues = new Dictionary<string, string>();
            Cables = new List<CableRow>();
            MaintainedBy = new SignoffEntry();
            VerifiedBy = new SignoffEntry();
        }
    }

    /// <summary>Root of the persisted demo database.</summary>
    public class Database
    {
        public List<Client> Clients { get; set; }
        public List<Project> Projects { get; set; }
        public List<ChecklistTemplate> Templates { get; set; }
        public List<QcForm> Forms { get; set; }
        public List<CableRegister> Registers { get; set; }

        /// <summary>Append-only audit trail.</summary>
        public List<AuditEntry> Audit { get; set; }

        /// <summary>Tracked non-conformance reports.</summary>
        public List<Ncr> Ncrs { get; set; }

        public OrgSettings Settings { get; set; }

        public Database()
        {
            Clients = new List<Client>();
            Projects = new List<Project>();
            Templates = new List<ChecklistTemplate>();
            Forms = new List<QcForm>();
            Registers = new List<CableRegister>();
            Audit = new List<AuditEntry>();
            Ncrs = new List<Ncr>();
            Settings = new OrgSettings();
        }
    }
}
