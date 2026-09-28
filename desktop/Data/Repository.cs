using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// In-memory database with JSON persistence to the user's AppData folder. Stands in
    /// for the Supabase backend a production deployment would use, and keeps the demo
    /// data stable across runs.
    /// </summary>
    internal static class Repository
    {
        private static Database _db;

        /// <summary>Signed-in role for the current session.</summary>
        public static UserRole CurrentRole { get; private set; }

        /// <summary>Display name of the signed-in demo user.</summary>
        public static string CurrentUserName { get; private set; }

        public static bool IsAdmin
        {
            get { return CurrentRole == UserRole.Admin; }
        }

        public static Database Db
        {
            get
            {
                if (_db == null) Load();
                return _db;
            }
        }

        private static string StoragePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MzukuluQC");
                return Path.Combine(dir, "qc-db-v1.json");
            }
        }

        public static void SignIn(UserRole role, string displayName)
        {
            CurrentRole = role;
            CurrentUserName = displayName;
        }

        public static void SignOut()
        {
            CurrentRole = UserRole.None;
            CurrentUserName = null;
        }

        #region Persistence

        public static void Load()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    using (FileStream fs = File.OpenRead(StoragePath))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(Database));
                        _db = (Database)serializer.ReadObject(fs);
                    }

                    if (_db != null && _db.Templates.Count > 0)
                    {
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupt or unreadable store: fall through and reseed rather than
                // leaving the app with no data.
            }

            _db = Seed.Build();
            Save();
        }

        public static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(StoragePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(Database));
                    serializer.WriteObject(ms, _db);
                    File.WriteAllBytes(StoragePath, ms.ToArray());
                }
            }
            catch (Exception)
            {
                // Persistence is a convenience for the demo; an IO failure should not
                // take the UI down. In-memory state stays valid for this session.
            }
        }

        /// <summary>Discards the saved store and rebuilds the seeded demo data.</summary>
        public static void ResetToSeed()
        {
            _db = Seed.Build();
            Save();
        }

        #endregion

        #region Lookups

        public static Client GetClient(string id)
        {
            return Db.Clients.FirstOrDefault(c => c.Id == id);
        }

        public static Project GetProject(string id)
        {
            return Db.Projects.FirstOrDefault(p => p.Id == id);
        }

        public static ChecklistTemplate GetTemplate(string id)
        {
            return Db.Templates.FirstOrDefault(t => t.Id == id);
        }

        public static QcForm GetForm(string id)
        {
            return Db.Forms.FirstOrDefault(f => f.Id == id);
        }

        public static CableRegister GetRegister(string id)
        {
            return Db.Registers.FirstOrDefault(r => r.Id == id);
        }

        public static List<Project> ProjectsForClient(string clientId)
        {
            return Db.Projects.Where(p => p.ClientId == clientId).ToList();
        }

        public static List<QcForm> FormsForProject(string projectId)
        {
            return Db.Forms.Where(f => f.ProjectId == projectId).ToList();
        }

        public static List<CableRegister> RegistersForProject(string projectId)
        {
            return Db.Registers.Where(r => r.ProjectId == projectId).ToList();
        }

        #endregion

        #region Helpers

        public static string NewId(string prefix)
        {
            return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 10);
        }

        /// <summary>
        /// Allocates the next sequential QC record number across both forms and
        /// registers, so numbering is unique per organisation.
        /// </summary>
        public static string NextQcRecordNo()
        {
            var numbers = new List<int>();

            foreach (string candidate in Db.Forms.Select(f => f.QcRecordNo)
                                          .Concat(Db.Registers.Select(r => r.QcRecordNo)))
            {
                if (string.IsNullOrEmpty(candidate)) continue;

                int dash = candidate.LastIndexOf('-');
                if (dash < 0 || dash == candidate.Length - 1) continue;

                int parsed;
                if (int.TryParse(candidate.Substring(dash + 1),
                                 NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    numbers.Add(parsed);
                }
            }

            int next = (numbers.Count > 0 ? numbers.Max() : 0) + 1;
            return "QC-" + next.ToString("D4", CultureInfo.InvariantCulture);
        }

        /// <summary>Percentage of a project's records that have reached a Complete state.</summary>
        public static int ProjectCompletionPercent(string projectId, out int done, out int total)
        {
            var statuses = FormsForProject(projectId).Select(f => f.Status)
                .Concat(RegistersForProject(projectId).Select(r => r.Status))
                .ToList();

            total = statuses.Count;
            done = statuses.Count(s => s != RecordStatus.InProgress);

            return total == 0 ? 0 : (int)Math.Round(done * 100.0 / total);
        }

        #endregion
    }
}
