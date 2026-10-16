using System;
using System.Collections.Generic;
using System.IO;

namespace MzuApplication.Data
{
    /// <summary>
    /// SMTP settings for sending MFA codes. Kept out of source control in an external
    /// <c>email.config</c> file next to the executable (same approach as the database
    /// connection), because it contains a mailbox password.
    ///
    /// Format (one key=value per line, '#' for comments):
    ///   Smtp.Host=smtp.office365.com
    ///   Smtp.Port=587
    ///   Smtp.EnableSsl=true
    ///   Smtp.User=10506152@rcconnect.edu.za
    ///   Smtp.Password=YOUR_PASSWORD_HERE
    ///   Smtp.From=10506152@rcconnect.edu.za
    ///   Smtp.FromName=Mzukulu QMS
    /// </summary>
    internal static class EmailConfig
    {
        private static bool _loaded;
        private static readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string Host { get { return Get("Smtp.Host"); } }
        public static int Port { get { return GetInt("Smtp.Port", 587); } }
        public static bool EnableSsl { get { return GetBool("Smtp.EnableSsl", true); } }
        public static string User { get { return Get("Smtp.User"); } }
        public static string Password { get { return Get("Smtp.Password"); } }
        public static string From { get { return Get("Smtp.From") ?? Get("Smtp.User"); } }
        public static string FromName { get { return Get("Smtp.FromName") ?? "Mzukulu QMS"; } }

        /// <summary>True when enough is configured to attempt sending email.</summary>
        public static bool IsConfigured
        {
            get
            {
                return !string.IsNullOrEmpty(Host)
                    && !string.IsNullOrEmpty(User)
                    && !string.IsNullOrEmpty(Password)
                    && !IsPlaceholder(Password)
                    && !IsPlaceholder(User);
            }
        }

        private static string ConfigPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "email.config"); }
        }

        /// <summary>True for an unedited placeholder value from the sample.</summary>
        private static bool IsPlaceholder(string value)
        {
            return value != null
                && value.IndexOf("YOUR_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Get(string key)
        {
            if (!_loaded) Load();
            string v;
            return _values.TryGetValue(key, out v) ? v : null;
        }

        private static int GetInt(string key, int fallback)
        {
            int n;
            return int.TryParse(Get(key), out n) ? n : fallback;
        }

        private static bool GetBool(string key, bool fallback)
        {
            string v = Get(key);
            if (string.IsNullOrEmpty(v)) return fallback;
            return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        private static void Load()
        {
            _loaded = true;
            try
            {
                if (!File.Exists(ConfigPath)) return;

                foreach (string raw in File.ReadAllLines(ConfigPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    _values[key] = value;
                }
            }
            catch (Exception)
            {
                _values.Clear();
            }
        }
    }
}
