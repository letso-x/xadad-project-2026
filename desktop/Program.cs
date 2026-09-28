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

            // Loop so "switch user" returns to the login screen instead of exiting.
            while (true)
            {
                using (LoginForm login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    Repository.SignIn(login.SelectedRole, login.DisplayName);

                    Audit.Log(AuditAction.SignedIn, "Session", null, login.DisplayName,
                        "Signed in as " + Format.RoleName(login.SelectedRole));
                    Repository.Save();
                }

                using (MainForm main = new MainForm())
                {
                    main.ShowDialog();

                    if (main.DialogResult != DialogResult.Retry)
                    {
                        return;
                    }
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
