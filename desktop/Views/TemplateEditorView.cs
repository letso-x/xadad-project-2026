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
    /// The checklist builder. Edits a working copy so cancelling leaves the stored
    /// template untouched, and saving never alters forms already completed.
    /// </summary>
    public class TemplateEditorView : ViewBase
    {
        private readonly string _templateId;
        private ChecklistTemplate _draft;

        public TemplateEditorView(MainForm shell, string templateId) : base(shell)
        {
            _templateId = templateId;
        }

        public override string RouteKey
        {
            get { return "templates"; }
        }

        protected override void BuildContent()
        {
            if (!Repository.IsAdmin)
            {
                TemplatesView.AddAccessDenied(this);
                return;
            }

            // Build the working copy once, then keep it across rebuilds so in-progress
            // edits survive adding or removing rows.
            if (_draft == null)
            {
                _draft = _templateId == null
                    ? NewDraft()
                    : CloneTemplate(Repository.GetTemplate(_templateId));
            }

            if (_draft == null)
            {
                Label missing = Ui.Body("Template not found.");
                missing.Location = new Point(Left1, Y);
                Controls.Add(missing);
                Y = missing.Bottom + 20;
                return;
            }

            AddCrumbs("Checklist Templates",
                () => Shell.Navigate(new TemplatesView(Shell)),
                _templateId == null ? "New Template" : _draft.DocNumber);

            AddPageHeader(null,
                _templateId == null ? "Create Checklist Template" : "Edit Checklist Template",
                null);

            AddDocumentInfoCard();
            AddProjectFieldsCard();
            AddScopeCard();
            AddSectionsHeading();
            AddSections();
            AddSaveRow();
        }

        private static ChecklistTemplate NewDraft()
        {
            ChecklistTemplate draft = new ChecklistTemplate
            {
                Id = null,
                DocNumber = string.Empty,
                DocName = string.Empty,
                Description = string.Empty,
                Revision = "Rev 1",
                IssueDate = DateTime.Today,
                Active = true,
                Scope = string.Empty,
                Acceptance = string.Empty
            };
            draft.Sections.Add(new TemplateSection { Id = Repository.NewId("sec"), Title = string.Empty });
            return draft;
        }

        /// <summary>Deep copy so edits do not touch the stored template until saved.</summary>
        private static ChecklistTemplate CloneTemplate(ChecklistTemplate source)
        {
            if (source == null) return null;

            ChecklistTemplate copy = new ChecklistTemplate
            {
                Id = source.Id,
                DocNumber = source.DocNumber,
                DocName = source.DocName,
                Description = source.Description,
                Revision = source.Revision,
                IssueDate = source.IssueDate,
                Active = source.Active,
                Scope = source.Scope,
                Acceptance = source.Acceptance,
                ProjectFields = new List<string>(source.ProjectFields)
            };

            foreach (TemplateSection section in source.Sections)
            {
                TemplateSection sectionCopy = new TemplateSection
                {
                    Id = section.Id,
                    Title = section.Title
                };

                foreach (TemplateItem item in section.Items)
                {
                    sectionCopy.Items.Add(new TemplateItem
                    {
                        Id = item.Id,
                        Text = item.Text,
                        RefLabel = item.RefLabel,
                        RefRequired = item.RefRequired
                    });
                }

                copy.Sections.Add(sectionCopy);
            }

            return copy;
        }

        #region Document information

        private void AddDocumentInfoCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Document Information", InnerWidth);
            card.Controls.Add(bar);

            const int gap = 16;
            int colWidth = (InnerWidth - 36 - gap) / 2;
            int top = bar.Bottom + 16;

            TextInput docNumber = AddField(card, "Document Number", _draft.DocNumber,
                new Point(18, top), colWidth, "MZT-QC-307");
            docNumber.ValueChanged += (s, e) => _draft.DocNumber = docNumber.Value;

            TextInput docName = AddField(card, "Document Name", _draft.DocName,
                new Point(18 + colWidth + gap, top), colWidth, "e.g. Earthing & Bonding Installation");
            docName.ValueChanged += (s, e) => _draft.DocName = docName.Value;

            top = docNumber.Bottom + 14;

            TextInput description = AddField(card, "Description / Subtitle", _draft.Description,
                new Point(18, top), InnerWidth - 36);
            description.ValueChanged += (s, e) => _draft.Description = description.Value;

            top = description.Bottom + 14;

            TextInput revision = AddField(card, "Revision", _draft.Revision,
                new Point(18, top), colWidth);
            revision.ValueChanged += (s, e) => _draft.Revision = revision.Value;

            Label issueLabel = Ui.FieldLabel("Issue Date");
            issueLabel.Location = new Point(18 + colWidth + gap, top);
            card.Controls.Add(issueLabel);

            DateTimePicker issueDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                Font = Theme.Body,
                Location = new Point(18 + colWidth + gap, issueLabel.Bottom + 8),
                Width = colWidth,
                Value = _draft.IssueDate
            };
            issueDate.ValueChanged += (s, e) => _draft.IssueDate = issueDate.Value.Date;
            card.Controls.Add(issueDate);

            top = Math.Max(revision.Bottom, issueDate.Bottom) + 14;

            CheckBox active = Ui.Check("Active (visible in Form Library)");
            active.Checked = _draft.Active;
            active.Location = new Point(18, top);
            active.CheckedChanged += (s, e) => _draft.Active = active.Checked;
            card.Controls.Add(active);

            card.Height = active.Bottom + 18;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        private static TextInput AddField(Card parent, string label, string value,
                                          Point location, int width, string placeholder = null)
        {
            Label caption = Ui.FieldLabel(label);
            caption.Location = location;
            parent.Controls.Add(caption);

            TextInput input = new TextInput
            {
                Location = new Point(location.X, caption.Bottom + 5),
                Width = width,
                Height = Dpi.S(34),
                Value = value ?? string.Empty
            };
            if (!string.IsNullOrEmpty(placeholder)) input.Placeholder = placeholder;

            parent.Controls.Add(input);
            return input;
        }

        #endregion

        #region Project fields

        private void AddProjectFieldsCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Project Information Fields", InnerWidth);
            card.Controls.Add(bar);

            Label help = Ui.Wrapped(
                "Extra site-specific fields shown on the form header, in addition to the standard "
                + "Project / Client / QC Record No. Example: \"Enclosure / Cabinet N°\".",
                Theme.Tiny, Theme.Slate500, InnerWidth - 36);
            help.Location = new Point(18, bar.Bottom + 14);
            card.Controls.Add(help);

            int top = help.Bottom + 12;

            for (int i = 0; i < _draft.ProjectFields.Count; i++)
            {
                int index = i;

                TextInput input = new TextInput
                {
                    Location = new Point(18, top),
                    Width = InnerWidth - 36 - 40,
                    Height = Dpi.S(34),
                    Value = _draft.ProjectFields[i],
                    Placeholder = "e.g. Enclosure / Cabinet N°"
                };
                input.ValueChanged += (s, e) => _draft.ProjectFields[index] = input.Value;
                card.Controls.Add(input);

                IconButton remove = new IconButton
                {
                    Glyph = Glyph.Cross,
                    Location = new Point(input.Right + 8, top + 4),
                    Size = new Size(Dpi.S(28), Dpi.S(28)),
                    BorderTint = Theme.Slate200
                };
                remove.Click += (s, e) =>
                {
                    _draft.ProjectFields.RemoveAt(index);
                    Rebuild();
                };
                card.Controls.Add(remove);

                top = input.Bottom + 8;
            }

            FlatButton add = new FlatButton
            {
                Text = "Add Field",
                Glyph = Glyph.Plus,
                Width = Dpi.S(120),
                Height = Dpi.S(32),
                Location = new Point(18, top + 4)
            };
            add.Click += (s, e) =>
            {
                _draft.ProjectFields.Add(string.Empty);
                Rebuild();
            };
            card.Controls.Add(add);

            card.Height = add.Bottom + 16;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        #endregion

        #region Scope

        private void AddScopeCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Scope & Acceptance Criteria", InnerWidth);
            card.Controls.Add(bar);

            const int gap = 16;
            int colWidth = (InnerWidth - 36 - gap) / 2;
            int top = bar.Bottom + 16;

            Label scopeLabel = Ui.FieldLabel("Scope");
            scopeLabel.Location = new Point(18, top);
            card.Controls.Add(scopeLabel);

            TextInput scope = new TextInput
            {
                Location = new Point(18, scopeLabel.Bottom + 5),
                Width = colWidth,
                Height = Dpi.S(84),
                Multiline = true,
                Value = _draft.Scope ?? string.Empty
            };
            scope.ValueChanged += (s, e) => _draft.Scope = scope.Value;
            card.Controls.Add(scope);

            Label acceptLabel = Ui.FieldLabel("Acceptance Criteria");
            acceptLabel.Location = new Point(18 + colWidth + gap, top);
            card.Controls.Add(acceptLabel);

            TextInput acceptance = new TextInput
            {
                Location = new Point(18 + colWidth + gap, acceptLabel.Bottom + 5),
                Width = colWidth,
                Height = Dpi.S(84),
                Multiline = true,
                Value = _draft.Acceptance ?? string.Empty
            };
            acceptance.ValueChanged += (s, e) => _draft.Acceptance = acceptance.Value;
            card.Controls.Add(acceptance);

            card.Height = scope.Bottom + 18;
            Controls.Add(card);
            Y = card.Bottom + 20;
        }

        #endregion

        #region Sections and items

        private void AddSectionsHeading()
        {
            Label heading = new Label
            {
                AutoSize = true,
                Font = Theme.SectionTitle,
                ForeColor = Theme.Navy950,
                Location = new Point(Left1, Y),
                Text = "Checklist Sections"
            };
            Controls.Add(heading);

            FlatButton add = new FlatButton
            {
                Text = "Add Section",
                Glyph = Glyph.Plus,
                Width = Dpi.S(128),
                Height = Dpi.S(32),
                Location = new Point(Left1 + InnerWidth - 128, Y - 4)
            };
            add.Click += (s, e) =>
            {
                _draft.Sections.Add(new TemplateSection
                {
                    Id = Repository.NewId("sec"),
                    Title = string.Empty
                });
                Rebuild();
            };
            Controls.Add(add);

            Y = Math.Max(heading.Bottom, add.Bottom) + 12;
        }

        private void AddSections()
        {
            for (int s = 0; s < _draft.Sections.Count; s++)
            {
                AddSectionCard(_draft.Sections[s], s);
            }
        }

        private void AddSectionCard(TemplateSection section, int sectionIndex)
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            // Navy header carrying the editable section title.
            Card bar = new Card
            {
                Fill = Theme.Navy800,
                BorderWidth = 0,
                Radius = Theme.Radius,
                TopOnly = true,
                Width = InnerWidth,
                Height = Dpi.S(52),
                Location = new Point(Dpi.S(0), Dpi.S(0))
            };

            TextInput title = new TextInput
            {
                Location = new Point(Dpi.S(16), Dpi.S(10)),
                Width = Math.Max(200, (int)(InnerWidth * 0.55)),
                Height = Dpi.S(32),
                Value = section.Title ?? string.Empty,
                Placeholder = "Section title (optional)"
            };
            title.ValueChanged += (s, e) => section.Title = title.Value;
            bar.Controls.Add(title);

            FlatButton removeSection = new FlatButton
            {
                Text = "Remove Section",
                Variant = ButtonVariant.Danger,
                Width = Dpi.S(148),
                Height = Dpi.S(30),
                Location = new Point(InnerWidth - 164, 11)
            };
            removeSection.Click += (s, e) =>
            {
                if (MessageBox.Show(Shell,
                        "Remove this section and its items?", "Confirm",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                _draft.Sections.RemoveAt(sectionIndex);
                Rebuild();
            };
            bar.Controls.Add(removeSection);

            card.Controls.Add(bar);

            int top = bar.Height + 14;

            for (int i = 0; i < section.Items.Count; i++)
            {
                top = AddItemEditor(card, section, i, top);
            }

            FlatButton addItem = new FlatButton
            {
                Text = "Add Checklist Item",
                Glyph = Glyph.Plus,
                Width = Dpi.S(172),
                Height = Dpi.S(32),
                Location = new Point(18, top + 4)
            };
            addItem.Click += (s, e) =>
            {
                section.Items.Add(new TemplateItem
                {
                    Id = Repository.NewId("itm"),
                    Text = string.Empty,
                    RefLabel = string.Empty,
                    RefRequired = false
                });
                Rebuild();
            };
            card.Controls.Add(addItem);

            card.Height = addItem.Bottom + 16;
            Controls.Add(card);
            Y = card.Bottom + 16;
        }

        /// <summary>Renders one editable checklist item row and returns the next Y.</summary>
        private int AddItemEditor(Card parent, TemplateSection section, int itemIndex, int top)
        {
            TemplateItem item = section.Items[itemIndex];

            int available = InnerWidth - 36;
            int numberWidth = 34;
            int removeWidth = 86;
            int gap = 12;

            int fieldsWidth = available - numberWidth - removeWidth - gap * 2;
            int textWidth = (int)(fieldsWidth * 0.58);
            int refWidth = fieldsWidth - textWidth - gap;

            Label number = new Label
            {
                AutoSize = true,
                Font = Theme.MonoSmall,
                ForeColor = Theme.Slate400,
                Location = new Point(18, top + 22),
                Text = (itemIndex + 1) + "."
            };
            parent.Controls.Add(number);

            int x = 18 + numberWidth;

            Label textLabel = Ui.FieldLabel("Requirement / question");
            textLabel.Location = new Point(x, top);
            parent.Controls.Add(textLabel);

            TextInput text = new TextInput
            {
                Location = new Point(x, textLabel.Bottom + 5),
                Width = textWidth,
                Height = Dpi.S(54),
                Multiline = true,
                Value = item.Text ?? string.Empty
            };
            text.ValueChanged += (s, e) => item.Text = text.Value;
            parent.Controls.Add(text);

            x += textWidth + gap;

            Label refLabelCaption = Ui.FieldLabel("Reference / reading field");
            refLabelCaption.Location = new Point(x, top);
            parent.Controls.Add(refLabelCaption);

            TextInput refLabel = new TextInput
            {
                Location = new Point(x, refLabelCaption.Bottom + 5),
                Width = refWidth,
                Height = Dpi.S(34),
                Value = item.RefLabel ?? string.Empty,
                Placeholder = "Leave blank if none"
            };
            refLabel.ValueChanged += (s, e) => item.RefLabel = refLabel.Value;
            parent.Controls.Add(refLabel);

            CheckBox required = Ui.Check("Required");
            required.Checked = item.RefRequired;
            required.Location = new Point(x, refLabel.Bottom + 6);
            required.CheckedChanged += (s, e) => item.RefRequired = required.Checked;
            parent.Controls.Add(required);

            x += refWidth + gap;

            FlatButton remove = new FlatButton
            {
                Text = "Remove",
                Variant = ButtonVariant.Danger,
                Width = removeWidth,
                Height = Dpi.S(30),
                Location = new Point(x, textLabel.Bottom + 7)
            };
            remove.Click += (s, e) =>
            {
                section.Items.RemoveAt(itemIndex);
                Rebuild();
            };
            parent.Controls.Add(remove);

            int bottom = Math.Max(text.Bottom, required.Bottom);

            Panel rule = Ui.Divider(available);
            rule.Location = new Point(18, bottom + 10);
            rule.BackColor = Theme.Slate100;
            parent.Controls.Add(rule);

            return rule.Bottom + 14;
        }

        #endregion

        #region Save

        private void AddSaveRow()
        {
            FlatButton save = new FlatButton
            {
                Text = "Save Template",
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(148),
                Height = Dpi.S(34),
                Location = new Point(Left1, Y)
            };
            save.Click += (s, e) => SaveTemplate();
            Controls.Add(save);

            FlatButton cancel = new FlatButton
            {
                Text = "Cancel",
                Width = Dpi.S(96),
                Height = Dpi.S(34),
                Location = new Point(save.Right + 8, Y)
            };
            cancel.Click += (s, e) => Shell.Navigate(new TemplatesView(Shell));
            Controls.Add(cancel);

            if (_draft.Id != null)
            {
                FlatButton archive = new FlatButton
                {
                    Text = _draft.Active ? "Archive Template" : "Reactivate Template",
                    Variant = ButtonVariant.Danger,
                    Width = Dpi.S(178),
                    Height = Dpi.S(34),
                    Location = new Point(cancel.Right + 8, Y)
                };
                archive.Click += (s, e) => ToggleArchive();
                Controls.Add(archive);
            }

            Y = save.Bottom + 6;
        }

        private void SaveTemplate()
        {
            if (string.IsNullOrWhiteSpace(_draft.DocNumber) || string.IsNullOrWhiteSpace(_draft.DocName))
            {
                Notify("Document number and name are required.", true);
                return;
            }

            if (_draft.Sections.All(s => s.Items.Count == 0))
            {
                Notify("Add at least one checklist item.", true);
                return;
            }

            if (_draft.Id == null)
            {
                _draft.Id = Repository.NewId("tpl");
                Repository.Db.Templates.Add(_draft);
            }
            else
            {
                int index = Repository.Db.Templates.FindIndex(t => t.Id == _draft.Id);
                if (index >= 0) Repository.Db.Templates[index] = _draft;
                else Repository.Db.Templates.Add(_draft);
            }

            Repository.Save();
            Notify("Checklist template saved.");
            Shell.Navigate(new TemplatesView(Shell));
        }

        private void ToggleArchive()
        {
            ChecklistTemplate stored = Repository.GetTemplate(_draft.Id);
            if (stored == null) return;

            stored.Active = !stored.Active;
            _draft.Active = stored.Active;

            Repository.Save();
            Notify(stored.Active ? "Template reactivated." : "Template archived.");
            Rebuild();
        }

        #endregion
    }
}
