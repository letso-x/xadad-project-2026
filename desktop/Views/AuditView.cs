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

            FlatButton verify = new FlatButton
            {
                Text = "Verify Integrity",
                Glyph = Glyph.Shield,
                Width = Dpi.S(150),
                Height = Dpi.S(34)
            };
            verify.Click += (s, e) => VerifyIntegrity();

            FlatButton exportReport = new FlatButton
            {
                Text = "Export Report",
                Glyph = Glyph.Download,
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(150),
                Height = Dpi.S(34)
            };
            exportReport.Click += (s, e) => ExportBrandedReport();

            FlatButton exportCsv = new FlatButton
            {
                Text = "Export CSV",
                Glyph = Glyph.Download,
                Width = Dpi.S(128),
                Height = Dpi.S(34)
            };
            exportCsv.Click += (s, e) => ExportTrail(fullTrail: true);

            AddPageHeader("Governance", "Audit Trail",
                "An append-only, tamper-evident record of every change, for review and handover.",
                verify, exportCsv, exportReport);

            AddNotice();
            AddStats();
            AddFilters();
            AddTable();

            StartAutoRefresh();
        }

        private Timer _refreshTimer;
        private string _lastSignature;

        /// <summary>
        /// Keeps the on-screen trail live by polling the shared store every few seconds and
        /// restacking only when something actually changed (new count or newest entry).
        /// </summary>
        private void StartAutoRefresh()
        {
            if (_refreshTimer != null) return;

            _lastSignature = null;
            _refreshTimer = new Timer { Interval = 5000 };
            _refreshTimer.Tick += (s, e) => PollForChanges();
            _refreshTimer.Start();

            // Prime the baseline once in the background so the first tick has something
            // to compare against without blocking the UI on load.
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string sig = SafeSignature();
                if (!IsDisposed) BeginInvoke((MethodInvoker)(() => _lastSignature = sig));
            });
        }

        private bool _polling;

        /// <summary>
        /// Checks the shared trail's change signature on a background thread so a slow or
        /// dropped connection never freezes the screen, then restacks on the UI thread
        /// only when something actually changed.
        /// </summary>
        private void PollForChanges()
        {
            if (_polling || IsDisposed) return;
            _polling = true;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string sig = SafeSignature();
                try
                {
                    if (IsDisposed) return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed) return;
                        if (_lastSignature != null && sig != _lastSignature)
                        {
                            _lastSignature = sig;
                            ReloadAndRestack();
                        }
                        else
                        {
                            _lastSignature = sig;
                        }
                    });
                }
                catch (Exception) { }
                finally { _polling = false; }
            });
        }

        private static string SafeSignature()
        {
            try { return Audit.Signature(); }
            catch (Exception) { return null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>Quick summary stats across the whole trail.</summary>
        private void AddStats()
        {
            var all = Audit.Query(null, "All", null, null);
            int total = all.Count;
            int users = all.Select(a => a.User).Distinct().Count();
            int today = all.Count(a =>
                (a.At.Kind == DateTimeKind.Utc ? a.At.ToLocalTime() : a.At).Date == DateTime.Today);

            Label stats = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                BackColor = Color.Transparent,
                Location = new Point(Left1, Y),
                Text = string.Format(
                    "{0} total events  ·  {1} users  ·  {2} today", total, users, today)
            };
            Controls.Add(stats);
            Y = stats.Bottom + Dpi.S(10);
        }

        private void VerifyIntegrity()
        {
            int count; AuditEntry broken;
            bool ok = Audit.VerifyIntegrity(out count, out broken);

            if (ok)
            {
                Notify("Integrity verified — " + count + " entries, chain intact.");
            }
            else
            {
                Notify("Integrity CHECK FAILED — chain broken at "
                    + (broken != null ? broken.EntityLabel ?? broken.Id : "an entry")
                    + ". The trail may have been tampered with.", true);
            }
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
                Text = Audit.Query(null, "All", null, null).Count + " entries recorded"
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
            _table.DefineColumns("When", 1.4f, "User", 1.3f, "Action", 1.2f,
                                 "Document / Entity", 1.8f, "Context", 1.4f,
                                 "Detail", 2.4f, "From", 1.0f);

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

                DateTime localAt = entry.At.Kind == DateTimeKind.Utc
                    ? entry.At.ToLocalTime() : entry.At;
                row.Cells.Add(new TableCell(localAt.ToString("dd MMM yyyy HH:mm"))
                {
                    Mono = true,
                    SubText = Format.Relative(localAt)
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
                row.Cells.Add(new TableCell(entry.EntityLabel ?? "—")
                {
                    Bold = true,
                    SubText = entry.EntityType ?? string.Empty
                });
                row.Cells.Add(new TableCell(string.IsNullOrEmpty(entry.Context) ? "—" : entry.Context)
                {
                    Muted = true
                });
                row.Cells.Add(new TableCell(entry.Detail ?? string.Empty) { Muted = true });
                row.Cells.Add(new TableCell(string.IsNullOrEmpty(entry.Host) ? "—" : entry.Host)
                {
                    Muted = true,
                    Mono = true
                });

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

        /// <summary>
        /// Exports a polished, branded audit report (HTML) and opens it so it can be
        /// reviewed and printed to PDF for a client handover pack.
        /// </summary>
        private void ExportBrandedReport()
        {
            var entries = Audit.All();

            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "Audit report (*.html)|*.html";
                save.FileName = "Mzukulu-Audit-Report-"
                    + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".html";

                if (save.ShowDialog(Shell) != DialogResult.OK) return;

                try
                {
                    AuditReport.Export(entries, save.FileName);
                    Notify("Report created — opening for preview. Use your browser's Print "
                        + "→ Save as PDF for a PDF copy.");
                    try { System.Diagnostics.Process.Start(save.FileName); }
                    catch (Exception) { /* file still written; just couldn't auto-open */ }
                }
                catch (Exception ex)
                {
                    Notify("Report export failed: " + ex.Message, true);
                }
            }
        }

        /// <summary>
        /// Exports the trail to CSV. When <paramref name="fullTrail"/> is true the entire
        /// footprint is written regardless of the on-screen filters; otherwise just the
        /// current filtered view is exported.
        /// </summary>
        private void ExportTrail(bool fullTrail)
        {
            List<AuditEntry> entries;
            if (fullTrail)
            {
                entries = Audit.All();
            }
            else
            {
                string type = _typeFilter != null && _typeFilter.SelectedItem != null
                    ? _typeFilter.SelectedItem.ToString()
                    : "All";
                entries = Audit.Query(_search != null ? _search.Value : null, type, null, null);
            }

            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "CSV file (*.csv)|*.csv";
                save.FileName = (fullTrail ? "audit-trail-full-" : "audit-trail-view-")
                    + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv";

                if (save.ShowDialog(Shell) != DialogResult.OK) return;

                try
                {
                    Exporter.ExportAudit(entries, save.FileName);
                    Notify("Exported " + entries.Count
                        + (fullTrail ? " entries (full trail)." : " entries (current view)."));
                }
                catch (Exception ex)
                {
                    Notify("Export failed: " + ex.Message, true);
                }
            }
        }
    }
}
