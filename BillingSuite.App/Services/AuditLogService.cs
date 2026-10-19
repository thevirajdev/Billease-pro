using System;
using System.IO;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Append-only security audit log. Writes one line per event to a
    /// rotating daily log file in %LOCALAPPDATA%\BillingSuite\audit_logs\.
    /// Each line: ISO-8601 UTC timestamp | event | user email | details
    /// </summary>
    public static class AuditLogService
    {
        public static class Events
        {
            public const string LoginSuccess   = "LOGIN_SUCCESS";
            public const string LoginFailed    = "LOGIN_FAILED";
            public const string LoginLockout   = "LOGIN_LOCKOUT";
            public const string Logout         = "LOGOUT";
            public const string SessionTimeout = "SESSION_TIMEOUT";
            public const string PasswordChange = "PASSWORD_CHANGE";
            public const string AccountCreated = "ACCOUNT_CREATED";
            public const string DataExport     = "DATA_EXPORT";
            public const string BackupCreated  = "BACKUP_CREATED";
        }

        private static string LogFolder =>
            Path.Combine(AppPaths.DataFolder, "audit_logs");

        private static string TodayLogFile =>
            Path.Combine(LogFolder, $"audit_{DateTime.UtcNow:yyyy-MM-dd}.log");

        private static readonly object _syncLock = new();

        public static void Log(string eventType, string? userEmail = null, string? details = null)
        {
            try
            {
                lock (_syncLock)
                {
                    Directory.CreateDirectory(LogFolder);
                    var line = $"{DateTime.UtcNow:O}\t{eventType}\t{userEmail ?? "-"}\t{details ?? "-"}";
                    File.AppendAllText(TodayLogFile, line + Environment.NewLine);
                }
            }
            catch { /* never crash the app for a log failure */ }
        }
    }
}
