using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Models;

namespace MzuApplication.Forms
{
    /// <summary>
    /// Demo sign-in. The user picks one of two roles instead of entering credentials,
    /// which lets the rest of the app demonstrate role-based access.
    /// </summary>
    public class LoginForm : Form
    {
        private Card _card;

        public LoginForm()
        {
            Text = "Mzukulu QMS — Sign in";
            ClientSize = new Size(Dpi.S(940), Dpi.S(620));
            MinimumSize = new Size(Dpi.S(560), Dpi.S(560));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Theme.Navy950;
            Font = Theme.Body;

            BuildCard();
            Resize += (s, e) => CenterCard();
            CenterCard();
        }

        /// <summary>Role chosen by the user; valid once the dialog returns OK.</summary>
        public UserRole SelectedRole { get; private set; }

        /// <summary>Display name of the chosen demo account.</summary>
        public string DisplayName { get; private set; }

        /// <summary>Radial navy gradient backdrop, matching the design.</summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Rectangle r = ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;

            using (SolidBrush baseBrush = new SolidBrush(Theme.Navy950))
            {
                e.Graphics.FillRectangle(baseBrush, r);
            }

            // Highlight offset toward the upper left, as per the CSS radial gradient.
            int size = (int)(Math.Max(r.Width, r.Height) * 1.5);
            Rectangle glow = new Rectangle(
                (int)(r.Width * 0.30) - size / 2,
                (int)(r.Height * 0.20) - size / 2,
                size, size);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(glow);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Theme.Navy800;
                    brush.SurroundColors = new[] { Theme.Navy950 };
                    e.Graphics.FillRectangle(brush, r);
                }
            }
        }

        private void BuildCard()
        {
            const int cardWidth = 410;

            _card = new Card
            {
                Width = cardWidth,
                Fill = Color.White,
                BorderWidth = 0,
                Radius = 16
            };

            // ---- Navy header -------------------------------------------------
            Card header = new Card
            {
                Fill = Theme.Navy950,
                BorderWidth = 0,
                Radius = 16,
                TopOnly = true,
                Width = cardWidth,
                Height = Dpi.S(132),
                Location = new Point(Dpi.S(0), Dpi.S(0))
            };

            MzukuluLogo mark = new MzukuluLogo
            {
                Location = new Point(Dpi.S(28), Dpi.S(28)),
                Size = new Size(Dpi.S(32), Dpi.S(32))
            };
            header.Controls.Add(mark);

            Label brand = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(26), Dpi.S(70)),
                Text = "Mzukulu QMS"
            };
            header.Controls.Add(brand);

            Label tagline = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Color.FromArgb(0x9F, 0xB0, 0xC6),
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(28), Dpi.S(99)),
                Text = "Quality Management System"
            };
            header.Controls.Add(tagline);

            _card.Controls.Add(header);

            // ---- Body --------------------------------------------------------
            int y = header.Bottom + 24;

            Label prompt = Ui.Eyebrow("Select a demo account");
            prompt.Location = new Point(28, y);
            _card.Controls.Add(prompt);
            y = prompt.Bottom + 12;

            RoleOption admin = new RoleOption
            {
                Title = "Admin Demo — S. Ndlovu",
                Detail = "Full access: clients, projects, checklist builder",
                BadgeText = "ADMIN",
                BadgeColor = Theme.Brick600,
                Role = UserRole.Admin,
                Location = new Point(28, y),
                Width = cardWidth - 56
            };
            admin.Click += RoleSelected;
            _card.Controls.Add(admin);
            y = admin.Bottom + 10;

            RoleOption user = new RoleOption
            {
                Title = "Site User Demo — T. Mahlangu",
                Detail = "Complete forms, view projects, search records",
                BadgeText = "USER",
                BadgeColor = Theme.Navy800,
                Role = UserRole.SiteUser,
                Location = new Point(28, y),
                Width = cardWidth - 56
            };
            user.Click += RoleSelected;
            _card.Controls.Add(user);
            y = user.Bottom + 16;

            Label note = Ui.Wrapped(
                "This demo signs you in as one of two roles to show role-based access. "
                + "A production deployment would use real authenticated accounts.",
                Theme.Tiny, Theme.Slate400, cardWidth - 56);
            note.Location = new Point(28, y);
            _card.Controls.Add(note);

            _card.Height = note.Bottom + 26;
            Controls.Add(_card);
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Left = (ClientSize.Width - _card.Width) / 2;
            _card.Top = Math.Max(16, (ClientSize.Height - _card.Height) / 2);
        }

        private void RoleSelected(object sender, EventArgs e)
        {
            RoleOption option = sender as RoleOption;
            if (option == null) return;

            SelectedRole = option.Role;
            DisplayName = option.Role == UserRole.Admin ? "S. Ndlovu" : "T. Mahlangu";

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>A clickable demo-account row with a title, detail line and role badge.</summary>
        private sealed class RoleOption : Control
        {
            private bool _hovered;

            public RoleOption()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer
                         | ControlStyles.AllPaintingInWmPaint
                         | ControlStyles.UserPaint
                         | ControlStyles.ResizeRedraw
                         | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Height = Dpi.S(62);
                Cursor = Cursors.Hand;
            }

            public string Title { get; set; }
            public string Detail { get; set; }
            public string BadgeText { get; set; }
            public Color BadgeColor { get; set; }
            public UserRole Role { get; set; }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hovered = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hovered = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                if (r.Width <= 0 || r.Height <= 0) return;

                using (GraphicsPath path = Card.Build(r, Theme.Radius, false, false))
                using (SolidBrush fill = new SolidBrush(_hovered ? Theme.Slate50 : Color.White))
                using (Pen border = new Pen(_hovered ? Theme.Navy600 : Theme.Slate150))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }

                // Role badge, right aligned.
                int badgeWidth = 0;
                if (!string.IsNullOrEmpty(BadgeText))
                {
                    using (Font bf = Theme.TinyBold)
                    {
                        SizeF size = g.MeasureString(BadgeText, bf);
                        badgeWidth = (int)Math.Ceiling(size.Width) + 20;
                        int badgeHeight = 20;
                        Rectangle badge = new Rectangle(Width - badgeWidth - 16,
                            (Height - badgeHeight) / 2, badgeWidth, badgeHeight);

                        using (GraphicsPath bp = Card.Build(badge, badgeHeight / 2, false, false))
                        using (SolidBrush bb = new SolidBrush(BadgeColor))
                        {
                            g.FillPath(bb, bp);
                        }

                        using (StringFormat fmt = new StringFormat())
                        {
                            fmt.Alignment = StringAlignment.Center;
                            fmt.LineAlignment = StringAlignment.Center;
                            g.DrawString(BadgeText, bf, Brushes.White, badge, fmt);
                        }
                    }
                }

                int textRight = Width - badgeWidth - 28;

                using (Font tf = Theme.BodyBold)
                using (SolidBrush tb = new SolidBrush(Theme.Slate900))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(Title, tf, tb, new RectangleF(16, 13, textRight - 16, 18), fmt);
                }

                using (Font df = Theme.Tiny)
                using (SolidBrush db = new SolidBrush(Theme.Slate600))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(Detail, df, db, new RectangleF(16, 34, textRight - 16, 16), fmt);
                }
            }
        }
    }
}
