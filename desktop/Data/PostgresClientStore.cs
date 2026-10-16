using System;
using System.Collections.Generic;
using Npgsql;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Client persistence backed by the shared Supabase <c>"Client"</c> table, so clients
    /// added by any user are visible to everyone. The app's simple Client model maps onto
    /// the team's richer columns: the single contact field maps to ContactName. Falls back
    /// to the local store when Postgres is unavailable.
    /// </summary>
    internal static class PostgresClientStore
    {
        private static NpgsqlConnection Open()
        {
            var conn = new NpgsqlConnection(DbConfig.ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>Current signed-in user's numeric id, for CreatedBy / LastModifiedBy.</summary>
        private static int CurrentUserId()
        {
            int id;
            if (Auth.Current != null && int.TryParse(Auth.Current.Id, out id)) return id;
            return 1; // fall back to the seed admin if unknown
        }

        public static List<Client> GetAll()
        {
            var list = new List<Client>();
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(
                "select \"ClientID\", \"ClientName\", \"ContactName\", \"IsActive\" "
                + "from \"Client\" order by \"ClientName\"", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Client
                    {
                        Id = r.GetInt32(0).ToString(),
                        Name = r.IsDBNull(1) ? string.Empty : r.GetString(1),
                        Contact = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                        Active = !r.IsDBNull(3) && r.GetBoolean(3)
                    });
                }
            }
            return list;
        }

        /// <summary>Inserts a new client and returns the assigned ClientID as the model Id.</summary>
        public static void Insert(Client client)
        {
            using (var conn = Open())
            {
                int newId;
                using (var cmd = new NpgsqlCommand(
                    "select coalesce(max(\"ClientID\"), 0) + 1 from \"Client\"", conn))
                {
                    newId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                const string sql = @"
insert into ""Client""
    (""ClientID"", ""ClientName"", ""ContactName"", ""IsActive"", ""CreatedBy"", ""CreatedAt"")
values
    (@id, @name, @contact, @active, @by, @at)";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", newId);
                    cmd.Parameters.AddWithValue("name", client.Name ?? string.Empty);
                    cmd.Parameters.AddWithValue("contact", (object)client.Contact ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("active", client.Active);
                    cmd.Parameters.AddWithValue("by", CurrentUserId());
                    cmd.Parameters.AddWithValue("at", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }

                client.Id = newId.ToString();
            }
        }

        /// <summary>Updates an existing client's mutable fields.</summary>
        public static void Update(Client client)
        {
            int id;
            if (!int.TryParse(client.Id, out id)) return;

            const string sql = @"
update ""Client"" set
    ""ClientName"" = @name,
    ""ContactName"" = @contact,
    ""IsActive"" = @active,
    ""LastModifiedBy"" = @by,
    ""LastModifiedAt"" = @at
where ""ClientID"" = @id";

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("name", client.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("contact", (object)client.Contact ?? DBNull.Value);
                cmd.Parameters.AddWithValue("active", client.Active);
                cmd.Parameters.AddWithValue("by", CurrentUserId());
                cmd.Parameters.AddWithValue("at", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
