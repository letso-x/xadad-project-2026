using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>Creates and progresses non-conformance reports.</summary>
    internal static class NcrService
    {
        /// <summary>Allocates the next sequential NCR number.</summary>
        public static string NextNcrNo()
        {
            var numbers = new List<int>();

            foreach (Ncr ncr in Repository.Db.Ncrs)
            {
                if (string.IsNullOrEmpty(ncr.NcrNo)) continue;

                int dash = ncr.NcrNo.LastIndexOf('-');
                if (dash < 0 || dash == ncr.NcrNo.Length - 1) continue;

                int parsed;
                if (int.TryParse(ncr.NcrNo.Substring(dash + 1),
                                 NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    numbers.Add(parsed);
                }
            }

            int next = (numbers.Count > 0 ? numbers.Max() : 0) + 1;
            return "NCR-" + next.ToString("D4", CultureInfo.InvariantCulture);
        }

        /// <summary>Raises a new NCR, defaulting the due date from org settings.</summary>
        public static Ncr Raise(string projectId, string formId, string title,
                                string description, NcrSeverity severity, string assignedTo)
        {
            Ncr ncr = new Ncr
            {
                Id = Repository.NewId("ncr"),
                NcrNo = NextNcrNo(),
                ProjectId = projectId,
                FormId = formId,
                Title = title,
                Description = description,
                Severity = severity,
                State = NcrState.Open,
                RaisedBy = Repository.CurrentUserName,
                RaisedAt = DateTime.Now,
                AssignedTo = assignedTo,
                DueDate = DateTime.Today.AddDays(Repository.Db.Settings.NcrDueDays),
                RootCause = string.Empty,
                CorrectiveAction = string.Empty
            };

            Repository.Db.Ncrs.Add(ncr);

            Audit.Log(AuditAction.NcrRaised, "NCR", ncr.Id, ncr.NcrNo,
                severity + " · " + title);

            return ncr;
        }

        /// <summary>Moves an NCR to a new state, recording close-out details.</summary>
        public static void SetState(Ncr ncr, NcrState state)
        {
            NcrState previous = ncr.State;
            ncr.State = state;

            if (state == NcrState.Closed)
            {
                ncr.ClosedBy = Repository.CurrentUserName;
                ncr.ClosedAt = DateTime.Now;

                Audit.Log(AuditAction.NcrClosed, "NCR", ncr.Id, ncr.NcrNo,
                    "Closed out by " + Repository.CurrentUserName);
            }
            else
            {
                ncr.ClosedBy = null;
                ncr.ClosedAt = null;

                Audit.Log(AuditAction.StatusChanged, "NCR", ncr.Id, ncr.NcrNo,
                    Format.NcrStateText(previous) + " → " + Format.NcrStateText(state));
            }
        }

        public static List<Ncr> Open()
        {
            return Repository.Db.Ncrs
                .Where(n => n.State != NcrState.Closed)
                .OrderBy(n => n.DueDate ?? DateTime.MaxValue)
                .ToList();
        }

        public static List<Ncr> Overdue()
        {
            return Repository.Db.Ncrs.Where(n => n.IsOverdue).ToList();
        }

        public static List<Ncr> ForProject(string projectId)
        {
            return Repository.Db.Ncrs.Where(n => n.ProjectId == projectId).ToList();
        }
    }
}
