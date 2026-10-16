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
    /// The audit trail. Read-only by design: entries are appended by the app and never
    /// edited here, which is what makes the log worth anything in a review.
    /// </summary>
    public class AuditView : ViewBase
    {
        private static readonly string[] TypeFilters =
        {
            "All", "Form", "Register", "Project", "Client", "Template", "NCR"
        };

        private DataTable _table;
        private TextInput _search;
        private ComboBox _typeFilter;

        public AuditView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "audit"; }
        }

        protected override void BuildContent()
        {
            // Administrator-only: the trail records who did what, so it is a
            // supervisory tool rather than something every site user should browse.
            if (!Repository.IsAdmin)
            {
                TemplatesView.AddAccessDenied(this);
                return;
            }

            FlatButton export = new FlatButton
            {
                Text = "Export CSV",
                Glyph = Glyph.Download,
                Width = Dpi.S(128),
                Height = Dpi.S(34)
            };
            export.Click += (s, e) => ExportTrail();

            AddPageHeader("Governance", "Audit Trail",
                "An append-only record of every change, for review and handover.",
                export);

            AddNotice();
            AddFilters();
            AddTable();
        }

        /// <summary>Explains the immutability guarantee so it is not mistaken for a feed.</summary>
        private void AddNotice()
        {
            int textWidth = InnerWidth - Dpi.S(72);

            Label heading = Ui.Eyebrow("Append-only record");
            heading.ForeColor = Theme.Navy700;

            Label body = Ui.Wrapped(
                "Entries are written by the application as work happens and cannot be edited "
                + "or removed from this screen. Export the trail to include it in a handover pack.",
                Theme.Small, Theme.Slate700, textWidth);

            Card box = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Theme.Slate50,
                BorderTint = Theme.Slate150,
                Radius = Theme.RadiusSm,
                LeftAccentWidth = Dpi.S(4),
                LeftAccentColor = Theme.Navy700
            };

            Card icon = new Card
            {
                Location = new Point(Dpi.S(18), Dpi.S(16)),
                Size = new Size(Dpi.S(20), Dpi.S(20)),
                BorderWidth = 0,
                Fill = Color.Transparent
            };
            icon.Paint += (s, e) =>
            {
                Glyphs.Draw(e.Graphics, Glyph.Shield,
                    new Rectangle(0, 0, Dpi.S(19), Dpi.S(19)), Theme.Navy700, Dpi.S(1.7f));
            };
            box.Controls.Add(icon);

            heading.Location = new Point(Dpi.S(48), Dpi.S(15));
            box.Controls.Add(heading);

            body.Location = new Point(Dpi.S(48), heading.Bottom + Dpi.S(5));
            box.Controls.Add(body);

            box.Height = body.Bottom + Dpi.S(16);
            Controls.Add(box);
            Y = box.Bottom + Dpi.S(18);
        }

        private void AddFilters()
        {
            _search = new TextInput
            {
                Glyph = Glyph.Search,
                Placeholder = "Search user, entity or detail...",
                Location = new Point(Left1, Y),
                Width = Dpi.S(300),
                Height = Dpi.S(34)
            };
            _search.ValueChanged += (s, e) => ReloadAndRestack();
            Controls.Add(_search);

            _typeFilter = Ui.Dropdown(Dpi.S(160));
            _typeFilter.Location = new Point(_search.Right + Dpi.S(10), Y + Dpi.S(3));
            foreach (string option in TypeFilters) _typeFilter.Items.Add(option);
            _typeFilter.SelectedIndex = 0;
            _typeFilter.SelectedIndexChanged += (s, e) => ReloadAndRestack();
            Controls.Add(_typeFilter);

            Label count = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate500,
                Text = Repository.Db.Audit.Count + " entries recorded"
            };
            Controls.Add(count);
            count.Location = new Point(Left1 + InnerWidth - count.Width, Y + Dpi.S(10));

            Y = _search.Bottom + Dpi.S(14);
        }

        private void AddTable()
        {
            _table = new DataTable
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                EmptyMessage = "No audit entries match the current filter."
            };
            _table.DefineColumns("When", 1.5f, "User", 1.3f, "Action", 1.3f,
                                 "Type", 0.9f, "Entity", 1.8f, "Detail", 2.8f);

            Controls.Add(_table);
            LoadRows();

            Y = _table.Bottom + Dpi.S(8);
        }

        private void LoadRows()
        {
            string type = _typeFilter != null && _typeFilter.SelectedItem != null
                ? _typeFilter.SelectedItem.ToString()
                : "All";

            var entries = Audit.Query(_search != null ? _search.Value : null, type, null, null);
            var rows = new List<TableRow>();

            foreach (AuditEntry entry in entries)
            {
                TableRow row = new TableRow { Tag = entry };

                row.Cells.Add(new TableCell(entry.At.ToString("dd MMM yyyy HH:mm"))
                {
                    Mono = true,
                    SubText = Format.Relative(entry.At)
                });
                row.Cells.Add(new TableCell(entry.User)
                {
                    Bold = true,
                    SubText = Format.RoleBadge(entry.Role)
                });
                row.Cells.Add(new TableCell(Audit.ActionText(entry.Action))
                {
                    PillBack = ActionBack(entry.Action),
                    PillFore = ActionFore(entry.Action)
                });
                row.Cells.Add(new TableCell(entry.EntityType ?? "—") { Muted = true });
                row.Cells.Add(new TableCell(entry.EntityLabel ?? "—"));
                row.Cells.Add(new TableCell(entry.Detail ?? string.Empty) { Muted = true });

                rows.Add(row);
            }

            _table.SetRows(rows);
        }

        private static Color ActionBack(AuditAction action)
        {
            switch (action)
            {
                case AuditAction.NcrRaised:
                case AuditAction.Deleted:
                    return Theme.Red100;
                case AuditAction.Submitted:
                case AuditAction.SignedOff:
                case AuditAction.NcrClosed:
                    return Theme.Green100;
                case AuditAction.Exported:
                    return Theme.Slate150;
                default:
                    return Theme.Slate100;
            }
        }

        private static Color ActionFore(AuditAction action)
        {
            switch (action)
            {
                case AuditAction.NcrRaised:
                case AuditAction.Deleted:
                    return Theme.Red700;
                case AuditAction.Submitted:
                case AuditAction.SignedOff:
                case AuditAction.NcrClosed:
                    return Theme.Green700;
                default:
                    return Theme.Slate700;
            }
        }

        /// <summary>
        /// Reloads rows then restacks the page. Deferred via BeginInvoke so the rebuild
        /// never runs inside a layout pass, which would dispose the controls the layout
        /// engine is still walking.
        /// </summary>
        private void ReloadAndRestack()
        {
            if (IsDisposed) return;

            BeginInvoke((MethodInvoker)delegate
            {
                if (IsDisposed) return;
                LoadRows();
                Rebuild();
            });
        }

        private void ExportTrail()
        {
            string type = _typeFilter != null && _typeFilter.SelectedItem != null
                ? _typeFilter.SelectedItem.ToString()
                : "All";
            var entries = Audit.Query(_search != null ? _search.Value : null, type, null, null);

            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "CSV file (*.csv)|*.csv";
                save.FileName = "audit-trail-" + DateTime.Now.ToString("yyyyMMdd") + ".csv";

                if (save.ShowDialog(Shell) != DialogResult.OK) return;

                try
                {
                    Exporter.ExportAudit(entries, save.FileName);
                    Notify("Exported " + entries.Count + " audit entries.");
                }
                catch (Exception ex)
                {
                    Notify("Export failed: " + ex.Message, true);
                }
            }
        }
    }
}
