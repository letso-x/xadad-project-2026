using System;
using System.Collections.Generic;
using Npgsql;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Non-conformance reports in a shared Postgres table so an NCR raised by any user is
    /// visible to everyone. The table is auto-created. Falls back to the local store when
    /// Postgres is unavailable.
    /// </summary>
    internal static class PostgresNcrStore
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
create table if not exists app_ncr (
    id text primary key,
    ncr_no text,
    project_id text,
    form_id text,
    title text,
    description text,
    severity integer not null default 0,
    state integer not null default 0,
    raised_by text,
    raised_at timestamptz,
    assigned_to text,
    due_date timestamptz,
    root_cause text,
    corrective_action text,
    closed_by text,
    closed_at timestamptz
);
create index if not exists app_ncr_project_idx on app_ncr (project_id);";

            using (var cmd = new NpgsqlCommand(ddl, conn)) cmd.ExecuteNonQuery();
            _schemaEnsured = true;
        }

        public static List<Ncr> GetAll()
        {
            var list = new List<Ncr>();
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(Select + " from app_ncr order by raised_at desc", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public static void Insert(Ncr n)
        {
            const string sql = @"
insert into app_ncr
    (id, ncr_no, project_id, form_id, title, description, severity, state,
     raised_by, raised_at, assigned_to, due_date, root_cause, corrective_action,
     closed_by, closed_at)
values
    (@id, @no, @pid, @fid, @title, @desc, @sev, @state,
     @rby, @rat, @ass, @due, @root, @corr, @cby, @cat)";

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                Bind(cmd, n);
                cmd.ExecuteNonQuery();
            }
        }

        public static void Update(Ncr n)
        {
            const string sql = @"
update app_ncr set
    ncr_no = @no, project_id = @pid, form_id = @fid, title = @title,
    description = @desc, severity = @sev, state = @state, raised_by = @rby,
    raised_at = @rat, assigned_to = @ass, due_date = @due, root_cause = @root,
    corrective_action = @corr, closed_by = @cby, closed_at = @cat
where id = @id";

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                Bind(cmd, n);
                cmd.ExecuteNonQuery();
            }
        }

        private static void Bind(NpgsqlCommand cmd, Ncr n)
        {
            cmd.Parameters.AddWithValue("id", n.Id);
            cmd.Parameters.AddWithValue("no", (object)n.NcrNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("pid", (object)n.ProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("fid", (object)n.FormId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("title", (object)n.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("desc", (object)n.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sev", (int)n.Severity);
            cmd.Parameters.AddWithValue("state", (int)n.State);
            cmd.Parameters.AddWithValue("rby", (object)n.RaisedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rat", (object)n.RaisedAt.ToUniversalTime());
            cmd.Parameters.AddWithValue("ass", (object)n.AssignedTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("due",
                n.DueDate.HasValue ? (object)n.DueDate.Value.ToUniversalTime() : DBNull.Value);
            cmd.Parameters.AddWithValue("root", (object)n.RootCause ?? DBNull.Value);
            cmd.Parameters.AddWithValue("corr", (object)n.CorrectiveAction ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cby", (object)n.ClosedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cat",
                n.ClosedAt.HasValue ? (object)n.ClosedAt.Value.ToUniversalTime() : DBNull.Value);
        }

        private const string Select =
            "select id, ncr_no, project_id, form_id, title, description, severity, state, "
            + "raised_by, raised_at, assigned_to, due_date, root_cause, corrective_action, "
            + "closed_by, closed_at";

        private static DateTime? LocalNullable(NpgsqlDataReader r, int i)
        {
            if (r.IsDBNull(i)) return null;
            return DateTime.SpecifyKind(r.GetDateTime(i).ToUniversalTime(), DateTimeKind.Utc).ToLocalTime();
        }

        private static Ncr Map(NpgsqlDataReader r)
        {
            return new Ncr
            {
                Id = r.GetString(0),
                NcrNo = r.IsDBNull(1) ? null : r.GetString(1),
                ProjectId = r.IsDBNull(2) ? null : r.GetString(2),
                FormId = r.IsDBNull(3) ? null : r.GetString(3),
                Title = r.IsDBNull(4) ? null : r.GetString(4),
                Description = r.IsDBNull(5) ? null : r.GetString(5),
                Severity = (NcrSeverity)r.GetInt32(6),
                State = (NcrState)r.GetInt32(7),
                RaisedBy = r.IsDBNull(8) ? null : r.GetString(8),
                RaisedAt = r.IsDBNull(9) ? DateTime.Now : r.GetDateTime(9).ToUniversalTime().ToLocalTime(),
                AssignedTo = r.IsDBNull(10) ? null : r.GetString(10),
                DueDate = LocalNullable(r, 11),
                RootCause = r.IsDBNull(12) ? null : r.GetString(12),
                CorrectiveAction = r.IsDBNull(13) ? null : r.GetString(13),
                ClosedBy = r.IsDBNull(14) ? null : r.GetString(14),
                ClosedAt = LocalNullable(r, 15)
            };
        }
    }
}
