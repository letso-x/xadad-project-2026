using System.Collections.Generic;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Reads and writes QC forms, routing to the shared <c>app_forms</c> table when
    /// connected so every user sees the same forms, or the local store otherwise.
    /// </summary>
    internal static class FormService
    {
        private static bool UsePostgres
        {
            get { return DbConfig.UsePostgres; }
        }

        public static List<QcForm> All()
        {
            if (UsePostgres)
            {
                try { return PostgresFormStore.GetAll(); }
                catch (System.Exception) { }
            }
            return Repository.Db.Forms;
        }

        public static List<QcForm> ForProject(string projectId)
        {
            if (UsePostgres)
            {
                try { return PostgresFormStore.ForProject(projectId); }
                catch (System.Exception) { }
            }
            return Repository.Db.Forms.Where(f => f.ProjectId == projectId).ToList();
        }

        public static QcForm Get(string id)
        {
            if (UsePostgres)
            {
                try
                {
                    QcForm f = PostgresFormStore.Get(id);
                    if (f != null) return f;
                }
                catch (System.Exception) { }
            }
            return Repository.Db.Forms.FirstOrDefault(f => f.Id == id);
        }

        /// <summary>Persists a form (create or update) to the shared store and local mirror.</summary>
        public static void Save(QcForm form)
        {
            if (UsePostgres)
            {
                try { PostgresFormStore.Save(form); }
                catch (System.Exception) { }
            }
            MirrorLocal(form);
            Repository.Save();
        }

        private static void MirrorLocal(QcForm form)
        {
            QcForm existing = Repository.Db.Forms.FirstOrDefault(f => f.Id == form.Id);
            if (existing == null) Repository.Db.Forms.Add(form);
            else
            {
                int i = Repository.Db.Forms.IndexOf(existing);
                Repository.Db.Forms[i] = form;
            }
        }
    }
}
