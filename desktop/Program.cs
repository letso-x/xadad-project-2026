using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MzuApplication.Data;
using MzuApplication.Forms;
using MzuApplication.Models;

namespace MzuApplication
{
    internal static class Program
    {
        // DPI awareness is declared in app.manifest, which is the reliable route.
        // These calls are a runtime fallback for the case where the manifest is not
        // applied (for example when the assembly is loaded by another host).
        private const int DpiAwarenessPerMonitorV2 = -4;
        private const int DpiAwarenessPerMonitor = -3;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(int value);

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            EnableHighDpi();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Capture the display scale before any control exists.
            Dpi.Initialise();

            Repository.Load();

            // If configured for Postgres, verify the connection up front. On failure,
            // tell the user and fall back to the local store so a demo still runs.
            if (DbConfig.UsePostgres)
            {
                string error = PostgresUserStore.Initialise();
                if (error != null)
                {
                    DbConfig.DisablePostgres();
                    MessageBox.Show(
                        "Could not connect to the Supabase database:\r\n\r\n" + error
                        + "\r\n\r\nThe app will use the local offline account store instead.",
                        "Database connection failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // Outer loop: sign-in / registration cycle. Breaks out only on a
            // successful authenticated session or when the user quits.
            while (true)
            {
                if (!RunAuthentication())
                {
                    return; // user closed the login window
                }

                // A reset password forces the user to choose a new one before entering.
                if (Auth.CurrentMustChangePassword)
                {
                    using (ChangePasswordForm change = new ChangePasswordForm())
                    {
                        if (change.ShowDialog() != DialogResult.OK)
                        {
                            // Refused to set a new password: sign out and return to login.
                            Auth.SignOut();
                            continue;
                        }
                    }
                }

                using (MainForm main = new MainForm())
                {
                    main.ShowDialog();

                    // "Switch user" returns to the login screen; anything else quits.
                    if (main.DialogResult != DialogResult.Retry)
                    {
                        return;
                    }

                    Auth.SignOut();
                }
            }
        }

        /// <summary>
        /// Runs the login screen, letting the user flip to registration and back. Returns
        /// true once a user is authenticated, false if they close the window to quit.
        /// </summary>
        private static bool RunAuthentication()
        {
            while (true)
            {
                using (LoginForm login = new LoginForm())
                {
                    DialogResult result = login.ShowDialog();

                    if (result == DialogResult.OK)
                    {
                        return true; // Auth.Current is set
                    }

                    if (login.RegisterRequested)
                    {
                        using (RegisterForm register = new RegisterForm())
                        {
                            register.ShowDialog();
                            // Whether they registered or cancelled, loop back to login.
                        }
                        continue;
                    }

                    return false; // closed/cancelled -> quit
                }
            }
        }

        private static void EnableHighDpi()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(DpiAwarenessPerMonitorV2)) return;
                if (SetProcessDpiAwarenessContext(DpiAwarenessPerMonitor)) return;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            try
            {
                // 2 == PROCESS_PER_MONITOR_DPI_AWARE
                if (SetProcessDpiAwareness(2) == 0) return;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            try
            {
                SetProcessDPIAware();
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
