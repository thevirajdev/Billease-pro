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

        /// <summary>
        /// Set to true while PullFromCloudAsync is running to prevent QueueInstantSync
        /// from firing a concurrent sync that would cause UNIQUE constraint violations.
        /// </summary>
        internal static volatile bool _isPulling = false;

        public static string LastStatus { get; private set; } = "Not started";
        public static int CurrentProgressPercent { get; private set; } = 0;
        public static DateTime? LastSuccessfulSync { get; private set; }

        public static event Action<string>? StatusChanged;
        public static event Action? SyncCompleted;

        private static System.Threading.Timer? _instantSyncDebounceTimer;
        private static readonly object _debounceLock = new();

        /// <summary>
        /// Debounces and triggers background sync within 1.5 seconds of local business changes.
        /// </summary>
        public static void QueueInstantSync(int delayMs = 1500)
        {
            if (_isSyncing || _isPulling || !AppSettingsService.SyncEnabled) return;

            lock (_debounceLock)
            {
                _instantSyncDebounceTimer?.Dispose();
                _instantSyncDebounceTimer = new System.Threading.Timer(async _ =>
                {
                    if (AppSettingsService.SyncEnabled && !_isSyncing)
                    {
                        await SyncNowAsync(silent: true);
                    }
                }, null, delayMs, System.Threading.Timeout.Infinite);
            }
        }

        public static void NotifySyncCompleted()
        {
            try { SyncCompleted?.Invoke(); } catch { }
        }

        /// <summary>
        /// Starts the background sync worker. Safe to call on startup.
        /// </summary>
        public static void StartBackgroundWorker()
        {
            if (_syncTimer != null) return;

            int intervalMinutes = Math.Max(5, AppSettingsService.SyncIntervalMinutes);
            var period = TimeSpan.FromMinutes(intervalMinutes);

            // Fire first sync after 10 seconds, then repeat periodically
            _syncTimer = new System.Threading.Timer(async _ =>
            {
                if (AppSettingsService.SyncEnabled)
                {
                    await SyncNowAsync(silent: true);
                }
            }, null, TimeSpan.FromSeconds(10), period);

            // Listen for network connectivity restored (immediate sync upon reconnect)
            try
            {
                NetworkChange.NetworkAvailabilityChanged += (s, e) =>
                {
                    if (e.IsAvailable && AppSettingsService.SyncEnabled && !_isSyncing && !_isPulling)
                    {
                        _ = SyncNowAsync(silent: true);
                    }
                };
            }
            catch { }
        }

        /// <summary>
        /// Updates the interval if user modifies it in Settings.
        /// </summary>
        public static void UpdateInterval(int minutes)
        {
            if (_syncTimer == null) return;
            int interval = Math.Max(5, minutes);
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
        public static async Task<(bool Success, string Message)> SyncNowAsync(bool silent = false, bool forceFull = false)
        {
            if (_isSyncing)
            {
                var activeMsg = $"Cloud sync is actively running in background ({CurrentProgressPercent}%): {LastStatus}";
                return (true, activeMsg);
            }

            _isSyncing = true;
            UpdateStatus("Checking network connection...", 5);

            try
            {
                if (!IsNetworkAvailable())
                {
                    var offlineMsg = "Offline: Waiting for internet connection.";
                    UpdateStatus(offlineMsg, 0);
                    return (false, offlineMsg);
                }

                var connStr = CloudDbConfig.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                    connStr = AppSettingsService.SyncConnectionString?.Trim();

                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    bool isPendingRestorePush = AppSettingsService.GetBool("RestoreBackupForceCloudPush", false);
                    if (isPendingRestorePush)
                    {
                        // User restored from a local backup: PUSH restored database to cloud first!
                        // This prevents old corrupted cloud data from overwriting the user's restored backup.
                        UpdateStatus("Synchronizing restored backup to cloud (Overwrite Protection)...", 20);
                        var (pushRestoreOk, pushRestoreMsg) = await OnlineDatabaseService.PushToCloudAsync(connStr, AuthService.CurrentUser?.Id, (status, pct) =>
                        {
                            UpdateStatus($"[Restore Push] {status}", 20 + (int)(pct * 0.7));
                        });
                        if (pushRestoreOk)
                        {
                            AppSettingsService.Set("RestoreBackupForceCloudPush", "false");
                            var nowRestore = DateTime.UtcNow;
                            LastSuccessfulSync = nowRestore;
                            AppSettingsService.Set(AppSettingKeys.LastSyncAtUtc, nowRestore.ToString("o"));
                            var localTime = DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt");
                            var msg = $"Restored backup successfully pushed to cloud at {localTime}.";
                            UpdateStatus(msg, 100);
                            NotifySyncCompleted();
                            return (true, msg);
                        }
                    }

                    // Calculate since timestamp for delta sync to eliminate bandwidth overuse
                    DateTime? sinceUtc = null;
                    if (!forceFull)
                    {
                        sinceUtc = LastSuccessfulSync;
                        if (!sinceUtc.HasValue)
                        {
                            var storedLastSync = AppSettingsService.GetString(AppSettingKeys.LastSyncAtUtc);
                            if (!string.IsNullOrWhiteSpace(storedLastSync) && DateTime.TryParse(storedLastSync, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                            {
                                sinceUtc = parsed.ToUniversalTime();
                            }
                        }
                    }

                    // 1. Pull latest cloud updates from other devices into local SQLite (delta)
                    UpdateStatus("Pulling latest cloud changes from other devices (1/2)...", 10);
                    var (pullOk, pullMsg) = await OnlineDatabaseService.PullFromCloudAsync(connStr, AuthService.CurrentUser?.Id, (status, pct) =>
                    {
                        UpdateStatus($"[1/2 Pull] {status}", 10 + (int)(pct * 0.45));
                    }, sinceUtc: sinceUtc);

                    if (!pullOk)
                    {
                        UpdateStatus($"Cloud pull notice: {pullMsg}", 50);
                    }

                    // 2. Push local device updates to cloud database (delta)
                    UpdateStatus("Pushing local business updates to cloud (2/2)...", 55);
                    var (pushOk, pushMsg) = await OnlineDatabaseService.PushToCloudAsync(connStr, AuthService.CurrentUser?.Id, (status, pct) =>
                    {
                        UpdateStatus($"[2/2 Push] {status}", 55 + (int)(pct * 0.45));
                    }, sinceUtc: sinceUtc);

                    if (pushOk)
                    {
                        var now = DateTime.UtcNow;
                        LastSuccessfulSync = now;
                        AppSettingsService.Set(AppSettingKeys.LastSyncAtUtc, now.ToString("o"));
                        var localTimeStr = DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt");
                        var deviceName = Environment.MachineName;
                        var successMsg = $"24/7 Multi-Device Cloud Sync active ({deviceName}). All records synchronized at {localTimeStr}.";
                        UpdateStatus(successMsg, 100);
                        NotifySyncCompleted();
                        return (true, successMsg);
                    }
                    else
                    {
                        UpdateStatus(pushMsg, 0);
                        return (false, pushMsg);
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
                    string vacuumSql = "VACUUM INTO '" + snapshotFile.Replace("'", "''") + "'";
                    db.Database.ExecuteSqlRaw(vacuumSql);
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
                // If the main UI window is already running, skip headless sync to prevent duplicate work and egress
                var currentProc = System.Diagnostics.Process.GetCurrentProcess();
                var runningProcs = System.Diagnostics.Process.GetProcessesByName(currentProc.ProcessName);
                foreach (var p in runningProcs)
                {
                    if (p.Id != currentProc.Id && p.MainWindowHandle != IntPtr.Zero)
                    {
                        return;
                    }
                }

                DbInitializer.EnsureCreatedAndSeed();
                AppSettingsService.SeedDefaults();

                if (!AppSettingsService.SyncEnabled) return;
                if (!IsNetworkAvailable()) return;

                // Load active user session so user-specific records sync properly
                AuthService.TryAutoLogin();

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

        private static void UpdateStatus(string status, int percent = -1)
        {
            LastStatus = status;
            if (percent >= 0) CurrentProgressPercent = Math.Clamp(percent, 0, 100);
            try
            {
                StatusChanged?.Invoke(status);
            }
            catch { }
        }
    }
}
