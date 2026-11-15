using System;
using System.IO;
using System.Net;

namespace MzuApplication.Data
{
    /// <summary>
    /// Approximate city lookup from the machine's public IP, for the audit trail's
    /// "from" column. City-level only (e.g. Durban, Johannesburg) — never an exact
    /// address — and resolved once per session. Uses a free IP-geolocation service; if
    /// it is unreachable the city is simply omitted, never blocking anything.
    ///
    /// NOTE: IP geolocation reflects the ISP's routing location, so it is approximate and
    /// may differ from the user's real city, especially on mobile networks.
    /// </summary>
    internal static class GeoLocation
    {
        private static bool _resolved;
        private static string _city;

        /// <summary>The resolved city (empty if unknown / not yet resolved).</summary>
        public static string City
        {
            get { return _city ?? string.Empty; }
        }

        /// <summary>
        /// Resolves the city in the background once. Safe to call at sign-in; returns
        /// immediately and fills <see cref="City"/> when the lookup completes.
        /// </summary>
        public static void ResolveAsync()
        {
            if (_resolved) return;
            _resolved = true;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { _city = Lookup(); }
                catch (Exception) { _city = string.Empty; }
            });
        }

        /// <summary>Resolves synchronously (used by a background caller). Returns city or "".</summary>
        public static string Resolve()
        {
            if (_resolved && !string.IsNullOrEmpty(_city)) return _city;
            _resolved = true;
            try { _city = Lookup(); } catch (Exception) { _city = string.Empty; }
            return _city;
        }

        private static string Lookup()
        {
            // ip-api.com: free, no key, returns plain fields. We request just the city
            // and region so no precise coordinates are fetched.
            const string url = "http://ip-api.com/line/?fields=status,city,regionName";

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                | (SecurityProtocolType)768 /* Tls11 */ | (SecurityProtocolType)192 /* Tls */;

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 4000;
            req.ReadWriteTimeout = 4000;
            req.UserAgent = "MzukuluQMS";

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                string body = sr.ReadToEnd();
                // Lines: status / city / regionName
                string[] lines = body.Replace("\r", string.Empty).Split('\n');
                if (lines.Length >= 2 && lines[0].Trim() == "success")
                {
                    string city = lines[1].Trim();
                    string region = lines.Length >= 3 ? lines[2].Trim() : string.Empty;

                    if (!string.IsNullOrEmpty(city)) return city;
                    if (!string.IsNullOrEmpty(region)) return region;
                }
            }
            return string.Empty;
        }
    }
}
