using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;

namespace MzuApplication.Forms
{
    /// <summary>
    /// Shown after a period of inactivity to confirm the user is still present. If they
    /// do not respond within a short grace window, the dialog closes with Cancel and the
    /// shell signs the session out. Styled to match the rest of the branded chrome.
    /// </summary>
    public class IdlePromptForm : Form
    {
        /// <summary>Seconds the user has to respond before being signed out.</summary>
        private const int GraceSeconds = 30;

        private Card _card;
        private Label _countdown;
        private Timer _timer;
        private int _remaining = GraceSeconds;

        public IdlePromptForm()
        {
            Text = "Mzukulu QMS — Are you still there?";
            ClientSize = new Size(Dpi.S(560), Dpi.S(420));
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            BackColor = Theme.Navy950;
            Font = Theme.Body;
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            AppIcon.Apply(this);

            BuildCard();
            Resize += (s, e) => CenterCard();
            CenterCard();

            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();

            FormClosed += (s, e) => _timer.Stop();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Rectangle r = ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;

            using (SolidBrush baseBrush = new SolidBrush(Theme.Navy950))
            {
                e.Graphics.FillRectangle(baseBrush, r);
            }

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
            int cardWidth = Dpi.S(420);

            _card = new Card
            {
                Width = cardWidth,
                Fill = Color.White,
                BorderWidth = 0,
                Radius = Dpi.S(16)
            };

            Card header = new Card
            {
                Fill = Theme.Navy950,
                BorderWidth = 0,
                Radius = Dpi.S(16),
                TopOnly = true,
                Width = cardWidth,
                Height = Dpi.S(110),
                Location = new Point(0, 0)
            };

            MzukuluLogo mark = new MzukuluLogo
            {
                Location = new Point(Dpi.S(28), Dpi.S(24)),
                Size = new Size(Dpi.S(32), Dpi.S(32))
            };
            header.Controls.Add(mark);

            Label brand = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(26), Dpi.S(62)),
                Text = "Are you still there?"
            };
            header.Controls.Add(brand);

            _card.Controls.Add(header);

            int left = Dpi.S(28);
            int fieldWidth = cardWidth - Dpi.S(56);
            int y = header.Bottom + Dpi.S(18);

            string who = Auth.Current != null && !string.IsNullOrWhiteSpace(Auth.Current.FullName)
                ? Auth.Current.FullName
                : "you";

            Label intro = Ui.Wrapped(
                "Your session has been inactive for a while. For security, " + who
                + " will be signed out automatically unless you confirm you're still here.",
                Theme.Small, Theme.Slate700, fieldWidth);
            intro.Location = new Point(left, y);
            _card.Controls.Add(intro);
            y = intro.Bottom + Dpi.S(14);

            _countdown = new Label
            {
                AutoSize = false,
                Font = Theme.SmallBold,
                ForeColor = Theme.Brick700,
                BackColor = Color.Transparent,
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(20),
                Text = CountdownText()
            };
            _card.Controls.Add(_countdown);
            y = _countdown.Bottom + Dpi.S(14);

            FlatButton stay = new FlatButton
            {
                Text = "I'm still here",
                Variant = ButtonVariant.Primary,
                Width = fieldWidth,
                Height = Dpi.S(40),
                Location = new Point(left, y)
            };
            stay.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            _card.Controls.Add(stay);
            y = stay.Bottom + Dpi.S(10);

            FlatButton signOut = new FlatButton
            {
                Text = "Sign out",
                Variant = ButtonVariant.Ghost,
                Width = fieldWidth,
                Height = Dpi.S(36),
                Location = new Point(left, y)
            };
            signOut.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _card.Controls.Add(signOut);

            _card.Height = signOut.Bottom + Dpi.S(24);
            Controls.Add(_card);

            ActiveControl = stay;
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Left = (ClientSize.Width - _card.Width) / 2;
            _card.Top = Math.Max(Dpi.S(16), (ClientSize.Height - _card.Height) / 2);
        }

        private void Tick()
        {
            _remaining--;
            if (_remaining <= 0)
            {
                _timer.Stop();
                DialogResult = DialogResult.Cancel; // no response -> sign out
                Close();
                return;
            }
            _countdown.Text = CountdownText();
        }

        private string CountdownText()
        {
            return "Signing out in " + _remaining + " second" + (_remaining == 1 ? "" : "s") + "…";
        }
    }
}
