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
