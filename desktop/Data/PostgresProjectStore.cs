using System;
using System.Collections.Generic;
using Npgsql;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Project persistence backed by the shared Supabase <c>"Project"</c> table, so a
    /// project added by any user is visible to everyone. The app's richer Project model
    /// maps onto the team's columns; fields the table has no column for (site, contract
    /// number, dates) stay in the local store. Falls back to local when offline.
    /// </summary>
    internal static class PostgresProjectStore
    {
        private static NpgsqlConnection Open()
        {
            var conn = new NpgsqlConnection(DbConfig.ConnectionString);
            conn.Open();
            return conn;
        }

        private static int CurrentUserId()
        {
            int id;
            if (Auth.Current != null && int.TryParse(Auth.Current.Id, out id)) return id;
            return 1;
        }

        public static List<Project> GetAll()
        {
            var list = new List<Project>();
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(
                "select \"ProjectID\", \"ClientID\", \"ProjectName\", \"ProjectNumber\", "
                + "\"ProjectDetails\", \"IsActive\" from \"Project\" order by \"ProjectName\"", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Project
                    {
                        Id = r.GetInt32(0).ToString(),
                        ClientId = r.GetInt32(1).ToString(),
                        Name = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                        Number = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                        Description = r.IsDBNull(4) ? string.Empty : r.GetString(4),
                        Status = (!r.IsDBNull(5) && r.GetBoolean(5)) ? "Active" : "Complete",
                        Site = string.Empty,
                        ContractNo = string.Empty
                    });
                }
            }
            return list;
        }

        public static void Insert(Project p)
        {
            using (var conn = Open())
            {
                int newId;
                using (var cmd = new NpgsqlCommand(
                    "select coalesce(max(\"ProjectID\"), 0) + 1 from \"Project\"", conn))
                {
                    newId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                const string sql = @"
insert into ""Project""
    (""ProjectID"", ""ClientID"", ""ProjectName"", ""ProjectNumber"", ""ProjectDetails"",
     ""IsActive"", ""CreatedBy"", ""CreatedAt"")
values
    (@id, @cid, @name, @num, @details, @active, @by, @at)";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", newId);
                    cmd.Parameters.AddWithValue("cid", ParseId(p.ClientId));
                    cmd.Parameters.AddWithValue("name", p.Name ?? string.Empty);
                    cmd.Parameters.AddWithValue("num", p.Number ?? string.Empty);
                    cmd.Parameters.AddWithValue("details", (object)p.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("active", !string.Equals(p.Status, "Complete", StringComparison.OrdinalIgnoreCase));
                    cmd.Parameters.AddWithValue("by", CurrentUserId());
                    cmd.Parameters.AddWithValue("at", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
                p.Id = newId.ToString();
            }
        }

        public static void Update(Project p)
        {
            int id;
            if (!int.TryParse(p.Id, out id)) return;

            const string sql = @"
update ""Project"" set
    ""ClientID"" = @cid, ""ProjectName"" = @name, ""ProjectNumber"" = @num,
    ""ProjectDetails"" = @details, ""IsActive"" = @active,
    ""LastModifiedBy"" = @by, ""LastModifiedAt"" = @at
where ""ProjectID"" = @id";

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("cid", ParseId(p.ClientId));
                cmd.Parameters.AddWithValue("name", p.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("num", p.Number ?? string.Empty);
                cmd.Parameters.AddWithValue("details", (object)p.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("active", !string.Equals(p.Status, "Complete", StringComparison.OrdinalIgnoreCase));
                cmd.Parameters.AddWithValue("by", CurrentUserId());
                cmd.Parameters.AddWithValue("at", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }

        private static int ParseId(string id)
        {
            int n;
            return int.TryParse(id, out n) ? n : 0;
        }
    }
}
