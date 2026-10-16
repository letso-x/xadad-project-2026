using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using MzuApplication.Controls;

namespace MzuApplication.Forms
{
    /// <summary>
    /// A translucent full-window overlay showing the branded Mzukulu spinner while a
    /// background operation runs, so the UI never looks frozen. Use <see cref="Run"/> to
    /// execute work off the UI thread with the overlay shown and dismissed automatically.
    /// </summary>
    public class LoadingOverlay : Form
    {
        private readonly MzukuluSpinner _spinner;

        private LoadingOverlay(Form owner, string caption)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(0x0A, 0x15, 0x26); // navy, dimmed via Opacity
            Opacity = 0.82;
            DoubleBuffered = true;

            if (owner != null)
            {
                Bounds = owner.Bounds;
            }

            _spinner = new MzukuluSpinner
            {
                Caption = caption ?? "Working…",
                Size = new Size(Dpi.S(180), Dpi.S(210))
            };
            _spinner.ForeColor = Color.FromArgb(0xC8, 0xD4, 0xE2);
            Controls.Add(_spinner);
            Resize += (s, e) => CentreSpinner();
            CentreSpinner();
        }

        private void CentreSpinner()
        {
            _spinner.Location = new Point(
                (ClientSize.Width - _spinner.Width) / 2,
                (ClientSize.Height - _spinner.Height) / 2);
        }

        /// <summary>
        /// Shows the overlay over <paramref name="owner"/>, runs <paramref name="work"/>
        /// on a background thread, and closes the overlay when it completes. Any exception
        /// from the work is rethrown on the UI thread after the overlay closes.
        /// </summary>
        public static void Run(Form owner, string caption, Action work)
        {
            using (var overlay = new LoadingOverlay(owner, caption))
            {
                Exception captured = null;

                overlay.Shown += (s, e) =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { work(); }
                        catch (Exception ex) { captured = ex; }
                        finally
                        {
                            if (!overlay.IsDisposed && overlay.IsHandleCreated)
                            {
                                try { overlay.BeginInvoke((Action)overlay.Close); }
                                catch (Exception) { }
                            }
                        }
                    });
                };

                overlay.ShowDialog(owner);

                if (captured != null)
                {
                    throw new Exception(captured.Message, captured);
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // Composited for flicker-free translucency over the app.
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
    }
}
