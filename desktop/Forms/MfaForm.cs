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
        private Card _card;
        private TextInput _code;
        private Label _error;
        private Label _info;
        private Label _setupKeyLink;
        private FlatButton _verifyButton;
        private Label _setupLink;
        private bool _busy;

        public MfaForm(UserAccount account)
        {

            Text = "Mzukulu QMS â€” Verify it's you";
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
                DesktopSession.Current.FactorId == null
                    ? "Set up an authenticator app to protect your existing account."
                    : "Enter the 6-digit code from your authenticator app to finish signing in.",
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
            _verifyButton = verify;
            _card.Controls.Add(verify);
            y = verify.Bottom + Dpi.S(10);

            _setupKeyLink = new Label { AutoSize = true, Font = Theme.SmallBold, ForeColor = Theme.Navy700, BackColor = Color.Transparent, Location = new Point(left,y), Text = "Show setup key", Cursor = Cursors.Hand, Visible = false };
            _setupKeyLink.Click += (s,e) => ShowSetupKey();
            _card.Controls.Add(_setupKeyLink);
            y = _setupKeyLink.Bottom + Dpi.S(10);

            Label resend = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Theme.Navy700,
                BackColor = Color.Transparent,
                Location = new Point(left, y),
                Text = "Set up authenticator",
                Cursor = Cursors.Hand
            };
            _setupLink = resend;
            resend.Visible = DesktopSession.Current.FactorId == null;
            resend.Click += (s, e) => BeginEnrollment();
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
                if (_busy) return;
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
                Text = "Sending a code to your emailâ€¦"
            };
            _card.Controls.Add(_info);

            _card.Height = _info.Bottom + Dpi.S(24);
            Controls.Add(_card);

            ActiveControl = _code;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _verifyButton.Enabled = _code.Enabled = DesktopSession.Current.FactorId != null;
            SetInfo(DesktopSession.Current.FactorId == null ? "Choose Set up authenticator to begin." : "Enter a current code from your authenticator app.", false);
        }
        private async void BeginEnrollment()
        {
            if (_busy) return; _busy = true; _setupLink.Enabled = _verifyButton.Enabled = false;
            try
            {
                await DesktopSession.Current.EnrollAsync();
                if (IsDisposed) return;
                _setupLink.Visible = false; _setupKeyLink.Visible = DesktopSession.Current.SetupKey != null;
                _code.Enabled = true;
                SetInfo("Add the setup key in your authenticator app, then enter its code.", false);
            }
            catch (Exception ex) { if(!IsDisposed) SetInfo(ex is System.Net.Http.HttpRequestException ? "Check your connection and try again." : ex.Message, true); }
            finally { _busy = false; if (!IsDisposed) { _setupLink.Enabled = true; _verifyButton.Enabled = DesktopSession.Current.FactorId != null; } }
        }
        private void ShowSetupKey()
        {
            using (var dialog = new Dialog("Authenticator setup key", Dpi.S(540)))
            {
                dialog.AddNote("Can't scan a code? Add this key manually in your authenticator app. Keep it private.");
                var key = dialog.AddTextField("Setup key", DesktopSession.Current.SetupKey); key.ReadOnlyValue = true;
                dialog.AddPrimaryAction("Done", () => dialog.DialogResult = DialogResult.OK);
                dialog.ShowDialog(this); key.Value = string.Empty;
            }
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

        private async void Verify()
        {
            if (_busy) return; _busy = true; _verifyButton.Enabled = _setupLink.Enabled = false; _error.Text = string.Empty;
            try
            {
                await DesktopSession.Current.VerifyAsync(_code.Value.Trim());
                if (IsDisposed) return;
                _code.Value = string.Empty; DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { if (!IsDisposed) { _error.Text = ex is System.Net.Http.HttpRequestException ? "Check your connection and try again." : ex.Message; _code.Value = string.Empty; } }
            finally { _busy = false; if(!IsDisposed) { _verifyButton.Enabled = DesktopSession.Current.FactorId != null; _setupLink.Enabled = true; } }
        }
    }
}
