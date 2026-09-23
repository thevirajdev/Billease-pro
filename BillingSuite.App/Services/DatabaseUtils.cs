using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using BillingSuite.App.Data;
using BillingSuite.App.Models;

namespace BillingSuite.App.Services
{
    public static class DatabaseUtils
    {
        private static string DbFolder => AppPaths.DataFolder;
        private static string DbPath => AppPaths.DatabaseFile;

        public static void BackupDatabase()
        {
            try
            {
                using var db = new AppDbContext();
                
                using var sfd = new SaveFileDialog
                {
                    Filter = "Billing Backup (*.bak)|*.bak|All files (*.*)|*.*",
                    FileName = $"BillingBackup_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
                    Title = "Export Database Backup (Pro Mode)"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    // PRO MODE: Use VACUUM INTO for a safe, atomic, live backup
                    // SQLite requires the destination file to NOT exist for VACUUM INTO
                    if (File.Exists(sfd.FileName))
                    {
                        File.Delete(sfd.FileName);
                    }

                    // Perform the atomic backup
                    // This creates a single, consistent .bak file even if WAL mode is on
                    db.Database.ExecuteSqlRaw($"VACUUM INTO '{sfd.FileName}'");

                    MessageBox.Show($"Database backup successful!\n\nLocation: {sfd.FileName}\n\nThis file contains all your current data.", 
                        "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create backup: {ex.Message}\n\nTip: Ensure the backup location is writable.", 
                    "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static bool RestoreDatabase()
        {
            try
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "Database Backup (*.bak;*.db)|*.bak;*.db|Backup Files (*.bak)|*.bak|Database Files (*.db)|*.db|All files (*.*)|*.*",
                    Title = "Select Database Backup to Restore (Legacy or Modern)"
                };

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    var result = MessageBox.Show(
                        "Are you sure you want to restore this backup? \n\n" +
                        "WARNING: Your current data will be replaced with the selected backup data.\n" +
                        "The application will close now and apply the restore on next launch.\n\n" +
                        "Notice for Older Backups: If you are restoring a backup from an older version without user accounts, all legacy products, customers, and invoices will be automatically converted and claimed by your active login upon startup.",
                        "Confirm Restore",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        // Schedule the restore by copying the backup to a .pending file
                        // The DbInitializer will handle the actual swap on the next startup
                        string pendingPath = DbPath + ".pending";
                        
                        if (!Directory.Exists(DbFolder))
                        {
                            Directory.CreateDirectory(DbFolder);
                        }

                        // Copy to pending location
                        File.Copy(ofd.FileName, pendingPath, true);
                        
                        MessageBox.Show("Restore scheduled successfully!\n\nThe application will now close. Please start it again to finalize the migration and restore.", 
                            "Ready to Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        Application.Exit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to prepare restore: {ex.Message}", "Restore Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        /// <summary>
        /// Claims all legacy or unowned data (where OwnerUserId IS NULL) and assigns it
        /// to the specified logged-in user so older version data is immediately accessible.
        /// </summary>
        public static int ClaimLegacyUnownedData(int ownerUserId)
        {
            if (ownerUserId <= 0) return 0;
            int totalClaimed = 0;
            try
            {
                using var db = new AppDbContext();
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();

                string[] tables = new[]
                {
                    "Products", "ProductBatches", "Customers", "Invoices", "InvoiceItems",
                    "Payments", "Suppliers", "Purchases", "PurchaseItems", "PurchasePayments",
                    "Expenses", "DamagedItems", "RecycleBin", "AiChatMessages"
                };

                foreach (var tbl in tables)
                {
                    try
                    {
                        // Check if table exists
                        using var chk = conn.CreateCommand();
                        chk.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tbl}';";
                        if (Convert.ToInt32(chk.ExecuteScalar()) == 0) continue;

                        // Check if OwnerUserId column exists
                        using var pragma = conn.CreateCommand();
                        pragma.CommandText = $"PRAGMA table_info(\"{tbl}\");";
                        bool hasCol = false;
                        using (var rdr = pragma.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                if (string.Equals(rdr[1]?.ToString(), "OwnerUserId", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasCol = true;
                                    break;
                                }
                            }
                        }
                        if (!hasCol) continue;

                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"UPDATE \"{tbl}\" SET OwnerUserId = @uid WHERE OwnerUserId IS NULL;";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@uid";
                        p.Value = ownerUserId;
                        cmd.Parameters.Add(p);
                        totalClaimed += cmd.ExecuteNonQuery();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClaimLegacyUnownedData error: {ex.Message}");
            }
            return totalClaimed;
        }

        /// <summary>
        /// Called once on startup. If automatic backups are enabled and the interval has
        /// elapsed since the last backup, silently creates a new backup in the data folder
        /// and prunes old backups beyond the configured keep-count.
        /// </summary>
        public static void RunAutoBackupIfDue()
        {
            try
            {
                if (!AppSettingsService.GetBool(AppSettingKeys.AutoBackupEnabled, false))
                    return;

                var backupDir = Path.Combine(AppPaths.DataFolder, "AutoBackups");
                Directory.CreateDirectory(backupDir);

                // Check last auto-backup time
                int intervalHours = AppSettingsService.GetInt(AppSettingKeys.AutoBackupIntervalHours, 24);
                var lastBackupKey = "AutoBackupLastRun";
                var lastStr = AppSettingsService.GetString(lastBackupKey);
                if (DateTime.TryParse(lastStr, out var last) && (DateTime.UtcNow - last).TotalHours < intervalHours)
                    return; // Not yet due

                // Perform the silent backup via VACUUM INTO
                var destFile = Path.Combine(backupDir, $"auto_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                if (File.Exists(destFile)) File.Delete(destFile);

                using (var db = new Data.AppDbContext())
                    db.Database.ExecuteSqlRaw($"VACUUM INTO '{destFile}'");

                // Record the run time
                AppSettingsService.Set(lastBackupKey, DateTime.UtcNow.ToString("o"));

                // Prune oldest backups beyond keep-count
                int keepCount = AppSettingsService.GetInt(AppSettingKeys.AutoBackupKeepCount, 7);
                var allBackups = Directory.GetFiles(backupDir, "auto_*.bak")
                    .OrderByDescending(f => File.GetCreationTimeUtc(f))
                    .ToList();
                foreach (var old in allBackups.Skip(keepCount))
                    try { File.Delete(old); } catch { }
            }
            catch
            {
                // Silent — auto-backup must never crash the app on startup.
            }
        }

        /// <summary>
        /// Purges RecycleBin rows that are older than the configured retention period.
        /// Safe to call on every startup; has no effect if the table is empty or the
        /// retention period has not been exceeded.
        /// </summary>
        public static void PurgeExpiredRecycleBin()
        {
            try
            {
                int days = AppSettingsService.GetInt(AppSettingKeys.RecycleBinRetentionDays, 30);
                var cutoff = DateTime.UtcNow.AddDays(-days);
                using var db = new Data.AppDbContext();
                var expired = db.RecycleBin.Where(r => r.DeletedAt < cutoff).ToList();
                if (expired.Count > 0)
                {
                    db.RecycleBin.RemoveRange(expired);
                    db.SaveChanges();
                }
            }
            catch
            {
                // Silent — purge must never block startup.
            }
        }
    }
}
