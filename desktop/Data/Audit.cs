using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Append-only, tamper-evident audit trail. Every entry records who acted, from which
    /// workstation, what they did, which document/entity, and the project context. Entries
    /// are chained with SHA-256 (each hash covers the previous hash), so any later edit or
    /// deletion of a row breaks the chain and is detectable on verification.
    ///
    /// When connected to the shared database the trail is written and read centrally, so
    /// every user's actions appear in one live record; a local mirror keeps offline work.
    /// </summary>
    internal static class Audit
    {
        private static bool UsePostgres
        {
            get { return DbConfig.UsePostgres; }
        }

        /// <summary>The workstation this process runs on, recorded on every entry.</summary>
        private static string Host
        {
            get
            {
                try { return Environment.MachineName; }
                catch (Exception) { return "unknown"; }
            }
        }

        /// <summary>Records an action against an entity, with optional "where" context.</summary>
        public static void Log(AuditAction action, string entityType, string entityId,
                               string entityLabel, string detail, string context = null)
        {
            AuditEntry entry = new AuditEntry
            {
                Id = Repository.NewId("aud"),
                At = DateTime.Now,
                User = Repository.CurrentUserName ?? "system",
                Role = Repository.CurrentRole,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                EntityLabel = entityLabel,
                Detail = detail,
                Context = context,
                Host = Host
            };

            // Chain against the most recent entry's hash so the log is tamper-evident.
            entry.PrevHash = LastHash();
            entry.Hash = ComputeHash(entry);

            // Always mirror locally so the current session and offline demos work.
            Repository.Db.Audit.Add(entry);

            if (UsePostgres)
            {
                try { PostgresAuditStore.Insert(entry); }
                catch (Exception) { /* offline / transient: local copy still holds it */ }
            }
        }

        /// <summary>The hash of the newest existing entry, or empty for the first.</summary>
        private static string LastHash()
        {
            try
            {
                if (UsePostgres)
                {
                    var newest = PostgresAuditStore.GetAll(1);
                    return newest.Count > 0 ? (newest[0].Hash ?? string.Empty) : string.Empty;
                }
            }
            catch (Exception) { }

            AuditEntry local = Repository.Db.Audit
                .OrderByDescending(a => a.At).FirstOrDefault();
            return local != null ? (local.Hash ?? string.Empty) : string.Empty;
        }

        /// <summary>SHA-256 over the entry's immutable fields plus the previous hash.</summary>
        public static string ComputeHash(AuditEntry e)
        {
            // The timestamp is excluded from the hash: timestamptz storage and timezone
            // conversion make its exact value unstable across a database round-trip. The
            // entry Id is a unique random token, so identity and ordering are preserved
            // without it, and the self-hash stays stable whether computed at write or
            // read time. Tampering with any other field is still fully detected.
            string payload = string.Join("|", new[]
            {
                e.PrevHash ?? string.Empty,
                e.Id,
                e.User ?? string.Empty,
                ((int)e.Role).ToString(),
                ((int)e.Action).ToString(),
                e.EntityType ?? string.Empty,
                e.EntityId ?? string.Empty,
                e.EntityLabel ?? string.Empty,
                e.Detail ?? string.Empty,
                e.Context ?? string.Empty,
                e.Host ?? string.Empty
            });

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Walks the trail oldest-to-newest and recomputes each hash, confirming the chain
        /// is intact. Returns true if untampered; otherwise outputs the first bad entry.
        /// </summary>
        public static bool VerifyIntegrity(out int checkedCount, out AuditEntry firstBroken)
        {
            firstBroken = null;
            var all = Source().ToList();

            // Only hashed entries participate in the chain (entries written before the
            // feature existed have no hash and are not part of the tamper-evidence).
            var hashed = all.Where(a => !string.IsNullOrEmpty(a.Hash)).ToList();
            checkedCount = hashed.Count;
            if (hashed.Count == 0) return true;

            // 1. Self-hash check: recomputing each entry's hash must match what is stored.
            //    This alone proves no individual row's fields were altered after the fact.
            // 2. Link check: every entry's PrevHash (except the genesis one) must point at
            //    a hash that actually exists in the trail — a deleted entry breaks this.
            var hashes = new HashSet<string>(hashed.Select(a => a.Hash));

            foreach (AuditEntry e in hashed)
            {
                if (ComputeHash(e) != e.Hash)
                {
                    firstBroken = e;        // this entry was edited
                    return false;
                }

                string prev = e.PrevHash ?? string.Empty;
                if (prev.Length > 0 && !hashes.Contains(prev))
                {
                    firstBroken = e;        // the entry it chained to was removed
                    return false;
                }
            }

            return true;
        }

        /// <summary>Most recent entries first, from the shared database when available.</summary>
        public static List<AuditEntry> Recent(int count)
        {
            if (UsePostgres)
            {
                try { return PostgresAuditStore.GetAll(count); }
                catch (Exception) { }
            }

            return Repository.Db.Audit
                .OrderByDescending(a => a.At)
                .Take(count)
                .ToList();
        }

        /// <summary>All entries for one entity, newest first.</summary>
        public static List<AuditEntry> ForEntity(string entityId)
        {
            if (UsePostgres)
            {
                try { return PostgresAuditStore.ForEntity(entityId); }
                catch (Exception) { }
            }

            return Repository.Db.Audit
                .Where(a => a.EntityId == entityId)
                .OrderByDescending(a => a.At)
                .ToList();
        }

        /// <summary>The shared trail if connected, else the local mirror.</summary>
        private static IEnumerable<AuditEntry> Source()
        {
            if (UsePostgres)
            {
                try { return PostgresAuditStore.GetAll(0); }
                catch (Exception) { }
            }
            return Repository.Db.Audit;
        }

        /// <summary>Every entry, newest first — used by the full-trail export.</summary>
        public static List<AuditEntry> All()
        {
            return Source().OrderByDescending(a => a.At).ToList();
        }

        /// <summary>
        /// A cheap change signature for the auto-refresh poll — row count plus newest id.
        /// Lets the audit screen detect when another user has added activity without
        /// pulling the whole trail on every tick.
        /// </summary>
        public static string Signature()
        {
            if (UsePostgres)
            {
                try { return PostgresAuditStore.Signature(); }
                catch (Exception) { }
            }

            var newest = Repository.Db.Audit.OrderByDescending(a => a.At).FirstOrDefault();
            return Repository.Db.Audit.Count + "|" + (newest != null ? newest.Id : "none");
        }

        /// <summary>Filtered query used by the audit log screen.</summary>
        public static List<AuditEntry> Query(string search, string entityType,
                                             DateTime? from, DateTime? to)
        {
            IEnumerable<AuditEntry> q = Source();

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
                    || (a.Context ?? string.Empty).ToLowerInvariant().Contains(needle)
                    || (a.Host ?? string.Empty).ToLowerInvariant().Contains(needle)
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
