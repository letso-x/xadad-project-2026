using System;
using System.Drawing;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;

namespace MzuApplication.Views
{
    /// <summary>
    /// Organisation settings: branding, document numbering and the QC policy switches
    /// that the submission rules read.
    /// </summary>
    public class SettingsView : ViewBase
    {
        public SettingsView(MainForm shell) : base(shell) { }

        public override string RouteKey
        {
            get { return "settings"; }
        }

        protected override void BuildContent()
        {
            if (!Repository.IsAdmin)
            {
                TemplatesView.AddAccessDenied(this);
                return;
            }

            AddPageHeader("Administration", "Organisation Settings",
                "Branding, numbering and the quality policy applied across every project.");

            OrgSettings settings = Repository.Db.Settings;

            AddOrganisationCard(settings);
            AddPolicyCard(settings);
            AddDataCard();
        }

        private void AddOrganisationCard(OrgSettings settings)
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Organisation", InnerWidth);
            card.Controls.Add(bar);

            int gap = Dpi.S(16);
            int colWidth = (InnerWidth - Dpi.S(36) - gap) / 2;
            int top = bar.Bottom + Dpi.S(16);

            Label nameLabel = Ui.FieldLabel("Company name");
            nameLabel.Location = new Point(Dpi.S(18), top);
            card.Controls.Add(nameLabel);

            TextInput companyName = new TextInput
            {
                Location = new Point(Dpi.S(18), nameLabel.Bottom + Dpi.S(5)),
                Width = colWidth,
                Height = Dpi.S(34),
                Value = settings.CompanyName
            };
            card.Controls.Add(companyName);

            Label prefixLabel = Ui.FieldLabel("QC record prefix");
            prefixLabel.Location = new Point(Dpi.S(18) + colWidth + gap, top);
            card.Controls.Add(prefixLabel);

            TextInput prefix = new TextInput
            {
                Location = new Point(Dpi.S(18) + colWidth + gap, prefixLabel.Bottom + Dpi.S(5)),
                Width = colWidth,
                Height = Dpi.S(34),
                Value = settings.DocumentPrefix
            };
            card.Controls.Add(prefix);

            Label help = Ui.Wrapped(
                "The prefix applies to newly created records. Existing record numbers are "
                + "left as they are, so historical references stay valid.",
                Theme.Tiny, Theme.Slate500, InnerWidth - Dpi.S(36));
            help.Location = new Point(Dpi.S(18), companyName.Bottom + Dpi.S(10));
            card.Controls.Add(help);

            FlatButton save = new FlatButton
            {
                Text = "Save Organisation",
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(168),
                Height = Dpi.S(34),
                Location = new Point(Dpi.S(18), help.Bottom + Dpi.S(12))
            };
            save.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(companyName.Value))
                {
                    Notify("Company name is required.", true);
                    return;
                }

                settings.CompanyName = companyName.Value.Trim();
                settings.DocumentPrefix = string.IsNullOrWhiteSpace(prefix.Value)
                    ? "QC" : prefix.Value.Trim();

                Repository.Save();
                Audit.Log(AuditAction.Updated, "Settings", null, "Organisation",
                    "Company details updated");
                Repository.Save();

                Notify("Organisation settings saved.");
            };
            card.Controls.Add(save);

            card.Height = save.Bottom + Dpi.S(16);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);
        }

        private void AddPolicyCard(OrgSettings settings)
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Quality Policy", InnerWidth);
            card.Controls.Add(bar);

            int y = bar.Bottom + Dpi.S(16);

            CheckBox enforceNcr = AddPolicySwitch(card, ref y,
                "Require an NCR number on rejected lines",
                "A form with any line marked R cannot be submitted until an NCR number is recorded.",
                settings.EnforceNcrOnReject);

            CheckBox dualSignoff = AddPolicySwitch(card, ref y,
                "Require a second signature before approval",
                "In addition to Inspected / Tested By, the Reviewed By block must be signed "
                + "before a form can be finalised.",
                settings.RequireDualSignoff);

            Label dueLabel = Ui.FieldLabel("NCR close-out window (days)");
            dueLabel.Location = new Point(Dpi.S(20), y);
            card.Controls.Add(dueLabel);

            TextInput dueDays = new TextInput
            {
                Location = new Point(Dpi.S(20), dueLabel.Bottom + Dpi.S(5)),
                Width = Dpi.S(120),
                Height = Dpi.S(34),
                Value = settings.NcrDueDays.ToString()
            };
            card.Controls.Add(dueDays);

            y = dueDays.Bottom + Dpi.S(14);

            FlatButton save = new FlatButton
            {
                Text = "Save Policy",
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(136),
                Height = Dpi.S(34),
                Location = new Point(Dpi.S(20), y)
            };
            save.Click += (s, e) =>
            {
                int days;
                if (!int.TryParse(dueDays.Value, out days) || days < 1 || days > 365)
                {
                    Notify("Close-out window must be between 1 and 365 days.", true);
                    return;
                }

                settings.EnforceNcrOnReject = enforceNcr.Checked;
                settings.RequireDualSignoff = dualSignoff.Checked;
                settings.NcrDueDays = days;

                Audit.Log(AuditAction.Updated, "Settings", null, "Quality Policy",
                    "Dual sign-off " + (dualSignoff.Checked ? "on" : "off")
                    + " · NCR enforcement " + (enforceNcr.Checked ? "on" : "off")
                    + " · window " + days + " days");
                Repository.Save();

                Notify("Quality policy saved.");
            };
            card.Controls.Add(save);

            card.Height = save.Bottom + Dpi.S(16);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);
        }

        /// <summary>A labelled toggle with an explanatory line beneath it.</summary>
        private CheckBox AddPolicySwitch(Card parent, ref int y, string title,
                                         string detail, bool state)
        {
            CheckBox box = Ui.Check(title);
            box.Font = Theme.BodyBold;
            box.Checked = state;
            box.Location = new Point(Dpi.S(20), y);
            parent.Controls.Add(box);

            Label help = Ui.Wrapped(detail, Theme.Tiny, Theme.Slate500,
                parent.Width - Dpi.S(56));
            help.Location = new Point(Dpi.S(38), box.Bottom + Dpi.S(3));
            parent.Controls.Add(help);

            y = help.Bottom + Dpi.S(14);
            return box;
        }

        private void AddDataCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Data", InnerWidth, Theme.Navy800);
            card.Controls.Add(bar);

            Label where = Ui.Wrapped(
                "Demo data is stored locally on this machine. A production deployment would "
                + "use a shared database so every user sees the same records.",
                Theme.Small, Theme.Slate700, InnerWidth - Dpi.S(40));
            where.Location = new Point(Dpi.S(20), bar.Bottom + Dpi.S(16));
            card.Controls.Add(where);

            FlatButton reseed = new FlatButton
            {
                Text = "Reset Demo Data",
                Variant = ButtonVariant.Danger,
                Width = Dpi.S(168),
                Height = Dpi.S(34),
                Location = new Point(Dpi.S(20), where.Bottom + Dpi.S(14))
            };
            reseed.Click += (s, e) => ConfirmReseed();
            card.Controls.Add(reseed);

            Label warn = Ui.Wrapped(
                "This discards every form, register, NCR and audit entry and restores the "
                + "original demo dataset. It cannot be undone.",
                Theme.Tiny, Theme.Slate500, InnerWidth - Dpi.S(40));
            warn.Location = new Point(Dpi.S(20), reseed.Bottom + Dpi.S(8));
            card.Controls.Add(warn);

            card.Height = warn.Bottom + Dpi.S(16);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(8);
        }

        /// <summary>
        /// Reseeding destroys the audit trail, so it needs an explicit confirmation
        /// rather than a single click.
        /// </summary>
        private void ConfirmReseed()
        {
            DialogResult answer = MessageBox.Show(Shell,
                "This will permanently delete all forms, registers, NCRs and audit "
                + "entries, then restore the original demo data.\r\n\r\nContinue?",
                "Reset demo data",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes) return;

            Repository.ResetToSeed();
            Notify("Demo data restored.");
            Shell.Navigate(new DashboardView(Shell));
        }
    }
}
