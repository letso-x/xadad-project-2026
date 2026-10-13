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
    /// Second authentication factor: the user enters the one-time code emailed to them
    /// after a correct password. The dialog returns OK only when the code verifies.
    /// </summary>
    public class MfaForm : Form
    {
        private readonly UserAccount _account;
        private Card _card;
        private TextInput _code;
        private Label _error;
        private Label _info;
        private CheckBox _trustDevice;

        /// <summary>
        /// True if the user ticked "Don't ask again" — the shell then trusts this device
        /// so no code is required for the trust window. Default off: a code every sign-in.
        /// </summary>
        public bool TrustThisDevice
        {
            get { return _trustDevice != null && _trustDevice.Checked; }
        }

        public MfaForm(UserAccount account)
        {
            _account = account;

            Text = "Mzukulu QMS — Verify it's you";
            ClientSize = new Size(Dpi.S(760), Dpi.S(560));
            MinimumSize = new Size(Dpi.S(520), Dpi.S(520));
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Theme.Navy950;
            Font = Theme.Body;
            ControlBox = false; // completing or cancelling MFA is explicit
            AppIcon.Apply(this);

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
                Text = "Two-step verification"
            };
            header.Controls.Add(brand);

            _card.Controls.Add(header);

            int left = Dpi.S(28);
            int fieldWidth = cardWidth - Dpi.S(56);
            int y = header.Bottom + Dpi.S(18);

            Label intro = Ui.Wrapped(
                "We've emailed a 6-digit verification code to " + MaskEmail(_account.Email)
                + ". Enter it below to finish signing in.",
                Theme.Small, Theme.Slate700, fieldWidth);
            intro.Location = new Point(left, y);
            _card.Controls.Add(intro);
            y = intro.Bottom + Dpi.S(16);

            Label codeLabel = Ui.FieldLabel("Verification Code");
            codeLabel.Location = new Point(left, y);
            _card.Controls.Add(codeLabel);
            y = codeLabel.Bottom + Dpi.S(5);

            _code = new TextInput
            {
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(38),
                Placeholder = "6-digit code"
            };
            _code.Submitted += (s, e) => Verify();
            _card.Controls.Add(_code);
            y = _code.Bottom + Dpi.S(10);

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

            FlatButton verify = new FlatButton
            {
                Text = "Verify & Continue",
                Variant = ButtonVariant.Primary,
                Width = fieldWidth,
                Height = Dpi.S(40),
                Location = new Point(left, y)
            };
            verify.Click += (s, e) => Verify();
            _card.Controls.Add(verify);
            y = verify.Bottom + Dpi.S(10);

            // "Don't ask again" — when ticked, this device is trusted after verifying so
            // no code is needed for the trust window (default 14 days).
            _trustDevice = Ui.Check("Don't ask again for "
                + DeviceTrust.TrustDays + " days on this device");
            _trustDevice.Location = new Point(left, y);
            _trustDevice.Width = fieldWidth;
            _card.Controls.Add(_trustDevice);
            y = _trustDevice.Bottom + Dpi.S(10);

            Label resend = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Theme.Navy700,
                BackColor = Color.Transparent,
                Location = new Point(left, y),
                Text = "Resend code",
                Cursor = Cursors.Hand
            };
            resend.Click += (s, e) => Resend();
            _card.Controls.Add(resend);

            Label cancel = new Label
            {
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Slate500,
                BackColor = Color.Transparent,
                Location = new Point(left + fieldWidth - Dpi.S(60), y),
                Text = "Cancel",
                Cursor = Cursors.Hand
            };
            cancel.Click += (s, e) =>
            {
                MfaService.Clear(_account);
                DialogResult = DialogResult.Cancel;
                Close();
            };
            _card.Controls.Add(cancel);
            y = resend.Bottom + Dpi.S(10);

            _info = new Label
            {
                AutoSize = false,
                Font = Theme.Tiny,
                ForeColor = Theme.Slate400,
                BackColor = Color.Transparent,
                Location = new Point(left, y),
                Width = fieldWidth,
                Height = Dpi.S(16),
                Text = "Sending a code to your email…"
            };
            _card.Controls.Add(_info);

            _card.Height = _info.Bottom + Dpi.S(24);
            Controls.Add(_card);

            ActiveControl = _code;
        }

        /// <summary>
        /// Once the branded form is on screen, send the code on a background thread so the
        /// window never freezes while the mail server is contacted. Status updates are
        /// marshalled back to the UI thread.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            IssueCodeAsync();
        }

        private void IssueCodeAsync()
        {
            SetInfo("Sending a code to your email…", false);

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string err = MfaService.Issue(_account);

                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)(() =>
                {
                    if (err == null)
                    {
                        SetInfo("Code sent. It is valid for "
                            + MfaService.ValidityMinutesValue + " minutes.", false);
                    }
                    else
                    {
                        SetInfo(err, true);
                    }
                }));
            });
        }

        private void SetInfo(string text, bool isError)
        {
            if (_info == null) return;
            _info.ForeColor = isError ? Theme.Red600 : Theme.Slate400;
            _info.Text = text;
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Left = (ClientSize.Width - _card.Width) / 2;
            _card.Top = Math.Max(Dpi.S(16), (ClientSize.Height - _card.Height) / 2);
        }

        private void Verify()
        {
            _error.Text = string.Empty;

            MfaResult result = MfaService.Verify(_account, _code.Value);
            if (result == MfaResult.Success)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _error.Text = MfaService.Describe(result);
            _code.Value = string.Empty;
            _code.Focus();

            // A dead challenge (expired / too many attempts) means they must start over.
            if (result == MfaResult.Expired || result == MfaResult.TooManyAttempts
                || result == MfaResult.NotRequested)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void Resend()
        {
            _error.Text = string.Empty;
            _code.Value = string.Empty;
            IssueCodeAsync();
        }

        /// <summary>Masks an email for display, e.g. "jo****@example.com".</summary>
        private static string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "your email";
            int at = email.IndexOf('@');
            if (at <= 1) return email;

            string name = email.Substring(0, at);
            string domain = email.Substring(at);
            string shown = name.Length <= 2
                ? name.Substring(0, 1)
                : name.Substring(0, 2);
            return shown + new string('*', Math.Max(2, name.Length - shown.Length)) + domain;
        }
    }
}
