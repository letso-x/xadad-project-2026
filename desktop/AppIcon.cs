using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace MzuApplication
{
    /// <summary>
    /// Provides the application icon (the Mzukulu "M" mark) for window title bars and
    /// the taskbar. The same icon is embedded into the executable via the project's
    /// ApplicationIcon setting, which is what Windows Explorer shows on the .exe.
    /// </summary>
    internal static class AppIcon
    {
        private static Icon _icon;
        private static bool _loaded;

        /// <summary>The app icon, or null if it could not be loaded.</summary>
        public static Icon Value
        {
            get
            {
                if (_loaded) return _icon;
                _loaded = true;
                _icon = Load();
                return _icon;
            }
        }

        /// <summary>Applies the icon to a form, if available.</summary>
        public static void Apply(System.Windows.Forms.Form form)
        {
            Icon icon = Value;
            if (icon != null) form.Icon = icon;
        }

        private static Icon Load()
        {
            // 1. Prefer the icon embedded in the executable (ApplicationIcon).
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    Icon extracted = Icon.ExtractAssociatedIcon(exe);
                    if (extracted != null) return extracted;
                }
            }
            catch (Exception) { }

            // 2. Fall back to app.ico sitting next to the executable.
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(path)) return new Icon(path);
            }
            catch (Exception) { }

            return null;
        }
    }
}
