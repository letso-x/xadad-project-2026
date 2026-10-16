using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// "Trust this device" for multi-factor authentication. After a user completes the
    /// email verification once, this machine is remembered for a fixed window (14 days),
    /// so subsequent sign-ins by that user on this machine skip the code — the same way
    /// Windows / Microsoft accounts remember a trusted device.
    ///
    /// Trust is per user and per machine. A small file next to the executable records,
    /// for each user id, a random token and its expiry. The token is a secret generated
    /// at trust time; knowing a user id is not enough to forge trust. The file is not a
    /// credential store — it only lets a user skip the second factor on this one machine.
    /// </summary>
    internal static class DeviceTrust
    {
        /// <summary>Default trust window if settings are unavailable.</summary>
        public const int DefaultTrustDays = 14;

        /// <summary>The active trust window, from organisation settings.</summary>
        public static int TrustDays
        {
            get
            {
                try
                {
                    int d = Repository.Db.Settings.DeviceTrustDays;
                    return d >= 1 ? d : DefaultTrustDays;
                }
                catch (Exception) { return DefaultTrustDays; }
            }
        }

        private sealed class Entry
        {
            public string Token;
            public DateTime Expires;
        }

        private static readonly object _lock = new object();
        private static Dictionary<string, Entry> _entries;

        private static string StorePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "device-trust.dat"); }
        }

        /// <summary>True if this machine is currently trusted for the given account.</summary>
        public static bool IsTrusted(UserAccount account)
        {
            if (account == null) return false;

            lock (_lock)
            {
                Load();
                Entry e;
                if (!_entries.TryGetValue(account.Id, out e)) return false;

                if (DateTime.UtcNow > e.Expires)
                {
                    _entries.Remove(account.Id);
                    Save();
                    return false;
                }
                return true;
            }
        }

        /// <summary>Marks this machine as trusted for the account for the trust window.</summary>
        public static void Trust(UserAccount account)
        {
            if (account == null) return;

            lock (_lock)
            {
                Load();
                _entries[account.Id] = new Entry
                {
                    Token = NewToken(),
                    Expires = DateTime.UtcNow.AddDays(TrustDays)
                };
                Save();
            }
        }

        /// <summary>Removes trust for an account on this machine (forces MFA next time).</summary>
        public static void Revoke(UserAccount account)
        {
            if (account == null) return;

            lock (_lock)
            {
                Load();
                if (_entries.Remove(account.Id)) Save();
            }
        }

        /// <summary>The date trust expires for the account, or null if not trusted.</summary>
        public static DateTime? TrustedUntil(UserAccount account)
        {
            if (account == null) return null;
            lock (_lock)
            {
                Load();
                Entry e;
                if (_entries.TryGetValue(account.Id, out e) && DateTime.UtcNow <= e.Expires)
                {
                    return e.Expires.ToLocalTime();
                }
                return null;
            }
        }

        private static string NewToken()
        {
            byte[] b = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return Convert.ToBase64String(b);
        }

        private static void Load()
        {
            if (_entries != null) return;
            _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

            try
            {
                if (!File.Exists(StorePath)) return;

                foreach (string raw in File.ReadAllLines(StorePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;

                    // Format: userId|token|expiresUtcTicks
                    string[] parts = line.Split('|');
                    if (parts.Length != 3) continue;

                    long ticks;
                    if (!long.TryParse(parts[2], out ticks)) continue;

                    _entries[parts[0]] = new Entry
                    {
                        Token = parts[1],
                        Expires = new DateTime(ticks, DateTimeKind.Utc)
                    };
                }
            }
            catch (Exception)
            {
                _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            }
        }

        private static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in _entries)
                {
                    // Skip already-expired entries so the file self-prunes.
                    if (DateTime.UtcNow > kv.Value.Expires) continue;
                    sb.Append(kv.Key).Append('|')
                      .Append(kv.Value.Token).Append('|')
                      .Append(kv.Value.Expires.Ticks).Append('\n');
                }
                File.WriteAllText(StorePath, sb.ToString());
            }
            catch (Exception)
            {
                // Non-fatal: if we can't persist trust, the user is simply asked for a
                // code again next time.
            }
        }
    }
}
