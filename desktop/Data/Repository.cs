using System;
using System.Collections.Generic;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// In-memory presentation cache populated exclusively by the shared API.
    /// Historical local JSON is never read, rewritten, deleted or used as fallback.
    /// </summary>
    internal static class Repository
    {
        private static Database _db;

        /// <summary>Signed-in role for the current session.</summary>
        public static UserRole CurrentRole { get; private set; }

        /// <summary>Display name of the signed-in QMS user.</summary>
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

        public static void SignIn(UserRole role, string displayName)
        {
            CurrentRole = role;
            CurrentUserName = displayName;
        }

        public static void SignOut()
        {
            CurrentRole = UserRole.None;
            CurrentUserName = null;
            SharedData.ResetViewScope();
            _db = new Database();
            _db.Settings.IdleTimeoutMinutes = LocalPreferences.IdleMinutes;
        }

        #region Presentation cache

        public static void Load() { _db = new Database(); LocalPreferences.Load(); _db.Settings.IdleTimeoutMinutes=LocalPreferences.IdleMinutes; }

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

        /// <summary>Percentage of a project's records that have reached a Complete state.</summary>
        public static int ProjectCompletionPercent(string projectId, out int done, out int total)
        {
            var statuses = FormsForProject(projectId).Select(f => f.Status)
                .Concat(RegistersForProject(projectId).Select(r => r.Status))
                .ToList();

            total = statuses.Count;
            done = statuses.Count(s => s == RecordStatus.CompleteAccepted);

            return total == 0 ? 0 : (int)Math.Round(done * 100.0 / total);
        }

        #endregion
    }
}
