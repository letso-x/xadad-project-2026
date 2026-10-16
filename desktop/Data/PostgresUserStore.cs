using System;
using System.Collections.Generic;
using Npgsql;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// User account persistence backed by the shared Supabase Postgres <c>"User"</c>
    /// table (the schema designed by the wider team). Only the user table is in
    /// Postgres; the rest of the demo data stays in the local JSON store.
    ///
    /// The team's table stores a single <c>PasswordHash</c> column, a split
    /// first/last name, and a text role. To keep the desktop app's PBKDF2 auth working
    /// without altering the shared schema, the salt and iteration count are packed into
    /// the <c>PasswordHash</c> value using a self-describing format:
    /// <code>pbkdf2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</code>
    /// There is no username column, so the app's login name maps to <c>Email</c>.
    /// There is no must-change-password column, so that remains a local-session concept.
    /// All methods are synchronous to fit the existing synchronous UI flow.
    /// </summary>
    internal static class PostgresUserStore
    {
        private const string Table = "\"User\"";

        private const string RoleAdmin = "Administrator";
        private const string RoleSiteUser = "SiteUser";

        /// <summary>Opens a connection using the configured string.</summary>
        private static NpgsqlConnection Open()
        {
            var conn = new NpgsqlConnection(DbConfig.ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Verifies connectivity and that the shared user table is reachable, seeding the
        /// default admin / site-user accounts only if the table is completely empty.
        /// Returns null on success, or an error message.
        /// </summary>
        public static string Initialise()
        {
            try
            {
                using (var conn = Open())
                {
                    if (CountUsers(conn) == 0)
                    {
                        InsertUser(conn, Auth.CreateSeedAccount(
                            "0", "admin@mzukulu.co.za", "System Administrator",
                            "admin@mzukulu.co.za", "admin123", UserRole.Admin));
                        InsertUser(conn, Auth.CreateSeedAccount(
                            "0", "siteuser@mzukulu.co.za", "Site User",
                            "siteuser@mzukulu.co.za", "user123", UserRole.SiteUser));
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static int CountUsers(NpgsqlConnection conn)
        {
            using (var cmd = new NpgsqlCommand("select count(*) from " + Table, conn))
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        #region CRUD

        public static List<UserAccount> GetAll()
        {
            var list = new List<UserAccount>();

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(SelectColumns + " from " + Table + " order by \"UserID\"", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(Map(reader));
                }
            }

            return list;
        }

        /// <summary>Looks a user up by login name, which maps to the Email column.</summary>
        public static UserAccount FindByUsername(string username)
        {
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(
                SelectColumns + " from " + Table + " where lower(\"Email\") = lower(@u)", conn))
            {
                cmd.Parameters.AddWithValue("u", username);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        public static void Insert(UserAccount account)
        {
            using (var conn = Open())
            {
                InsertUser(conn, account);
            }
        }

        private static void InsertUser(NpgsqlConnection conn, UserAccount a)
        {
            // UserID has no default/identity in the shared schema, so allocate the next id.
            int newId;
            using (var cmd = new NpgsqlCommand(
                "select coalesce(max(\"UserID\"), 0) + 1 from " + Table, conn))
            {
                newId = Convert.ToInt32(cmd.ExecuteScalar());
            }

            const string sql = @"
insert into ""User""
    (""UserID"", ""FirstName"", ""LastName"", ""Email"", ""PasswordHash"", ""Role"", ""IsActive"", ""CreatedAt"", ""LastModifiedAt"")
values
    (@id, @first, @last, @email, @hash, @role, @active, @created, @modified)";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                BindWrite(cmd, a, newId);
                cmd.ExecuteNonQuery();
            }

            // Reflect the assigned id back onto the model so callers hold the real key.
            a.Id = newId.ToString();
        }

        /// <summary>Writes mutable fields (name, email, password, role, active) back.</summary>
        public static void Update(UserAccount a)
        {
            const string sql = @"
update ""User"" set
    ""FirstName"" = @first,
    ""LastName"" = @last,
    ""Email"" = @email,
    ""PasswordHash"" = @hash,
    ""Role"" = @role,
    ""IsActive"" = @active,
    ""LastModifiedAt"" = @modified
where ""UserID"" = @id";

            using (var conn = Open())
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                BindWrite(cmd, a, ParseId(a.Id));
                cmd.ExecuteNonQuery();
            }
        }

        public static void Delete(string id)
        {
            using (var conn = Open())
            using (var cmd = new NpgsqlCommand("delete from " + Table + " where \"UserID\" = @id", conn))
            {
                cmd.Parameters.AddWithValue("id", ParseId(id));
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Mapping

        private const string SelectColumns =
            "select \"UserID\", \"FirstName\", \"LastName\", \"Email\", "
            + "\"PasswordHash\", \"Role\", \"IsActive\", \"CreatedAt\", \"LastModifiedAt\"";

        private static UserAccount Map(NpgsqlDataReader r)
        {
            string first = r.IsDBNull(1) ? string.Empty : r.GetString(1);
            string last = r.IsDBNull(2) ? string.Empty : r.GetString(2);
            string email = r.IsDBNull(3) ? string.Empty : r.GetString(3);

            var account = new UserAccount
            {
                Id = r.GetInt32(0).ToString(),
                Username = email,
                FullName = (first + " " + last).Trim(),
                Email = email,
                Role = ParseRole(r.IsDBNull(5) ? string.Empty : r.GetString(5)),
                Active = !r.IsDBNull(6) && r.GetBoolean(6),
                MustChangePassword = false,
                CreatedAt = r.IsDBNull(7) ? DateTime.Now : r.GetDateTime(7),
                LastLoginAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8)
            };

            // UnpackHash also restores the must-change-password flag packed in the column.
            UnpackHash(r.IsDBNull(4) ? string.Empty : r.GetString(4), account);
            return account;
        }

        private static void BindWrite(NpgsqlCommand cmd, UserAccount a, int id)
        {
            string first, last;
            SplitName(a.FullName, out first, out last);

            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("first", first);
            cmd.Parameters.AddWithValue("last", last);
            cmd.Parameters.AddWithValue("email", a.Email ?? a.Username ?? string.Empty);
            cmd.Parameters.AddWithValue("hash", PackHash(a));
            cmd.Parameters.AddWithValue("role", a.Role == UserRole.Admin ? RoleAdmin : RoleSiteUser);
            cmd.Parameters.AddWithValue("active", a.Active);
            cmd.Parameters.AddWithValue("created", a.CreatedAt == default(DateTime) ? DateTime.Now : a.CreatedAt);
            cmd.Parameters.AddWithValue("modified", (object)DateTime.Now);
        }

        private static int ParseId(string id)
        {
            int n;
            return int.TryParse(id, out n) ? n : 0;
        }

        private static UserRole ParseRole(string role)
        {
            if (!string.IsNullOrEmpty(role)
                && role.TrimStart().StartsWith("admin", StringComparison.OrdinalIgnoreCase))
            {
                return UserRole.Admin;
            }
            return UserRole.SiteUser;
        }

        private static void SplitName(string fullName, out string first, out string last)
        {
            fullName = (fullName ?? string.Empty).Trim();
            int space = fullName.IndexOf(' ');
            if (space < 0)
            {
                first = fullName.Length == 0 ? "User" : fullName;
                last = string.Empty;
            }
            else
            {
                first = fullName.Substring(0, space).Trim();
                last = fullName.Substring(space + 1).Trim();
            }
        }

        #endregion

        #region Password hash packing

        // Packs the PBKDF2 parameters AND the must-change-password flag into the single
        // PasswordHash column, since the shared schema has no columns for them:
        //   pbkdf2$<iterations>$<saltBase64>$<hashBase64>$<mustChange 0|1>
        // The trailing flag is optional, so older 4-segment values still parse.

        private static string PackHash(UserAccount a)
        {
            return string.Format("pbkdf2${0}${1}${2}${3}",
                a.HashIterations, a.PasswordSalt, a.PasswordHash,
                a.MustChangePassword ? "1" : "0");
        }

        private static void UnpackHash(string packed, UserAccount a)
        {
            // Default values for rows not written by this app (e.g. placeholder seeds).
            a.HashIterations = 100000;
            a.PasswordSalt = string.Empty;
            a.PasswordHash = packed ?? string.Empty;
            a.MustChangePassword = false;

            if (string.IsNullOrEmpty(packed)) return;
            if (!packed.StartsWith("pbkdf2$", StringComparison.Ordinal)) return;

            string[] parts = packed.Split('$');
            if (parts.Length < 4) return;

            int iter;
            if (int.TryParse(parts[1], out iter)) a.HashIterations = iter;
            a.PasswordSalt = parts[2];
            a.PasswordHash = parts[3];

            // Optional 5th segment: the must-change-password flag.
            if (parts.Length >= 5) a.MustChangePassword = parts[4] == "1";
        }

        #endregion
    }
}
