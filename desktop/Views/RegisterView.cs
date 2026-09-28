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
    /// The MZT-QC-203 Cable Schedule &amp; Installation Register: header fields, an
    /// editable per-cable grid, notes and two sign-off blocks.
    /// </summary>
    public class RegisterView : ViewBase
    {
        private static readonly string[] HeaderFields =
        {
            "Project", "Project N°", "Client", "Contract / Order N°",
            "Area / Location", "Drawing N° & Rev.", "Cable Schedule Ref. & Rev.", "Register Revision"
        };

        private readonly string _registerId;
        private CableRegister _register;
        private Project _project;

        public RegisterView(MainForm shell, string registerId) : base(shell)
        {
            _registerId = registerId;
        }

        public override string RouteKey
        {
            get { return "library"; }
        }

        protected override void BuildContent()
        {
            _register = Repository.GetRegister(_registerId);
            if (_register == null)
            {
                Label missing = Ui.Body("Register not found.");
                missing.Location = new Point(Left1, Y);
                Controls.Add(missing);
                Y = missing.Bottom + 20;
                return;
            }

            _project = Repository.GetProject(_register.ProjectId);
            if (_project == null)
            {
                Label missing = Ui.Body("This register references a missing project.");
                missing.Location = new Point(Left1, Y);
                Controls.Add(missing);
                Y = missing.Bottom + 20;
                return;
            }

            AddCrumbs(_project.Name,
                () => Shell.Navigate(new ProjectDetailView(Shell, _project.Id)),
                CableRegister.DocNumber);

            AddHeaderBar();
            AddProjectInfoCard();
            AddGuidanceBox();
            AddCableGrid();
            AddNotesCard();
            AddSignoffCard();
            AddAuditLine();
            AddBottomActions();
        }

        #region Header

        private void AddHeaderBar()
        {
            int total = _register.Cables.Count;
            int complete = _register.Cables.Count(c => c.Complete);
            int percent = total == 0 ? 0 : (int)Math.Round(complete * 100.0 / total);

            Card bar = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Label eyebrow = new Label
            {
                AutoSize = true,
                Font = Theme.Eyebrow,
                ForeColor = Theme.Brick600,
                Location = new Point(Dpi.S(18), Dpi.S(16)),
                Text = CableRegister.DocNumber + " · Quality Control Register · " + _register.QcRecordNo
            };
            bar.Controls.Add(eyebrow);

            Label title = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Theme.Navy950,
                Location = new Point(17, eyebrow.Bottom + 4),
                Width = InnerWidth - 220,
                Height = Dpi.S(26),
                Text = CableRegister.DocName
            };
            bar.Controls.Add(title);

            Label project = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                Location = new Point(18, title.Bottom + 2),
                Text = _project.Name
            };
            bar.Controls.Add(project);

            Pill status = new Pill { Height = 24 };
            status.ApplyStatus(_register.Status);
            status.Location = new Point(InnerWidth - status.Width - 18, 16);
            bar.Controls.Add(status);

            int progressTop = project.Bottom + 14;

            ProgressTrack track = new ProgressTrack
            {
                Location = new Point(18, progressTop + 5),
                Width = InnerWidth - 250,
                Height = Dpi.S(7),
                Percent = percent
            };
            track.BarColor = percent >= 100 ? Theme.Green600 : Theme.Amber600;
            bar.Controls.Add(track);

            Label label = new Label
            {
                AutoSize = true,
                Font = Theme.Tiny,
                ForeColor = Theme.Slate600,
                Location = new Point(track.Right + 12, progressTop),
                Text = complete + " / " + total + " cables complete · " + percent + "%"
            };
            bar.Controls.Add(label);

            int actionTop = progressTop + 26;

            FlatButton save = new FlatButton
            {
                Text = "Save Draft",
                Glyph = Glyph.CheckCircle,
                Width = Dpi.S(124),
                Height = Dpi.S(34),
                Location = new Point(18, actionTop)
            };
            save.Click += (s, e) => SaveDraft();
            bar.Controls.Add(save);

            FlatButton complete2 = new FlatButton
            {
                Text = "Mark Register Complete",
                Glyph = Glyph.Check,
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(206),
                Height = Dpi.S(34),
                Location = new Point(save.Right + 8, actionTop)
            };
            complete2.Click += (s, e) => MarkComplete();
            bar.Controls.Add(complete2);

            FlatButton export = new FlatButton
            {
                Text = "Export",
                Glyph = Glyph.Download,
                Width = Dpi.S(104),
                Height = Dpi.S(34),
                Location = new Point(complete2.Right + Dpi.S(8), actionTop)
            };
            export.Click += (s, e) => ExportRegister();
            bar.Controls.Add(export);

            FlatButton back = new FlatButton
            {
                Text = "Back to Project",
                Variant = ButtonVariant.Ghost,
                Width = Dpi.S(132),
                Height = Dpi.S(34),
                Location = new Point(export.Right + Dpi.S(8), actionTop)
            };
            back.Click += (s, e) => Shell.Navigate(new ProjectDetailView(Shell, _project.Id));
            bar.Controls.Add(back);

            bar.Height = actionTop + 34 + 16;
            Controls.Add(bar);
            Y = bar.Bottom + 18;
        }

        private void AddProjectInfoCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Project Information", InnerWidth);
            card.Controls.Add(bar);

            const int perRow = 3;
            const int gap = 16;
            int colWidth = (InnerWidth - 36 - gap * (perRow - 1)) / perRow;

            int top = bar.Bottom + 16;

            for (int i = 0; i < HeaderFields.Length; i++)
            {
                string field = HeaderFields[i];
                int col = i % perRow;
                int row = i / perRow;

                int x = 18 + col * (colWidth + gap);
                int y = top + row * 62;

                Label label = Ui.FieldLabel(field);
                label.Location = new Point(x, y);
                card.Controls.Add(label);

                string current;
                _register.FieldValues.TryGetValue(field, out current);

                TextInput input = new TextInput
                {
                    Location = new Point(x, label.Bottom + 5),
                    Width = colWidth,
                    Height = Dpi.S(32),
                    Value = current ?? string.Empty
                };

                string key = field;
                input.ValueChanged += (s, e) => _register.FieldValues[key] = input.Value;
                card.Controls.Add(input);
            }

            int rows = (int)Math.Ceiling(HeaderFields.Length / (double)perRow);
            int nextY = top + rows * 62;

            // QC record number, read-only chip.
            Label recordLabel = Ui.FieldLabel("QC Record No.");
            recordLabel.Location = new Point(18, nextY);
            card.Controls.Add(recordLabel);

            Label chip = new Label
            {
                AutoSize = true,
                Font = Theme.MonoSmall,
                ForeColor = Theme.Navy900,
                BackColor = Theme.Slate100,
                Padding = new Padding(Dpi.S(8), Dpi.S(4), Dpi.S(8), Dpi.S(4)),
                Location = new Point(18, recordLabel.Bottom + 5),
                Text = _register.QcRecordNo
            };
            card.Controls.Add(chip);

            card.Height = chip.Bottom + 18;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        private void AddGuidanceBox()
        {
            int textWidth = InnerWidth - 44;

            Label heading = Ui.Eyebrow("How to use this register");
            heading.ForeColor = Theme.Brick700;

            Label line1 = Ui.Wrapped(
                "One line per cable. This register is the single source of truth for cable progress "
                + "and must be updated as each step is completed.",
                Theme.Small, Theme.Slate800, textWidth);

            Label line2 = Ui.Wrapped(
                "Enter the QC record number of the test certificate against each cable — not just a tick.",
                Theme.SmallBold, Theme.Slate900, textWidth);

            Card box = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Theme.Brick050,
                BorderTint = Theme.Brick100,
                Radius = Theme.RadiusSm,
                LeftAccentWidth = 4,
                LeftAccentColor = Theme.Brick600
            };

            heading.Location = new Point(Dpi.S(20), Dpi.S(15));
            box.Controls.Add(heading);

            line1.Location = new Point(20, heading.Bottom + 7);
            box.Controls.Add(line1);

            line2.Location = new Point(20, line1.Bottom + 6);
            box.Controls.Add(line2);

            box.Height = line2.Bottom + 16;
            Controls.Add(box);
            Y = box.Bottom + 16;
        }

        #endregion

        #region Cable grid

        /// <summary>
        /// The cable register uses a real DataGridView. Unlike the read-only lists
        /// elsewhere, this grid is edited cell by cell, which is exactly what
        /// DataGridView handles well.
        /// </summary>
        private void AddCableGrid()
        {
            int total = _register.Cables.Count;
            int complete = _register.Cables.Count(c => c.Complete);

            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Cable Register", InnerWidth);
            Label counter = new Label
            {
                AutoSize = true,
                Font = Theme.MonoSmall,
                ForeColor = Color.FromArgb(190, 255, 255, 255),
                BackColor = Color.Transparent,
                Text = complete + " / " + total + " complete"
            };
            bar.Controls.Add(counter);
            counter.Location = new Point(InnerWidth - counter.Width - 18, 12);
            card.Controls.Add(bar);

            DataGridView grid = BuildGrid();
            grid.Location = new Point(1, bar.Height);
            grid.Width = InnerWidth - 2;
            grid.Height = Math.Max(120, 34 + _register.Cables.Count * 30 + 4);
            card.Controls.Add(grid);

            FlatButton addRow = new FlatButton
            {
                Text = "Add Cable Row",
                Glyph = Glyph.Plus,
                Width = Dpi.S(156),
                Height = Dpi.S(32),
                Location = new Point(18, grid.Bottom + 14)
            };
            addRow.Click += (s, e) =>
            {
                _register.Cables.Add(new CableRow
                {
                    Id = Repository.NewId("cab"),
                    No = _register.Cables.Count + 1,
                    CableNo = string.Empty, From = string.Empty, To = string.Empty,
                    CableType = string.Empty, DrumNo = string.Empty, Length = string.Empty,
                    IrCert = string.Empty, ContCert = string.Empty
                });
                Repository.Save();
                Rebuild();
            };
            card.Controls.Add(addRow);

            card.Height = addRow.Bottom + 16;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        private DataGridView BuildGrid()
        {
            DataGridView grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeight = Dpi.S(34),
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false,
                Font = Theme.Small,
                GridColor = Theme.Slate100,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                ScrollBars = ScrollBars.Horizontal
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Slate50;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Slate500;
            grid.ColumnHeadersDefaultCellStyle.Font = Theme.Label;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(Dpi.S(6), Dpi.S(0), Dpi.S(0), Dpi.S(0));

            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Theme.Slate900;
            grid.DefaultCellStyle.SelectionBackColor = Theme.Slate50;
            grid.DefaultCellStyle.SelectionForeColor = Theme.Slate900;
            grid.DefaultCellStyle.Padding = new Padding(Dpi.S(6), Dpi.S(0), Dpi.S(0), Dpi.S(0));
            grid.RowTemplate.Height = Dpi.S(30);

            AddTextColumn(grid, "No", "N°", 42, true);
            AddTextColumn(grid, "CableNo", "Cable N°", 90, false);
            AddTextColumn(grid, "From", "From", 84, false);
            AddTextColumn(grid, "To", "To", 84, false);
            AddTextColumn(grid, "CableType", "Type / Size / Cores", 150, false);
            AddTextColumn(grid, "DrumNo", "Drum N°", 80, false);
            AddTextColumn(grid, "Length", "Length (m)", 78, false);
            AddCheckColumn(grid, "Pulled", "Pulled");
            AddCheckColumn(grid, "Glanded", "Glanded");
            AddCheckColumn(grid, "Terminated", "Terminated");
            AddTextColumn(grid, "IrCert", "IR Cert. N°", 92, false);
            AddTextColumn(grid, "ContCert", "Cont. Cert. N°", 100, false);
            AddCheckColumn(grid, "Complete", "Complete");

            foreach (CableRow cable in _register.Cables)
            {
                int index = grid.Rows.Add(
                    cable.No, cable.CableNo, cable.From, cable.To, cable.CableType,
                    cable.DrumNo, cable.Length, cable.Pulled, cable.Glanded, cable.Terminated,
                    cable.IrCert, cable.ContCert, cable.Complete);
                grid.Rows[index].Tag = cable;
            }

            // Write edits straight back to the model.
            grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;

                CableRow cable = grid.Rows[e.RowIndex].Tag as CableRow;
                if (cable == null) return;

                object value = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                string column = grid.Columns[e.ColumnIndex].Name;

                ApplyCellEdit(cable, column, value);
            };

            // Commit checkbox toggles immediately rather than on focus loss.
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty
                    && grid.CurrentCell is DataGridViewCheckBoxCell)
                {
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            return grid;
        }

        private static void ApplyCellEdit(CableRow cable, string column, object value)
        {
            string text = Convert.ToString(value) ?? string.Empty;

            switch (column)
            {
                case "CableNo": cable.CableNo = text; break;
                case "From": cable.From = text; break;
                case "To": cable.To = text; break;
                case "CableType": cable.CableType = text; break;
                case "DrumNo": cable.DrumNo = text; break;
                case "Length": cable.Length = text; break;
                case "IrCert": cable.IrCert = text; break;
                case "ContCert": cable.ContCert = text; break;
                case "Pulled": cable.Pulled = value is bool && (bool)value; break;
                case "Glanded": cable.Glanded = value is bool && (bool)value; break;
                case "Terminated": cable.Terminated = value is bool && (bool)value; break;
                case "Complete": cable.Complete = value is bool && (bool)value; break;
            }
        }

        private static void AddTextColumn(DataGridView grid, string name, string header,
                                          int width, bool readOnly)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                MinimumWidth = width,
                FillWeight = width,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            if (readOnly)
            {
                column.DefaultCellStyle.Font = Theme.MonoTiny;
                column.DefaultCellStyle.ForeColor = Theme.Slate500;
            }

            grid.Columns.Add(column);
        }

        private static void AddCheckColumn(DataGridView grid, string name, string header)
        {
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = name,
                HeaderText = header,
                MinimumWidth = Dpi.S(64),
                FillWeight = 64,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        #endregion

        #region Notes, sign-off, actions

        private void AddNotesCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Notes", InnerWidth);
            card.Controls.Add(bar);

            TextInput notes = new TextInput
            {
                Location = new Point(18, bar.Bottom + 16),
                Width = InnerWidth - 36,
                Height = Dpi.S(84),
                Multiline = true,
                Value = _register.Notes ?? string.Empty
            };
            notes.ValueChanged += (s, e) => _register.Notes = notes.Value;
            card.Controls.Add(notes);

            card.Height = notes.Bottom + 18;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        private void AddSignoffCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Sign-Off", InnerWidth);
            card.Controls.Add(bar);

            const int gap = 16;
            int blockWidth = (InnerWidth - 36 - gap) / 2;
            int top = bar.Bottom + 16;

            int h1 = AddSignoffBlock(card, "Register Maintained By", _register.MaintainedBy,
                "Mzukulu Technologies — QC", new Point(18, top), blockWidth);
            int h2 = AddSignoffBlock(card, "Verified By", _register.VerifiedBy,
                "Managing Contractor", new Point(18 + blockWidth + gap, top), blockWidth);

            card.Height = top + Math.Max(h1, h2) + 18;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        private int AddSignoffBlock(Card parent, string title, SignoffEntry entry,
                                    string roleHint, Point location, int width)
        {
            bool signed = entry.IsSigned;

            Card block = new Card
            {
                Location = location,
                Width = width,
                Fill = signed ? Theme.Green050 : Color.White,
                BorderTint = signed ? Theme.Green100 : Theme.Slate150,
                Radius = Theme.Radius
            };

            Label label = Ui.FieldLabel(title);
            label.Location = new Point(Dpi.S(16), Dpi.S(15));
            block.Controls.Add(label);

            Label status = new Label
            {
                AutoSize = true,
                Font = Theme.Label,
                ForeColor = signed ? Theme.Green700 : Theme.Amber700,
                Location = new Point(16, label.Bottom + 6),
                Text = signed ? "SIGNED" : "PENDING"
            };
            block.Controls.Add(status);

            TextInput name = new TextInput
            {
                Location = new Point(16, status.Bottom + 8),
                Width = width - 32,
                Height = Dpi.S(32),
                Value = entry.Name ?? string.Empty,
                Placeholder = "Name (" + roleHint + ")"
            };
            name.ValueChanged += (s, e) => entry.Name = name.Value;
            block.Controls.Add(name);

            TextInput signature = new TextInput
            {
                Location = new Point(16, name.Bottom + 8),
                Width = width - 32,
                Height = Dpi.S(32),
                Value = entry.Signature ?? string.Empty,
                Placeholder = "Signature (type initials)"
            };
            signature.ValueChanged += (s, e) => entry.Signature = signature.Value;
            block.Controls.Add(signature);

            DateTimePicker date = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
                Font = Theme.Body,
                Location = new Point(16, signature.Bottom + 8),
                Width = width - 32
            };
            if (entry.Date.HasValue)
            {
                date.Value = entry.Date.Value;
                date.Checked = true;
            }
            else
            {
                date.Checked = false;
            }
            date.ValueChanged += (s, e) => entry.Date = date.Checked ? date.Value.Date : (DateTime?)null;
            block.Controls.Add(date);

            block.Height = date.Bottom + 16;
            parent.Controls.Add(block);

            return block.Height;
        }

        private void AddAuditLine()
        {
            Label audit = Ui.Wrapped(
                "Created " + Format.DateTimeLong(_register.CreatedAt) + " by " + _register.CreatedBy
                + " · Last updated " + Format.DateTimeLong(_register.UpdatedAt),
                Theme.Tiny, Theme.Slate500, InnerWidth);
            audit.Location = new Point(Left1, Y);
            Controls.Add(audit);
            Y = audit.Bottom + 16;
        }

        private void AddBottomActions()
        {
            FlatButton save = new FlatButton
            {
                Text = "Save Draft",
                Width = Dpi.S(116),
                Height = Dpi.S(34),
                Location = new Point(Left1, Y)
            };
            save.Click += (s, e) => SaveDraft();
            Controls.Add(save);

            FlatButton complete = new FlatButton
            {
                Text = "Mark Register Complete",
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(196),
                Height = Dpi.S(34),
                Location = new Point(save.Right + 8, Y)
            };
            complete.Click += (s, e) => MarkComplete();
            Controls.Add(complete);

            FlatButton back = new FlatButton
            {
                Text = "Back to Project",
                Width = Dpi.S(134),
                Height = Dpi.S(34),
                Location = new Point(complete.Right + 8, Y)
            };
            back.Click += (s, e) => Shell.Navigate(new ProjectDetailView(Shell, _project.Id));
            Controls.Add(back);

            Y = save.Bottom + 6;
        }

        private void SaveDraft()
        {
            _register.Status = RecordStatus.InProgress;
            _register.UpdatedAt = DateTime.Now;

            int done = _register.Cables.Count(c => c.Complete);
            Audit.Log(AuditAction.Updated, "Register", _register.Id,
                CableRegister.DocNumber + " · " + _register.QcRecordNo,
                done + " of " + _register.Cables.Count + " cables complete");

            Repository.Save();
            Notify("Register saved.");
            Rebuild();
        }

        private void MarkComplete()
        {
            _register.Status = RecordStatus.CompleteAccepted;
            _register.UpdatedAt = DateTime.Now;

            Audit.Log(AuditAction.Submitted, "Register", _register.Id,
                CableRegister.DocNumber + " · " + _register.QcRecordNo,
                "Register marked complete");

            Repository.Save();
            Notify("Register marked complete.");
            Rebuild();
        }

        /// <summary>Exports the cable grid to CSV.</summary>
        private void ExportRegister()
        {
            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "CSV file (*.csv)|*.csv";
                save.FileName = Exporter.SafeFileName(
                    CableRegister.DocNumber + "-" + _register.QcRecordNo) + ".csv";

                if (save.ShowDialog(Shell) != DialogResult.OK) return;

                try
                {
                    Exporter.ExportRegister(_register, save.FileName);
                    Repository.Save();
                    Notify("Exported " + _register.QcRecordNo + ".");
                }
                catch (Exception ex)
                {
                    Notify("Export failed: " + ex.Message, true);
                }
            }
        }

        #endregion
    }
}
