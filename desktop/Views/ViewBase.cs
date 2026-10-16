using System;
using System.Drawing;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Forms;

namespace MzuApplication.Views
{
    /// <summary>
    /// Base for every page. Owns the padded content column, exposes a running Y cursor
    /// so views can stack blocks, and rebuilds itself on resize so the layout is fluid.
    /// </summary>
    public abstract class ViewBase : Panel
    {
        private int _lastWidth = -1;
        private bool _building;

        protected ViewBase(MainForm shell)
        {
            Shell = shell;
            BackColor = Theme.Slate25;
            AutoSize = false;
            Padding = new Padding(Dpi.S(0));
        }

        protected MainForm Shell { get; private set; }

        /// <summary>Sidebar route this view belongs to, used to highlight the nav link.</summary>
        public abstract string RouteKey { get; }

        /// <summary>Vertical cursor used while stacking content.</summary>
        protected int Y { get; set; }

        /// <summary>Width available inside the page padding.</summary>
        protected int InnerWidth
        {
            get { return Math.Max(360, Width - Theme.PagePadding * 2); }
        }

        protected int Left1
        {
            get { return Theme.PagePadding; }
        }

        /// <summary>Lays out the page. Called once when the view is shown.</summary>
        public void Build()
        {
            _building = true;
            SuspendLayout();

            Controls.Clear();
            Y = 26;

            BuildContent();

            AddFooter();

            Height = Y + 8;
            ResumeLayout(true);
            _building = false;
            _lastWidth = Width;
        }

        /// <summary>Re-runs <see cref="Build"/>, preserving nothing. Safe to call after edits.</summary>
        public void Rebuild()
        {
            Build();
        }

        protected abstract void BuildContent();

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Rebuild on meaningful width changes so cards and grids reflow.
            if (_building || Width <= 0) return;
            if (Math.Abs(Width - _lastWidth) < 12) return;

            _lastWidth = Width;
            Build();
        }

        #region Shared building blocks

        /// <summary>Adds the eyebrow / title / subtitle header, with optional right-side buttons.</summary>
        protected void AddPageHeader(string eyebrow, string title, string subtitle,
                                     params Control[] rightControls)
        {
            if (!string.IsNullOrEmpty(eyebrow))
            {
                Label eb = Ui.Eyebrow(eyebrow);
                eb.Location = new Point(Left1, Y);
                Controls.Add(eb);
                Y = eb.Bottom + 5;
            }

            Label heading = Ui.PageTitle(title);
            heading.Location = new Point(Left1 - 1, Y);
            Controls.Add(heading);

            int headerBottom = heading.Bottom;

            if (!string.IsNullOrEmpty(subtitle))
            {
                Label sub = Ui.SubTitle(subtitle);
                sub.Location = new Point(Left1, heading.Bottom + 4);
                Controls.Add(sub);
                headerBottom = sub.Bottom;
            }

            // Right-aligned action buttons, laid out from the right edge inward.
            if (rightControls != null && rightControls.Length > 0)
            {
                int x = Left1 + InnerWidth;
                for (int i = rightControls.Length - 1; i >= 0; i--)
                {
                    Control c = rightControls[i];
                    x -= c.Width;
                    c.Location = new Point(x, Y + 2);
                    Controls.Add(c);
                    x -= 8;
                    headerBottom = Math.Max(headerBottom, c.Bottom);
                }
            }

            Y = headerBottom + 20;
        }

        /// <summary>Adds a breadcrumb line with a clickable parent link.</summary>
        protected void AddCrumbs(string parentText, Action onParentClick, string currentText)
        {
            Panel row = new Panel
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Height = Dpi.S(20),
                BackColor = Color.Transparent
            };

            Label parent = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Navy700,
                Text = parentText,
                Location = new Point(Dpi.S(0), Dpi.S(0)),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            parent.Click += (s, e) => onParentClick();
            row.Controls.Add(parent);

            Label rest = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate500,
                Text = "  /  " + currentText,
                Location = new Point(parent.Right - 2, 0),
                BackColor = Color.Transparent
            };
            row.Controls.Add(rest);

            Controls.Add(row);
            Y = row.Bottom + 12;
        }

        /// <summary>Adds a card with a navy title bar and returns the body panel to fill.</summary>
        protected Card AddPanelCard(string title, int bodyHeight, Color? barColor = null)
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar(title, InnerWidth, barColor);
            bar.Location = new Point(Dpi.S(0), Dpi.S(0));
            card.Controls.Add(bar);

            card.Height = bar.Height + bodyHeight;
            Controls.Add(card);
            Y = card.Bottom + 16;

            return card;
        }

        /// <summary>Centred footer credit line, added at the bottom of every page.</summary>
        private void AddFooter()
        {
            Y += 14;

            Panel rule = Ui.Divider(InnerWidth);
            rule.Location = new Point(Left1, Y);
            Controls.Add(rule);
            Y = rule.Bottom + 18;

            Label credit = new Label
            {
                AutoSize = true,
                Font = Theme.Tiny,
                ForeColor = Theme.Slate400,
                Text = "Mzukulu Technologies (Pty) Ltd — Quality Management System · Demo data environment"
            };
            Controls.Add(credit);
            credit.Location = new Point(Left1 + Math.Max(0, (InnerWidth - credit.Width) / 2), Y);

            Y = credit.Bottom + 16;
        }

        /// <summary>Shows a toast on the shell.</summary>
        protected void Notify(string message, bool isError = false)
        {
            Shell.Notify(message, isError);
        }

        #endregion
    }
}
