using System;
using System.Drawing;
using System.Windows.Forms;

namespace MzuApplication.Controls
{
    /// <summary>
    /// A transient notification pinned to the bottom-right of its host form, with a
    /// coloured left edge indicating success or failure.
    /// </summary>
    internal sealed class Toast : Card
    {
        private readonly Timer _timer;

        private Toast(string message, bool isError)
        {
            Fill = Theme.Navy950;
            BorderWidth = 0;
            Radius = Theme.Radius;
            LeftAccentWidth = 4;
            LeftAccentColor = isError ? Theme.Red600 : Theme.Green600;

            Label label = new Label
            {
                AutoSize = false,
                Font = Theme.Small,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Text = message,
                Location = new Point(Dpi.S(18), Dpi.S(0))
            };

            int textWidth = Math.Min(360, Ui.MeasureWrapped(message, Theme.Small, 360) > 20 ? 340 : 340);
            label.Width = textWidth;
            label.Height = Math.Max(20, Ui.MeasureWrapped(message, Theme.Small, textWidth));

            Width = label.Right + 20;
            Height = label.Height + 26;
            label.Top = Dpi.S(13);

            Controls.Add(label);

            _timer = new Timer { Interval = 2600 };
            _timer.Tick += (s, e) => Close();
        }

        /// <summary>Shows a toast over <paramref name="host"/> for a couple of seconds.</summary>
        public static void Show(Form host, string message, bool isError = false)
        {
            if (host == null || host.IsDisposed) return;

            // Only one toast at a time keeps the corner readable.
            for (int i = host.Controls.Count - 1; i >= 0; i--)
            {
                Toast existing = host.Controls[i] as Toast;
                if (existing != null) existing.Close();
            }

            Toast toast = new Toast(message, isError);
            host.Controls.Add(toast);
            toast.BringToFront();
            toast.Reposition(host);

            host.Resize += toast.HostResized;
            toast._timer.Start();
        }

        private void HostResized(object sender, EventArgs e)
        {
            Reposition(sender as Form);
        }

        private void Reposition(Form host)
        {
            if (host == null || IsDisposed) return;
            Left = Math.Max(8, host.ClientSize.Width - Width - 24);
            Top = Math.Max(8, host.ClientSize.Height - Height - 24);
        }

        private void Close()
        {
            _timer.Stop();

            Form host = FindForm();
            if (host != null) host.Resize -= HostResized;

            if (Parent != null) Parent.Controls.Remove(this);
            Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
