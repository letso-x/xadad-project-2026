using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;

namespace MzuApplication.Forms
{
    /// <summary>
    /// Forced password change, shown immediately after a user signs in with a password
    /// an administrator reset. The user cannot proceed into the app until they set a new
    /// password. The dialog cannot be dismissed without completing the change.
    /// </summary>
    public class ChangePasswordForm : Form
    {
        private Card _card;
        private TextInput _password;
        private TextInput _confirm;
        private Label _error;

        public ChangePasswordForm()
        {
            Text = "Mzukulu QMS — Set a new password";
            ClientSize = new Size(Dpi.S(760), Dpi.S(560));
            MinimumSize = new Size(Dpi.S(520), Dpi.S(520));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Theme.Navy950;
            Font = Theme.Body;
            AppIcon.Apply(this);

            // No close box: the change is mandatory.
            ControlBox = false;

            BuildCard();
            Resize += (s, e) => CenterCard();
            CenterCard();
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
                Text = "Set a new password"
            };
            header.Controls.Add(brand);

            _card.Controls.Add(header);

            int left = Dpi.S(28);
            int fieldWidth = cardWidth - Dpi.S(56);
            int y = header.Bottom + Dpi.S(18);

            string who = Auth.Current != null ? Auth.Current.FullName : "your account";
            Label intro = Ui.Wrapped(
                "Your password was reset by an administrator. For security, please choose a "
                + "new password for " + who + " before continuing.",
                Theme.Small, Theme.Slate700, fieldWidth);
            intro.Location = new Point(left, y);
            _card.Controls.Add(intro);
            y = intro.Bottom + Dpi.S(16);

            Label newLabel = Ui.FieldLabel("New Password");
            newLabel.Location = new Point(left, y);
            _card.Controls.Add(newLabel);
            y = newLabel.Bottom + Dpi.S(5);

            _password = new TextInput
            {
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(36),
                UsePasswordChar = true
            };
            _card.Controls.Add(_password);
            y = _password.Bottom + Dpi.S(6);

            Label rule = Ui.Wrapped(Auth.PasswordRule, Theme.Tiny, Theme.Slate400, fieldWidth);
            rule.Location = new Point(left, y);
            _card.Controls.Add(rule);
            y = rule.Bottom + Dpi.S(10);

            Label confirmLabel = Ui.FieldLabel("Confirm Password");
            confirmLabel.Location = new Point(left, y);
            _card.Controls.Add(confirmLabel);
            y = confirmLabel.Bottom + Dpi.S(5);

            _confirm = new TextInput
            {
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(36),
                UsePasswordChar = true
            };
            _confirm.Submitted += (s, e) => Apply();
            _card.Controls.Add(_confirm);
            y = _confirm.Bottom + Dpi.S(10);

            _error = new Label
            {
                AutoSize = false,
                Font = Theme.Small,
                ForeColor = Theme.Red600,
                BackColor = Color.Transparent,
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(30),
                Text = string.Empty
            };
            _card.Controls.Add(_error);
            y = _error.Bottom + Dpi.S(2);

            FlatButton save = new FlatButton
            {
                Text = "Save New Password",
                Variant = ButtonVariant.Primary,
                Width = fieldWidth,
                Height = Dpi.S(40),
                Location = new Point(left, y)
            };
            save.Click += (s, e) => Apply();
            _card.Controls.Add(save);

            _card.Height = save.Bottom + Dpi.S(24);
            Controls.Add(_card);

            ActiveControl = _password;
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Left = (ClientSize.Width - _card.Width) / 2;
            _card.Top = Math.Max(Dpi.S(16), (ClientSize.Height - _card.Height) / 2);
        }

        private void Apply()
        {
            _error.Text = string.Empty;

            if (_password.Value != _confirm.Value)
            {
                _error.Text = "The two passwords do not match.";
                return;
            }

            AuthResult result = Auth.ChangeOwnPassword(_password.Value);
            if (result == AuthResult.Success)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _error.Text = Auth.Describe(result);
        }
    }
}
