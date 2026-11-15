using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MzuApplication.Data
{
    /// <summary>
    /// Read-only system diagnostics for the Settings "System Health" panel: app version,
    /// runtime, environment, storage mode, config presence and the current session. All
    /// values are observed facts — nothing here changes state.
    /// </summary>
    internal static class SystemHealth
    {
        /// <summary>A labelled set of health facts for display.</summary>
        public static List<KeyValuePair<string, string>> Collect()
        {
            var items = new List<KeyValuePair<string, string>>();

            items.Add(Kv("Application version", AppVersion()));
            items.Add(Kv("Build configuration", BuildConfig()));
            items.Add(Kv(".NET runtime", Environment.Version.ToString()));
            items.Add(Kv("Operating system", SafeOsVersion()));
            items.Add(Kv("Workstation", SafeMachine()));
            items.Add(Kv("Logged-in Windows user", SafeUser()));
            items.Add(Kv("Approx. location", string.IsNullOrEmpty(GeoLocation.City) ? "resolving…" : GeoLocation.City));

            items.Add(Kv("Data source", DbConfig.UsePostgres ? "Shared database (Supabase)" : "Local offline store"));
            items.Add(Kv("Database config", File.Exists(ConfigPath("database.config")) ? "Present" : "Missing"));
            items.Add(Kv("Email config", File.Exists(ConfigPath("email.config")) ? "Present" : "Not configured"));

            items.Add(Kv("Signed-in user", Auth.Current != null ? Auth.Current.FullName : "—"));
            items.Add(Kv("Role", Auth.Current != null ? Format.RoleName(Auth.Current.Role) : "—"));

            return items;
        }

        private static KeyValuePair<string, string> Kv(string k, string v)
        {
            return new KeyValuePair<string, string>(k, v);
        }

        private static string AppVersion()
        {
            try
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v != null ? v.ToString() : "1.0.0.0";
            }
            catch (Exception) { return "unknown"; }
        }

        private static string BuildConfig()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        private static string SafeOsVersion()
        {
            try { return Environment.OSVersion.VersionString; }
            catch (Exception) { return "unknown"; }
        }

        private static string SafeMachine()
        {
            try { return Environment.MachineName; }
            catch (Exception) { return "unknown"; }
        }

        private static string SafeUser()
        {
            try { return Environment.UserName; }
            catch (Exception) { return "unknown"; }
        }

        private static string ConfigPath(string file)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
        }
    }
}
