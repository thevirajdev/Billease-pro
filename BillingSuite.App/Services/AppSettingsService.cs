using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Typed access to the AppSettings table. Values are cached for the process
    /// lifetime; <see cref="Invalidate"/> is called after a settings save so the
    /// next read reflects the change without an app restart.
    /// </summary>
    public static class AppSettingsService
    {
        private static Dictionary<string, string?>? _cache;
        private static readonly object _gate = new();

        private static Dictionary<string, string?> Cache
        {
            get
            {
                lock (_gate)
                {
                    if (_cache != null) return _cache;
                    var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        using var db = new AppDbContext();
                        foreach (var s in db.AppSettings.AsNoTracking().ToList())
                            map[s.Key] = s.Value;
                    }
                    catch
                    {
                        // A settings read must never prevent the app from starting;
                        // callers fall back to the declared defaults below.
                    }
                    _cache = map;
                    return map;
                }
            }
        }

        public static void Invalidate()
        {
            lock (_gate) { _cache = null; }
        }

        public static string GetString(string key, string fallback = "")
            => Cache.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v! : fallback;

        public static int GetInt(string key, int fallback)
            => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public static decimal GetDecimal(string key, decimal fallback)
            => decimal.TryParse(GetString(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public static bool GetBool(string key, bool fallback)
            => bool.TryParse(GetString(key), out var v) ? v : fallback;

        public static string GetDate() => GetString(AppSettingKeys.DateFormat, "dd/MM/yyyy");
        public static string GetDateTime() => GetString(AppSettingKeys.DateTimeFormat, "dd/MM/yyyy HH:mm");

        // --- Company identity (replaces the "Your Company" literals) ---
        public static string CompanyName => GetString(AppSettingKeys.CompanyName, "Your Company");
        public static string CompanyAddress1 => GetString(AppSettingKeys.CompanyAddress1);
        public static string CompanyAddress2 => GetString(AppSettingKeys.CompanyAddress2);
        public static string CompanyCity => GetString(AppSettingKeys.CompanyCity);
        public static string CompanyState => GetString(AppSettingKeys.CompanyState);
        public static string CompanyPostalCode => GetString(AppSettingKeys.CompanyPostalCode);
        public static string CompanyCountry => GetString(AppSettingKeys.CompanyCountry);
        public static string CompanyPhone => GetString(AppSettingKeys.CompanyPhone);
        public static string CompanyEmail => GetString(AppSettingKeys.CompanyEmail);
        public static string CompanyWebsite => GetString(AppSettingKeys.CompanyWebsite);
        public static string CompanyTaxNumber => GetString(AppSettingKeys.CompanyTaxNumber);
        public static string CompanyTaxLabel => GetString(AppSettingKeys.CompanyTaxLabel, "GST");
        public static string CompanyLogoPath => GetString(AppSettingKeys.CompanyLogoPath);

        /// <summary>Single formatted company address block, used by every render path.</summary>
        public static string CompanyAddressBlock()
        {
            var parts = new[]
            {
                CompanyAddress1, CompanyAddress2,
                string.Join(", ", new[] { CompanyCity, CompanyState, CompanyPostalCode }
                                      .Where(p => !string.IsNullOrWhiteSpace(p))),
                CompanyCountry
            };
            return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        // --- Tax & currency ---
        public static string CurrencySymbol => GetString(AppSettingKeys.CurrencySymbol, "₹");
        public static string CurrencyCode => GetString(AppSettingKeys.CurrencyCode, "INR");
        public static decimal DefaultTaxRate => GetDecimal(AppSettingKeys.DefaultTaxRate, 0m);
        public static bool PricesIncludeTax => GetBool(AppSettingKeys.PricesIncludeTax, false);

        public static string Money(decimal amount)
            => CurrencySymbol + amount.ToString("N2", CultureInfo.InvariantCulture);

        // --- Alerts: one source of truth, replacing 30/90 divergence ---
        public static int DefaultExpiryAlertDays => GetInt(AppSettingKeys.DefaultExpiryAlertDays, 30);
        public static int DefaultLowStockThreshold => GetInt(AppSettingKeys.DefaultLowStockThreshold, 5);
        public static int RecycleBinRetentionDays => GetInt(AppSettingKeys.RecycleBinRetentionDays, 30);

        // --- Invoice content toggles ---
        public static bool InvoiceShow(string key, bool fallback = true) => GetBool(key, fallback);

        public static string InvoiceTitle => GetString(AppSettingKeys.InvoiceTitle, "INVOICE");
        public static string InvoiceFooterText => GetString(AppSettingKeys.InvoiceFooterText, "Thank you for your business!");
        public static string InvoicePaymentTerms => GetString(AppSettingKeys.InvoicePaymentTerms, "Due on receipt|Net 7|Net 15|Net 30");

        public static string[] PaymentTermPresets()
            => InvoicePaymentTerms.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // --- Layout ---
        public static string InvoicePageSize => GetString(AppSettingKeys.InvoicePageSize, "A4");
        public static bool InvoiceLandscape =>
            string.Equals(GetString(AppSettingKeys.InvoiceOrientation, "Portrait"), "Landscape", StringComparison.OrdinalIgnoreCase);
        public static decimal InvoiceMarginMm => GetDecimal(AppSettingKeys.InvoiceMarginMm, 10m);
        public static string InvoiceFontFamily => GetString(AppSettingKeys.InvoiceFontFamily, "Segoe UI");
        public static decimal InvoiceBaseFontSize => GetDecimal(AppSettingKeys.InvoiceBaseFontSize, 10m);
        public static string InvoiceAccentColor => GetString(AppSettingKeys.InvoiceAccentColor, "#007ACC");
        public static string InvoiceTemplate => GetString(AppSettingKeys.InvoiceTemplate, "Classic");

        // --- AI ---
        public static string AiBaseUrl => GetString(AppSettingKeys.AiBaseUrl, "https://generativelanguage.googleapis.com/v1beta/models/");
        public static string AiModel => GetString(AppSettingKeys.AiModel, "gemini-1.5-flash");

        // --- Sync & Online Database ---
        public static bool SyncEnabled => GetBool(AppSettingKeys.SyncEnabled, false);
        public static string SyncServerUrl => GetString(AppSettingKeys.SyncServerUrl);
        public static string SyncConnectionString => GetString(AppSettingKeys.SyncConnectionString);
        public static string SyncDbProvider => GetString(AppSettingKeys.SyncDbProvider, "PostgreSQL");
        public static bool SyncAutoMigrate => GetBool(AppSettingKeys.SyncAutoMigrate, true);
        public static int SyncIntervalMinutes => GetInt(AppSettingKeys.SyncIntervalMinutes, 15);
        public static bool SyncOnStartup => GetBool(AppSettingKeys.SyncOnStartup, true);
        public static string LastSyncAtUtc => GetString(AppSettingKeys.LastSyncAtUtc);

        /// <summary>
        /// Deletes the rows for the given keys so the next <see cref="SeedDefaults"/> call
        /// restores factory values. Used by the "Reset This Tab" action.
        /// </summary>
        public static void ResetKeys(IEnumerable<string> keys)
        {
            var set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            using (var db = new AppDbContext())
            {
                var rows = db.AppSettings.Where(s => set.Contains(s.Key)).ToList();
                if (rows.Count > 0)
                {
                    db.AppSettings.RemoveRange(rows);
                    db.SaveChanges();
                }
            }
            Invalidate();
        }

        /// <summary>Writes one value and invalidates the cache.</summary>
        public static void Set(string key, string? value)
        {
            using (var db = new AppDbContext())
            {
                var row = db.AppSettings.FirstOrDefault(s => s.Key == key);
                if (row == null)
                {
                    db.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    row.Value = value;
                    row.UpdatedAt = DateTime.UtcNow;
                }
                db.SaveChanges();
            }
            Invalidate();
        }

        /// <summary>Writes many values in one transaction.</summary>
        public static void SetMany(IEnumerable<KeyValuePair<string, string?>> values)
        {
            using (var db = new AppDbContext())
            {
                foreach (var kv in values)
                {
                    var row = db.AppSettings.FirstOrDefault(s => s.Key == kv.Key);
                    if (row == null)
                        db.AppSettings.Add(new AppSetting { Key = kv.Key, Value = kv.Value, UpdatedAt = DateTime.UtcNow });
                    else
                    {
                        row.Value = kv.Value;
                        row.UpdatedAt = DateTime.UtcNow;
                    }
                }
                db.SaveChanges();
            }
            Invalidate();
        }

        /// <summary>
        /// Seeds any setting that has no row yet. Safe to call on every startup;
        /// existing user values are never overwritten.
        /// </summary>
        public static void SeedDefaults()
        {
            var defaults = new Dictionary<string, string?>
            {
                [AppSettingKeys.CompanyName] = "Your Company",
                [AppSettingKeys.CompanyTaxLabel] = "GST",
                [AppSettingKeys.CurrencySymbol] = "₹",
                [AppSettingKeys.CurrencyCode] = "INR",
                [AppSettingKeys.DefaultTaxRate] = "0",
                [AppSettingKeys.PricesIncludeTax] = "false",
                [AppSettingKeys.DateFormat] = "dd/MM/yyyy",
                [AppSettingKeys.DateTimeFormat] = "dd/MM/yyyy HH:mm",
                [AppSettingKeys.WeekStartsOn] = "Sunday",

                [AppSettingKeys.InvoicePrefix] = "INV-",
                [AppSettingKeys.InvoiceNextNumber] = "1",
                [AppSettingKeys.InvoiceNumberPadding] = "4",

                [AppSettingKeys.InvoiceTitle] = "INVOICE",
                [AppSettingKeys.InvoiceFooterText] = "Thank you for your business!",
                [AppSettingKeys.InvoicePaymentTerms] = "Due on receipt|Net 7|Net 15|Net 30",
                [AppSettingKeys.InvoiceLateFeePercent] = "0",
                [AppSettingKeys.InvoiceSignatureLabel] = "Authorized Signature",

                [AppSettingKeys.InvoiceShowLogo] = "true",
                [AppSettingKeys.InvoiceShowCompanyAddress] = "true",
                [AppSettingKeys.InvoiceShowCompanyPhone] = "true",
                [AppSettingKeys.InvoiceShowCompanyEmail] = "true",
                [AppSettingKeys.InvoiceShowCompanyTaxNumber] = "true",
                [AppSettingKeys.InvoiceShowCustomerAddress] = "true",
                [AppSettingKeys.InvoiceShowCustomerPhone] = "true",
                [AppSettingKeys.InvoiceShowCustomerTaxNumber] = "true",
                [AppSettingKeys.InvoiceShowBillTo] = "false",
                [AppSettingKeys.InvoiceShowShipTo] = "false",
                [AppSettingKeys.InvoiceShowOrderRef] = "false",
                [AppSettingKeys.InvoiceShowDueDate] = "true",
                [AppSettingKeys.InvoiceShowBatch] = "true",
                [AppSettingKeys.InvoiceShowExpiry] = "true",
                [AppSettingKeys.InvoiceShowScheme] = "true",
                [AppSettingKeys.InvoiceShowPack] = "true",
                [AppSettingKeys.InvoiceShowMrp] = "true",
                [AppSettingKeys.InvoiceShowItemCode] = "true",
                [AppSettingKeys.InvoiceShowTaxColumn] = "false",
                [AppSettingKeys.InvoiceShowTaxAmount] = "true",
                [AppSettingKeys.InvoiceShowDiscount] = "true",
                [AppSettingKeys.InvoiceShowBackDues] = "true",
                [AppSettingKeys.InvoiceShowRoundOff] = "true",
                [AppSettingKeys.InvoiceShowAmountInWords] = "false",
                [AppSettingKeys.InvoiceShowSignature] = "true",
                [AppSettingKeys.InvoiceShowQrCode] = "false",

                [AppSettingKeys.InvoicePageSize] = "A4",
                [AppSettingKeys.InvoiceOrientation] = "Portrait",
                [AppSettingKeys.InvoiceMarginMm] = "10",
                [AppSettingKeys.InvoiceFontFamily] = "Segoe UI",
                [AppSettingKeys.InvoiceBaseFontSize] = "10",
                [AppSettingKeys.InvoiceAccentColor] = "#007ACC",
                [AppSettingKeys.InvoiceTemplate] = "Classic",

                // One value for both previously-conflicting 30 and 90 defaults.
                [AppSettingKeys.DefaultExpiryAlertDays] = "30",
                [AppSettingKeys.DefaultLowStockThreshold] = "5",
                [AppSettingKeys.AlertsShowOnStartup] = "true",
                [AppSettingKeys.RecycleBinRetentionDays] = "30",

                [AppSettingKeys.AiApiKey] = "",  // empty until user enters key in Settings → AI
                [AppSettingKeys.AiBaseUrl] = "https://generativelanguage.googleapis.com/v1beta/models/",
                [AppSettingKeys.AiModel] = "gemini-1.5-flash",
                [AppSettingKeys.AiTemperature] = "0.7",

                [AppSettingKeys.AutoBackupEnabled] = "false",
                [AppSettingKeys.AutoBackupIntervalHours] = "24",
                [AppSettingKeys.AutoBackupKeepCount] = "7",

                [AppSettingKeys.SyncEnabled] = "false",
                [AppSettingKeys.SyncDbProvider] = "PostgreSQL",
                [AppSettingKeys.SyncConnectionString] = "",
                [AppSettingKeys.SyncAutoMigrate] = "true",
                [AppSettingKeys.SyncIntervalMinutes] = "15",
                [AppSettingKeys.SyncOnStartup] = "true",

                [AppSettingKeys.AppTheme] = "Light",
                [AppSettingKeys.ConfirmOnDelete] = "true",
                [AppSettingKeys.AutoSaveSeconds] = "0",
                [AppSettingKeys.WhatsAppCountryCode] = "91",
            };

            var existing = Cache;
            var missing = defaults.Where(d => !existing.ContainsKey(d.Key)).ToList();
            if (missing.Count > 0) SetMany(missing);
        }
    }
}
