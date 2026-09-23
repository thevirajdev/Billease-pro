using System;
using System.IO;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Single source of truth for every filesystem path the app uses.
    /// Previously the "BillingSuite" folder / "billing.db" constants were duplicated
    /// across AppDbContext, AppDbContextFactory, DbInitializer, DatabaseUtils and Program.
    /// All of those now resolve through here.
    /// </summary>
    public static class AppPaths
    {
        public const string AppFolderName = "BillingSuite";
        public const string DbFileName = "billing.db";

        /// <summary>%LOCALAPPDATA%\BillingSuite — created on first access.</summary>
        public static string DataFolder
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppFolderName);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Full path to the live SQLite database file.</summary>
        public static string DatabaseFile => Path.Combine(DataFolder, DbFileName);

        /// <summary>A restore staged here is swapped in by DbInitializer on next startup.</summary>
        public static string PendingRestoreFile => DatabaseFile + ".pending";

        public static string SafetyBackupFile => DatabaseFile + ".safety_backup";

        /// <summary>Timestamped quarantine for a database that failed to open.</summary>
        public static string BrokenDatabaseFile(DateTime when)
            => $"{DatabaseFile}.broken_{when:yyyyMMdd_HHmmss}";

        /// <summary>Per-user config store. Must live in DataFolder, NOT beside the exe:
        /// an installed app under Program Files cannot write next to itself.</summary>
        public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

        public static string StartupErrorLog => Path.Combine(DataFolder, "startup_error.log");

        /// <summary>Scratch folder for generated invoice PDFs.</summary>
        public static string InvoiceTempFolder
        {
            get
            {
                var dir = Path.Combine(Path.GetTempPath(), AppFolderName, "Invoices");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Default filename for a generated invoice PDF.</summary>
        public static string InvoicePdfFileName(string invoiceNumber, DateTime when)
            => $"Invoice_{Sanitize(invoiceNumber)}_{when:yyyyMMddHHmmss}.pdf";

        public static string BackupFileName(DateTime when)
            => $"BillingBackup_{when:yyyyMMdd_HHmmss}.bak";

        /// <summary>Strips characters that are illegal in Windows filenames.</summary>
        public static string Sanitize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
