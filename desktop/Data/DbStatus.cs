using System;
using System.Diagnostics;
using Npgsql;

namespace MzuApplication.Data
{
    /// <summary>Result of a quick database connectivity check.</summary>
    internal sealed class DbPing
    {
        public bool Online;
        public long Milliseconds;
        public string Message;
    }

    /// <summary>
    /// Lightweight database connectivity check for the Settings status indicator. Runs a
    /// trivial round-trip (select 1) and reports whether the shared database is reachable.
    /// </summary>
    internal static class DbStatus
    {
        /// <summary>Pings the database. Fast, safe to call on demand.</summary>
        public static DbPing Check()
        {
            // Postgres not selected / not configured on this machine.
            if (!DbConfig.UsePostgres)
            {
                return new DbPing
                {
                    Online = false,
                    Message = "Local mode — not configured for the shared database."
                };
            }

            var sw = Stopwatch.StartNew();
            try
            {
                DbMetrics.Measure(() =>
                {
                    using (var conn = new NpgsqlConnection(DbConfig.ConnectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand("select 1", conn))
                        {
                            cmd.CommandTimeout = 8;
                            cmd.ExecuteScalar();
                        }
                    }
                });
                sw.Stop();
                return new DbPing
                {
                    Online = true,
                    Milliseconds = sw.ElapsedMilliseconds,
                    Message = "Connected to the shared Supabase database."
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                string m = ex.Message;
                if (m.IndexOf("28P01", StringComparison.OrdinalIgnoreCase) >= 0
                    || m.IndexOf("password authentication", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    m = "Password authentication failed — check database.config.";
                }
                return new DbPing { Online = false, Message = m };
            }
        }
    }
}
