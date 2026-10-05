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
    /// <summary>Administrator list of checklist templates.</summary>
    public class TemplatesView : ViewBase
    {
        public TemplatesView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "templates"; }
        }

        protected override void BuildContent()
        {
            // Guard the route the same way the spec's requireAdmin wrapper does.
            if (!Repository.IsAdmin)
            {
                AddAccessDenied(this);
                return;
            }

            FlatButton create = new FlatButton
            {
                Text = "Create Checklist Template",
                Glyph = Glyph.Plus,
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(216)
            };
            create.Click += (s, e) => Shell.Navigate(new TemplateEditorView(Shell, null));

            AddPageHeader("Administration", "Checklist Templates",
                "Configurable MZT-QC-3xx style checklists. Editing here never changes forms already completed.",
                create);

            DataTable table = new DataTable
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                EmptyMessage = "No templates yet."
            };
            table.DefineColumns("Doc No.", 1.2f, "Name", 3.0f, "Revision", 1.0f,
                                "Status", 1.1f, "Usage", 1.1f);
            table.RowActivated += (s, tag) =>
            {
                ChecklistTemplate template = tag as ChecklistTemplate;
                if (template != null) Shell.Navigate(new TemplateEditorView(Shell, template.Id));
            };

            var rows = new List<TableRow>();
            foreach (ChecklistTemplate template in Repository.Db.Templates)
            {
                int usage = Repository.Db.Forms.Count(f => f.TemplateId == template.Id);

                TableRow row = new TableRow { Tag = template };
                row.Cells.Add(new TableCell(template.DocNumber) { Mono = true });
                row.Cells.Add(new TableCell(template.DocName));
                row.Cells.Add(new TableCell(template.Revision) { Muted = true });
                row.Cells.Add(new TableCell(template.Active ? "Active" : "Archived")
                {
                    PillBack = template.Active ? Theme.Green100 : Theme.Slate150,
                    PillFore = template.Active ? Theme.Green700 : Theme.Slate600
                });
                row.Cells.Add(new TableCell(usage + (usage == 1 ? " instance" : " instances")) { Muted = true });

                row.FilterText = (template.DocNumber + " " + template.DocName).ToLowerInvariant();
                rows.Add(row);
            }

            table.SetRows(rows);
            Controls.Add(table);
            Y = table.Bottom + 8;
        }

        /// <summary>Shared empty state for administrator-only routes.</summary>
        internal static void AddAccessDenied(ViewBase view)
        {
            Panel host = new Panel
            {
                Location = new Point(Theme.PagePadding, 60),
                Width = Math.Max(360, view.Width - Theme.PagePadding * 2),
                Height = Dpi.S(120),
                BackColor = Color.Transparent
            };

            Label heading = new Label
            {
                AutoSize = true,
                Font = Theme.SectionTitle,
                ForeColor = Theme.Slate800,
                Text = "Administrator access required"
            };
            host.Controls.Add(heading);
            heading.Location = new Point((host.Width - heading.Width) / 2, 20);

            Label detail = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                Text = "You need administrator permissions to view this page."
            };
            host.Controls.Add(detail);
            detail.Location = new Point((host.Width - detail.Width) / 2, heading.Bottom + 8);

            view.Controls.Add(host);
        }
    }

}
