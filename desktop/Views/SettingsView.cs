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

            AddConnectionCard();
            AddPerformanceCard();
            AddSystemHealthCard();
            AddOrganisationCard(settings);
            AddPolicyCard(settings);
            AddSecurityCard(settings);
            AddDataCard();
        }

        private Label _statusDot;
        private Label _statusText;
        private Label _statusDetail;

        /// <summary>Live database connection status: green dot when online, red when offline.</summary>
        private void AddConnectionCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Database Connection", InnerWidth);
            card.Controls.Add(bar);

            int top = bar.Bottom + Dpi.S(18);

            // Status dot (a small filled circle label).
            _statusDot = new Label
            {
                AutoSize = false,
                Location = new Point(Dpi.S(20), top + Dpi.S(2)),
                Size = new Size(Dpi.S(14), Dpi.S(14)),
                BackColor = Theme.Amber600
            };
            _statusDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(_statusDot.Tag is Color ? (Color)_statusDot.Tag : Theme.Amber600))
                    e.Graphics.FillEllipse(b, 0, 0, _statusDot.Width - 1, _statusDot.Height - 1);
            };
            _statusDot.BackColor = Color.Transparent;
            _statusDot.Tag = Theme.Amber600;
            card.Controls.Add(_statusDot);

            _statusText = new Label
            {
                AutoSize = true,
                Font = Theme.BodyBold,
                ForeColor = Theme.Slate800,
                BackColor = Color.Transparent,
                Location = new Point(_statusDot.Right + Dpi.S(10), top),
                Text = "Checking…"
            };
            card.Controls.Add(_statusText);

            _statusDetail = Ui.Wrapped("Testing the connection to the shared database…",
                Theme.Small, Theme.Slate500, InnerWidth - Dpi.S(40));
            _statusDetail.Location = new Point(Dpi.S(20), top + Dpi.S(24));
            card.Controls.Add(_statusDetail);

            FlatButton test = new FlatButton
            {
                Text = "Test Connection",
                Variant = ButtonVariant.Default,
                Width = Dpi.S(150),
                Height = Dpi.S(34),
                Location = new Point(Dpi.S(20), _statusDetail.Bottom + Dpi.S(12))
            };
            test.Click += (s, e) => RefreshStatus();
            card.Controls.Add(test);

            card.Height = test.Bottom + Dpi.S(16);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);

            RefreshStatus();
        }

        /// <summary>Pings the DB on a background thread and updates the badge.</summary>
        private void RefreshStatus()
        {
            if (_statusText == null) return;

            SetStatus(Theme.Amber600, "Checking…", "Testing the connection to the shared database…");

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                DbPing ping = DbStatus.Check();
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed) return;
                    if (ping.Online)
                    {
                        SetStatus(Theme.Green600, "Online",
                            ping.Message + "  (" + ping.Milliseconds + " ms)");
                    }
                    else
                    {
                        SetStatus(Theme.Red600, "Offline", ping.Message);
                    }
                });
            });
        }

        private void SetStatus(Color dot, string text, string detail)
        {
            if (_statusDot == null) return;
            _statusDot.Tag = dot;
            _statusDot.Invalidate();
            _statusText.Text = text;
            _statusText.ForeColor = dot;
            _statusDetail.Text = detail;
        }

        private Label _mAvg, _mLast, _mUptime, _mSuccess, _mError, _mOps;
        private Timer _perfTimer;

        /// <summary>Live database performance metrics: query speed, uptime and error rate.</summary>
        private void AddPerformanceCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Performance Monitoring", InnerWidth);
            card.Controls.Add(bar);

            Label sub = Ui.Wrapped(
                "Live metrics for this session, measured from the app's own database calls.",
                Theme.Small, Theme.Slate500, InnerWidth - Dpi.S(40));
            sub.Location = new Point(Dpi.S(20), bar.Bottom + Dpi.S(12));
            card.Controls.Add(sub);

            // Six metric tiles in two rows of three.
            int gap = Dpi.S(14);
            int cols = 3;
            int tileW = (InnerWidth - Dpi.S(40) - gap * (cols - 1)) / cols;
            int tileH = Dpi.S(64);
            int top = sub.Bottom + Dpi.S(12);

            _mAvg = AddMetricTile(card, "Avg query speed", "—", 0, 0, tileW, tileH, top, gap);
            _mLast = AddMetricTile(card, "Last query", "—", 1, 0, tileW, tileH, top, gap);
            _mUptime = AddMetricTile(card, "Session uptime", "—", 2, 0, tileW, tileH, top, gap);
            _mSuccess = AddMetricTile(card, "Uptime / success", "—", 0, 1, tileW, tileH, top, gap);
            _mError = AddMetricTile(card, "Error rate", "—", 1, 1, tileW, tileH, top, gap);
            _mOps = AddMetricTile(card, "Total queries", "—", 2, 1, tileW, tileH, top, gap);

            int tilesBottom = top + tileH * 2 + gap;

            FlatButton reset = new FlatButton
            {
                Text = "Reset Metrics",
                Variant = ButtonVariant.Default,
                Width = Dpi.S(140),
                Height = Dpi.S(34),
                Location = new Point(Dpi.S(20), tilesBottom + Dpi.S(8))
            };
            reset.Click += (s, e) => { DbMetrics.Reset(); UpdateMetrics(); };
            card.Controls.Add(reset);

            card.Height = reset.Bottom + Dpi.S(16);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);

            UpdateMetrics();

            // Refresh the figures every 2 seconds while the screen is open.
            _perfTimer = new Timer { Interval = 2000 };
            _perfTimer.Tick += (s, e) => UpdateMetrics();
            _perfTimer.Start();
        }

        private Label AddMetricTile(Card parent, string label, string value,
                                    int col, int row, int w, int h, int top, int gap)
        {
            int x = Dpi.S(20) + col * (w + gap);
            int y = top + row * (h + gap);

            Card tile = new Card
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                Fill = Theme.Slate50,
                BorderTint = Theme.Slate150,
                Radius = Theme.RadiusSm
            };

            Label cap = new Label
            {
                AutoSize = false,
                Font = Theme.Tiny,
                ForeColor = Theme.Slate500,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(12), Dpi.S(9)),
                Size = new Size(w - Dpi.S(24), Dpi.S(14)),
                Text = label.ToUpperInvariant()
            };
            tile.Controls.Add(cap);

            Label val = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Navy900,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(12), cap.Bottom + Dpi.S(2)),
                Size = new Size(w - Dpi.S(24), Dpi.S(26)),
                Text = value
            };
            tile.Controls.Add(val);

            parent.Controls.Add(tile);
            return val;
        }

        private void UpdateMetrics()
        {
            if (_mAvg == null || IsDisposed) return;

            DbMetricsSnapshot s = DbMetrics.Snapshot();

            _mAvg.Text = s.AvgMs <= 0 ? "—" : Math.Round(s.AvgMs) + " ms";
            _mLast.Text = s.TotalOps == 0 ? "—" : s.LastMs + " ms";
            _mUptime.Text = FormatUptime(s.Uptime);
            _mSuccess.Text = Math.Round(s.SuccessRatePercent) + "%";
            _mError.Text = Math.Round(s.ErrorRatePercent, 1) + "%";
            _mError.ForeColor = s.Failures > 0 ? Theme.Red600 : Theme.Navy900;
            _mOps.Text = s.TotalOps.ToString();
        }

        private static string FormatUptime(TimeSpan t)
        {
            if (t.TotalHours >= 1) return (int)t.TotalHours + "h " + t.Minutes + "m";
            if (t.TotalMinutes >= 1) return t.Minutes + "m " + t.Seconds + "s";
            return t.Seconds + "s";
        }

        /// <summary>Read-only diagnostics: app version, runtime, environment, data source.</summary>
        private void AddSystemHealthCard()
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("System Health", InnerWidth);
            card.Controls.Add(bar);

            var items = SystemHealth.Collect();

            // Two columns of label/value rows.
            int cols = 2;
            int gap = Dpi.S(24);
            int colW = (InnerWidth - Dpi.S(40) - gap) / cols;
            int rowH = Dpi.S(40);
            int top = bar.Bottom + Dpi.S(14);
            int perCol = (int)Math.Ceiling(items.Count / (double)cols);

            for (int i = 0; i < items.Count; i++)
            {
                int col = i / perCol;
                int rowInCol = i % perCol;
                int x = Dpi.S(20) + col * (colW + gap);
                int y = top + rowInCol * rowH;

                Label label = new Label
                {
                    AutoSize = false,
                    Font = Theme.Tiny,
                    ForeColor = Theme.Slate400,
                    BackColor = Color.Transparent,
                    Location = new Point(x, y),
                    Size = new Size(colW, Dpi.S(14)),
                    Text = items[i].Key.ToUpperInvariant()
                };
                card.Controls.Add(label);

                bool good = IsHealthy(items[i].Key, items[i].Value);
                Label value = new Label
                {
                    AutoSize = false,
                    Font = Theme.BodyBold,
                    ForeColor = good ? Theme.Slate900 : Theme.Amber700,
                    BackColor = Color.Transparent,
                    Location = new Point(x, y + Dpi.S(15)),
                    Size = new Size(colW, Dpi.S(20)),
                    Text = items[i].Value
                };
                card.Controls.Add(value);
            }

            card.Height = top + perCol * rowH + Dpi.S(10);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);
        }

        /// <summary>Flags values that warrant attention (shown amber instead of dark).</summary>
        private static bool IsHealthy(string key, string value)
        {
            if (key == "Database config" && value != "Present") return false;
            if (key == "Data source" && value.StartsWith("Local")) return false;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _perfTimer != null)
            {
                _perfTimer.Stop();
                _perfTimer.Dispose();
                _perfTimer = null;
            }
            base.Dispose(disposing);
        }

        private void AddSecurityCard(OrgSettings settings)
        {
            Card card = new Card
            {
                Location = new Point(Left1, Y),
                Width = InnerWidth,
                Fill = Color.White,
                BorderTint = Theme.Slate150,
                Radius = Theme.Radius
            };

            Card bar = Ui.SectionBar("Security & Sessions", InnerWidth);
            card.Controls.Add(bar);

            int y = bar.Bottom + Dpi.S(16);

            CheckBox requireMfa = AddPolicySwitch(card, ref y,
                "Require email two-step verification at sign-in",
                "After the password, a one-time code is emailed to the user. Takes effect only "
                + "when email is configured on the machine. Trusted devices skip the code within "
                + "the trust window below.",
                settings.RequireMfa);

            int gap = Dpi.S(16);
            int colWidth = (InnerWidth - Dpi.S(40) - gap) / 2;

            Label idleLabel = Ui.FieldLabel("Inactivity timeout (minutes)");
            idleLabel.Location = new Point(Dpi.S(20), y);
            card.Controls.Add(idleLabel);

            TextInput idle = new TextInput
            {
                Location = new Point(Dpi.S(20), idleLabel.Bottom + Dpi.S(5)),
                Width = colWidth,
                Height = Dpi.S(34),
                Value = settings.IdleTimeoutMinutes.ToString()
            };
            card.Controls.Add(idle);

            Label trustLabel = Ui.FieldLabel("Trust this device for (days)");
            trustLabel.Location = new Point(Dpi.S(20) + colWidth + gap, y);
            card.Controls.Add(trustLabel);

            TextInput trustDays = new TextInput
            {
                Location = new Point(Dpi.S(20) + colWidth + gap, trustLabel.Bottom + Dpi.S(5)),
                Width = colWidth,
                Height = Dpi.S(34),
                Value = settings.DeviceTrustDays.ToString()
            };
            card.Controls.Add(trustDays);

            Label help = Ui.Wrapped(
                "Inactivity timeout prompts the user and signs them out when idle (set 0 to "
                + "disable). Device trust controls how long a verified machine can skip the "
                + "two-step code.",
                Theme.Tiny, Theme.Slate500, InnerWidth - Dpi.S(40));
            help.Location = new Point(Dpi.S(20), idle.Bottom + Dpi.S(10));
            card.Controls.Add(help);

            FlatButton save = new FlatButton
            {
                Text = "Save Security",
                Variant = ButtonVariant.Primary,
                Width = Dpi.S(150),
                Height = Dpi.S(34),
                Location = new Point(Dpi.S(20), help.Bottom + Dpi.S(12))
            };
            save.Click += (s, e) =>
            {
                int idleMin, days;
                if (!int.TryParse(idle.Value, out idleMin) || idleMin < 0 || idleMin > 240)
                {
                    Notify("Inactivity timeout must be between 0 and 240 minutes.", true);
                    return;
                }
                if (!int.TryParse(trustDays.Value, out days) || days < 1 || days > 365)
                {
                    Notify("Device trust must be between 1 and 365 days.", true);
                    return;
                }

                settings.RequireMfa = requireMfa.Checked;
                settings.IdleTimeoutMinutes = idleMin;
                settings.DeviceTrustDays = days;

                Audit.Log(AuditAction.Updated, "Settings", null, "Security",
                    "MFA " + (requireMfa.Checked ? "on" : "off")
                    + " · idle " + idleMin + "m · trust " + days + "d");
                Repository.Save();

                Notify("Security settings saved.");
            };
            card.Controls.Add(save);

            card.Height = save.Bottom + Dpi.S(16);
            Controls.Add(card);
            Y = card.Bottom + Dpi.S(18);
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

            CheckBox allowAttach = AddPolicySwitch(card, ref y,
                "Allow file attachments on records",
                "When on, users can attach supporting files (photos, certificates) to QC "
                + "records. Turn off to disallow attachments across the system.",
                settings.AllowAttachments);

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
                settings.AllowAttachments = allowAttach.Checked;
                settings.NcrDueDays = days;

                Audit.Log(AuditAction.Updated, "Settings", null, "Quality Policy",
                    "Dual sign-off " + (dualSignoff.Checked ? "on" : "off")
                    + " · NCR enforcement " + (enforceNcr.Checked ? "on" : "off")
                    + " · attachments " + (allowAttach.Checked ? "allowed" : "disallowed")
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
            if (!MessageDialog.Confirm(Shell, "Reset demo data",
                    "This will permanently delete all forms, registers, NCRs and audit "
                    + "entries, then restore the original demo data.\r\n\r\nContinue?",
                    "Reset", danger: true))
            {
                return;
            }

            Repository.ResetToSeed();
            Notify("Demo data restored.");
            Shell.Navigate(new DashboardView(Shell));
        }
    }
}
