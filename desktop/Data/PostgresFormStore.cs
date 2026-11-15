using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Npgsql;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Shared QC form storage. Each form is stored as its own JSON document in a dedicated
    /// <c>app_forms</c> table — the same reliable pattern used for the audit trail and NCRs
    /// — so forms round-trip exactly and are visible to every user. The table auto-creates.
    /// </summary>
    internal static class PostgresFormStore
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
create table if not exists app_forms (
    id text primary key,
    project_id text,
    template_id text,
    qc_record_no text,
    status integer not null default 0,
    updated_at timestamptz,
    doc text
);
create index if not exists app_forms_project_idx on app_forms (project_id);";
            using (var cmd = new NpgsqlCommand(ddl, conn)) cmd.ExecuteNonQuery();
            _schemaEnsured = true;
        }

        private static string ToJson(QcForm form)
        {
            var ser = new DataContractJsonSerializer(typeof(QcForm));
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, form);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static QcForm FromJson(string json)
        {
            var ser = new DataContractJsonSerializer(typeof(QcForm));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (QcForm)ser.ReadObject(ms);
            }
        }

        /// <summary>Inserts or updates a form (upsert by id).</summary>
        public static void Save(QcForm form)
        {
            DbMetrics.Measure(() =>
            {
                using (var conn = Open())
                using (var cmd = new NpgsqlCommand(@"
insert into app_forms (id, project_id, template_id, qc_record_no, status, updated_at, doc)
values (@id,@pid,@tid,@rec,@status,@upd,@doc)
on conflict (id) do update set
   project_id=@pid, template_id=@tid, qc_record_no=@rec,
   status=@status, updated_at=@upd, doc=@doc", conn))
                {
                    cmd.Parameters.AddWithValue("id", form.Id);
                    cmd.Parameters.AddWithValue("pid", (object)form.ProjectId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("tid", (object)form.TemplateId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("rec", (object)form.QcRecordNo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("status", (int)form.Status);
                    cmd.Parameters.AddWithValue("upd", form.UpdatedAt == default(DateTime)
                        ? (object)DateTime.UtcNow : form.UpdatedAt.ToUniversalTime());
                    cmd.Parameters.AddWithValue("doc", ToJson(form));
                    cmd.ExecuteNonQuery();
                }
            });
        }

        /// <summary>All forms across every project.</summary>
        public static List<QcForm> GetAll()
        {
            return DbMetrics.Measure(() =>
            {
                var list = new List<QcForm>();
                using (var conn = Open())
                using (var cmd = new NpgsqlCommand("select doc from app_forms order by updated_at desc", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        try { if (!r.IsDBNull(0)) list.Add(FromJson(r.GetString(0))); }
                        catch (Exception) { }
                    }
                }
                return list;
            });
        }

        /// <summary>Forms for one project.</summary>
        public static List<QcForm> ForProject(string projectId)
        {
            return DbMetrics.Measure(() =>
            {
                var list = new List<QcForm>();
                using (var conn = Open())
                using (var cmd = new NpgsqlCommand(
                    "select doc from app_forms where project_id=@p order by updated_at desc", conn))
                {
                    cmd.Parameters.AddWithValue("p", (object)projectId ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            try { if (!r.IsDBNull(0)) list.Add(FromJson(r.GetString(0))); }
                            catch (Exception) { }
                        }
                    }
                }
                return list;
            });
        }

        public static QcForm Get(string id)
        {
            return DbMetrics.Measure(() =>
            {
                using (var conn = Open())
                using (var cmd = new NpgsqlCommand("select doc from app_forms where id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    object o = cmd.ExecuteScalar();
                    return (o != null && o != DBNull.Value) ? FromJson((string)o) : null;
                }
            });
        }
    }
}
