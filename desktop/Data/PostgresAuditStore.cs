using System;
using System.Collections.Generic;
using Npgsql;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Shared, append-only audit trail in Supabase Postgres, so every user's actions are
    /// recorded centrally and the log is the same for everyone (truly "live"). Falls back
    /// to the local store when Postgres is unavailable. The table is auto-created.
    /// </summary>
    internal static class PostgresAuditStore
    {
        private static bool _schemaEnsured;

        private static NpgsqlConnection Open()
        {
            var conn = new NpgsqlConnection(DbConfig.ConnectionString);
            conn.Open();
            EnsureSchema(conn);
            return conn;
        }

        private static void EnsureSchema(NpgsqlConnection conn)
        {
            if (_schemaEnsured) return;

            const string ddl = @"
create table if not exists app_audit (
    id text primary key,
    at timestamptz not null,
    user_name text,
    role integer not null default 0,
    action integer not null,
    entity_type text,
    entity_id text,
    entity_label text,
    detail text,
    context text,
    host text,
    hash text,
    prev_hash text
);
create index if not exists app_audit_at_idx on app_audit (at desc);
create index if not exists app_audit_entity_idx on app_audit (entity_id);
-- Backfill columns for a table created by an earlier version.
alter table app_audit add column if not exists context text;
alter table app_audit add column if not exists host text;
alter table app_audit add column if not exists hash text;
alter table app_audit add column if not exists prev_hash text;";

            using (var cmd = new NpgsqlCommand(ddl, conn))
            {
                cmd.ExecuteNonQuery();
            }
            _schemaEnsured = true;
        }

        /// <summary>Appends one entry. Throws on failure so the caller can fall back.</summary>
        public static void Insert(AuditEntry e)
        {
            const string sql = @"
insert into app_audit
    (id, at, user_name, role, action, entity_type, entity_id, entity_label, detail,
     context, host, hash, prev_hash)
values
    (@id, @at, @user, @role, @action, @etype, @eid, @elabel, @detail,
     @context, @host, @hash, @prev)";

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", e.Id);
                cmd.Parameters.AddWithValue("at", e.At);
                cmd.Parameters.AddWithValue("user", (object)e.User ?? DBNull.Value);
                cmd.Parameters.AddWithValue("role", (int)e.Role);
                cmd.Parameters.AddWithValue("action", (int)e.Action);
                cmd.Parameters.AddWithValue("etype", (object)e.EntityType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("eid", (object)e.EntityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("elabel", (object)e.EntityLabel ?? DBNull.Value);
                cmd.Parameters.AddWithValue("detail", (object)e.Detail ?? DBNull.Value);
                cmd.Parameters.AddWithValue("context", (object)e.Context ?? DBNull.Value);
                cmd.Parameters.AddWithValue("host", (object)e.Host ?? DBNull.Value);
                cmd.Parameters.AddWithValue("hash", (object)e.Hash ?? DBNull.Value);
                cmd.Parameters.AddWithValue("prev", (object)e.PrevHash ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Returns all entries, newest first (optionally limited).</summary>
        public static List<AuditEntry> GetAll(int limit)
        {
            var list = new List<AuditEntry>();

            string sql = SelectColumns + " from app_audit order by at desc";
            if (limit > 0) sql += " limit " + limit;

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(sql, conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        /// <summary>
        /// A cheap change signature (row count + newest id) used to detect new activity
        /// for auto-refresh, without pulling the whole trail.
        /// </summary>
        public static string Signature()
        {
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(
                "select count(*)::text || '|' || coalesce((select id from app_audit order by at desc limit 1), 'none') from app_audit",
                conn))
            {
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? "0|none" : v.ToString();
            }
        }

        public static List<AuditEntry> ForEntity(string entityId)
        {
            var list = new List<AuditEntry>();

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(
                SelectColumns + " from app_audit where entity_id = @e order by at desc", conn))
            {
                cmd.Parameters.AddWithValue("e", (object)entityId ?? DBNull.Value);
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        private const string SelectColumns =
            "select id, at, user_name, role, action, entity_type, entity_id, entity_label, "
            + "detail, context, host, hash, prev_hash";

        private static AuditEntry Map(NpgsqlDataReader r)
        {
            return new AuditEntry
            {
                Id = r.GetString(0),
                // Keep the instant as UTC so hash recomputation is stable regardless of
                // the local timezone; the UI formats to local time when displaying.
                At = DateTime.SpecifyKind(r.GetDateTime(1).ToUniversalTime(), DateTimeKind.Utc),
                User = r.IsDBNull(2) ? "system" : r.GetString(2),
                Role = (UserRole)r.GetInt32(3),
                Action = (AuditAction)r.GetInt32(4),
                EntityType = r.IsDBNull(5) ? null : r.GetString(5),
                EntityId = r.IsDBNull(6) ? null : r.GetString(6),
                EntityLabel = r.IsDBNull(7) ? null : r.GetString(7),
                Detail = r.IsDBNull(8) ? null : r.GetString(8),
                Context = r.IsDBNull(9) ? null : r.GetString(9),
                Host = r.IsDBNull(10) ? null : r.GetString(10),
                Hash = r.IsDBNull(11) ? null : r.GetString(11),
                PrevHash = r.IsDBNull(12) ? null : r.GetString(12)
            };
        }
    }
}
