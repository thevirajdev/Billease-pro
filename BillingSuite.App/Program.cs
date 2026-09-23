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

            // Start background cloud sync engine
            Services.SyncService.StartBackgroundWorker();

            // Phase 7: gate the app behind a local account. First run (no account on this
            // device) opens on the create-account tab and captures the company profile.
            if (args.Length == 0 || args[0] != "--no-auth")
            {
                var firstRun = !Services.AuthService.AnyUserExists();
                using var login = new Forms.LoginForm(firstRun);
                if (login.ShowDialog() != DialogResult.OK)
                {
                    // User closed the sign-in window: do not open the app.
                    return;
                }
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
}