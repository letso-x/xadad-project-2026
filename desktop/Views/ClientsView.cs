using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;

namespace MzuApplication.Views
{
    /// <summary>Client list with a filter box; administrators can add and edit.</summary>
    public class ClientsView : ViewBase
    {
        private DataTable _table;
        private TextInput _filter;

        public ClientsView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "clients"; }
        }

        protected override void BuildContent()
        {
            Control[] actions = new Control[0];

            if (Repository.IsAdmin)
            {
                FlatButton add = new FlatButton
                {
                    Text = "New Client",
                    Glyph = Glyph.Plus,
                    Variant = ButtonVariant.Primary,
                    Width = Dpi.S(128)
                };
                add.Click += (s, e) => EditClient(null);
                actions = new Control[] { add };
            }

            AddPageHeader("Clients", "Clients",
                "Organisations Mzukulu delivers projects for.", actions);

            _filter = new TextInput
            {
                Glyph = Glyph.Search,
                Placeholder = "Filter clients...",
                Location = new Point(Left1, Y),
                Width = Dpi.S(280),
                Height = Dpi.S(34)
            };
            _filter.ValueChanged += (s, e) => _table.ApplyFilter(_filter.Value);
            Controls.Add(_filter);
            Y = _filter.Bottom + 14;

            _table = new DataTable
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                EmptyMessage = "No clients yet."
            };
            _table.DefineColumns("Client", 2.2f, "Contact", 3.2f, "Projects", 1.1f, "Status", 1.1f);
            _table.RowActivated += (s, tag) => ShowClient(tag as Client);

            var rows = new List<TableRow>();
            foreach (Client client in Repository.Db.Clients)
            {
                int count = Repository.ProjectsForClient(client.Id).Count;

                TableRow row = new TableRow { Tag = client };
                row.Cells.Add(new TableCell(client.Name) { Bold = true });
                row.Cells.Add(new TableCell(string.IsNullOrEmpty(client.Contact) ? "—" : client.Contact) { Muted = true });
                row.Cells.Add(new TableCell(count + (count == 1 ? " project" : " projects")));

                TableCell status = new TableCell(client.Active ? "Active" : "Inactive")
                {
                    PillBack = client.Active ? Theme.Green100 : Theme.Slate150,
                    PillFore = client.Active ? Theme.Green700 : Theme.Slate600,
                    PillGlyph = client.Active ? Glyph.Check : Glyph.None
                };
                row.Cells.Add(status);

                row.FilterText = (client.Name + " " + client.Contact).ToLowerInvariant();
                rows.Add(row);
            }

            _table.SetRows(rows);
            Controls.Add(_table);
            Y = _table.Bottom + 8;
        }

        private void ShowClient(Client client)
        {
            if (client == null) return;

            var projects = Repository.ProjectsForClient(client.Id);

            using (Dialog dialog = new Dialog("Client — " + client.Name, 480))
            {
                dialog.AddReadOnlyField("Contact", string.IsNullOrEmpty(client.Contact) ? "—" : client.Contact);
                dialog.AddReadOnlyField("Status", client.Active ? "Active" : "Inactive");
                dialog.AddReadOnlyField("Projects", projects.Count == 0
                    ? "No projects yet."
                    : string.Join(Environment.NewLine, projects.Select(p => p.Name + "  (" + p.Status + ")")));

                if (Repository.IsAdmin)
                {
                    dialog.AddPrimaryAction("Edit", () => dialog.DialogResult = DialogResult.Yes);
                }

                if (dialog.ShowDialog(Shell) == DialogResult.Yes)
                {
                    EditClient(client);
                }
            }
        }

        private void EditClient(Client existing)
        {
            using (Dialog dialog = new Dialog(existing == null ? "New Client" : "Edit Client", 480))
            {
                TextInput name = dialog.AddTextField("Client Name", existing != null ? existing.Name : string.Empty);
                TextInput contact = dialog.AddTextField("Contact Information",
                    existing != null ? existing.Contact : string.Empty, "Name · email · phone");
                CheckBox active = dialog.AddCheckField("Active", existing == null || existing.Active);

                dialog.AddPrimaryAction("Save", () =>
                {
                    if (string.IsNullOrWhiteSpace(name.Value))
                    {
                        Notify("Client name is required.", true);
                        return;
                    }

                    if (existing == null)
                    {
                        Repository.Db.Clients.Add(new Client
                        {
                            Id = Repository.NewId("cli"),
                            Name = name.Value.Trim(),
                            Contact = contact.Value.Trim(),
                            Active = active.Checked
                        });
                    }
                    else
                    {
                        existing.Name = name.Value.Trim();
                        existing.Contact = contact.Value.Trim();
                        existing.Active = active.Checked;
                    }

                    Repository.Save();
                    dialog.DialogResult = DialogResult.OK;
                });

                if (dialog.ShowDialog(Shell) == DialogResult.OK)
                {
                    Notify("Client saved.");
                    Rebuild();
                }
            }
        }
    }
}
