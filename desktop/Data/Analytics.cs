using System;
using System.Collections.Generic;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>A single measured value for the dashboard.</summary>
    internal class Kpi
    {
        public string Label { get; set; }
        public string Value { get; set; }

        /// <summary>Supporting caption, e.g. "3 of 4 accepted".</summary>
        public string Caption { get; set; }

        /// <summary>True to render in the warning palette.</summary>
        public bool Warn { get; set; }
    }

    /// <summary>Roll-up figures for management reporting.</summary>
    internal static class Analytics
    {
        /// <summary>
        /// First-pass yield: share of finalised forms accepted without an NCR. This is
        /// the headline quality measure most clients ask for.
        /// </summary>
        public static int FirstPassYield(out int accepted, out int finalised)
        {
            var done = Repository.Db.Forms
                .Where(f => f.Status != RecordStatus.InProgress)
                .ToList();

            finalised = done.Count;
            accepted = done.Count(f => f.Status == RecordStatus.CompleteAccepted);

            return finalised == 0 ? 0 : (int)Math.Round(accepted * 100.0 / finalised);
        }

        /// <summary>Share of all checklist lines answered across in-progress work.</summary>
        public static int OverallCompletion(out int answered, out int total)
        {
            answered = 0;
            total = 0;

            foreach (QcForm form in Repository.Db.Forms)
            {
                ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
                if (template == null) continue;

                total += template.AllItems().Count;
                answered += form.AnsweredCount;
            }

            return total == 0 ? 0 : (int)Math.Round(answered * 100.0 / total);
        }

        /// <summary>Count of individual rejected lines awaiting close-out.</summary>
        public static int OpenRejections()
        {
            return Repository.Db.Forms
                .SelectMany(f => f.Items)
                .Count(i => i.Response == ItemResponse.Rejected);
        }

        /// <summary>Average days from raising an NCR to closing it.</summary>
        public static double AverageNcrCloseDays(out int closedCount)
        {
            var closed = Repository.Db.Ncrs
                .Where(n => n.State == NcrState.Closed && n.ClosedAt.HasValue)
                .ToList();

            closedCount = closed.Count;
            if (closedCount == 0) return 0;

            return closed.Average(n => (n.ClosedAt.Value - n.RaisedAt).TotalDays);
        }

        /// <summary>Records touched in the last <paramref name="days"/> days.</summary>
        public static int RecentActivityCount(int days)
        {
            DateTime cutoff = DateTime.Now.AddDays(-days);

            return Repository.Db.Forms.Count(f => f.UpdatedAt >= cutoff)
                   + Repository.Db.Registers.Count(r => r.UpdatedAt >= cutoff);
        }

        /// <summary>Forms sitting unfinished with no update for a while.</summary>
        public static List<QcForm> StaleForms(int days)
        {
            DateTime cutoff = DateTime.Now.AddDays(-days);

            return Repository.Db.Forms
                .Where(f => f.Status == RecordStatus.InProgress && f.UpdatedAt < cutoff)
                .OrderBy(f => f.UpdatedAt)
                .ToList();
        }

        /// <summary>Forms fully answered but missing the required sign-off.</summary>
        public static List<QcForm> AwaitingSignoff()
        {
            var list = new List<QcForm>();

            foreach (QcForm form in Repository.Db.Forms)
            {
                if (form.Status != RecordStatus.InProgress) continue;

                ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
                if (template == null) continue;

                if (form.AnsweredCount >= template.AllItems().Count
                    && !form.Inspected.IsSigned)
                {
                    list.Add(form);
                }
            }

            return list;
        }

        /// <summary>Per-template usage and acceptance, for the reports screen.</summary>
        public static List<Tuple<ChecklistTemplate, int, int>> TemplateUsage()
        {
            var result = new List<Tuple<ChecklistTemplate, int, int>>();

            foreach (ChecklistTemplate template in Repository.Db.Templates)
            {
                var forms = Repository.Db.Forms.Where(f => f.TemplateId == template.Id).ToList();
                int acceptedCount = forms.Count(f => f.Status == RecordStatus.CompleteAccepted);

                result.Add(Tuple.Create(template, forms.Count, acceptedCount));
            }

            return result.OrderByDescending(r => r.Item2).ToList();
        }

        /// <summary>Response mix across every answered line, for the quality breakdown.</summary>
        public static void ResponseMix(out int accepted, out int rejected, out int notApplicable)
        {
            var all = Repository.Db.Forms.SelectMany(f => f.Items).ToList();

            accepted = all.Count(i => i.Response == ItemResponse.Accepted);
            rejected = all.Count(i => i.Response == ItemResponse.Rejected);
            notApplicable = all.Count(i => i.Response == ItemResponse.NotApplicable);
        }

        /// <summary>Items needing attention, shown as an action queue on the dashboard.</summary>
        public static List<Tuple<string, string, Action>> BuildActionQueue(
            Func<QcForm, Action> openForm, Func<Ncr, Action> openNcr)
        {
            var queue = new List<Tuple<string, string, Action>>();

            foreach (Ncr ncr in NcrService.Overdue())
            {
                queue.Add(Tuple.Create(
                    ncr.NcrNo + " overdue",
                    "Due " + Format.Date(ncr.DueDate) + " · " + ncr.Title,
                    openNcr(ncr)));
            }

            foreach (QcForm form in AwaitingSignoff())
            {
                ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
                queue.Add(Tuple.Create(
                    "Awaiting sign-off",
                    (template != null ? template.DocNumber + " · " : string.Empty) + form.QcRecordNo,
                    openForm(form)));
            }

            foreach (QcForm form in StaleForms(21))
            {
                ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
                queue.Add(Tuple.Create(
                    "No progress in 21 days",
                    (template != null ? template.DocNumber + " · " : string.Empty) + form.QcRecordNo
                        + " · " + Format.Relative(form.UpdatedAt),
                    openForm(form)));
            }

            return queue;
        }
    }
}
