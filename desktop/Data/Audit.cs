using System;
using System.Collections.Generic;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Append-only audit trail. Every entry records the acting user, so the log stays
    /// defensible; nothing here edits or removes existing entries.
    /// </summary>
    internal static class Audit
    {
        /// <summary>Records an action against an entity.</summary>
        public static void Log(AuditAction action, string entityType, string entityId,
                               string entityLabel, string detail)
        {
            Repository.Db.Audit.Add(new AuditEntry
            {
                Id = Repository.NewId("aud"),
                At = DateTime.Now,
                User = Repository.CurrentUserName ?? "system",
                Role = Repository.CurrentRole,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityLabel = entityLabel,
                Detail = detail
            });
        }

        /// <summary>Most recent entries first.</summary>
        public static List<AuditEntry> Recent(int count)
        {
            return Repository.Db.Audit
                .OrderByDescending(a => a.At)
                .Take(count)
                .ToList();
        }

        /// <summary>All entries for one entity, newest first.</summary>
        public static List<AuditEntry> ForEntity(string entityId)
        {
            return Repository.Db.Audit
                .Where(a => a.EntityId == entityId)
                .OrderByDescending(a => a.At)
                .ToList();
        }

        /// <summary>Filtered query used by the audit log screen.</summary>
        public static List<AuditEntry> Query(string search, string entityType,
                                             DateTime? from, DateTime? to)
        {
            IEnumerable<AuditEntry> q = Repository.Db.Audit;

            if (!string.IsNullOrWhiteSpace(entityType) && entityType != "All")
            {
                q = q.Where(a => a.EntityType == entityType);
            }

            if (from.HasValue)
            {
                q = q.Where(a => a.At.Date >= from.Value.Date);
            }

            if (to.HasValue)
            {
                q = q.Where(a => a.At.Date <= to.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                q = q.Where(a =>
                    (a.User ?? string.Empty).ToLowerInvariant().Contains(needle)
                    || (a.EntityLabel ?? string.Empty).ToLowerInvariant().Contains(needle)
                    || (a.Detail ?? string.Empty).ToLowerInvariant().Contains(needle)
                    || (a.EntityType ?? string.Empty).ToLowerInvariant().Contains(needle));
            }

            return q.OrderByDescending(a => a.At).ToList();
        }

        /// <summary>Display label for an action.</summary>
        public static string ActionText(AuditAction action)
        {
            switch (action)
            {
                case AuditAction.Created: return "Created";
                case AuditAction.Updated: return "Updated";
                case AuditAction.Submitted: return "Submitted";
                case AuditAction.StatusChanged: return "Status changed";
                case AuditAction.SignedOff: return "Signed off";
                case AuditAction.Deleted: return "Deleted";
                case AuditAction.Exported: return "Exported";
                case AuditAction.SignedIn: return "Signed in";
                case AuditAction.NcrRaised: return "NCR raised";
                case AuditAction.NcrClosed: return "NCR closed";
                default: return action.ToString();
            }
        }
    }
}
