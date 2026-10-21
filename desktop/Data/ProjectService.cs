using System.Collections.Generic;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Reads and writes projects, routing to the shared Postgres <c>Project</c> table when
    /// connected so every user sees the same projects, or the local store otherwise.
    /// </summary>
    internal static class ProjectService
    {
        private static bool UsePostgres
        {
            get { return DbConfig.UsePostgres; }
        }

        public static List<Project> All()
        {
            if (UsePostgres)
            {
                try { return PostgresProjectStore.GetAll(); }
                catch (System.Exception) { }
            }
            return Repository.Db.Projects;
        }

        public static void Add(Project project)
        {
            if (UsePostgres)
            {
                try
                {
                    PostgresProjectStore.Insert(project);
                    MirrorLocal(project);
                    return;
                }
                catch (System.Exception) { }
            }
            Repository.Db.Projects.Add(project);
            Repository.Save();
        }

        public static void Update(Project project)
        {
            if (UsePostgres)
            {
                try { PostgresProjectStore.Update(project); }
                catch (System.Exception) { }
            }
            MirrorLocal(project);
            Repository.Save();
        }

        private static void MirrorLocal(Project project)
        {
            Project existing = Repository.Db.Projects.FirstOrDefault(p => p.Id == project.Id);
            if (existing == null)
            {
                Repository.Db.Projects.Add(project);
            }
            else
            {
                existing.ClientId = project.ClientId;
                existing.Name = project.Name;
                existing.Number = project.Number;
                existing.Description = project.Description;
                existing.Status = project.Status;
                existing.Site = project.Site;
                existing.ContractNo = project.ContractNo;
            }
        }
    }
}
