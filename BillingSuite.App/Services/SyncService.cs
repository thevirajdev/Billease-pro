using System;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Background cloud synchronization and network resilience service.
    /// Manages offline-first operation with automatic background sync when network
    /// is available, and silent headless execution when the application is closed.
    /// </summary>
    public static class SyncService
    {
        private static System.Threading.Timer? _syncTimer;
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        private static bool _isSyncing = false;

        public static string LastStatus { get; private set; } = "Not started";
        public static DateTime? LastSuccessfulSync { get; private set; }

        public static event Action<string>? StatusChanged;

        /// <summary>
        /// Starts the background sync worker. Safe to call on startup.
        /// </summary>
        public static void StartBackgroundWorker()
        {
            if (_syncTimer != null) return;

            int intervalMinutes = Math.Max(1, AppSettingsService.SyncIntervalMinutes);
            var period = TimeSpan.FromMinutes(intervalMinutes);

            // Fire first sync after 15 seconds, then repeat periodically
            _syncTimer = new System.Threading.Timer(async _ =>
            {
                if (AppSettingsService.SyncEnabled)
                {
                    await SyncNowAsync(silent: true);
                }
            }, null, TimeSpan.FromSeconds(15), period);
        }

        /// <summary>
        /// Updates the interval if user modifies it in Settings.
        /// </summary>
        public static void UpdateInterval(int minutes)
        {
            if (_syncTimer == null) return;
            int interval = Math.Max(1, minutes);
            _syncTimer.Change(TimeSpan.FromMinutes(interval), TimeSpan.FromMinutes(interval));
        }

        /// <summary>
        /// Checks if network connectivity is available.
        /// </summary>
        public static bool IsNetworkAvailable()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Performs a synchronization pass.
        /// If offline, logs the state and pauses gracefully without errors.
        /// If online and server is configured, packages the user database snapshot and synchronizes.
        /// </summary>
        public static async Task<(bool Success, string Message)> SyncNowAsync(bool silent = false)
        {
            if (_isSyncing) return (false, "Sync is already in progress.");

            _isSyncing = true;
            UpdateStatus("Checking network connection...");

            try
            {
                if (!IsNetworkAvailable())
                {
                    var offlineMsg = "Offline: Waiting for internet connection.";
                    UpdateStatus(offlineMsg);
                    return (false, offlineMsg);
                }

                var connStr = CloudDbConfig.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                    connStr = AppSettingsService.SyncConnectionString?.Trim();

                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    UpdateStatus("Connecting to online cloud database...");
                    var (onlineOk, onlineMsg) = await OnlineDatabaseService.PushToCloudAsync(connStr, AuthService.CurrentUser?.Id);
                    if (onlineOk)
                    {
                        var now = DateTime.UtcNow;
                        LastSuccessfulSync = now;
                        AppSettingsService.Set(AppSettingKeys.LastSyncAtUtc, now.ToString("o"));
                        var successMsg = $"Synced successfully to cloud database at {DateTime.Now:HH:mm:ss}";
                        UpdateStatus(successMsg);
                        return (true, successMsg);
                    }
                    else
                    {
                        UpdateStatus(onlineMsg);
                        return (false, onlineMsg);
                    }
                }

                var serverUrl = AppSettingsService.SyncServerUrl?.Trim();
                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    var noUrlMsg = "Sync is enabled but no database connection string or server URL is configured.";
                    UpdateStatus(noUrlMsg);
                    return (false, noUrlMsg);
                }

                UpdateStatus("Preparing database sync package...");

                // 1. Create a clean snapshot of the local database
                var syncDir = Path.Combine(AppPaths.DataFolder, "SyncQueue");
                Directory.CreateDirectory(syncDir);

                var snapshotFile = Path.Combine(syncDir, $"sync_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");
                if (File.Exists(snapshotFile)) File.Delete(snapshotFile);

                using (var db = new Data.AppDbContext())
                {
                    db.Database.ExecuteSqlRaw($"VACUUM INTO '{snapshotFile}'");
                }

                UpdateStatus("Synchronizing with remote server...");

                // 2. Perform the upload/sync
                bool syncSuccess = false;
                string syncResultDetails = "";

                if (serverUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    // HTTP/HTTPS endpoint
                    using var content = new MultipartFormDataContent();
                    using var fileStream = File.OpenRead(snapshotFile);
                    using var streamContent = new StreamContent(fileStream);

                    content.Add(streamContent, "database", Path.GetFileName(snapshotFile));
                    var userEmail = AuthService.CurrentUser?.Email ?? "offline_user";
                    content.Add(new StringContent(userEmail), "userEmail");
                    content.Add(new StringContent(Environment.MachineName), "deviceName");

                    var endpoint = serverUrl.TrimEnd('/') + "/api/sync";
                    var response = await _httpClient.PostAsync(endpoint, content);

                    if (response.IsSuccessStatusCode)
                    {
                        syncSuccess = true;
                        syncResultDetails = $"Server HTTP {(int)response.StatusCode}";
                    }
                    else
                    {
                        syncResultDetails = $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}";
                    }
                }
                else
                {
                    // Local directory / Network share path (e.g. \\server\share or D:\CloudDrive\Sync)
                    if (!Directory.Exists(serverUrl))
                    {
                        Directory.CreateDirectory(serverUrl);
                    }
                    var destFile = Path.Combine(serverUrl, Path.GetFileName(snapshotFile));
                    File.Copy(snapshotFile, destFile, true);
                    syncSuccess = true;
                    syncResultDetails = "Copied to network/cloud storage.";
                }

                // Clean up local temp snapshot
                try { if (File.Exists(snapshotFile)) File.Delete(snapshotFile); } catch { }

                if (syncSuccess)
                {
                    var now = DateTime.UtcNow;
                    LastSuccessfulSync = now;
                    AppSettingsService.Set(AppSettingKeys.LastSyncAtUtc, now.ToString("o"));
                    var successMsg = $"Synced successfully at {DateTime.Now:HH:mm:ss} ({syncResultDetails})";
                    UpdateStatus(successMsg);
                    return (true, successMsg);
                }
                else
                {
                    var failMsg = $"Sync failed: {syncResultDetails}";
                    UpdateStatus(failMsg);
                    return (false, failMsg);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Sync error: {ex.Message}";
                UpdateStatus(errorMsg);
                return (false, errorMsg);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        /// <summary>
        /// Headless sync execution for background execution (e.g. Windows Task Scheduler)
        /// when the main app is closed.
        /// </summary>
        public static void RunHeadlessSync()
        {
            try
            {
                DbInitializer.EnsureCreatedAndSeed();
                AppSettingsService.SeedDefaults();

                if (!AppSettingsService.SyncEnabled) return;
                if (!IsNetworkAvailable()) return;

                SyncNowAsync(silent: true).GetAwaiter().GetResult();
            }
            catch { }
        }

        /// <summary>
        /// Registers a scheduled task in Windows Task Scheduler so sync can run
        /// automatically in the background even when the desktop application is closed.
        /// </summary>
        public static (bool Success, string Message) RegisterWindowsScheduledTask()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return (false, "Could not determine application executable path.");

                int interval = Math.Max(5, AppSettingsService.SyncIntervalMinutes);
                string taskName = "BillingSuite_BackgroundSync";
                string arguments = "--sync-background";

                // Use schtasks to create a task running every N minutes
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{taskName}\" /tr \"\\\"{exePath}\\\" {arguments}\" /sc minute /mo {interval} /f",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(10000);
                    if (proc.ExitCode == 0)
                        return (true, $"Background scheduled task registered successfully (every {interval} minutes).");
                    else
                    {
                        var err = proc.StandardError.ReadToEnd();
                        return (false, $"Failed to register scheduled task: {err}");
                    }
                }
                return (false, "Could not start schtasks process.");
            }
            catch (Exception ex)
            {
                return (false, $"Error creating scheduled task: {ex.Message}");
            }
        }

        private static void UpdateStatus(string status)
        {
            LastStatus = status;
            try
            {
                StatusChanged?.Invoke(status);
            }
            catch { }
        }
    }
}
