using System;

namespace BillingSuite.App.Models
{
    /// <summary>
    /// One user-configurable setting, stored as a key/value row so new settings
    /// need no migration. Keys are defined in <see cref="AppSettingKeys"/>.
    /// </summary>
    public class AppSetting
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Canonical setting keys. The comments record where each value was previously
    /// hardcoded, so the migration is auditable.
    /// </summary>
    public static class AppSettingKeys
    {
        // --- Company identity ---
        // Previously hardcoded "Your Company"/"Your Address"/"Your GST" in
        // InvoicePreviewForm.cs:111, :368-369, Form1.cs:3253-3254 and
        // InvoiceEditForm.cs:3069-3073, :3144-3148.
        public const string CompanyName = "company.name";
        public const string CompanyAddress1 = "company.address1";
        public const string CompanyAddress2 = "company.address2";
        public const string CompanyCity = "company.city";
        public const string CompanyState = "company.state";
        public const string CompanyPostalCode = "company.postalCode";
        public const string CompanyCountry = "company.country";
        public const string CompanyPhone = "company.phone";
        public const string CompanyEmail = "company.email";
        public const string CompanyWebsite = "company.website";
        public const string CompanyTaxNumber = "company.taxNumber";   // GST / NTN / VAT
        public const string CompanyTaxLabel = "company.taxLabel";     // "GST", "NTN", "VAT"
        public const string CompanyLogoPath = "company.logoPath";

        // --- Tax & currency ---
        // Previously a literal "₹" at InvoiceEditForm.cs:3108, :3136, :3189 and an
        // estimated rate at ReportsForm.cs:427/429.
        public const string CurrencySymbol = "finance.currencySymbol";
        public const string CurrencyCode = "finance.currencyCode";
        public const string DefaultTaxRate = "finance.defaultTaxRate";
        public const string PricesIncludeTax = "finance.pricesIncludeTax";
        public const string CurrencyFormat = "finance.currencyFormat";

        // --- Invoice numbering ---
        // Previously a prefix literal at InvoiceEditForm.cs:2121.
        public const string InvoicePrefix = "invoice.numberPrefix";
        public const string InvoiceNextNumber = "invoice.nextNumber";
        public const string InvoiceNumberPadding = "invoice.numberPadding";

        // --- Invoice content / display ---
        // Controls what appears on the invoice across editor, preview, PDF and print.
        public const string InvoiceShowLogo = "invoice.show.logo";
        public const string InvoiceShowCompanyAddress = "invoice.show.companyAddress";
        public const string InvoiceShowCompanyPhone = "invoice.show.companyPhone";
        public const string InvoiceShowCompanyEmail = "invoice.show.companyEmail";
        public const string InvoiceShowCompanyTaxNumber = "invoice.show.companyTaxNumber";
        public const string InvoiceShowCustomerAddress = "invoice.show.customerAddress";
        public const string InvoiceShowCustomerPhone = "invoice.show.customerPhone";
        public const string InvoiceShowCustomerTaxNumber = "invoice.show.customerTaxNumber";
        public const string InvoiceShowBillTo = "invoice.show.billTo";
        public const string InvoiceShowShipTo = "invoice.show.shipTo";
        public const string InvoiceShowOrderRef = "invoice.show.orderRef";
        public const string InvoiceShowDueDate = "invoice.show.dueDate";
        public const string InvoiceShowBatch = "invoice.show.batch";
        public const string InvoiceShowExpiry = "invoice.show.expiry";
        public const string InvoiceShowScheme = "invoice.show.scheme";
        public const string InvoiceShowPack = "invoice.show.pack";
        public const string InvoiceShowMrp = "invoice.show.mrp";
        public const string InvoiceShowItemCode = "invoice.show.itemCode";
        public const string InvoiceShowTaxColumn = "invoice.show.taxColumn";
        public const string InvoiceShowTaxAmount = "invoice.show.taxAmount";
        public const string InvoiceShowDiscount = "invoice.show.discount";
        public const string InvoiceShowBackDues = "invoice.show.backDues";
        public const string InvoiceShowRoundOff = "invoice.show.roundOff";
        public const string InvoiceShowAmountInWords = "invoice.show.amountInWords";
        public const string InvoiceShowSignature = "invoice.show.signature";
        public const string InvoiceShowQrCode = "invoice.show.qrCode";

        // --- Invoice text content ---
        public const string InvoiceTitle = "invoice.text.title";
        public const string InvoiceFooterText = "invoice.text.footer";
        public const string InvoiceTermsText = "invoice.text.terms";
        public const string InvoiceSignatureLabel = "invoice.text.signatureLabel";
        public const string InvoiceLateFeePercent = "invoice.lateFeePercent";
        public const string InvoicePaymentTerms = "invoice.paymentTerms"; // pipe-separated presets

        // --- Invoice layout ---
        // The render paths currently disagree: A4 hardcoded at InvoicePreviewForm.cs:99
        // and PageSizes.A4 at :360-361.
        public const string InvoicePageSize = "invoice.layout.pageSize"; // A4, Letter, A5, Thermal80
        public const string InvoiceOrientation = "invoice.layout.orientation";
        public const string InvoiceMarginMm = "invoice.layout.marginMm";
        public const string InvoiceFontFamily = "invoice.layout.fontFamily";
        public const string InvoiceBaseFontSize = "invoice.layout.baseFontSize";
        public const string InvoiceAccentColor = "invoice.layout.accentColor";
        public const string InvoiceTemplate = "invoice.layout.template"; // Classic, Compact, Wide

        // --- Alerts ---
        // Currently contradictory: 30 in AlertsForm.cs:46 / Form1.cs:112,755;
        // 90 in Product.cs:38 / Form1.cs:773,2158.
        public const string DefaultExpiryAlertDays = "alerts.defaultExpiryAlertDays";
        public const string DefaultLowStockThreshold = "alerts.defaultLowStockThreshold";
        public const string AlertsShowOnStartup = "alerts.showOnStartup";
        public const string RecycleBinRetentionDays = "alerts.recycleBinRetentionDays";

        // --- Dates & formats ---
        public const string DateFormat = "format.date";
        public const string DateTimeFormat = "format.dateTime";
        public const string WeekStartsOn = "format.weekStartsOn";

        // --- AI ---
        // BaseUrl hardcoded at AiAgentService.cs:15; model at :140.
        public const string AiApiKey = "ai.apiKey";
        public const string AiBaseUrl = "ai.baseUrl";
        public const string AiModel = "ai.model";
        public const string AiTemperature = "ai.temperature";

        // --- Storage & backup ---
        public const string BackupFolder = "storage.backupFolder";
        public const string AutoBackupEnabled = "storage.autoBackupEnabled";
        public const string AutoBackupIntervalHours = "storage.autoBackupIntervalHours";
        public const string AutoBackupKeepCount = "storage.autoBackupKeepCount";

        // --- Sync & Online Database ---
        public const string SyncEnabled = "sync.enabled";
        public const string SyncServerUrl = "sync.serverUrl";
        public const string SyncConnectionString = "sync.connectionString";
        public const string SyncDbProvider = "sync.dbProvider"; // PostgreSQL (default)
        public const string SyncAutoMigrate = "sync.autoMigrate";
        public const string SyncIntervalMinutes = "sync.intervalMinutes";
        public const string SyncOnStartup = "sync.onStartup";
        public const string LastSyncAtUtc = "sync.lastSyncAtUtc";

        // --- App behaviour ---
        public const string AppTheme = "app.theme";
        public const string ConfirmOnDelete = "app.confirmOnDelete";
        public const string AutoSaveSeconds = "app.autoSaveSeconds";
        public const string WhatsAppCountryCode = "app.whatsappCountryCode"; // was literal "91" at InvoiceEditForm.cs:339-344
    }
}
