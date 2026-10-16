using System.Collections.Generic;
using System.Linq;
using MzuApplication.Models;

namespace MzuApplication.Data
{
    /// <summary>
    /// Reads and writes clients, routing to the shared Postgres <c>Client</c> table when
    /// connected so every user sees the same client list, or the local store otherwise.
    /// </summary>
    internal static class ClientService
    {
        private static bool UsePostgres
        {
            get { return DbConfig.UsePostgres; }
        }

        /// <summary>All clients, sorted by name.</summary>
        public static List<Client> All()
        {
            if (UsePostgres)
            {
                try { return PostgresClientStore.GetAll(); }
                catch (System.Exception) { /* fall back to local */ }
            }

            return Repository.Db.Clients
                .OrderBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Adds a new client to the shared table (and mirrors locally).</summary>
        public static void Add(Client client)
        {
            if (UsePostgres)
            {
                try
                {
                    PostgresClientStore.Insert(client);
                    MirrorLocal(client);
                    return;
                }
                catch (System.Exception) { /* fall back to local-only below */ }
            }

            Repository.Db.Clients.Add(client);
            Repository.Save();
        }

        /// <summary>Persists edits to an existing client.</summary>
        public static void Update(Client client)
        {
            if (UsePostgres)
            {
                try { PostgresClientStore.Update(client); }
                catch (System.Exception) { /* fall back to local save below */ }
            }

            MirrorLocal(client);
            Repository.Save();
        }

        /// <summary>Keeps a local copy so other screens reading Repository stay consistent.</summary>
        private static void MirrorLocal(Client client)
        {
            Client existing = Repository.Db.Clients.FirstOrDefault(c => c.Id == client.Id);
            if (existing == null)
            {
                Repository.Db.Clients.Add(client);
            }
            else
            {
                existing.Name = client.Name;
                existing.Contact = client.Contact;
                existing.Active = client.Active;
            }
        }
    }
}
