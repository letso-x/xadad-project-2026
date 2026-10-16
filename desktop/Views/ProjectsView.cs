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
    /// <summary>Project list with filter; administrators can create and edit.</summary>
    public class ProjectsView : ViewBase
    {
        private static readonly string[] StatusOptions = { "Planning", "Active", "On Hold", "Complete" };

        private DataTable _table;
        private TextInput _filter;

        public ProjectsView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "projects"; }
        }

        protected override void BuildContent()
        {
            Control[] actions = new Control[0];

            if (Repository.IsAdmin)
            {
                FlatButton add = new FlatButton
                {
                    Text = "New Project",
                    Glyph = Glyph.Plus,
                    Variant = ButtonVariant.Primary,
                    Width = Dpi.S(136)
                };
                add.Click += (s, e) => EditProject(null);
                actions = new Control[] { add };
            }

            AddPageHeader("Projects", "Projects", "All client site projects.", actions);

            _filter = new TextInput
            {
                Glyph = Glyph.Search,
                Placeholder = "Filter projects...",
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
                EmptyMessage = "No projects yet."
            };
            _table.DefineColumns("Project", 2.6f, "Client", 1.3f, "Site", 2.2f,
                                 "Status", 1.2f, "QC Forms", 1.0f);
            _table.RowActivated += (s, tag) =>
            {
                Project project = tag as Project;
                if (project != null) Shell.Navigate(new ProjectDetailView(Shell, project.Id));
            };

            var rows = new List<TableRow>();
            foreach (Project project in Repository.Db.Projects)
            {
                Client client = Repository.GetClient(project.ClientId);
                int recordCount = Repository.FormsForProject(project.Id).Count
                                  + Repository.RegistersForProject(project.Id).Count;

                Color back, fore;
                Format.ProjectStatusColors(project.Status, out back, out fore);

                TableRow row = new TableRow { Tag = project };
                row.Cells.Add(new TableCell(project.Name) { Bold = true, SubText = project.Number });
                row.Cells.Add(new TableCell(client != null ? client.Name : "—"));
                row.Cells.Add(new TableCell(string.IsNullOrEmpty(project.Site) ? "—" : project.Site) { Muted = true });
                row.Cells.Add(new TableCell(project.Status) { PillBack = back, PillFore = fore });
                row.Cells.Add(new TableCell(recordCount + (recordCount == 1 ? " form" : " forms")) { Muted = true });

                row.FilterText = string.Join(" ", new[]
                {
                    project.Name, project.Number, project.Site,
                    client != null ? client.Name : string.Empty, project.Status
                }).ToLowerInvariant();

                rows.Add(row);
            }

            _table.SetRows(rows);
            Controls.Add(_table);
            Y = _table.Bottom + 8;
        }

        /// <summary>Shared create/edit dialog, also used from the project detail page.</summary>
        internal static void EditProjectDialog(MainForm shell, Project existing, Action onSaved)
        {
            var clients = Repository.Db.Clients;
            if (clients.Count == 0)
            {
                shell.Notify("Create a client before adding a project.", true);
                return;
            }

            using (Dialog dialog = new Dialog(existing == null ? "New Project" : "Edit Project", 540))
            {
                Client currentClient = existing != null ? Repository.GetClient(existing.ClientId) : null;
                ComboBox client = dialog.AddDropdown("Client",
                    clients.Select(c => c.Name),
                    currentClient != null ? currentClient.Name : null);

                TextInput name = dialog.AddTextField("Project Name", existing != null ? existing.Name : string.Empty);
                TextInput number = dialog.AddTextField("Project No.", existing != null ? existing.Number : string.Empty);
                TextInput contract = dialog.AddTextField("Contract / Order No.",
                    existing != null ? existing.ContractNo : string.Empty);
                TextInput site = dialog.AddTextField("Site / Location", existing != null ? existing.Site : string.Empty);
                TextInput description = dialog.AddTextArea("Description",
                    existing != null ? existing.Description : string.Empty);
                ComboBox status = dialog.AddDropdown("Status", StatusOptions,
                    existing != null ? existing.Status : "Planning");
                DateTimePicker start = dialog.AddDateField("Start Date", existing != null ? existing.StartDate : null);
                DateTimePicker end = dialog.AddDateField("End Date", existing != null ? existing.EndDate : null);

                dialog.AddPrimaryAction("Save", () =>
                {
                    if (string.IsNullOrWhiteSpace(name.Value))
                    {
                        shell.Notify("Project name is required.", true);
                        return;
                    }

                    Client picked = clients.FirstOrDefault(c => c.Name == (string)client.SelectedItem);
                    if (picked == null)
                    {
                        shell.Notify("Select a client.", true);
                        return;
                    }

                    Project target = existing ?? new Project { Id = Repository.NewId("prj") };

                    target.ClientId = picked.Id;
                    target.Name = name.Value.Trim();
                    target.Number = number.Value.Trim();
                    target.ContractNo = contract.Value.Trim();
                    target.Site = site.Value.Trim();
                    target.Description = description.Value.Trim();
                    target.Status = (string)status.SelectedItem;
                    target.StartDate = start.Checked ? start.Value.Date : (DateTime?)null;
                    target.EndDate = end.Checked ? end.Value.Date : (DateTime?)null;

                    if (existing == null) Repository.Db.Projects.Add(target);

                    Repository.Save();
                    dialog.DialogResult = DialogResult.OK;
                });

                if (dialog.ShowDialog(shell) == DialogResult.OK)
                {
                    shell.Notify("Project saved.");
                    onSaved();
                }
            }
        }

        private void EditProject(Project existing)
        {
            EditProjectDialog(Shell, existing, Rebuild);
        }
    }
}
