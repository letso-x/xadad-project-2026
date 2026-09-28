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
    /// Fills in a checklist instance: header info, scope box, the A/R/NA item grid,
    /// NCR capture, remarks and the three sign-off blocks.
    /// </summary>
    public class FormFillView : ViewBase
    {
        private readonly string _formId;
        private QcForm _form;
        private ChecklistTemplate _template;
        private Project _project;

        private ProgressTrack _progress;
        private Label _progressLabel;
        private Pill _statusPill;
        private TextInput _ncrInput;

        public FormFillView(MainForm shell, string formId) : base(shell)
        {
            _formId = formId;
        }

        public override string RouteKey
        {
            get { return "library"; }
        }

        protected override void BuildContent()
        {
            _form = Repository.GetForm(_formId);
            if (_form == null)
            {
                Label missing = Ui.Body("Form not found.");
                missing.Location = new Point(Left1, Y);
                Controls.Add(missing);
                Y = missing.Bottom + 20;
                return;
            }

            _template = Repository.GetTemplate(_form.TemplateId);
            _project = Repository.GetProject(_form.ProjectId);

            if (_template == null || _project == null)
            {
                Label missing = Ui.Body("This form references a missing template or project.");
                missing.Location = new Point(Left1, Y);
                Controls.Add(missing);
                Y = missing.Bottom + 20;
                return;
            }

            AddCrumbs(_project.Name,
                () => Shell.Navigate(new ProjectDetailView(Shell, _project.Id)),
                _template.DocNumber);

            AddFormHeaderBar();
            AddProjectInfoCard();
            AddScopeBox();
            AddItemsCard();
            AddLegend();
            AddNcrCard();
            AddRemarksCard();
            AddSignoffCard();
            AddAuditLine();
            AddBottomActions();
        }

        #region Header bar

        private void AddFormHeaderBar()
        {
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
                Text = _template.DocNumber + " · " + _template.Revision + " · " + _form.QcRecordNo
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
                Text = _template.DocName
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

            _statusPill = new Pill { Height = 24 };
            _statusPill.ApplyStatus(_form.Status);
            _statusPill.Location = new Point(InnerWidth - _statusPill.Width - 18, 16);
            bar.Controls.Add(_statusPill);

            // Progress row.
            int progressTop = project.Bottom + 14;

            _progress = new ProgressTrack
            {
                Location = new Point(18, progressTop + 5),
                Width = InnerWidth - 210,
                Height = Dpi.S(7)
            };
            bar.Controls.Add(_progress);

            _progressLabel = new Label
            {
                AutoSize = true,
                Font = Theme.Tiny,
                ForeColor = Theme.Slate600,
                Location = new Point(_progress.Right + 12, progressTop)
            };
            bar.Controls.Add(_progressLabel);

            UpdateProgress();

            // Action row.
            int actionTop = progressTop + 26;

            FlatButton saveDraft = new FlatButton
            {
                Text = "Save Draft",
                Glyph = Glyph.CheckCircle,
                Width = Dpi.S(124),
                Height = Dpi.S(34),
                Location = new Point(18, actionTop)
            };
            saveDraft.Click += (s, e) => SaveDraft();
            bar.Controls.Add(saveDraft);

            FlatButton submit = new FlatButton
            {
                Text = "Submit / Finalise",
                Glyph = Glyph.Check,
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(164),
                Height = Dpi.S(34),
                Location = new Point(saveDraft.Right + 8, actionTop)
            };
            submit.Click += (s, e) => Submit();
            bar.Controls.Add(submit);

            FlatButton export = new FlatButton
            {
                Text = "Export",
                Glyph = Glyph.Download,
                Width = Dpi.S(104),
                Height = Dpi.S(34),
                Location = new Point(submit.Right + Dpi.S(8), actionTop)
            };
            export.Click += (s, e) => ExportForm();
            bar.Controls.Add(export);

            int nextLeft = export.Right + Dpi.S(8);

            // Only offer the NCR shortcut once a line has actually been rejected.
            if (_form.HasRejected)
            {
                FlatButton raise = new FlatButton
                {
                    Text = "Raise NCR",
                    Glyph = Glyph.Alert,
                    Variant = ButtonVariant.Danger,
                    Width = Dpi.S(124),
                    Height = Dpi.S(34),
                    Location = new Point(nextLeft, actionTop)
                };
                raise.Click += (s, e) => RaiseNcrFromForm();
                bar.Controls.Add(raise);
                nextLeft = raise.Right + Dpi.S(8);
            }

            FlatButton back = new FlatButton
            {
                Text = "Back to Project",
                Variant = ButtonVariant.Ghost,
                Width = Dpi.S(132),
                Height = Dpi.S(34),
                Location = new Point(nextLeft, actionTop)
            };
            back.Click += (s, e) => Shell.Navigate(new ProjectDetailView(Shell, _project.Id));
            bar.Controls.Add(back);

            bar.Height = actionTop + Dpi.S(34) + Dpi.S(16);
            Controls.Add(bar);
            Y = bar.Bottom + 18;
        }

        private void UpdateProgress()
        {
            int total = _template.AllItems().Count;
            int answered = _form.AnsweredCount;
            int percent = total == 0 ? 0 : (int)Math.Round(answered * 100.0 / total);

            _progress.Percent = percent;
            _progress.ApplyTone(_form.HasRejected);

            _progressLabel.Text = answered + " / " + total + " items · " + percent + "%";
        }

        #endregion

        #region Project info and scope

        private void AddProjectInfoCard()
        {
            Client client = Repository.GetClient(_project.ClientId);

            var cells = new List<Tuple<string, string, bool>>
            {
                Tuple.Create("Client", client != null ? client.Name : "—", false),
                Tuple.Create("Project", _project.Name, false),
                Tuple.Create("Project No.", _project.Number, false),
                Tuple.Create("Contract / Order No.",
                    string.IsNullOrEmpty(_project.ContractNo) ? "—" : _project.ContractNo, false),
                Tuple.Create("Area / Location",
                    string.IsNullOrEmpty(_project.Site) ? "—" : _project.Site, false),
                Tuple.Create("QC Record No.", _form.QcRecordNo, true)
            };

            const int perRow = 3;
            const int gap = 16;
            int colWidth = (InnerWidth - 36 - gap * (perRow - 1)) / perRow;

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

            int bodyTop = bar.Height + 16;
            int rowHeight = 52;

            for (int i = 0; i < cells.Count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;
                var cell = cells[i];

                int x = 18 + col * (colWidth + gap);
                int y = bodyTop + row * rowHeight;

                Label label = Ui.FieldLabel(cell.Item1);
                label.Location = new Point(x, y);
                card.Controls.Add(label);

                if (cell.Item3)
                {
                    // QC record number gets the mono chip treatment.
                    Label chip = new Label
                    {
                        AutoSize = true,
                        Font = Theme.MonoSmall,
                        ForeColor = Theme.Navy900,
                        BackColor = Theme.Slate100,
                        Padding = new Padding(Dpi.S(8), Dpi.S(4), Dpi.S(8), Dpi.S(4)),
                        Location = new Point(x, label.Bottom + 4),
                        Text = cell.Item2
                    };
                    card.Controls.Add(chip);
                }
                else
                {
                    Label value = new Label
                    {
                        AutoSize = false,
                        Font = Theme.Body,
                        ForeColor = Theme.Slate900,
                        Location = new Point(x, label.Bottom + 4),
                        Width = colWidth,
                        Height = Dpi.S(18),
                        Text = cell.Item2
                    };
                    card.Controls.Add(value);
                }
            }

            int rows = (int)Math.Ceiling(cells.Count / (double)perRow);
            int nextY = bodyTop + rows * rowHeight;

            // Editable template-specific header fields.
            if (_template.ProjectFields.Count > 0)
            {
                for (int i = 0; i < _template.ProjectFields.Count; i++)
                {
                    string field = _template.ProjectFields[i];
                    int col = i % perRow;
                    int row = i / perRow;

                    int x = 18 + col * (colWidth + gap);
                    int y = nextY + row * 62;

                    Label label = Ui.FieldLabel(field);
                    label.Location = new Point(x, y);
                    card.Controls.Add(label);

                    string current;
                    _form.FieldValues.TryGetValue(field, out current);

                    TextInput input = new TextInput
                    {
                        Location = new Point(x, label.Bottom + 5),
                        Width = colWidth,
                        Height = Dpi.S(32),
                        Value = current ?? string.Empty
                    };

                    string key = field;
                    input.ValueChanged += (s, e) => _form.FieldValues[key] = input.Value;
                    card.Controls.Add(input);
                }

                int fieldRows = (int)Math.Ceiling(_template.ProjectFields.Count / (double)perRow);
                nextY += fieldRows * 62;
            }

            card.Height = nextY + 10;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        private void AddScopeBox()
        {
            int textWidth = InnerWidth - 44;

            Label heading = Ui.Eyebrow("Scope & Acceptance Criteria");
            Label scope = Ui.Wrapped(_template.Scope ?? string.Empty, Theme.Small, Theme.Slate800, textWidth);
            Label acceptance = Ui.Wrapped(_template.Acceptance ?? string.Empty,
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

            heading.ForeColor = Theme.Brick700;
            heading.Location = new Point(Dpi.S(20), Dpi.S(15));
            box.Controls.Add(heading);

            scope.Location = new Point(20, heading.Bottom + 7);
            box.Controls.Add(scope);

            acceptance.Location = new Point(20, scope.Bottom + 6);
            box.Controls.Add(acceptance);

            box.Height = acceptance.Bottom + 16;
            Controls.Add(box);
            Y = box.Bottom + 16;
        }

        #endregion

        #region Checklist items

        private void AddItemsCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Inspection / Test Requirements", InnerWidth);
            card.Controls.Add(bar);

            int y = bar.Height;
            int itemNumber = 0;

            foreach (TemplateSection section in _template.Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                {
                    Panel sectionBar = Ui.SubSectionBar(section.Title, InnerWidth - 2);
                    sectionBar.Location = new Point(1, y);
                    card.Controls.Add(sectionBar);
                    y = sectionBar.Bottom + 12;
                }
                else
                {
                    y += 12;
                }

                foreach (TemplateItem item in section.Items)
                {
                    itemNumber++;
                    y = AddItemCard(card, item, itemNumber, y);
                }
            }

            card.Height = y + 8;
            Controls.Add(card);
            Y = card.Bottom + 12;
        }

        private int AddItemCard(Card parent, TemplateItem item, int number, int y)
        {
            FormItemResult result = _form.Items.FirstOrDefault(i => i.ItemId == item.Id);
            if (result == null)
            {
                // Template gained an item after this form was created; back-fill it.
                result = new FormItemResult
                {
                    ItemId = item.Id,
                    Response = ItemResponse.Unanswered,
                    Ref = string.Empty,
                    Initials = string.Empty
                };
                _form.Items.Add(result);
            }

            int cardWidth = InnerWidth - 36;
            int textWidth = cardWidth - 36;

            Color fill, border;
            ResolveItemTone(result.Response, out fill, out border);

            Card itemCard = new Card
            {
                Location = new Point(18, y),
                Width = cardWidth,
                Fill = fill,
                BorderTint = border,
                Radius = Theme.Radius
            };

            // Numbered requirement text.
            Label numberLabel = new Label
            {
                AutoSize = true,
                Font = Theme.MonoSmall,
                ForeColor = Theme.Slate400,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(17), Dpi.S(17)),
                Text = number + "."
            };
            itemCard.Controls.Add(numberLabel);

            Label text = Ui.Wrapped(item.Text, Theme.Small, Theme.Slate900,
                textWidth - numberLabel.Width - 6);
            text.Location = new Point(numberLabel.Right + 4, 16);
            itemCard.Controls.Add(text);

            int controlsTop = Math.Max(text.Bottom, numberLabel.Bottom) + 14;

            // Result / reference / initials / date columns.
            int columns = item.HasRef ? 4 : 3;
            int gap = 12;
            int colWidth = (textWidth - gap * (columns - 1)) / columns;
            int selectorWidth = Math.Max(168, colWidth);

            int x = 17;

            Label resultLabel = Ui.FieldLabel("Result");
            resultLabel.Location = new Point(x, controlsTop);
            itemCard.Controls.Add(resultLabel);

            ResponseSelector selector = new ResponseSelector
            {
                Location = new Point(x, resultLabel.Bottom + 5),
                Width = selectorWidth,
                Height = Dpi.S(34),
                Value = result.Response
            };
            itemCard.Controls.Add(selector);

            x += selectorWidth + gap;
            int remaining = textWidth - (x - 17);
            int fieldColumns = item.HasRef ? 3 : 2;
            int fieldWidth = (remaining - gap * (fieldColumns - 1)) / fieldColumns;

            TextInput refInput = null;
            if (item.HasRef)
            {
                Label refLabel = Ui.FieldLabel(item.RefLabel + (item.RefRequired ? " *" : string.Empty));
                refLabel.Location = new Point(x, controlsTop);
                itemCard.Controls.Add(refLabel);

                refInput = new TextInput
                {
                    Location = new Point(x, refLabel.Bottom + 5),
                    Width = fieldWidth,
                    Height = Dpi.S(34),
                    Value = result.Ref ?? string.Empty
                };
                refInput.ValueChanged += (s, e) => result.Ref = refInput.Value;
                itemCard.Controls.Add(refInput);

                x += fieldWidth + gap;
            }

            Label initialsLabel = Ui.FieldLabel("Initials");
            initialsLabel.Location = new Point(x, controlsTop);
            itemCard.Controls.Add(initialsLabel);

            TextInput initials = new TextInput
            {
                Location = new Point(x, initialsLabel.Bottom + 5),
                Width = fieldWidth,
                Height = Dpi.S(34),
                Value = result.Initials ?? string.Empty
            };
            initials.ValueChanged += (s, e) => result.Initials = initials.Value;
            itemCard.Controls.Add(initials);

            x += fieldWidth + gap;

            Label dateLabel = Ui.FieldLabel("Date");
            dateLabel.Location = new Point(x, controlsTop);
            itemCard.Controls.Add(dateLabel);

            DateTimePicker date = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
                Font = Theme.Body,
                Location = new Point(x, dateLabel.Bottom + 8),
                Width = fieldWidth
            };
            if (result.Date.HasValue)
            {
                date.Value = result.Date.Value;
                date.Checked = true;
            }
            else
            {
                date.Checked = false;
            }
            date.ValueChanged += (s, e) => result.Date = date.Checked ? date.Value.Date : (DateTime?)null;
            itemCard.Controls.Add(date);

            int bottom = Math.Max(selector.Bottom, date.Bottom);

            // NCR reminder shown only while the line is rejected.
            Label ncrHint = null;
            if (result.Response == ItemResponse.Rejected)
            {
                ncrHint = new Label
                {
                    AutoSize = false,
                    Font = Theme.TinyBold,
                    ForeColor = Theme.Red700,
                    BackColor = Color.Transparent,
                    Location = new Point(17, bottom + 8),
                    Width = selectorWidth + 40,
                    Height = Dpi.S(18),
                    Text = "Raise an NCR below"
                };
                itemCard.Controls.Add(ncrHint);
                bottom = ncrHint.Bottom;
            }

            itemCard.Height = bottom + 16;

            // Changing the response repaints tone and may add/remove the NCR panel,
            // so the whole view is rebuilt to keep layout honest.
            selector.ValueChanged += (s, e) =>
            {
                result.Response = selector.Value;

                if (result.Response != ItemResponse.Unanswered && !result.Date.HasValue)
                {
                    result.Date = DateTime.Today;
                }

                Rebuild();
            };

            parent.Controls.Add(itemCard);
            return itemCard.Bottom + 12;
        }

        private static void ResolveItemTone(ItemResponse response, out Color fill, out Color border)
        {
            switch (response)
            {
                case ItemResponse.Accepted:
                    fill = Theme.Green050; border = Theme.Green100; break;
                case ItemResponse.Rejected:
                    fill = Theme.Red050; border = Theme.Red600; break;
                case ItemResponse.NotApplicable:
                    fill = Theme.Slate50; border = Theme.Slate150; break;
                default:
                    fill = Color.White; border = Theme.Slate150; break;
            }
        }

        private void AddLegend()
        {
            Label legend = Ui.Wrapped(
                "A = Accepted / conforms   ·   R = Rejected — raise an NCR and record the number   "
                + "·   N/A = Not applicable. Every rejected line must be closed out before sign-off.",
                Theme.Tiny, Theme.Slate500, InnerWidth);
            legend.Location = new Point(Left1, Y);
            Controls.Add(legend);
            Y = legend.Bottom + 16;
        }

        #endregion

        #region NCR, remarks, sign-off

        private void AddNcrCard()
        {
            if (!_form.HasRejected) return;

            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Non-Conformance", InnerWidth, Theme.Red700);
            card.Controls.Add(bar);

            Label warning = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Theme.Red700,
                Location = new Point(38, bar.Bottom + 16),
                Text = "This form contains rejected items — an NCR number is required."
            };
            card.Controls.Add(warning);

            Card iconHost = new Card
            {
                Location = new Point(18, bar.Bottom + 15),
                Size = new Size(Dpi.S(16), Dpi.S(16)),
                BorderWidth = 0,
                Fill = Color.Transparent
            };
            iconHost.Paint += (s, e) =>
            {
                Glyphs.Draw(e.Graphics, Glyph.Alert, new Rectangle(0, 1, 15, 15), Theme.Red700, 1.7f);
            };
            card.Controls.Add(iconHost);

            Label label = Ui.FieldLabel("NCR Number");
            label.Location = new Point(18, warning.Bottom + 14);
            card.Controls.Add(label);

            _ncrInput = new TextInput
            {
                Location = new Point(18, label.Bottom + 5),
                Width = Math.Min(280, InnerWidth - 36),
                Height = Dpi.S(34),
                Value = _form.NcrNo ?? string.Empty,
                Placeholder = "e.g. NCR-0007"
            };
            _ncrInput.ValueChanged += (s, e) => _form.NcrNo = _ncrInput.Value;
            card.Controls.Add(_ncrInput);

            card.Height = _ncrInput.Bottom + 18;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        private void AddRemarksCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Remarks / Observations / Deviations", InnerWidth);
            card.Controls.Add(bar);

            TextInput remarks = new TextInput
            {
                Location = new Point(18, bar.Bottom + 16),
                Width = InnerWidth - 36,
                Height = Dpi.S(92),
                Multiline = true,
                Value = _form.Remarks ?? string.Empty
            };
            remarks.ValueChanged += (s, e) => _form.Remarks = remarks.Value;
            card.Controls.Add(remarks);

            card.Height = remarks.Bottom + 18;
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
            int blockWidth = (InnerWidth - 36 - gap * 2) / 3;
            int top = bar.Bottom + 16;

            int h1 = AddSignoffBlock(card, "Inspected / Tested By", _form.Inspected,
                new Point(18, top), blockWidth);
            int h2 = AddSignoffBlock(card, "Reviewed By", _form.Reviewed,
                new Point(18 + blockWidth + gap, top), blockWidth);
            int h3 = AddSignoffBlock(card, "Approved By", _form.Approved,
                new Point(18 + (blockWidth + gap) * 2, top), blockWidth);

            card.Height = top + Math.Max(h1, Math.Max(h2, h3)) + 18;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        /// <summary>Renders one sign-off block and returns its height.</summary>
        private int AddSignoffBlock(Card parent, string title, SignoffEntry entry,
                                    Point location, int width)
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
                Placeholder = "Name"
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
                "Created " + Format.DateTimeLong(_form.CreatedAt) + " by " + _form.CreatedBy
                + " · Last updated " + Format.DateTimeLong(_form.UpdatedAt),
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

            FlatButton submit = new FlatButton
            {
                Text = "Submit / Finalise",
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(150),
                Height = Dpi.S(34),
                Location = new Point(save.Right + 8, Y)
            };
            submit.Click += (s, e) => Submit();
            Controls.Add(submit);

            FlatButton back = new FlatButton
            {
                Text = "Back to Project",
                Width = Dpi.S(134),
                Height = Dpi.S(34),
                Location = new Point(submit.Right + 8, Y)
            };
            back.Click += (s, e) => Shell.Navigate(new ProjectDetailView(Shell, _project.Id));
            Controls.Add(back);

            Y = save.Bottom + 6;
        }

        #endregion

        #region Persist

        private void SaveDraft()
        {
            _form.UpdatedAt = DateTime.Now;

            int total = _template.AllItems().Count;
            Audit.Log(AuditAction.Updated, "Form", _form.Id,
                _template.DocNumber + " · " + _form.QcRecordNo,
                _form.AnsweredCount + " of " + total + " items answered");

            Repository.Save();
            Notify("Form saved.");
            Rebuild();
        }

        /// <summary>Exports this checklist, including every line result, to CSV.</summary>
        private void ExportForm()
        {
            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "CSV file (*.csv)|*.csv";
                save.FileName = Exporter.SafeFileName(
                    _template.DocNumber + "-" + _form.QcRecordNo) + ".csv";

                if (save.ShowDialog(Shell) != DialogResult.OK) return;

                try
                {
                    Exporter.ExportForm(_form, save.FileName);
                    Repository.Save();
                    Notify("Exported " + _form.QcRecordNo + ".");
                }
                catch (Exception ex)
                {
                    Notify("Export failed: " + ex.Message, true);
                }
            }
        }

        /// <summary>
        /// Raises a tracked NCR from this form and writes the number back onto it, so
        /// the free-text field and the NCR register stay in step.
        /// </summary>
        private void RaiseNcrFromForm()
        {
            using (Dialog dialog = new Dialog("Raise NCR from " + _form.QcRecordNo, Dpi.S(560)))
            {
                var rejected = _form.Items
                    .Where(i => i.Response == ItemResponse.Rejected)
                    .Select(i => _template.FindItem(i.ItemId))
                    .Where(i => i != null)
                    .ToList();

                string suggested = rejected.Count > 0
                    ? rejected[0].Text
                    : _template.DocName + " non-conformance";

                if (suggested.Length > 90) suggested = suggested.Substring(0, 90) + "...";

                dialog.AddReadOnlyField("Source", _template.DocNumber + " · " + _form.QcRecordNo);
                dialog.AddReadOnlyField("Rejected lines", rejected.Count.ToString());

                TextInput title = dialog.AddTextField("Title", suggested);
                TextInput detail = dialog.AddTextArea("Detail",
                    rejected.Count > 0
                        ? string.Join(Environment.NewLine,
                            rejected.Select((r, idx) => (idx + 1) + ". " + r.Text))
                        : string.Empty);
                ComboBox severity = dialog.AddDropdown("Severity",
                    new[] { "Minor", "Major", "Critical" }, "Major");
                TextInput owner = dialog.AddTextField("Assign to", string.Empty);

                dialog.AddPrimaryAction("Raise", () =>
                {
                    if (string.IsNullOrWhiteSpace(title.Value))
                    {
                        Notify("A title is required.", true);
                        return;
                    }

                    NcrSeverity sev = (NcrSeverity)Enum.Parse(
                        typeof(NcrSeverity), (string)severity.SelectedItem);

                    Ncr ncr = NcrService.Raise(_project.Id, _form.Id, title.Value.Trim(),
                        detail.Value.Trim(), sev, owner.Value.Trim());

                    _form.NcrNo = ncr.NcrNo;
                    _form.UpdatedAt = DateTime.Now;

                    Repository.Save();
                    dialog.DialogResult = DialogResult.OK;
                });

                if (dialog.ShowDialog(Shell) == DialogResult.OK)
                {
                    Notify("NCR raised and linked to " + _form.QcRecordNo + ".");
                    Rebuild();
                }
            }
        }

        /// <summary>
        /// Validates the form against the acceptance rules, then finalises it as either
        /// accepted or NCR-raised.
        /// </summary>
        private void Submit()
        {
            var items = _template.AllItems();

            if (_form.AnsweredCount < items.Count)
            {
                Notify("All checklist items must be marked A, R or N/A before submitting.", true);
                return;
            }

            // A required reading must be present on any accepted line.
            foreach (FormItemResult result in _form.Items)
            {
                if (result.Response != ItemResponse.Accepted) continue;

                TemplateItem item = _template.FindItem(result.ItemId);
                if (item != null && item.RefRequired && string.IsNullOrWhiteSpace(result.Ref))
                {
                    Notify("A required reference / reading is missing on an accepted item.", true);
                    return;
                }
            }

            // NCR enforcement is a configurable policy, set in Organisation Settings.
            if (Repository.Db.Settings.EnforceNcrOnReject
                && _form.HasRejected
                && string.IsNullOrWhiteSpace(_form.NcrNo))
            {
                Notify("Record the NCR number before submitting a form with rejected items.", true);
                return;
            }

            if (!_form.Inspected.IsSigned)
            {
                Notify("\"Inspected / Tested By\" sign-off is required before submitting.", true);
                return;
            }

            // Optional second signature, also policy-driven.
            if (Repository.Db.Settings.RequireDualSignoff && !_form.Reviewed.IsSigned)
            {
                Notify("Organisation policy requires a \"Reviewed By\" signature before submitting.", true);
                return;
            }

            _form.Status = _form.HasRejected
                ? RecordStatus.CompleteNcrRaised
                : RecordStatus.CompleteAccepted;
            _form.UpdatedAt = DateTime.Now;

            Audit.Log(AuditAction.Submitted, "Form", _form.Id,
                _template.DocNumber + " · " + _form.QcRecordNo,
                "Submitted — " + Format.StatusText(_form.Status)
                    + (string.IsNullOrWhiteSpace(_form.NcrNo)
                        ? string.Empty : " · " + _form.NcrNo));

            Repository.Save();
            Notify("Form submitted — " + Format.StatusText(_form.Status) + ".");
            Rebuild();
        }

        #endregion
    }
}
