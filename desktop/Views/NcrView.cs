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
    /// The NCR register: summary counters, a filterable log and close-out workflow.
    /// </summary>
    public class NcrView : ViewBase
    {
        private static readonly string[] StateFilters =
        {
            "All", "Open", "In Remediation", "Awaiting Verification", "Closed", "Overdue"
        };

        private DataTable _table;
        private TextInput _filter;
        private ComboBox _stateFilter;

        private bool _loaded;
        public NcrView(MainForm shell) : base(shell) { HandleCreated += async(s,e) => { if(_loaded)return;_loaded=true;try{await NcrData.LoadAsync();if(!IsDisposed)Rebuild();}catch(Exception ex){if(!IsDisposed)Notify(ex.Message,true);} }; }

        public override string RouteKey
        {
            get { return "ncr"; }
        }

        protected override void BuildContent()
        {
            FlatButton raise = new FlatButton
            {
                Text = "Raise NCR",
                Glyph = Glyph.Plus,
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(126),
                Height = Dpi.S(34)
            };
            raise.Click += (s, e) => RaiseNcr();

            FlatButton export = new FlatButton
            {
                Text = "Export CSV",
                Glyph = Glyph.Download,
                Width = Dpi.S(128),
                Height = Dpi.S(34)
            };
            export.Click += (s, e) => ExportLog();

            AddPageHeader("Quality", "Non-Conformance Register",
                "Every raised non-conformance, its owner and its close-out status.",
                export, raise);

            AddCounters();
            AddFilters();
            AddTable();
        }

        private void AddCounters()
        {
            var all = Repository.Db.Ncrs;

            var tiles = new List<Tuple<Glyph, string, string, bool>>
            {
                Tuple.Create(Glyph.Alert, all.Count(n => n.State != NcrState.Closed).ToString(),
                    "Open NCRs", false),
                Tuple.Create(Glyph.Clock, all.Count(n => n.IsOverdue).ToString(),
                    "Overdue", true),
                Tuple.Create(Glyph.CheckCircle, all.Count(n => n.State == NcrState.Closed).ToString(),
                    "Closed", false),
                Tuple.Create(Glyph.BarChart, BuildAverageCaption(), "Avg. days to close", false)
            };

            const int perRow = 4;
            int gap = Dpi.S(14);
            int height = Dpi.S(96);
            int width = (InnerWidth - gap * (perRow - 1)) / perRow;

            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                Card tile = StatTile.Build(t.Item1, t.Item2, t.Item3, t.Item4, width, height);
                tile.Location = new Point(Left1 + i * (width + gap), Y);
                Controls.Add(tile);
            }

            Y += height + Dpi.S(22);
        }

        private static string BuildAverageCaption()
        {
            return "Unavailable";
        }

        private void AddFilters()
        {
            _filter = new TextInput
            {
                Glyph = Glyph.Search,
                Placeholder = "Filter by number, title, owner...",
                Location = new Point(Left1, Y),
                Width = Dpi.S(300),
                Height = Dpi.S(34)
            };
            _filter.ValueChanged += (s, e) => ReloadAndRestack();
            Controls.Add(_filter);

            _stateFilter = Ui.Dropdown(Dpi.S(200));
            _stateFilter.Location = new Point(_filter.Right + Dpi.S(10), Y + Dpi.S(3));
            foreach (string option in StateFilters) _stateFilter.Items.Add(option);
            _stateFilter.SelectedIndex = 0;
            _stateFilter.SelectedIndexChanged += (s, e) => ReloadAndRestack();
            Controls.Add(_stateFilter);

            Y = _filter.Bottom + Dpi.S(14);
        }

        private void AddTable()
        {
            _table = new DataTable
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                EmptyMessage = "No non-conformances raised."
            };
            _table.DefineColumns("NCR No.", 1.0f, "Title", 3.0f, "Severity", 1.0f,
                                 "State", 1.4f, "Owner", 1.2f, "Due", 1.0f);
            _table.RowActivated += (s, tag) =>
            {
                Ncr ncr = tag as Ncr;
                if (ncr != null) OpenNcr(ncr);
            };

            Controls.Add(_table);
            ApplyFilters();

            Y = _table.Bottom + Dpi.S(8);
        }

        private void ApplyFilters()
        {
            if (_table == null) return;

            string state = _stateFilter != null && _stateFilter.SelectedItem != null
                ? _stateFilter.SelectedItem.ToString()
                : "All";

            IEnumerable<Ncr> source = Repository.Db.Ncrs;

            switch (state)
            {
                case "Open": source = source.Where(n => n.State == NcrState.Open); break;
                case "In Remediation": source = source.Where(n => n.State == NcrState.InRemediation); break;
                case "Awaiting Verification": source = source.Where(n => n.State == NcrState.AwaitingVerification); break;
                case "Closed": source = source.Where(n => n.State == NcrState.Closed); break;
                case "Overdue": source = source.Where(n => n.IsOverdue); break;
            }

            var rows = new List<TableRow>();

            foreach (Ncr ncr in source.OrderBy(n => n.State == NcrState.Closed)
                                      .ThenBy(n => n.DueDate ?? DateTime.MaxValue))
            {
                Color sevBack, sevFore, stateBack, stateFore;
                Format.SeverityColors(ncr.Severity, out sevBack, out sevFore);
                Format.NcrStateColors(ncr.State, out stateBack, out stateFore);

                TableRow row = new TableRow { Tag = ncr };
                row.Cells.Add(new TableCell(ncr.NcrNo) { Mono = true, Bold = true });
                row.Cells.Add(new TableCell(ncr.Title));
                row.Cells.Add(new TableCell(ncr.Severity.ToString())
                {
                    PillBack = sevBack, PillFore = sevFore
                });
                row.Cells.Add(new TableCell(Format.NcrStateText(ncr.State))
                {
                    PillBack = stateBack,
                    PillFore = stateFore,
                    PillGlyph = ncr.State == NcrState.Closed ? Glyph.CheckCircle : Glyph.Alert
                });
                row.Cells.Add(new TableCell(string.IsNullOrEmpty(ncr.AssignedTo) ? "—" : ncr.AssignedTo));

                // Flag overdue dates so they read as a problem at a glance.
                TableCell due = new TableCell(Format.Date(ncr.DueDate));
                if (ncr.IsOverdue)
                {
                    due.PillBack = Theme.Red100;
                    due.PillFore = Theme.Red700;
                    due.Text = Format.Date(ncr.DueDate) + " · overdue";
                }
                else
                {
                    due.Muted = true;
                }
                row.Cells.Add(due);

                row.FilterText = string.Join(" ", new[]
                {
                    ncr.NcrNo, ncr.Title, ncr.AssignedTo, ncr.RaisedBy,
                    Format.NcrStateText(ncr.State), ncr.Severity.ToString()
                }).ToLowerInvariant();

                rows.Add(row);
            }

            _table.SetRows(rows);
            _table.ApplyFilter(_filter != null ? _filter.Value : string.Empty);
        }

        /// <summary>
        /// Reloads rows then restacks the page, because the table's height depends on
        /// the row count. Deferred so it never runs while the layout engine is mid-pass,
        /// which would tear down the controls it is walking.
        /// </summary>
        private void ReloadAndRestack()
        {
            if (IsDisposed) return;

            BeginInvoke((MethodInvoker)delegate
            {
                if (!IsDisposed) Rebuild();
            });
        }

        private void RaiseNcr()
        { Notify("Standalone NCR creation is not exposed by the API. Mark the QC requirement R to raise its NCR automatically.", true); }

        /// <summary>Opens an NCR for review, allowing state and close-out edits.</summary>
        private void OpenNcr(Ncr ncr)
        {
            using (Dialog dialog = new Dialog(ncr.NcrNo + " — " + ncr.Title, Dpi.S(600)))
            {
                dialog.AddReadOnlyField("Requirement", ncr.Title);
                dialog.AddReadOnlyField("Raised by", ncr.RaisedBy);
                dialog.AddReadOnlyField("State", Format.NcrStateText(ncr.State));
                dialog.AddReadOnlyField("Corrective action", ncr.CorrectiveAction);
                if (Json.Bool(ncr.RemoteRecord, "isLegacyOnly"))
                { dialog.AddNote("This historical NCR is read-only through the current API."); dialog.ShowDialog(Shell); return; }
                ComboBox severity = dialog.AddDropdown("Severity", new[] { "Minor", "Major", "Critical" }, ncr.Severity.ToString());
                TextInput classification = dialog.AddTextField("Classification", Json.Text(ncr.RemoteRecord, "classification"));
                CheckBox blocking = dialog.AddCheckField("Blocking", Json.Bool(ncr.RemoteRecord, "isBlocking"));
                TextInput disposition = dialog.AddTextField("Disposition", Json.Text(ncr.RemoteRecord, "disposition"));
                TextInput action = dialog.AddTextArea("Corrective action / verification comments", ncr.CorrectiveAction);
                dialog.AddNote("Correction requires photographic evidence. Independent verification and permitted transitions are enforced by the service.");
                dialog.AddAsyncAction("Correct", async () =>
                {
                    using (var picker = new OpenFileDialog { Filter = "Photos|*.jpg;*.jpeg;*.png" })
                    {
                        if (picker.ShowDialog(Shell) != DialogResult.OK) return;
                        try
                        {
                            await NcrData.CorrectAsync(ncr, Convert.ToString(severity.SelectedItem), classification.Value, blocking.Checked, disposition.Value, action.Value, System.IO.File.ReadAllBytes(picker.FileName), System.IO.Path.GetFileName(picker.FileName), System.IO.Path.GetExtension(picker.FileName).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg");
                            dialog.DialogResult = DialogResult.OK;
                        }
                        catch (Exception ex) { Notify(ex.Message, true); }
                    }
                });
                dialog.AddAsyncAction("Verify passed", async () => { try { await NcrData.VerifyAsync(ncr, true, action.Value); dialog.DialogResult = DialogResult.OK; } catch (Exception ex) { Notify(ex.Message, true); } });
                dialog.AddAsyncAction("Verify failed", async () => { try { await NcrData.VerifyAsync(ncr, false, action.Value); dialog.DialogResult = DialogResult.OK; } catch (Exception ex) { Notify(ex.Message, true); } });
                if (dialog.ShowDialog(Shell) == DialogResult.OK) { Notify("NCR updated by the shared service."); Rebuild(); }
            }
        }

        private static NcrState ParseState(string text)
        {
            switch (text)
            {
                case "In Remediation": return NcrState.InRemediation;
                case "Awaiting Verification": return NcrState.AwaitingVerification;
                case "Closed": return NcrState.Closed;
                default: return NcrState.Open;
            }
        }

        private void ExportLog()
        {
            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "CSV file (*.csv)|*.csv";
                save.FileName = "NCR-register-" + DateTime.Now.ToString("yyyyMMdd") + ".csv";

                if (save.ShowDialog(Shell) != DialogResult.OK) return;

                try
                {
                    Exporter.ExportNcrs(Repository.Db.Ncrs.ToList(), save.FileName);

                    Notify("Exported " + Repository.Db.Ncrs.Count + " NCRs.");
                }
                catch (Exception ex)
                {
                    Notify("Export failed: " + ex.Message, true);
                }
            }
        }
    }
}
