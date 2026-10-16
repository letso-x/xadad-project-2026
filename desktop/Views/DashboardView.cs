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
    /// The QC Control Centre: eight summary tiles, recent activity, per-project
    /// progress and quick actions.
    /// </summary>
    public class DashboardView : ViewBase
    {
        public DashboardView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "dashboard"; }
        }

        protected override void BuildContent()
        {
            Database db = Repository.Db;

            // Admins get the branded landing banner; site users keep the plain header.
            if (Repository.IsAdmin)
            {
                AddAdminBanner();
            }

            AddPageHeader("Overview", "QC Control Centre",
                "What's happening across every project, at a glance.");

            AddStatTiles(db);
            AddActivityAndProgress(db);
            AddQuickActions();
        }

        /// <summary>
        /// Branded welcome banner shown on the admin landing page: the Mzukulu logo
        /// beside a greeting, on a navy card.
        /// </summary>
        private void AddAdminBanner()
        {
            Card banner = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Height = Dpi.S(88),
                Fill = Theme.Navy950,
                BorderWidth = 0,
                Radius = Theme.Radius
            };

            MzukuluLogo logo = new MzukuluLogo
            {
                Location = new Point(Dpi.S(20), Dpi.S(16)),
                Size = new Size(Dpi.S(56), Dpi.S(56))
            };
            banner.Controls.Add(logo);

            Label welcome = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(90), Dpi.S(20)),
                Text = "Welcome back, " + Repository.CurrentUserName
            };
            banner.Controls.Add(welcome);

            Label sub = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Color.FromArgb(0x9F, 0xB0, 0xC6),
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(92), welcome.Bottom + Dpi.S(2)),
                Text = "Mzukulu Technologies — Quality Management System"
            };
            banner.Controls.Add(sub);

            Controls.Add(banner);
            Y = banner.Bottom + Dpi.S(18);
        }

        #region Stat tiles

        private void AddStatTiles(Database db)
        {
            int totalProjects = db.Projects.Count;
            int activeProjects = db.Projects.Count(p => p.Status == "Active");

            int inProgress = db.Forms.Count(f => f.Status == RecordStatus.InProgress)
                             + db.Registers.Count(r => r.Status == RecordStatus.InProgress);

            int completed = db.Forms.Count(f => f.Status != RecordStatus.InProgress)
                            + db.Registers.Count(r => r.Status != RecordStatus.InProgress);

            int totalRecords = db.Forms.Count + db.Registers.Count;

            // "Requiring attention" counts raised NCRs plus drafts that already have a
            // rejected line, matching the dashboard rule in the spec.
            int attention = db.Forms.Count(f => f.Status == RecordStatus.CompleteNcrRaised
                                                || (f.Status == RecordStatus.InProgress && f.HasRejected));

            var tiles = new List<Tuple<Glyph, string, string, bool>>
            {
                Tuple.Create(Glyph.Folder, totalProjects.ToString(), "Total Projects", false),
                Tuple.Create(Glyph.CheckCircle, activeProjects.ToString(), "Active Projects", false),
                Tuple.Create(Glyph.Clock, inProgress.ToString(), "Forms In Progress", false),
                Tuple.Create(Glyph.Clipboard, totalRecords.ToString(), "Total QC Records", false),
                Tuple.Create(Glyph.Alert, attention.ToString(), "Requiring Attention (NCR)", true),
                Tuple.Create(Glyph.Building, db.Clients.Count.ToString(), "Clients", false),
                Tuple.Create(Glyph.Tool, db.Templates.Count(t => t.Active).ToString(), "Active Checklist Templates", false),
                Tuple.Create(Glyph.Check, completed.ToString(), "Completed Forms", false)
            };

            const int perRow = 4;
            int gap = Dpi.S(14);
            int tileHeight = Dpi.S(104);

            int tileWidth = (InnerWidth - gap * (perRow - 1)) / perRow;

            for (int i = 0; i < tiles.Count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;

                var t = tiles[i];
                // Shared builder so the tile matches the NCR and Reports screens and
                // sizes its content to the (already DPI-scaled) box height.
                Card tile = StatTile.Build(t.Item1, t.Item2, t.Item3, t.Item4, tileWidth, tileHeight);
                tile.Location = new Point(Left1 + col * (tileWidth + gap),
                                          Y + row * (tileHeight + gap));
                Controls.Add(tile);
            }

            int rows = (int)Math.Ceiling(tiles.Count / (double)perRow);
            Y += rows * tileHeight + (rows - 1) * gap + Dpi.S(26);
        }

        #endregion

        #region Activity and progress

        private void AddActivityAndProgress(Database db)
        {
            const int gap = 14;
            int leftWidth = (int)((InnerWidth - gap) * 0.56);
            int rightWidth = InnerWidth - gap - leftWidth;

            int startY = Y;

            int leftBottom = AddActivityCard(db, Left1, startY, leftWidth);
            int rightBottom = AddProgressCard(db, Left1 + leftWidth + gap, startY, rightWidth);

            Y = Math.Max(leftBottom, rightBottom) + 24;
        }

        private int AddActivityCard(Database db, int x, int y, int width)
        {
            // Most recently touched records across both forms and registers.
            var entries = db.Forms
                .Select(f => new
                {
                    Label = BuildFormLabel(f),
                    ProjectId = f.ProjectId,
                    Record = f.QcRecordNo,
                    Updated = f.UpdatedAt,
                    Status = f.Status,
                    Nav = (Action)(() => Shell.Navigate(new FormFillView(Shell, f.Id)))
                })
                .Concat(db.Registers.Select(r => new
                {
                    Label = CableRegister.DocNumber + " — " + CableRegister.DocName,
                    ProjectId = r.ProjectId,
                    Record = r.QcRecordNo,
                    Updated = r.UpdatedAt,
                    Status = r.Status,
                    Nav = (Action)(() => Shell.Navigate(new RegisterView(Shell, r.Id)))
                }))
                .OrderByDescending(e => e.Updated)
                .Take(6)
                .ToList();

            Card card = new Card
            {
                Location = new Point(x, y),
                Width = width,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Recent QC Activity", width);
            card.Controls.Add(bar);

            int rowY = bar.Height;

            if (entries.Count == 0)
            {
                Label empty = Ui.Muted("No records yet.");
                empty.Location = new Point(18, rowY + 18);
                card.Controls.Add(empty);
                rowY += 56;
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    Project project = Repository.GetProject(entry.ProjectId);

                    ActivityRow row = new ActivityRow
                    {
                        Title = entry.Label,
                        Project = project != null ? project.Name : string.Empty,
                        RecordNumber = entry.Record,
                        Updated = "updated " + Format.Date(entry.Updated),
                        Status = entry.Status,
                        ShowDivider = i < entries.Count - 1,
                        Location = new Point(1, rowY),
                        Width = width - 2
                    };

                    Action nav = entry.Nav;
                    row.Click += (s, e) => nav();

                    card.Controls.Add(row);
                    rowY += row.Height;
                }
            }

            card.Height = rowY + 8;
            Controls.Add(card);
            return card.Bottom;
        }

        private static string BuildFormLabel(QcForm form)
        {
            ChecklistTemplate template = Repository.GetTemplate(form.TemplateId);
            return template == null
                ? form.QcRecordNo
                : template.DocNumber + " — " + template.DocName;
        }

        private int AddProgressCard(Database db, int x, int y, int width)
        {
            Card card = new Card
            {
                Location = new Point(x, y),
                Width = width,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Project Progress", width);
            card.Controls.Add(bar);

            int rowY = bar.Height + 18;
            int contentWidth = width - 36;

            // Busiest projects first, capped at five as per the design.
            var ranked = db.Projects
                .Select(p =>
                {
                    int done, total;
                    int pct = Repository.ProjectCompletionPercent(p.Id, out done, out total);
                    return new { Project = p, Done = done, Total = total, Percent = pct };
                })
                .OrderByDescending(r => r.Total)
                .Take(5)
                .ToList();

            if (ranked.Count == 0)
            {
                Label empty = Ui.Muted("No projects yet.");
                empty.Location = new Point(18, rowY);
                card.Controls.Add(empty);
                rowY += 40;
            }

            foreach (var item in ranked)
            {
                Label name = new Label
                {
                    AutoSize = false,
                    Font = Theme.SmallBold,
                    ForeColor = Theme.Slate900,
                    BackColor = Color.Transparent,
                    Location = new Point(18, rowY),
                    Width = contentWidth - 90,
                    Height = Dpi.S(18),
                    Text = item.Project.Name,
                    Cursor = Cursors.Hand
                };
                Project captured = item.Project;
                name.Click += (s, e) => Shell.Navigate(new ProjectDetailView(Shell, captured.Id));
                card.Controls.Add(name);

                Label fraction = new Label
                {
                    AutoSize = true,
                    Font = Theme.Tiny,
                    ForeColor = Theme.Slate600,
                    BackColor = Color.Transparent,
                    Text = item.Done + "/" + item.Total + " complete"
                };
                card.Controls.Add(fraction);
                fraction.Location = new Point(width - 18 - fraction.Width, rowY + 1);

                rowY += 24;

                ProgressTrack track = new ProgressTrack
                {
                    Location = new Point(18, rowY),
                    Width = contentWidth - 48,
                    Height = Dpi.S(7),
                    Percent = item.Percent
                };
                track.BarColor = item.Percent >= 100 ? Theme.Green600
                               : item.Percent == 0 ? Theme.Slate200
                               : Theme.Amber600;
                card.Controls.Add(track);

                Label pct = new Label
                {
                    AutoSize = true,
                    Font = Theme.MonoSmall,
                    ForeColor = Theme.Navy900,
                    BackColor = Color.Transparent,
                    Text = item.Percent + "%"
                };
                card.Controls.Add(pct);
                pct.Location = new Point(width - 18 - pct.Width, rowY - 4);

                rowY += 26;
            }

            card.Height = Math.Max(rowY + 6, bar.Height + 60);
            Controls.Add(card);
            return card.Bottom;
        }

        #endregion

        #region Quick actions

        private void AddQuickActions()
        {
            Label heading = new Label
            {
                AutoSize = true,
                Font = Theme.SectionTitle,
                ForeColor = Theme.Navy950,
                Location = new Point(Left1, Y),
                Text = "Quick Actions"
            };
            Controls.Add(heading);
            Y = heading.Bottom + Dpi.S(12);

            var actions = new List<Tuple<string, Glyph, Action>>
            {
                Tuple.Create("New Form", Glyph.Plus,
                    (Action)(() => Shell.Navigate(new LibraryView(Shell)))),
                Tuple.Create("View Projects", Glyph.Folder,
                    (Action)(() => Shell.Navigate(new ProjectsView(Shell)))),
                Tuple.Create("Search Records", Glyph.Search,
                    (Action)(() => Shell.Navigate(new SearchView(Shell, string.Empty)))),
                Tuple.Create("Form Library", Glyph.Layers,
                    (Action)(() => Shell.Navigate(new LibraryView(Shell))))
            };

            // Administrators get the template and client management shortcuts too.
            if (Repository.IsAdmin)
            {
                actions.Add(Tuple.Create("Checklist Templates", Glyph.Tool,
                    (Action)(() => Shell.Navigate(new TemplatesView(Shell)))));
                actions.Add(Tuple.Create("Create Template", Glyph.Plus,
                    (Action)(() => Shell.Navigate(new TemplateEditorView(Shell, null)))));
                actions.Add(Tuple.Create("Manage Clients", Glyph.Building,
                    (Action)(() => Shell.Navigate(new ClientsView(Shell)))));
                actions.Add(Tuple.Create("Manage Users", Glyph.Users,
                    (Action)(() => Shell.Navigate(new UsersView(Shell)))));
            }

            // Four per row keeps each button wide enough for its longest label
            // ("Checklist Templates") without clipping, at any DPI.
            const int perRow = 4;
            int gap = Dpi.S(14);
            int height = Dpi.S(42);
            int width = (InnerWidth - gap * (perRow - 1)) / perRow;

            for (int i = 0; i < actions.Count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;

                var a = actions[i];
                FlatButton button = new FlatButton
                {
                    Text = a.Item1,
                    Glyph = a.Item2,
                    LeftAlign = true,
                    // Grid buttons are equal width by design; the column is sized to
                    // fit the longest label, so they must not auto-grow individually.
                    AllowGrow = false,
                    Width = width,
                    Height = height,
                    Location = new Point(Left1 + col * (width + gap), Y + row * (height + gap))
                };

                Action handler = a.Item3;
                button.Click += (s, e) => handler();
                Controls.Add(button);
            }

            int rows = (int)Math.Ceiling(actions.Count / (double)perRow);
            Y += rows * height + (rows - 1) * gap + Dpi.S(6);
        }

        #endregion
    }
}
