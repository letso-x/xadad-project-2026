using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Models;

namespace MzuApplication.Forms
{
    /// <summary>
    /// Credential sign-in. Users authenticate with a username and password, or follow
    /// the link to enroll a new account. On success the signed-in account is available
    /// via <see cref="Auth.Current"/>.
    /// </summary>
    public class LoginForm : Form
    {
        private Card _card;
        private TextInput _username;
        private TextInput _password;
        private Label _error;

        public LoginForm()
        {
            Text = "Mzukulu QMS — Sign in";
            ClientSize = new Size(Dpi.S(940), Dpi.S(640));
            MinimumSize = new Size(Dpi.S(560), Dpi.S(600));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Theme.Navy950;
            Font = Theme.Body;
            AppIcon.Apply(this);

            FormClosed += (s,e) => { if(DialogResult != DialogResult.OK) Auth.SignOut(); };
            BuildCard();
            Resize += (s, e) => CenterCard();
            CenterCard();
        }

        /// <summary>True when the dialog closed because the user chose to register.</summary>
        public bool RegisterRequested { get; private set; }

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
            int cardWidth = Dpi.S(410);

            _card = new Card
            {
                Width = cardWidth,
                Fill = Color.White,
                BorderWidth = 0,
                Radius = Dpi.S(16)
            };

            // ---- Navy header -------------------------------------------------
            Card header = new Card
            {
                Fill = Theme.Navy950,
                BorderWidth = 0,
                Radius = Dpi.S(16),
                TopOnly = true,
                Width = cardWidth,
                Height = Dpi.S(132),
                Location = new Point(0, 0)
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
            int left = Dpi.S(28);
            int fieldWidth = cardWidth - Dpi.S(56);
            int y = header.Bottom + Dpi.S(22);

            Label prompt = Ui.Eyebrow("Sign in to your account");
            prompt.Location = new Point(left, y);
            _card.Controls.Add(prompt);
            y = prompt.Bottom + Dpi.S(14);

            Label userLabel = Ui.FieldLabel("Email");
            userLabel.Location = new Point(left, y);
            _card.Controls.Add(userLabel);
            y = userLabel.Bottom + Dpi.S(5);

            _username = new TextInput
            {
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(36),
                Placeholder = "e.g. admin"
            };
            _username.Submitted += (s, e) => _password.Focus();
            _card.Controls.Add(_username);
            y = _username.Bottom + Dpi.S(14);

            Label passLabel = Ui.FieldLabel("Password");
            passLabel.Location = new Point(left, y);
            _card.Controls.Add(passLabel);
            y = passLabel.Bottom + Dpi.S(5);

            _password = new TextInput
            {
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(36),
                UsePasswordChar = true
            };
            _password.Submitted += (s, e) => AttemptSignIn();
            _card.Controls.Add(_password);
            y = _password.Bottom + Dpi.S(10);

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

            FlatButton signIn = new FlatButton
            {
                Text = "Sign In",
                Variant = ButtonVariant.Primary,
                Width = fieldWidth,
                Height = Dpi.S(40),
                Location = new Point(left, y)
            };
            signIn.Click += (s, e) => AttemptSignIn();
            _card.Controls.Add(signIn);
            y = signIn.Bottom + Dpi.S(12);

            // ---- Forgot password link ---------------------------------------
            Label forgotLink = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Theme.Navy700,
                BackColor = Color.Transparent,
                Location = new Point(left, y),
                Text = "Forgot password?",
                Cursor = Cursors.Hand
            };
            forgotLink.Click += (s, e) => ForgotPassword();
            _card.Controls.Add(forgotLink);
            y = forgotLink.Bottom + Dpi.S(14);

            // ---- Register link ----------------------------------------------
            Panel registerRow = new Panel
            {
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(20),
                BackColor = Color.Transparent
            };

            Label noAccount = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                BackColor = Color.Transparent,
                Location = new Point(0, 0),
                Text = "New here?"
            };
            registerRow.Controls.Add(noAccount);

            Label registerLink = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Theme.Navy700,
                BackColor = Color.Transparent,
                Location = new Point(noAccount.Right + Dpi.S(4), 0),
                Text = "Account access",
                Cursor = Cursors.Hand
            };
            registerLink.Click += (s, e) =>
            {
                _error.Text = "Account provisioning requires your Supabase/QMS administrator.";
            };
            registerRow.Controls.Add(registerLink);

            _card.Controls.Add(registerRow);
            y = registerRow.Bottom + Dpi.S(14);

            // ---- Demo credentials hint --------------------------------------
            Label hint = Ui.Wrapped(
                "Sign in with your shared Mzukulu account email.",
                Theme.Tiny, Theme.Slate400, fieldWidth);
            hint.Location = new Point(left, y);
            _card.Controls.Add(hint);

            _card.Height = hint.Bottom + Dpi.S(24);
            Controls.Add(_card);

            ActiveControl = _username;
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Left = (ClientSize.Width - _card.Width) / 2;
            _card.Top = Math.Max(Dpi.S(16), (ClientSize.Height - _card.Height) / 2);
        }

        /// <summary>
        /// Self-service password reset. Prompts for the account email, generates a new
        /// temporary password, and displays it. The user is forced to change it at their
        /// next sign-in (handled by the must-change-password flow after login).
        /// </summary>
        private void ForgotPassword()
        {
            _error.Text = "Use the mobile password recovery flow or contact your administrator. Desktop recovery callbacks are not registered.";
        }

        private bool _signingIn;
        private async void AttemptSignIn()
        {
            if (_signingIn) return;
            _signingIn = true; _error.Text = "Signing in…"; _username.Enabled = _password.Enabled = false;
            try
            {
                await Auth.SignInAsync(_username.Value, _password.Value);
                if(IsDisposed)return;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { if(!IsDisposed) _error.Text = ex is System.Net.Http.HttpRequestException ? "Check your connection and try again." : ex.Message; }
            finally { _signingIn = false; if (!IsDisposed) { _password.Value = ""; _username.Enabled = _password.Enabled = true; } }
        }
    }
}
