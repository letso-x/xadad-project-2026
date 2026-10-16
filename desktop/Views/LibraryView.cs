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
    /// Form Library: pick a project, then start a checklist or the cable register.
    /// </summary>
    public class LibraryView : ViewBase
    {
        private readonly string _preselectProjectId;
        private ComboBox _projectPicker;

        public LibraryView(MainForm shell, string preselectProjectId = null) : base(shell)
        {
            _preselectProjectId = preselectProjectId;
        }

        public override string RouteKey
        {
            get { return "library"; }
        }

        protected override void BuildContent()
        {
            AddPageHeader("Form Library", "Form Library",
                "Select a project, choose a checklist or register, and start a new QC record.");

            AddProjectPicker();
            AddChecklists();
            AddRegisters();
            AddAdminHint();
        }

        private void AddProjectPicker()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Height = Dpi.S(108),
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Label label = Ui.FieldLabel("Project for the new form");
            label.Location = new Point(Dpi.S(20), Dpi.S(18));
            card.Controls.Add(label);

            _projectPicker = Ui.Dropdown(Math.Min(460, InnerWidth - 44));
            _projectPicker.Location = new Point(Dpi.S(20), label.Bottom + Dpi.S(8));

            var projects = Repository.Db.Projects;
            foreach (Project project in projects)
            {
                _projectPicker.Items.Add(project.Name);
            }

            if (projects.Count == 0)
            {
                _projectPicker.Items.Add("No projects — create one first");
                _projectPicker.Enabled = false;
                _projectPicker.SelectedIndex = 0;
            }
            else
            {
                Project preselect = _preselectProjectId != null
                    ? projects.FirstOrDefault(p => p.Id == _preselectProjectId)
                    : null;
                _projectPicker.SelectedIndex = preselect != null
                    ? projects.IndexOf(preselect)
                    : 0;
            }

            card.Controls.Add(_projectPicker);

            Label help = Ui.Help("This project will be used for whichever form you create below.");
            help.Location = new Point(Dpi.S(20), _projectPicker.Bottom + Dpi.S(8));
            help.Width = InnerWidth - 44;
            help.Height = Dpi.S(16);
            card.Controls.Add(help);

            Controls.Add(card);
            Y = card.Bottom + Dpi.S(24);
        }

        /// <summary>Resolves the project currently chosen in the picker.</summary>
        private Project SelectedProject()
        {
            var projects = Repository.Db.Projects;
            if (projects.Count == 0 || _projectPicker.SelectedIndex < 0) return null;
            if (_projectPicker.SelectedIndex >= projects.Count) return null;
            return projects[_projectPicker.SelectedIndex];
        }

        private void AddChecklists()
        {
            AddGroupHeading("Checklists", "— configurable, MZT-QC-3xx");

            var templates = Repository.Db.Templates.Where(t => t.Active).ToList();

            if (templates.Count == 0)
            {
                Label empty = Ui.Muted("No active checklist templates.");
                empty.Location = new Point(Left1, Y);
                Controls.Add(empty);
                Y = empty.Bottom + Dpi.S(24);
                return;
            }

            const int perRow = 3;
            int gap = Dpi.S(14);
            int minHeight = Dpi.S(150);
            int width = (InnerWidth - gap * (perRow - 1)) / perRow;

            // Build every card first so each row can adopt the height of its tallest
            // card. That keeps the grid aligned even when descriptions wrap to
            // different line counts.
            var cards = new List<Card>();
            foreach (ChecklistTemplate template in templates)
            {
                ChecklistTemplate captured = template;
                cards.Add(BuildTemplateCard(
                    template.DocNumber,
                    template.DocName,
                    (template.Description ?? string.Empty) + " · " + template.Revision,
                    "Create Form",
                    width, minHeight,
                    () => StartForm(captured)));
            }

            int rowTop = Y;
            for (int i = 0; i < cards.Count; i += perRow)
            {
                int rowHeight = 0;
                for (int c = i; c < i + perRow && c < cards.Count; c++)
                {
                    rowHeight = Math.Max(rowHeight, cards[c].Height);
                }

                for (int c = i; c < i + perRow && c < cards.Count; c++)
                {
                    int col = c - i;
                    cards[c].Height = rowHeight;
                    cards[c].Location = new Point(Left1 + col * (width + gap), rowTop);
                    Controls.Add(cards[c]);
                }

                rowTop += rowHeight + gap;
            }

            Y = rowTop + Dpi.S(12);
        }

        private void AddRegisters()
        {
            AddGroupHeading("Registers", "— specialised forms");

            int gap = Dpi.S(14);
            int width = (InnerWidth - gap * 2) / 3;

            Card card = BuildTemplateCard(
                CableRegister.DocNumber,
                CableRegister.DocName,
                "Quality control register — master tracking of every cable from drum through to "
                + "termination and test.",
                "Create Register",
                width, Dpi.S(150),
                StartRegister);

            card.Location = new Point(Left1, Y);
            Controls.Add(card);

            Y = card.Bottom + Dpi.S(20);
        }

        private void AddGroupHeading(string title, string note)
        {
            Label heading = new Label
            {
                AutoSize = true,
                Font = Theme.CardHeading,
                ForeColor = Theme.Navy950,
                Location = new Point(Left1, Y),
                Text = title
            };
            Controls.Add(heading);

            Label suffix = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate500,
                Location = new Point(heading.Right + 6, Y + 2),
                Text = note
            };
            Controls.Add(suffix);

            Y = heading.Bottom + Dpi.S(12);
        }

        private Card BuildTemplateCard(string docTag, string title, string description,
                                       string buttonText, int width, int height, Action onCreate)
        {
            Card card = new Card
            {
                Width = width,
                Height = height,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            // Mono document tag chip.
            Label tag = new Label
            {
                AutoSize = true,
                Font = Theme.MonoTiny,
                ForeColor = Theme.Navy700,
                BackColor = Theme.Slate50,
                Location = new Point(Dpi.S(19), Dpi.S(18)),
                Padding = new Padding(Dpi.S(7), Dpi.S(4), Dpi.S(7), Dpi.S(4)),
                Text = docTag
            };
            card.Controls.Add(tag);

            int textWidth = width - Dpi.S(38);

            Label heading = Ui.Wrapped(title, Theme.BodyBold, Theme.Navy950, textWidth);
            heading.Location = new Point(Dpi.S(19), tag.Bottom + Dpi.S(10));
            card.Controls.Add(heading);

            // The description wraps to as many lines as it needs; the card grows to
            // fit it rather than clipping. This is why the passed-in height is only a
            // minimum, not a hard cap.
            Label desc = Ui.Wrapped(description, Theme.Tiny, Theme.Slate600, textWidth);
            desc.Location = new Point(Dpi.S(19), heading.Bottom + Dpi.S(6));
            card.Controls.Add(desc);

            FlatButton create = new FlatButton
            {
                Text = buttonText,
                Glyph = Glyph.Plus,
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(120),
                Height = Dpi.S(32),
                Location = new Point(Dpi.S(19), desc.Bottom + Dpi.S(14))
            };
            // Auto-grow (on by default) expands this to fit "Create Register" etc.
            create.Click += (s, e) => onCreate();
            card.Controls.Add(create);

            // Size the card to its content, never smaller than the requested minimum.
            card.Height = Math.Max(height, create.Bottom + Dpi.S(16));

            return card;
        }

        private void StartForm(ChecklistTemplate template)
        {
            Project project = SelectedProject();
            if (project == null)
            {
                Notify("Select a project first.", true);
                return;
            }

            QcForm form = CreateForm(template, project);
            Repository.Db.Forms.Add(form);

            Audit.Log(AuditAction.Created, "Form", form.Id,
                template.DocNumber + " · " + form.QcRecordNo,
                "Checklist started on " + project.Name);

            Repository.Save();

            Shell.Navigate(new FormFillView(Shell, form.Id));
        }

        /// <summary>Builds an empty form instance with one result row per template item.</summary>
        private static QcForm CreateForm(ChecklistTemplate template, Project project)
        {
            QcForm form = new QcForm
            {
                Id = Repository.NewId("frm"),
                TemplateId = template.Id,
                ProjectId = project.Id,
                QcRecordNo = Repository.NextQcRecordNo(),
                Status = RecordStatus.InProgress,
                Remarks = string.Empty,
                NcrNo = string.Empty,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                CreatedBy = Repository.CurrentUserName
            };

            foreach (string field in template.ProjectFields)
            {
                form.FieldValues[field] = string.Empty;
            }

            foreach (TemplateItem item in template.AllItems())
            {
                form.Items.Add(new FormItemResult
                {
                    ItemId = item.Id,
                    Response = ItemResponse.Unanswered,
                    Ref = string.Empty,
                    Initials = string.Empty
                });
            }

            return form;
        }

        private void StartRegister()
        {
            Project project = SelectedProject();
            if (project == null)
            {
                Notify("Select a project first.", true);
                return;
            }

            Client client = Repository.GetClient(project.ClientId);

            CableRegister register = new CableRegister
            {
                Id = Repository.NewId("reg"),
                ProjectId = project.Id,
                QcRecordNo = Repository.NextQcRecordNo(),
                Status = RecordStatus.InProgress,
                Notes = string.Empty,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                CreatedBy = Repository.CurrentUserName
            };

            register.FieldValues["Project"] = project.Name;
            register.FieldValues["Project N°"] = project.Number;
            register.FieldValues["Client"] = client != null ? client.Name : string.Empty;
            register.FieldValues["Contract / Order N°"] = project.ContractNo ?? string.Empty;
            register.FieldValues["Area / Location"] = project.Site ?? string.Empty;
            register.FieldValues["Drawing N° & Rev."] = string.Empty;
            register.FieldValues["Cable Schedule Ref. & Rev."] = string.Empty;
            register.FieldValues["Register Revision"] = "Rev 1";

            register.Cables.Add(new CableRow
            {
                Id = Repository.NewId("cab"),
                No = 1,
                CableNo = string.Empty, From = string.Empty, To = string.Empty,
                CableType = string.Empty, DrumNo = string.Empty, Length = string.Empty,
                IrCert = string.Empty, ContCert = string.Empty
            });

            Repository.Db.Registers.Add(register);

            Audit.Log(AuditAction.Created, "Register", register.Id,
                CableRegister.DocNumber + " · " + register.QcRecordNo,
                "Register started on " + project.Name);

            Repository.Save();

            Shell.Navigate(new RegisterView(Shell, register.Id));
        }

        private void AddAdminHint()
        {
            if (!Repository.IsAdmin) return;

            Label hint = new Label
            {
                AutoSize = true,
                Font = Theme.Tiny,
                ForeColor = Theme.Slate500,
                Location = new Point(Left1, Y),
                Text = "Need a new checklist type?"
            };
            Controls.Add(hint);

            Label link = new Label
            {
                AutoSize = true,
                Font = Theme.TinyBold,
                ForeColor = Theme.Navy700,
                Location = new Point(hint.Right + 4, Y),
                Text = "Create a checklist template",
                Cursor = Cursors.Hand
            };
            link.Click += (s, e) => Shell.Navigate(new TemplateEditorView(Shell, null));
            Controls.Add(link);

            Y = hint.Bottom + Dpi.S(10);
        }
    }
}
