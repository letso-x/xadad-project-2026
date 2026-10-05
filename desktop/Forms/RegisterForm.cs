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
    /// Self-enrollment. A new user supplies a full name, username, optional email and a
    /// password (entered twice). On success the account is created and the dialog
    /// returns OK with the chosen username for a convenient hand-off to sign-in.
    /// </summary>
    public class RegisterForm : Form
    {
        private Card _card;
        private TextInput _fullName;
        private TextInput _username;
        private TextInput _email;
        private TextInput _password;
        private TextInput _confirm;
        private Label _error;

        public RegisterForm()
        {
            Text = "Mzukulu QMS — Create account";
            ClientSize = new Size(Dpi.S(940), Dpi.S(720));
            MinimumSize = new Size(Dpi.S(560), Dpi.S(680));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Theme.Navy950;
            Font = Theme.Body;

            BuildCard();
            Resize += (s, e) => CenterCard();
            CenterCard();
        }

        /// <summary>The username that was created, valid once the dialog returns OK.</summary>
        public string CreatedUsername { get; private set; }

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
            int cardWidth = Dpi.S(430);

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
                Height = Dpi.S(120),
                Location = new Point(0, 0)
            };

            MzukuluLogo mark = new MzukuluLogo
            {
                Location = new Point(Dpi.S(28), Dpi.S(26)),
                Size = new Size(Dpi.S(32), Dpi.S(32))
            };
            header.Controls.Add(mark);

            Label brand = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(26), Dpi.S(66)),
                Text = "Create your account"
            };
            header.Controls.Add(brand);

            Label tagline = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Color.FromArgb(0x9F, 0xB0, 0xC6),
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(28), Dpi.S(92)),
                Text = "Enroll yourself into Mzukulu QMS"
            };
            header.Controls.Add(tagline);

            _card.Controls.Add(header);

            int left = Dpi.S(28);
            int fieldWidth = cardWidth - Dpi.S(56);
            int y = header.Bottom + Dpi.S(20);

            _fullName = AddField("Full Name", "e.g. Thabo Mahlangu", left, fieldWidth, ref y, false);
            _username = AddField("Username", "Used to sign in", left, fieldWidth, ref y, false);
            _email = AddField("Email (optional)", "name@company.co.za", left, fieldWidth, ref y, false);
            _password = AddField("Password", null, left, fieldWidth, ref y, true);

            Label rule = Ui.Wrapped(Auth.PasswordRule, Theme.Tiny, Theme.Slate400, fieldWidth);
            rule.Location = new Point(left, y);
            _card.Controls.Add(rule);
            y = rule.Bottom + Dpi.S(10);

            _confirm = AddField("Confirm Password", null, left, fieldWidth, ref y, true);

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

            FlatButton create = new FlatButton
            {
                Text = "Create Account",
                Variant = ButtonVariant.Primary,
                Width = fieldWidth,
                Height = Dpi.S(40),
                Location = new Point(left, y)
            };
            create.Click += (s, e) => AttemptRegister();
            _card.Controls.Add(create);
            y = create.Bottom + Dpi.S(14);

            Panel backRow = new Panel
            {
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(20),
                BackColor = Color.Transparent
            };
            Label have = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate600,
                Location = new Point(0, 0),
                Text = "Already have an account?"
            };
            backRow.Controls.Add(have);
            Label backLink = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Theme.Navy700,
                Location = new Point(have.Right + Dpi.S(4), 0),
                Text = "Back to sign in",
                Cursor = Cursors.Hand
            };
            backLink.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            backRow.Controls.Add(backLink);
            _card.Controls.Add(backRow);

            _card.Height = backRow.Bottom + Dpi.S(22);
            Controls.Add(_card);

            ActiveControl = _fullName;
        }

        private TextInput AddField(string label, string placeholder, int left, int width,
                                   ref int y, bool password)
        {
            Label caption = Ui.FieldLabel(label);
            caption.Location = new Point(left, y);
            _card.Controls.Add(caption);
            y = caption.Bottom + Dpi.S(5);

            TextInput input = new TextInput
            {
                Location = new Point(left, y),
                Width = width,
                Height = Dpi.S(36),
                UsePasswordChar = password
            };
            if (!string.IsNullOrEmpty(placeholder)) input.Placeholder = placeholder;

            _card.Controls.Add(input);
            y = input.Bottom + Dpi.S(12);
            return input;
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Left = (ClientSize.Width - _card.Width) / 2;
            _card.Top = Math.Max(Dpi.S(16), (ClientSize.Height - _card.Height) / 2);
        }

        private void AttemptRegister()
        {
            _error.Text = string.Empty;

            if (_password.Value != _confirm.Value)
            {
                _error.Text = "The two passwords do not match.";
                return;
            }

            UserAccount created;
            AuthResult result = Auth.Register(
                _username.Value, _fullName.Value, _email.Value, _password.Value, out created);

            if (result == AuthResult.Success)
            {
                CreatedUsername = created.Username;

                string roleNote = created.Role == UserRole.Admin
                    ? "You are the first user, so you have been enrolled as an administrator."
                    : "Your account has been created.";

                MessageBox.Show(this,
                    roleNote + "\r\n\r\nYou can now sign in with your username and password.",
                    "Account created", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _error.Text = Auth.Describe(result);
        }
    }
}
