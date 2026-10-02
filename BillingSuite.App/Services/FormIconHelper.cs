using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace BillingSuite.App.Services
{
    public static class FormIconHelper
    {
        private static Icon? _appIcon;

        /// <summary>
        /// Returns the billease app icon.
        ///
        /// Loading order (first success wins):
        ///   1. Embedded Resource in Assembly (billease.ico embedded via csproj - guaranteed 100% reliable)
        ///   2. Loose .ico file beside the exe or working directory
        ///   3. Icon.ExtractAssociatedIcon from the exe itself
        /// </summary>
        public static Icon? GetAppIcon()
        {
            if (_appIcon != null) return _appIcon;

            // ── 1. Embedded Resource inside the compiled assembly ───────────────────
            // This is completely self-contained and immune to single-file extraction or path issues.
            try
            {
                var asm = typeof(FormIconHelper).Assembly;
                var resNames = asm.GetManifestResourceNames();
                var match = resNames.FirstOrDefault(n => n.EndsWith("billease.ico", StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    using var s = asm.GetManifestResourceStream(match);
                    if (s != null)
                    {
                        _appIcon = new Icon(s);
                        return _appIcon;
                    }
                }
            }
            catch { }

            // ── 2. Loose .ico file right beside the running exe ─────────────────────
            try
            {
                var candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "billease.ico"),
                    Path.Combine(Application.StartupPath,                "billease.ico"),
                    Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? "", "billease.ico"),
                };

                foreach (var path in candidates)
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        _appIcon = new Icon(path);
                        return _appIcon;
                    }
                }
            }
            catch { }

            // ── 3. Extract from the exe resource (embedded via <ApplicationIcon>) ───
            try
            {
                var exePath = Application.ExecutablePath;
                if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                {
                    _appIcon = Icon.ExtractAssociatedIcon(exePath);
                    if (_appIcon != null) return _appIcon;
                }
            }
            catch { }

            return _appIcon;
        }

        /// <summary>
        /// Apply the app icon to a dialog / child form and hide it from the
        /// Windows taskbar. Call this in every form constructor that is NOT
        /// the main Form1 window.
        /// </summary>
        public static void ApplyIcon(Form form)
        {
            // Set ShowInTaskbar = false FIRST, independently so it always succeeds.
            try
            {
                form.ShowInTaskbar = false;
            }
            catch { }

            try
            {
                var icon = GetAppIcon();
                if (icon != null)
                    form.Icon = icon;
            }
            catch { }
        }

        /// <summary>
        /// Apply the app icon to the MAIN application window (Form1).
        /// Keeps ShowInTaskbar = true so the main window still shows in the taskbar.
        /// </summary>
        public static void ApplyIconMain(Form form)
        {
            try
            {
                form.ShowInTaskbar = true;
            }
            catch { }

            try
            {
                var icon = GetAppIcon();
                if (icon != null)
                    form.Icon = icon;
            }
            catch { }
        }
    }
}
