using System;
using System.Collections.Generic;

namespace MzuApplication.Models
{
    /// <summary>Kind of change recorded in the audit trail.</summary>
    public enum AuditAction
    {
        Created = 0,
        Updated = 1,
        Submitted = 2,
        StatusChanged = 3,
        SignedOff = 4,
        Deleted = 5,
        Exported = 6,
        SignedIn = 7,
        NcrRaised = 8,
        NcrClosed = 9
    }

    /// <summary>
    /// One immutable audit entry. A QC system needs a defensible record of who changed
    /// what and when, so entries are appended and never edited.
    /// </summary>
    public class AuditEntry
    {
        public string Id { get; set; }
        public DateTime At { get; set; }
        public string User { get; set; }
        public UserRole Role { get; set; }
        public AuditAction Action { get; set; }

        /// <summary>Entity type touched, e.g. "Form", "Project", "Template".</summary>
        public string EntityType { get; set; }

        public string EntityId { get; set; }

        /// <summary>Human-readable label, e.g. "MZT-QC-301 · QC-0001".</summary>
        public string EntityLabel { get; set; }

        /// <summary>What changed, in plain language.</summary>
        public string Detail { get; set; }
    }

    /// <summary>Severity of a non-conformance report.</summary>
    public enum NcrSeverity
    {
        Minor = 0,
        Major = 1,
        Critical = 2
    }

    /// <summary>Lifecycle state of a non-conformance report.</summary>
    public enum NcrState
    {
        Open = 0,
        InRemediation = 1,
        AwaitingVerification = 2,
        Closed = 3
    }

    /// <summary>
    /// A formal non-conformance report. Previously the app only stored a free-text NCR
    /// number on a form; this promotes it to a tracked record with ownership, due date
    /// and close-out evidence, which is what an auditor expects to see.
    /// </summary>
    public class Ncr
    {
        public string Id { get; set; }
        public string NcrNo { get; set; }
        public string ProjectId { get; set; }

        /// <summary>Form this NCR was raised from, if any.</summary>
        public string FormId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
        public NcrSeverity Severity { get; set; }
        public NcrState State { get; set; }

        public string RaisedBy { get; set; }
        public DateTime RaisedAt { get; set; }

        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }

        public string RootCause { get; set; }
        public string CorrectiveAction { get; set; }

        public string ClosedBy { get; set; }
        public DateTime? ClosedAt { get; set; }

        public bool IsOverdue
        {
            get
            {
                return State != NcrState.Closed
                       && DueDate.HasValue
                       && DueDate.Value.Date < DateTime.Today;
            }
        }
    }

    /// <summary>A saved organisation-level setting.</summary>
    public class OrgSettings
    {
        public string CompanyName { get; set; }
        public string DocumentPrefix { get; set; }

        /// <summary>Require a second signature before a form counts as approved.</summary>
        public bool RequireDualSignoff { get; set; }

        /// <summary>Block submitting a form that has any rejected line without an NCR.</summary>
        public bool EnforceNcrOnReject { get; set; }

        /// <summary>Default number of days allowed to close out a new NCR.</summary>
        public int NcrDueDays { get; set; }

        public OrgSettings()
        {
            CompanyName = "Mzukulu Technologies (Pty) Ltd";
            DocumentPrefix = "QC";
            RequireDualSignoff = false;
            EnforceNcrOnReject = true;
            NcrDueDays = 14;
        }
    }
}

namespace MzuApplication.Models
{
    /// <summary>
    /// A registered user account. Passwords are never stored directly — only a salted
    /// PBKDF2 hash and the salt used to produce it, so the stored data cannot be
    /// reversed into the original password.
    /// </summary>
    public class UserAccount
    {
        public string Id { get; set; }

        /// <summary>Login name, matched case-insensitively.</summary>
        public string Username { get; set; }

        /// <summary>Full display name shown in the UI and audit trail.</summary>
        public string FullName { get; set; }

        public string Email { get; set; }

        /// <summary>Base64 PBKDF2 hash of the password.</summary>
        public string PasswordHash { get; set; }

        /// <summary>Base64 random salt mixed into the hash.</summary>
        public string PasswordSalt { get; set; }

        /// <summary>PBKDF2 iteration count used, stored so it can be raised later.</summary>
        public int HashIterations { get; set; }

        public UserRole Role { get; set; }

        /// <summary>Disabled accounts cannot sign in.</summary>
        public bool Active { get; set; }

        /// <summary>
        /// When true, the user is forced to set a new password at their next sign-in.
        /// Set after an administrator resets the password.
        /// </summary>
        public bool MustChangePassword { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
