namespace BillingSuite.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // QuestPDF Community License. Must be set before any PDF is generated,
        // including the headless self-test path below.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Headless verification path, used to check the settings layer without a UI.
        if (args.Length > 0 && args[0] == "--settings-selftest")
        {
            Environment.ExitCode = SettingsSelfTest.Run();
            return;
        }

        // Headless background sync path, used by Windows Task Scheduler when app is closed.
        if (args.Length > 0 && args[0] == "--sync-background")
        {
            Services.SyncService.RunHeadlessSync();
            return;
        }

        // 24/7 Background Sync Service Mode (Lightweight background service without full UI)
        if (args.Length > 0 && (args[0].Equals("--sync-service", StringComparison.OrdinalIgnoreCase) ||
                               args[0].Equals("--background-worker", StringComparison.OrdinalIgnoreCase) ||
                               args[0].Equals("--autostart", StringComparison.OrdinalIgnoreCase)))
        {
            RunSyncServiceApp();
            return;
        }

        // Signal any running background sync service to step aside so main UI can open cleanly
        try
        {
            if (System.Threading.EventWaitHandle.TryOpenExisting(@"Global\BilleasePro_SyncService_Stop_Event", out var stopEvt))
            {
                using (stopEvt)
                {
                    stopEvt.Set();
                }
                System.Threading.Thread.Sleep(200);
            }
        }
        catch { }

        // Headless cloud database test & initialization
        if (args.Length > 0 && args[0] == "--test-cloud-db")
        {
            var connStr = args.Length > 1 ? args[1] : Services.CloudDbConfig.GetConnectionString();
            var testRes = Services.OnlineDatabaseService.TestConnectionAsync(connStr).GetAwaiter().GetResult();
            Console.WriteLine(testRes.Success ? "TEST SUCCESS: " + testRes.Message : "TEST FAILED: " + testRes.Message);
            if (testRes.Success)
            {
                var schemaRes = Services.OnlineDatabaseService.EnsureSchemaAsync(connStr).GetAwaiter().GetResult();
                Console.WriteLine(schemaRes.Success ? "SCHEMA SUCCESS: " + schemaRes.Message : "SCHEMA FAILED: " + schemaRes.Message);


            }
            return;
        }

        if (args.Length > 0 && args[0] == "--test-sync")
        {
            Services.DbInitializer.EnsureCreatedAndSeed();
            var connStr = Services.CloudDbConfig.GetConnectionString();
            Console.WriteLine("Testing PullFromCloudAsync...");
            try
            {
                var (pullOk, pullMsg) = Services.OnlineDatabaseService.PullFromCloudAsync(connStr, Services.AuthService.CurrentUser?.Id ?? 5, (status, pct) =>
                {
                    Console.WriteLine($"Pull: {pct}% - {status}");
                }).GetAwaiter().GetResult();
                Console.WriteLine($"Pull result: ok={pullOk}, msg={pullMsg}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Pull Exception: {ex}");
            }

            Console.WriteLine("\nTesting PushToCloudAsync...");
            try
            {
                var (pushOk, pushMsg) = Services.OnlineDatabaseService.PushToCloudAsync(connStr, Services.AuthService.CurrentUser?.Id ?? 5, (status, pct) =>
                {
                    Console.WriteLine($"Push: {pct}% - {status}");
                }).GetAwaiter().GetResult();
                Console.WriteLine($"Push result: ok={pushOk}, msg={pushMsg}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Push Exception: {ex}");
            }
            return;
        }

        // Clean up any stale headless/background processes of BillingSuite.App if user is explicitly launching the main app
        try
        {
            var currentProc = System.Diagnostics.Process.GetCurrentProcess();
            var runningList = System.Diagnostics.Process.GetProcessesByName(currentProc.ProcessName)
                .Where(p => p.Id != currentProc.Id)
                .ToList();

            foreach (var p in runningList)
            {
                // If it has NO main window, it's a headless background sync or stuck phantom process!
                if (p.MainWindowHandle == IntPtr.Zero)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                    catch { }
                }
                else
                {
                    // It has an existing visible UI window! Focus it and exit.
                    ShowWindow(p.MainWindowHandle, 9 /* SW_RESTORE */);
                    SetForegroundWindow(p.MainWindowHandle);
                    var showMsg = Form1.RegisterWindowMessage(Form1.SingleInstanceMessage);
                    PostMessage((IntPtr)0xffff /* HWND_BROADCAST */, showMsg, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
            }
        }
        catch { }

        // Single-instance enforcement: prevent multiple instances from running simultaneously
        const string appMutexGuid = @"Global\BilleasePro_App_SingleInstance_Mutex_99938A2D";
        using var appMutex = new System.Threading.Mutex(false, appMutexGuid);
        bool hasOwnership = false;
        try
        {
            hasOwnership = appMutex.WaitOne(1000, false);
        }
        catch (System.Threading.AbandonedMutexException)
        {
            hasOwnership = true;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (s, e) =>
        {
            try { MessageBox.Show($"An unexpected error occurred:\n{e.Exception.Message}\n\n{e.Exception}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try { MessageBox.Show($"A fatal error occurred:\n{(e.ExceptionObject as Exception)?.Message}\n\n{e.ExceptionObject}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
        };
        try
        {
            Services.DbInitializer.EnsureCreatedAndSeed();
            Services.AppSettingsService.SeedDefaults();

            // Run silent maintenance tasks before showing any UI.
            Services.DatabaseUtils.RunAutoBackupIfDue();
            Services.DatabaseUtils.PurgeExpiredRecycleBin();

            // Start background cloud sync engine & register 24/7 Windows Scheduled Task
            Services.SyncService.StartBackgroundWorker();
            if (Services.AppSettingsService.SyncEnabled)
            {
                Services.SyncService.RegisterWindowsScheduledTask();
            }

            // Play Video Splash Screen (splash.mp4) or smooth Fade-Up GDI+ Splash Screen
            try
            {
                var videoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "splash.mp4");
                if (!System.IO.File.Exists(videoPath)) videoPath = System.IO.Path.Combine(Application.StartupPath, "splash.mp4");

                if (System.IO.File.Exists(videoPath))
                {
                    using var videoSplash = new Forms.VideoSplashForm();
                    videoSplash.ShowDialog();
                }
                else
                {
                    using var splash = new Forms.SplashForm();
                    splash.ShowDialog();
                }
            }
            catch
            {
                try
                {
                    using var splash = new Forms.SplashForm();
                    splash.ShowDialog();
                }
                catch { }
            }

            bool isAutostart = args.Length > 0 && args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);

            // Gate the app behind user authentication
            if (args.Length == 0 || !args.Contains("--no-auth", StringComparer.OrdinalIgnoreCase))
            {
                // Attempt auto-login using saved session
                bool autoLoggedIn = Services.AuthService.TryAutoLogin();

                if (!autoLoggedIn)
                {
                    var firstRun = !Services.AuthService.AnyUserExists();
                    using var login = new Forms.LoginForm(firstRun);
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        // User closed sign in dialog: exit app
                        return;
                    }
                }

                // Check if business profile needs initial setup (Full-Window Onboarding)
                var onboardingDone = Services.AppSettingsService.GetBool("onboarding.completed", false);
                var currentComp = Services.AppSettingsService.CompanyName;

                // If company name already configured, mark onboarding completed automatically
                if (!string.IsNullOrWhiteSpace(currentComp) && currentComp != "Your Company" && !onboardingDone)
                {
                    Services.AppSettingsService.Set("onboarding.completed", "true");
                    onboardingDone = true;
                }

                if (!onboardingDone && (string.IsNullOrWhiteSpace(currentComp) || currentComp == "Your Company"))
                {
                    using var setup = new Forms.OnboardingSetupForm();
                    if (setup.ShowDialog() != DialogResult.OK)
                    {
                        // User cancelled business profile setup: exit app immediately
                        return;
                    }
                }
            }

            // Ensure 24/7 background Windows startup autostart is registered when sync is enabled
            if (Services.AppSettingsService.SyncEnabled)
            {
                try { Services.WindowsStartupUtils.SetAutoStart(true); } catch { }
            }

            Application.Run(new Form1());
        }
        catch (Exception ex)
        {
            // Startup failures must be recorded on disk: a modal MessageBox can sit
            // behind other windows (or off-screen), which looks like "app won't open".
            try
            {
                var logPath = Services.AppPaths.StartupErrorLog;
                System.IO.File.WriteAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}\r\n");
            }
            catch { }
            MessageBox.Show($"Startup error:\n{ex.Message}\n\n{ex}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void RunSyncServiceApp()
    {
        const string syncMutexGuid = @"Global\BilleasePro_SyncService_Mutex_482D1C90";
        using var syncMutex = new System.Threading.Mutex(false, syncMutexGuid);
        bool hasOwnership = false;
        try
        {
            hasOwnership = syncMutex.WaitOne(0, false);
        }
        catch (System.Threading.AbandonedMutexException)
        {
            hasOwnership = true;
        }

        if (!hasOwnership)
        {
            // Sync service is already running
            return;
        }

        ApplicationConfiguration.Initialize();

        using var stopEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, @"Global\BilleasePro_SyncService_Stop_Event");

        NotifyIcon? trayIcon = null;
        try
        {
            Services.DbInitializer.EnsureCreatedAndSeed();
            Services.AppSettingsService.SeedDefaults();
            Services.AuthService.TryAutoLogin();

            // Start 24/7 background sync engine & schedule task
            Services.SyncService.StartBackgroundWorker();
            if (Services.AppSettingsService.SyncEnabled)
            {
                Services.SyncService.RegisterWindowsScheduledTask();
            }

            // Fire initial background sync
            if (Services.AppSettingsService.SyncEnabled)
            {
                _ = Services.SyncService.SyncNowAsync(silent: true);
            }

            var trayMenu = new ContextMenuStrip();
            var miOpen = new ToolStripMenuItem("💻 Open Billease Pro", null, (s, e) => LaunchMainApp());
            var miSync = new ToolStripMenuItem("🔄 Sync Now", null, async (s, e) =>
            {
                await Services.SyncService.SyncNowAsync();
            });
            var miExit = new ToolStripMenuItem("❌ Exit Sync Service", null, (s, e) =>
            {
                try { if (trayIcon != null) trayIcon.Visible = false; } catch { }
                Application.Exit();
            });

            trayMenu.Items.AddRange(new ToolStripItem[] { miOpen, miSync, new ToolStripSeparator(), miExit });

            trayIcon = new NotifyIcon
            {
                Icon = Services.FormIconHelper.GetAppIcon() ?? SystemIcons.Application,
                Text = "Billease Pro - 24/7 Cloud Sync Active",
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => LaunchMainApp();

            // Watch for stop signal from main app in background thread
            var stopThread = new System.Threading.Thread(() =>
            {
                try
                {
                    stopEvent.WaitOne();
                    try { if (trayIcon != null) trayIcon.Visible = false; } catch { }
                    Application.Exit();
                }
                catch { }
            })
            { IsBackground = true };
            stopThread.Start();

            Application.Run();
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = System.IO.Path.Combine(Services.AppPaths.DataFolder, "sync_service_error.log");
                System.IO.File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}\r\n");
            }
            catch { }
        }
        finally
        {
            try { trayIcon?.Dispose(); } catch { }
        }
    }

    private static void LaunchMainApp()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}