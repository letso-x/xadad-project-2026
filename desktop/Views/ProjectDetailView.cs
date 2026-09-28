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
    /// <summary>
    /// A single project: summary tiles, description, and the list of QC records raised
    /// against it.
    /// </summary>
    public class ProjectDetailView : ViewBase
    {
        private readonly string _projectId;

        public ProjectDetailView(MainForm shell, string projectId) : base(shell)
        {
            _projectId = projectId;
        }

        public override string RouteKey
        {
            get { return "projects"; }
        }

        protected override void BuildContent()
        {
            Project project = Repository.GetProject(_projectId);
            if (project == null)
            {
                Label missing = Ui.Body("Project not found.");
                missing.Location = new Point(Left1, Y);
                Controls.Add(missing);
                Y = missing.Bottom + 20;
                return;
            }

            Client client = Repository.GetClient(project.ClientId);

            AddCrumbs("Projects", () => Shell.Navigate(new ProjectsView(Shell)), project.Name);

            var actions = new List<Control>();

            if (Repository.IsAdmin)
            {
                FlatButton edit = new FlatButton { Text = "Edit Project", Width = 116 };
                edit.Click += (s, e) => ProjectsView.EditProjectDialog(Shell, project, Rebuild);
                actions.Add(edit);
            }

            FlatButton newForm = new FlatButton
            {
                Text = "New Form",
                Glyph = Glyph.Plus,
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(124)
            };
            newForm.Click += (s, e) => Shell.Navigate(new LibraryView(Shell, project.Id));
            actions.Add(newForm);

            AddPageHeader(client != null ? client.Name : string.Empty,
                project.Name,
                project.Number + (string.IsNullOrEmpty(project.Site) ? string.Empty : " · " + project.Site),
                actions.ToArray());

            AddSummaryTiles(project, client);
            AddDescription(project);
            AddRecordsTable(project);
        }

        private void AddSummaryTiles(Project project, Client client)
        {
            int recordCount = Repository.FormsForProject(project.Id).Count
                              + Repository.RegistersForProject(project.Id).Count;

            var cells = new List<Tuple<string, string, bool>>
            {
                Tuple.Create("Client", client != null ? client.Name : "—", false),
                Tuple.Create("Contract / Order No.",
                    string.IsNullOrEmpty(project.ContractNo) ? "—" : project.ContractNo, false),
                Tuple.Create("Status", project.Status, true),
                Tuple.Create("Start Date", Format.Date(project.StartDate), false),
                Tuple.Create("End Date", Format.Date(project.EndDate), false),
                Tuple.Create("QC Forms", recordCount.ToString(), false)
            };

            const int perRow = 3;
            const int gap = 14;
            const int height = 78;
            int width = (InnerWidth - gap * (perRow - 1)) / perRow;

            for (int i = 0; i < cells.Count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;
                var cell = cells[i];

                Card card = new Card
                {
                    Width = width,
                    Height = height,
                    Fill = Color.White,
                    BorderTint = Theme.Slate150,
                    Radius = Theme.Radius,
                    Location = new Point(Left1 + col * (width + gap), Y + row * (height + gap))
                };

                Label label = Ui.FieldLabel(cell.Item1);
                label.Location = new Point(Dpi.S(18), Dpi.S(18));
                card.Controls.Add(label);

                if (cell.Item3)
                {
                    Color back, fore;
                    Format.ProjectStatusColors(project.Status, out back, out fore);

                    Pill pill = new Pill
                    {
                        Text = cell.Item2,
                        Fill = back,
                        TextColor = fore,
                        Location = new Point(Dpi.S(18), Dpi.S(42)),
                        Height = Dpi.S(22)
                    };
                    pill.AutoFit();
                    card.Controls.Add(pill);
                }
                else
                {
                    Label value = new Label
                    {
                        AutoSize = false,
                        Font = Theme.Body,
                        ForeColor = Theme.Slate900,
                        Location = new Point(Dpi.S(18), Dpi.S(42)),
                        Width = width - 34,
                        Height = Dpi.S(20),
                        Text = cell.Item2
                    };
                    card.Controls.Add(value);
                }

                Controls.Add(card);
            }

            int rows = (int)Math.Ceiling(cells.Count / (double)perRow);
            Y += rows * height + (rows - 1) * gap + 18;
        }

        private void AddDescription(Project project)
        {
            if (string.IsNullOrWhiteSpace(project.Description)) return;

            Label label = Ui.FieldLabel("Description");
            Label text = Ui.Wrapped(project.Description, Theme.Body, Theme.Slate900, InnerWidth - 36);

            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Height = 20 + label.Height + 8 + text.Height + 18,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            label.Location = new Point(Dpi.S(18), Dpi.S(18));
            card.Controls.Add(label);

            text.Location = new Point(18, label.Bottom + 8);
            card.Controls.Add(text);

            Controls.Add(card);
            Y = card.Bottom + 18;
        }

        private void AddRecordsTable(Project project)
        {
            // Forms and registers merged into one chronological list.
            var entries = Repository.FormsForProject(project.Id)
                .Select(f =>
                {
                    ChecklistTemplate t = Repository.GetTemplate(f.TemplateId);
                    return new
                    {
                        Label = t != null ? t.DocNumber + " — " + t.DocName : f.QcRecordNo,
                        Record = f.QcRecordNo,
                        Status = f.Status,
                        Updated = f.UpdatedAt,
                        Nav = (Action)(() => Shell.Navigate(new FormFillView(Shell, f.Id)))
                    };
                })
                .Concat(Repository.RegistersForProject(project.Id).Select(r => new
                {
                    Label = CableRegister.DocNumber + " — " + CableRegister.DocName,
                    Record = r.QcRecordNo,
                    Status = r.Status,
                    Updated = r.UpdatedAt,
                    Nav = (Action)(() => Shell.Navigate(new RegisterView(Shell, r.Id)))
                }))
                .OrderByDescending(e => e.Updated)
                .ToList();

            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("QC Forms — " + project.Name, InnerWidth);
            card.Controls.Add(bar);

            DataTable table = new DataTable
            {
                Location = new Point(1, bar.Height),
                Width = InnerWidth - 2,
                EmptyMessage = "No forms yet for this project."
            };
            table.DefineColumns("Document", 3.4f, "QC Record No.", 1.3f, "Status", 1.5f, "Updated", 1.2f);
            table.RowActivated += (s, tag) =>
            {
                Action nav = tag as Action;
                if (nav != null) nav();
            };

            var rows = new List<TableRow>();
            foreach (var entry in entries)
            {
                TableRow row = new TableRow { Tag = entry.Nav };
                row.Cells.Add(new TableCell(entry.Label) { Bold = true });
                row.Cells.Add(new TableCell(entry.Record) { Chip = true });
                row.Cells.Add(new TableCell(Format.StatusText(entry.Status))
                {
                    PillBack = Format.StatusBack(entry.Status),
                    PillFore = Format.StatusFore(entry.Status),
                    PillGlyph = entry.Status == RecordStatus.InProgress ? Glyph.Clock
                              : entry.Status == RecordStatus.CompleteAccepted ? Glyph.CheckCircle
                              : Glyph.Alert
                });
                row.Cells.Add(new TableCell(Format.Date(entry.Updated)) { Muted = true });
                row.FilterText = entry.Label.ToLowerInvariant();
                rows.Add(row);
            }
            table.SetRows(rows);
            card.Controls.Add(table);

            card.Height = bar.Height + table.Height + 2;
            Controls.Add(card);
            Y = card.Bottom + 8;
        }
    }
}
