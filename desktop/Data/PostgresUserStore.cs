using System;
using System.Collections.Generic;
using Npgsql;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// User account persistence backed by Supabase Postgres via Npgsql. Only the user
    /// table is in Postgres for now; the rest of the demo data stays in the local JSON
    /// store. All methods are synchronous to fit the existing synchronous UI flow.
    /// </summary>
    internal static class PostgresUserStore
    {
        private static bool _schemaEnsured;

        /// <summary>Opens a connection using the configured string.</summary>
        private static NpgsqlConnection Open()
        {
            var conn = new NpgsqlConnection(DbConfig.ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Verifies connectivity and that the users table exists, creating it and the
        /// default accounts on first run. Returns null on success, or an error message.
        /// </summary>
        public static string Initialise()
        {
            try
            {
                using (var conn = Open())
                {
                    EnsureSchema(conn);

                    // Seed the default admin / site-user accounts if the table is empty,
                    // so a fresh database is immediately usable.
                    if (CountUsers(conn) == 0)
                    {
                        InsertUser(conn, Auth.CreateSeedAccount(
                            "usr-admin", "admin", "S. Ndlovu", "s.ndlovu@mzukulu.co.za",
                            "admin123", UserRole.Admin));
                        InsertUser(conn, Auth.CreateSeedAccount(
                            "usr-user", "siteuser", "T. Mahlangu", "t.mahlangu@mzukulu.co.za",
                            "user123", UserRole.SiteUser));
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void EnsureSchema(NpgsqlConnection conn)
        {
            if (_schemaEnsured) return;

            const string ddl = @"
create table if not exists app_users (
    id text primary key,
    username text not null unique,
    full_name text not null,
    email text,
    password_hash text not null,
    password_salt text not null,
    hash_iterations integer not null default 100000,
    role integer not null default 2,
    active boolean not null default true,
    must_change_password boolean not null default false,
    created_at timestamptz not null default now(),
    last_login_at timestamptz
);
create unique index if not exists app_users_username_lower_idx on app_users (lower(username));";

            using (var cmd = new NpgsqlCommand(ddl, conn))
            {
                cmd.ExecuteNonQuery();
            }

            _schemaEnsured = true;
        }

        private static int CountUsers(NpgsqlConnection conn)
        {
            using (var cmd = new NpgsqlCommand("select count(*) from app_users", conn))
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        #region CRUD

        public static List<UserAccount> GetAll()
        {
            var list = new List<UserAccount>();

            using (var conn = Open())
            {
                EnsureSchema(conn);
                using (var cmd = new NpgsqlCommand(SelectColumns + " from app_users", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        public static UserAccount FindByUsername(string username)
        {
            using (var conn = Open())
            {
                EnsureSchema(conn);
                using (var cmd = new NpgsqlCommand(
                    SelectColumns + " from app_users where lower(username) = lower(@u)", conn))
                {
                    cmd.Parameters.AddWithValue("u", username);
                    using (var reader = cmd.ExecuteReader())
                    {
                        return reader.Read() ? Map(reader) : null;
                    }
                }
            }
        }

        public static void Insert(UserAccount account)
        {
            using (var conn = Open())
            {
                EnsureSchema(conn);
                InsertUser(conn, account);
            }
        }

        private static void InsertUser(NpgsqlConnection conn, UserAccount a)
        {
            const string sql = @"
insert into app_users
    (id, username, full_name, email, password_hash, password_salt,
     hash_iterations, role, active, must_change_password, created_at, last_login_at)
values
    (@id, @username, @full_name, @email, @hash, @salt,
     @iter, @role, @active, @must, @created, @last)";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                BindAll(cmd, a);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Writes mutable fields (password, flags, role, last login) back.</summary>
        public static void Update(UserAccount a)
        {
            const string sql = @"
update app_users set
    full_name = @full_name,
    email = @email,
    password_hash = @hash,
    password_salt = @salt,
    hash_iterations = @iter,
    role = @role,
    active = @active,
    must_change_password = @must,
    last_login_at = @last
where id = @id";

            using (var conn = Open())
            {
                EnsureSchema(conn);
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    BindAll(cmd, a);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void Delete(string id)
        {
            using (var conn = Open())
            {
                EnsureSchema(conn);
                using (var cmd = new NpgsqlCommand("delete from app_users where id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Mapping

        private const string SelectColumns =
            "select id, username, full_name, email, password_hash, password_salt, "
            + "hash_iterations, role, active, must_change_password, created_at, last_login_at";

        private static UserAccount Map(NpgsqlDataReader r)
        {
            return new UserAccount
            {
                Id = r.GetString(0),
                Username = r.GetString(1),
                FullName = r.GetString(2),
                Email = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                PasswordHash = r.GetString(4),
                PasswordSalt = r.GetString(5),
                HashIterations = r.GetInt32(6),
                Role = (UserRole)r.GetInt32(7),
                Active = r.GetBoolean(8),
                MustChangePassword = r.GetBoolean(9),
                CreatedAt = r.GetDateTime(10),
                LastLoginAt = r.IsDBNull(11) ? (DateTime?)null : r.GetDateTime(11)
            };
        }

        private static void BindAll(NpgsqlCommand cmd, UserAccount a)
        {
            cmd.Parameters.AddWithValue("id", a.Id);
            cmd.Parameters.AddWithValue("username", a.Username);
            cmd.Parameters.AddWithValue("full_name", a.FullName ?? string.Empty);
            cmd.Parameters.AddWithValue("email", (object)a.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("hash", a.PasswordHash);
            cmd.Parameters.AddWithValue("salt", a.PasswordSalt);
            cmd.Parameters.AddWithValue("iter", a.HashIterations);
            cmd.Parameters.AddWithValue("role", (int)a.Role);
            cmd.Parameters.AddWithValue("active", a.Active);
            cmd.Parameters.AddWithValue("must", a.MustChangePassword);
            cmd.Parameters.AddWithValue("created", a.CreatedAt);
            cmd.Parameters.AddWithValue("last", (object)a.LastLoginAt ?? DBNull.Value);
        }

        #endregion
    }
}
