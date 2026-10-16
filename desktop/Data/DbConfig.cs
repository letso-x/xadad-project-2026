using System;
using System.Configuration;
using System.IO;

namespace MzuApplication.Data
{
    /// <summary>
    /// Reads database configuration. The connection string (which contains the
    /// password) is kept out of source control in an external <c>database.config</c>
    /// file next to the executable; only the non-secret mode flag lives in App.config.
    /// </summary>
    internal static class DbConfig
    {
        private static bool _loaded;
        private static string _connectionString;

        /// <summary>
        /// Set when a Postgres connection attempt fails at startup, so the rest of the
        /// app transparently falls back to the local store for the session.
        /// </summary>
        private static bool _postgresDisabled;

        /// <summary>Disables Postgres for the remainder of this session.</summary>
        public static void DisablePostgres()
        {
            _postgresDisabled = true;
        }

        /// <summary>True when Postgres mode is selected, configured, and not disabled.</summary>
        public static bool UsePostgres
        {
            get
            {
                if (_postgresDisabled) return false;

                string mode = ConfigurationManager.AppSettings["Database.Mode"];
                bool postgresRequested = string.Equals(mode, "Postgres",
                    StringComparison.OrdinalIgnoreCase);

                return postgresRequested && !string.IsNullOrEmpty(ConnectionString);
            }
        }

        /// <summary>The Npgsql connection string, or null if not configured.</summary>
        public static string ConnectionString
        {
            get
            {
                if (!_loaded) Load();
                return _connectionString;
            }
        }

        private static string ConfigPath
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "database.config");
            }
        }

        private static void Load()
        {
            _loaded = true;
            _connectionString = null;

            try
            {
                if (!File.Exists(ConfigPath)) return;

                foreach (string raw in File.ReadAllLines(ConfigPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    const string key = "ConnectionString=";
                    if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring(key.Length).Trim();
                        if (value.Length > 0
                            && value.IndexOf("YOUR_PASSWORD_HERE", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            _connectionString = value;
                        }
                        return;
                    }
                }
            }
            catch (Exception)
            {
                _connectionString = null;
            }
        }
    }
}
