using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// The application's settings surface. Every value here is persisted through
    /// <see cref="AppSettingsService"/> into the AppSettings table, replacing the
    /// hardcoded literals that were previously spread across the forms.
    /// </summary>
    public class SettingsForm : Form
    {
        private TabControl _tabs = new();
        private Button _btnSave = new();
        private Button _btnCancel = new();
        private Button _btnResetTab = new();
        private Label _lblStatus = new();
        private Panel _pageHost = new();

        // Key -> editor control, so Save can read every tab in one pass.
        private readonly Dictionary<string, Func<string?>> _readers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Action<string?>> _writers = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _tabKeys = new();

        private static readonly Color Accent = Color.FromArgb(0, 122, 204);
        private static readonly Font HeaderFont = new("Segoe UI", 10F, FontStyle.Bold);
        private static readonly Font BodyFont = new("Segoe UI", 9F);

        public SettingsForm()
        {
            InitializeComponent();
            BuildTabs();
        }

        private void InitializeComponent()
        {
            Text = "Settings";
            Size = new Size(940, 700);
            MinimumSize = new Size(820, 600);
            StartPosition = FormStartPosition.CenterParent;
            Font = BodyFont;
            BackColor = Color.White;

            _tabs = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(900, 590),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = BodyFont
            };

            _lblStatus = new Label
            {
                Location = new Point(12, 612),
                Size = new Size(500, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                ForeColor = Color.DimGray,
                Text = ""
            };

            _btnSave = new Button
            {
                Text = "Save All Settings",
                Location = new Point(548, 610),
                Size = new Size(130, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnSave.Click += (s, e) => SaveAll();

            _btnResetTab = new Button
            {
                Text = "Reset This Tab",
                Location = new Point(408, 610),
                Size = new Size(130, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _btnResetTab.Click += (s, e) => ResetCurrentTab();

            _btnCancel = new Button
            {
                Text = "Close",
                Location = new Point(688, 610),
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[] { _tabs, _lblStatus, _btnResetTab, _btnSave, _btnCancel });
            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        // ---------------------------------------------------------------- layout helpers

        private Panel NewPage(string title, string description)
        {
            var page = new Panel { BackColor = Color.White, AutoScroll = true, Padding = new Padding(16) };

            var header = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Accent,
                Location = new Point(16, 12),
                AutoSize = true
            };
            page.Controls.Add(header);

            if (!string.IsNullOrEmpty(description))
            {
                page.Controls.Add(new Label
                {
                    Text = description,
                    ForeColor = Color.DimGray,
                    Location = new Point(16, 40),
                    MaximumSize = new Size(830, 0),
                    AutoSize = true
                });
            }

            _pageHost = page;
            return page;
        }

        private int _y = 76;
        private const int LabelX = 20;
        private const int FieldX = 260;
        private const int FieldW = 380;

        private void Row(Panel page, string label, Control editor, string? hint = null)
        {
            page.Controls.Add(new Label
            {
                Text = label,
                Location = new Point(LabelX, _y + 4),
                Size = new Size(FieldX - LabelX - 10, 20),
                TextAlign = ContentAlignment.MiddleLeft
            });
            editor.Location = new Point(FieldX, _y);
            editor.Width = FieldW;
            page.Controls.Add(editor);
            _y += 34;

            if (hint != null)
            {
                page.Controls.Add(new Label
                {
                    Text = hint,
                    ForeColor = Color.Gray,
                    Location = new Point(FieldX, _y - 6),
                    MaximumSize = new Size(FieldW + 120, 0),
                    AutoSize = true
                });
                _y += 22;
            }
        }

        private void SectionHeader(Panel page, string text)
        {
            _y += 8;
            page.Controls.Add(new Label
            {
                Text = text,
                Font = HeaderFont,
                ForeColor = Color.FromArgb(64, 64, 64),
                Location = new Point(LabelX, _y),
                AutoSize = true
            });
            _y += 30;
        }

        private TextBox TextRow(Panel page, string key, string label, string? hint = null, bool password = false)
        {
            var tb = new TextBox { Text = AppSettingsService.GetString(key) };
            if (password) tb.PasswordChar = '*';
            Row(page, label, tb, hint);
            _readers[key] = () => tb.Text.Trim();
            _writers[key] = v => tb.Text = v ?? "";
            _tabKeys.Add(key);
            return tb;
        }

        private NumericUpDown NumberRow(Panel page, string key, string label, decimal min, decimal max, int decimals = 0, string? hint = null)
        {
            var value = AppSettingsService.GetDecimal(key, min);
            var num = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Value = Math.Clamp(value, min, max)
            };
            Row(page, label, num, hint);
            _readers[key] = () => num.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _writers[key] = v =>
            {
                if (decimal.TryParse(v, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var d))
                    num.Value = Math.Clamp(d, min, max);
            };
            _tabKeys.Add(key);
            return num;
        }

        private CheckBox BoolRow(Panel page, string key, string label, string? hint = null)
        {
            var chk = new CheckBox
            {
                Text = label,
                Checked = AppSettingsService.GetBool(key, true),
                Location = new Point(LabelX, _y),
                Width = FieldW + 200,
                AutoSize = false,
                Height = 24
            };
            page.Controls.Add(chk);
            _y += 30;
            if (hint != null)
            {
                page.Controls.Add(new Label
                {
                    Text = hint,
                    ForeColor = Color.Gray,
                    Location = new Point(LabelX + 20, _y - 4),
                    MaximumSize = new Size(FieldW + 260, 0),
                    AutoSize = true
                });
                _y += 22;
            }
            _readers[key] = () => chk.Checked ? "true" : "false";
            _writers[key] = v => chk.Checked = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            _tabKeys.Add(key);
            return chk;
        }

        private ComboBox ComboRow(Panel page, string key, string label, string[] options, string? hint = null)
        {
            var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmb.Items.AddRange(options);
            var current = AppSettingsService.GetString(key);
            cmb.SelectedItem = options.FirstOrDefault(o => string.Equals(o, current, StringComparison.OrdinalIgnoreCase)) ?? options[0];
            Row(page, label, cmb, hint);
            _readers[key] = () => cmb.SelectedItem?.ToString() ?? options[0];
            _writers[key] = v =>
                cmb.SelectedItem = options.FirstOrDefault(o => string.Equals(o, v, StringComparison.OrdinalIgnoreCase)) ?? options[0];
            _tabKeys.Add(key);
            return cmb;
        }

        // ---------------------------------------------------------------- tabs

        private void BuildTabs()
        {
            _tabs.TabPages.Add(BuildCompanyTab());
            _tabs.TabPages.Add(BuildInvoiceContentTab());
            _tabs.TabPages.Add(BuildInvoiceLayoutTab());
            _tabs.TabPages.Add(BuildNumberingTab());
            _tabs.TabPages.Add(BuildTaxCurrencyTab());
            _tabs.TabPages.Add(BuildAlertsTab());
            _tabs.TabPages.Add(BuildAppearanceTab());
            _tabs.TabPages.Add(BuildAiTab());
            _tabs.TabPages.Add(BuildStorageTab());
            _tabs.TabPages.Add(BuildSyncTab());
            _tabs.TabPages.Add(BuildAboutTab());
        }

        private TabPage BuildCompanyTab()
        {
            var page = NewPage("Company Details",
                "These details appear on every invoice, purchase order, PDF and print output. " +
                "They previously showed as \"Your Company\" / \"Your Address\" / \"Your GST\".");
            var tp = new TabPage("Company") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            TextRow(page, AppSettingKeys.CompanyName, "Company Name");
            TextRow(page, AppSettingKeys.CompanyAddress1, "Address Line 1");
            TextRow(page, AppSettingKeys.CompanyAddress2, "Address Line 2");
            TextRow(page, AppSettingKeys.CompanyCity, "City");
            TextRow(page, AppSettingKeys.CompanyState, "State / Province");
            TextRow(page, AppSettingKeys.CompanyPostalCode, "Postal Code");
            TextRow(page, AppSettingKeys.CompanyCountry, "Country");
            SectionHeader(page, "Contact");
            TextRow(page, AppSettingKeys.CompanyPhone, "Phone");
            TextRow(page, AppSettingKeys.CompanyEmail, "Email");
            TextRow(page, AppSettingKeys.CompanyWebsite, "Website");
            SectionHeader(page, "Tax Registration");
            TextRow(page, AppSettingKeys.CompanyTaxLabel, "Tax Label", "What your tax number is called: GST, NTN, VAT, TIN.");
            TextRow(page, AppSettingKeys.CompanyTaxNumber, "Tax Registration Number");
            SectionHeader(page, "Logo");
            var logo = TextRow(page, AppSettingKeys.CompanyLogoPath, "Logo File", "PNG or JPG shown on the invoice header.");
            var browse = new Button { Text = "Browse...", Location = new Point(FieldX + FieldW + 8, _y - 34), Size = new Size(90, 24) };
            browse.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" };
                if (ofd.ShowDialog(this) == DialogResult.OK) logo.Text = ofd.FileName;
            };
            page.Controls.Add(browse);
            return tp;
        }

        private TabPage BuildInvoiceContentTab()
        {
            var page = NewPage("Invoice Content",
                "Choose exactly what appears on an invoice. These toggles apply to the editor, " +
                "the on-screen preview, the PDF and print output, so all four stay consistent.");
            var tp = new TabPage("Invoice Content") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            SectionHeader(page, "Header");
            BoolRow(page, AppSettingKeys.InvoiceShowLogo, "Show company logo");
            TextRow(page, AppSettingKeys.InvoiceTitle, "Document title", "Printed at the top, e.g. INVOICE, TAX INVOICE, BILL.");
            BoolRow(page, AppSettingKeys.InvoiceShowCompanyAddress, "Show company address");
            BoolRow(page, AppSettingKeys.InvoiceShowCompanyPhone, "Show company phone");
            BoolRow(page, AppSettingKeys.InvoiceShowCompanyEmail, "Show company email");
            BoolRow(page, AppSettingKeys.InvoiceShowCompanyTaxNumber, "Show company tax number");

            SectionHeader(page, "Customer Block");
            BoolRow(page, AppSettingKeys.InvoiceShowCustomerAddress, "Show customer address");
            BoolRow(page, AppSettingKeys.InvoiceShowCustomerPhone, "Show customer phone");
            BoolRow(page, AppSettingKeys.InvoiceShowCustomerTaxNumber, "Show customer tax number");
            BoolRow(page, AppSettingKeys.InvoiceShowBillTo, "Show Bill To block",
                "These fields are editable in the invoice editor but were previously never printed.");
            BoolRow(page, AppSettingKeys.InvoiceShowShipTo, "Show Ship To block");
            BoolRow(page, AppSettingKeys.InvoiceShowOrderRef, "Show order reference");
            BoolRow(page, AppSettingKeys.InvoiceShowDueDate, "Show due date");

            SectionHeader(page, "Item Table Columns");
            BoolRow(page, AppSettingKeys.InvoiceShowItemCode, "Show item code / barcode");
            BoolRow(page, AppSettingKeys.InvoiceShowBatch, "Show batch number");
            BoolRow(page, AppSettingKeys.InvoiceShowExpiry, "Show expiry date");
            BoolRow(page, AppSettingKeys.InvoiceShowScheme, "Show scheme");
            BoolRow(page, AppSettingKeys.InvoiceShowPack, "Show pack");
            BoolRow(page, AppSettingKeys.InvoiceShowMrp, "Show MRP");
            BoolRow(page, AppSettingKeys.InvoiceShowTaxColumn, "Show tax column");

            SectionHeader(page, "Totals & Footer");
            BoolRow(page, AppSettingKeys.InvoiceShowDiscount, "Show discount line");
            BoolRow(page, AppSettingKeys.InvoiceShowTaxAmount, "Show tax amount line");
            BoolRow(page, AppSettingKeys.InvoiceShowBackDues, "Show back dues line",
                "The HTML preview showed this but the PDF did not. Now controlled here for both.");
            BoolRow(page, AppSettingKeys.InvoiceShowRoundOff, "Show round-off line");
            BoolRow(page, AppSettingKeys.InvoiceShowAmountInWords, "Show amount in words");
            BoolRow(page, AppSettingKeys.InvoiceShowSignature, "Show signature area");
            BoolRow(page, AppSettingKeys.InvoiceShowQrCode, "Show payment QR code");

            SectionHeader(page, "Footer Text");
            TextRow(page, AppSettingKeys.InvoiceFooterText, "Footer text");
            TextRow(page, AppSettingKeys.InvoiceSignatureLabel, "Signature label");
            TextRow(page, AppSettingKeys.InvoicePaymentTerms, "Payment term presets",
                "Separate options with a pipe character, e.g. Due on receipt|Net 7|Net 30");
            NumberRow(page, AppSettingKeys.InvoiceLateFeePercent, "Late fee (%)", 0, 100, 2);
            return tp;
        }

        private TabPage BuildInvoiceLayoutTab()
        {
            var page = NewPage("Invoice Format",
                "Paper size, margins and typography. The render paths previously hardcoded A4 " +
                "in two different places; this is now the single source.");
            var tp = new TabPage("Invoice Format") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            ComboRow(page, AppSettingKeys.InvoicePageSize, "Page size",
                new[] { "A4", "Letter", "A5", "Legal", "Thermal80", "Thermal58" },
                "Thermal sizes are for receipt printers.");
            ComboRow(page, AppSettingKeys.InvoiceOrientation, "Orientation", new[] { "Portrait", "Landscape" });
            NumberRow(page, AppSettingKeys.InvoiceMarginMm, "Margin (mm)", 0, 50, 1);
            ComboRow(page, AppSettingKeys.InvoiceTemplate, "Template",
                new[] { "Classic", "Compact", "Wide" },
                "The Template dropdown in the invoice editor previously changed nothing. It is wired to this now.");
            TextRow(page, AppSettingKeys.InvoiceFontFamily, "Font family");
            NumberRow(page, AppSettingKeys.InvoiceBaseFontSize, "Base font size (pt)", 6, 20, 1);
            TextRow(page, AppSettingKeys.InvoiceAccentColor, "Accent colour", "Hex value, e.g. #007ACC");
            return tp;
        }

        private TabPage BuildNumberingTab()
        {
            var page = NewPage("Invoice Numbering",
                "Controls how the next invoice number is generated.");
            var tp = new TabPage("Numbering") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            TextRow(page, AppSettingKeys.InvoicePrefix, "Number prefix", "e.g. INV- produces INV-0001");
            NumberRow(page, AppSettingKeys.InvoiceNextNumber, "Next number", 1, 999999999);
            NumberRow(page, AppSettingKeys.InvoiceNumberPadding, "Zero padding", 0, 10);
            return tp;
        }

        private TabPage BuildTaxCurrencyTab()
        {
            var page = NewPage("Tax & Currency",
                "Currency symbol and default tax behaviour. These were previously literals " +
                "embedded in the invoice and report code.");
            var tp = new TabPage("Tax & Currency") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            TextRow(page, AppSettingKeys.CurrencySymbol, "Currency symbol");
            TextRow(page, AppSettingKeys.CurrencyCode, "Currency code", "ISO code, e.g. INR, USD, PKR.");
            NumberRow(page, AppSettingKeys.DefaultTaxRate, "Default tax rate (%)", 0, 100, 2);
            BoolRow(page, AppSettingKeys.PricesIncludeTax, "Entered prices already include tax");
            ComboRow(page, AppSettingKeys.DateFormat, "Date format",
                new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MMM-yyyy" },
                "The HTML preview and PDF previously used different formats.");
            ComboRow(page, AppSettingKeys.WeekStartsOn, "Week starts on",
                new[] { "Sunday", "Monday" },
                "Affects weekly report grouping.");
            return tp;
        }

        private TabPage BuildAlertsTab()
        {
            var page = NewPage("Alerts & Defaults",
                "Default thresholds applied to new products. These values disagreed with each " +
                "other before: some code paths used 30 days, others 90.");
            var tp = new TabPage("Alerts") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            NumberRow(page, AppSettingKeys.DefaultExpiryAlertDays, "Default expiry alert (days)", 1, 365,
                0, "Used when a product is created or imported without its own value.");
            NumberRow(page, AppSettingKeys.DefaultLowStockThreshold, "Default low stock threshold", 0, 100000);
            BoolRow(page, AppSettingKeys.AlertsShowOnStartup, "Show alerts on startup");
            NumberRow(page, AppSettingKeys.RecycleBinRetentionDays, "Recycle bin retention (days)", 1, 3650,
                0, "The UI previously promised automatic purging, but no purge routine existed.");
            return tp;
        }

        private TabPage BuildAppearanceTab()
        {
            var page = NewPage("Appearance & Behaviour",
                "Application-wide behaviour and regional settings. These were previously hardcoded " +
                "throughout the source and could not be changed without editing the code.");
            var tp = new TabPage("Appearance") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            ComboRow(page, AppSettingKeys.AppTheme, "Theme",
                new[] { "Light", "Dark" },
                "Dark mode requires an app restart to apply fully.");
            BoolRow(page, AppSettingKeys.ConfirmOnDelete, "Ask for confirmation before deleting",
                "Shows a Yes/No dialog before permanently deleting any record.");
            NumberRow(page, AppSettingKeys.AutoSaveSeconds, "Auto-save interval (seconds)", 0, 600, 0,
                "How often the invoice editor auto-saves a draft. 0 = disabled.");
            SectionHeader(page, "Regional");
            TextRow(page, AppSettingKeys.WhatsAppCountryCode, "WhatsApp country code",
                "Numeric country code without '+', e.g. 91 for India, 92 for Pakistan.");
            ComboRow(page, AppSettingKeys.DateTimeFormat, "Date-time format",
                new[] { "dd/MM/yyyy HH:mm", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "dd-MMM-yyyy HH:mm" },
                "Used in date-time columns and printed on documents.");
            return tp;
        }

        private TabPage BuildAiTab()
        {
            var page = NewPage("AI Assistant",
                "Connection details for the AI features. The API key is stored in the settings " +
                "database (not beside the program) so it survives an install or upgrade.");
            var tp = new TabPage("AI") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            // Read from DB first; fall back to the JSON file via AiAgentService for existing installs
            var existingKey = AppSettingsService.GetString(AppSettingKeys.AiApiKey)
                              ?? new AiAgentService().GetApiKey()
                              ?? "";
            var keyBox = new TextBox { Text = existingKey, PasswordChar = '*' };
            Row(page, "Gemini API key", keyBox, "Get a key from Google AI Studio (aistudio.google.com).");
            _readers[AppSettingKeys.AiApiKey] = () => keyBox.Text.Trim();
            _writers[AppSettingKeys.AiApiKey] = v => keyBox.Text = v ?? "";

            TextRow(page, AppSettingKeys.AiBaseUrl, "API base URL",
                "Previously hardcoded. Change only if you use a proxy or compatible endpoint.");
            TextRow(page, AppSettingKeys.AiModel, "Model", "e.g. gemini-1.5-flash, gemini-1.5-pro");
            return tp;
        }

        private TabPage BuildStorageTab()
        {
            var page = NewPage("Storage & Backup",
                "Where your data lives and how backups are taken.");
            var tp = new TabPage("Storage") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            page.Controls.Add(new Label
            {
                Text = "Database location:",
                Location = new Point(20, _y + 4),
                Size = new Size(240, 20)
            });
            page.Controls.Add(new Label
            {
                Text = AppPaths.DatabaseFile,
                Location = new Point(260, _y + 4),
                Size = new Size(600, 20),
                ForeColor = Color.DimGray
            });
            _y += 34;

            BoolRow(page, AppSettingKeys.AutoBackupEnabled, "Enable automatic backups");
            NumberRow(page, AppSettingKeys.AutoBackupIntervalHours, "Backup every (hours)", 1, 720);
            NumberRow(page, AppSettingKeys.AutoBackupKeepCount, "Keep most recent backups", 1, 365);

            _y += 10;
            var btnBackup = new Button { Text = "Back Up Now", Location = new Point(20, _y), Size = new Size(120, 30) };
            btnBackup.Click += (s, e) => { DatabaseUtils.BackupDatabase(); };
            page.Controls.Add(btnBackup);

            var btnRestore = new Button { Text = "Restore From Backup", Location = new Point(150, _y), Size = new Size(150, 30) };
            btnRestore.Click += (s, e) => { DatabaseUtils.RestoreDatabase(); };
            page.Controls.Add(btnRestore);
            return tp;
        }

        private TabPage BuildSyncTab()
        {
            var page = NewPage("Cloud Backup & Sync",
                "Your computer is the source of truth and operates 100% offline with zero latency. " +
                "When connected to the internet, your business records are automatically synchronized and backed up to the cloud.");
            var tp = new TabPage("Cloud Sync") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            BoolRow(page, AppSettingKeys.SyncEnabled, "Enable automatic cloud sync & backup",
                "Keep your local records backed up to the cloud in the background.");
            NumberRow(page, AppSettingKeys.SyncIntervalMinutes, "Sync frequency (minutes)", 1, 1440, 15,
                "How often the app checks for changes and syncs with the cloud when online.");

            SectionHeader(page, "Cloud Sync Actions");

            // Buttons: Sync Now, Restore, and 24/7 Background Task
            var btnSyncNow = new Button
            {
                Text = "🔄 Sync Now",
                Location = new Point(20, _y),
                Size = new Size(140, 34),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            var btnRestore = new Button
            {
                Text = "⬇ Restore My Data from Cloud",
                Location = new Point(170, _y),
                Size = new Size(210, 34),
                FlatStyle = FlatStyle.Flat
            };
            var btnScheduleTask = new Button
            {
                Text = "⚡ Enable 24/7 Background Sync",
                Location = new Point(390, _y),
                Size = new Size(220, 34),
                FlatStyle = FlatStyle.Flat
            };

            btnSyncNow.Click += async (s, e) =>
            {
                btnSyncNow.Enabled = false;
                var (ok, msg) = await SyncService.SyncNowAsync();
                btnSyncNow.Enabled = true;
                MessageBox.Show(msg, "Cloud Sync", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            };

            btnRestore.Click += async (s, e) =>
            {
                var connStr = CloudDbConfig.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                    connStr = AppSettingsService.SyncConnectionString;

                if (string.IsNullOrWhiteSpace(connStr))
                {
                    MessageBox.Show("Cloud backend is not configured by the administrator yet.", "Cloud Restore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("Restore your business records from the cloud? This will update your local database with your latest cloud data.",
                        "Confirm Cloud Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                btnRestore.Enabled = false;
                var (ok, msg) = await OnlineDatabaseService.PullFromCloudAsync(connStr, AuthService.CurrentUser?.Id);
                btnRestore.Enabled = true;
                MessageBox.Show(msg, "Cloud Restore", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            };

            btnScheduleTask.Click += (s, e) =>
            {
                var (ok, msg) = SyncService.RegisterWindowsScheduledTask();
                MessageBox.Show(msg, "24/7 Background Sync", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            };

            page.Controls.Add(btnSyncNow);
            page.Controls.Add(btnRestore);
            page.Controls.Add(btnScheduleTask);
            _y += 46;

            SectionHeader(page, "Live Status");

            // Live status display
            var netAvailable = SyncService.IsNetworkAvailable();
            var lblNet = new Label
            {
                Text = netAvailable ? "🟢 Network: Online (Connected to internet)" : "🟠 Network: Offline (Local database active, changes will sync when reconnected)",
                ForeColor = netAvailable ? Color.SeaGreen : Color.DarkOrange,
                Location = new Point(20, _y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            page.Controls.Add(lblNet);
            _y += 24;

            var isConfigured = CloudDbConfig.IsConfigured || !string.IsNullOrWhiteSpace(AppSettingsService.SyncConnectionString);
            var lblBackend = new Label
            {
                Text = isConfigured ? "🟢 Cloud Backend: Configured & Ready" : "⚪ Cloud Backend: Pending Configuration",
                ForeColor = isConfigured ? Color.SeaGreen : Color.DimGray,
                Location = new Point(20, _y),
                AutoSize = true
            };
            page.Controls.Add(lblBackend);
            _y += 22;

            var last = AppSettingsService.LastSyncAtUtc;
            var lblLast = new Label
            {
                Text = string.IsNullOrWhiteSpace(last) ? "Last successful cloud backup: Never" : $"Last successful cloud backup: {last}",
                ForeColor = Color.DimGray,
                Location = new Point(20, _y),
                AutoSize = true
            };
            page.Controls.Add(lblLast);
            _y += 22;

            var lblSyncStatus = new Label
            {
                Text = $"Current sync status: {SyncService.LastStatus}",
                ForeColor = Color.DarkSlateBlue,
                Location = new Point(20, _y),
                AutoSize = true
            };
            page.Controls.Add(lblSyncStatus);

            return tp;
        }

        private TabPage BuildAboutTab()
        {
            var page = NewPage("About Billease Pro", "Application information, developer credits, and user documentation.");
            var tp = new TabPage("About") { BackColor = Color.White };
            tp.Controls.Add(page); page.Dock = DockStyle.Fill;

            _y = 76;
            var asm = typeof(SettingsForm).Assembly.GetName();
            void Info(string label, string value)
            {
                page.Controls.Add(new Label { Text = label, Location = new Point(20, _y), Size = new Size(180, 20), ForeColor = Color.DimGray });
                page.Controls.Add(new Label { Text = value, Location = new Point(210, _y), Size = new Size(640, 20), Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
                _y += 28;
            }

            void LinkInfo(string label, string text, string url)
            {
                page.Controls.Add(new Label { Text = label, Location = new Point(20, _y), Size = new Size(180, 20), ForeColor = Color.DimGray });
                var link = new LinkLabel
                {
                    Text = text,
                    Location = new Point(210, _y),
                    Size = new Size(640, 20),
                    LinkColor = Accent,
                    ActiveLinkColor = Color.Navy
                };
                link.LinkClicked += (s, e) =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
                    catch { }
                };
                page.Controls.Add(link);
                _y += 28;
            }

            Info("Application", "Billease Pro (Enterprise Billing & ERP)");
            Info("Version", asm.Version?.ToString() ?? "1.0.0");
            Info("Engineered By", "NexaAutomate Studio");
            LinkInfo("GitHub Developer", "github.com/thevirajdev", "https://github.com/thevirajdev");
            LinkInfo("GitHub Repository", "github.com/thevirajdev/Billease-pro", "https://github.com/thevirajdev/Billease-pro");
            LinkInfo("Social Profile", "@thevirajrealm (Twitter / X & Instagram)", "https://x.com/thevirajrealm");

            SectionHeader(page, "Local Storage");
            Info("Data folder", AppPaths.DataFolder);
            Info("Database", AppPaths.DatabaseFile);
            Info("Settings file", AppPaths.SettingsFile);

            _y += 10;
            var btnManual = new Button
            {
                Text = "📖 Open User Manual & Documentation Guide",
                Location = new Point(20, _y),
                Size = new Size(320, 36),
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnManual.Click += (s, e) =>
            {
                new HelpManualForm().ShowDialog(this);
            };
            page.Controls.Add(btnManual);

            return tp;
        }

        // ---------------------------------------------------------------- actions

        private void SaveAll()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var values = _readers.ToDictionary(kv => kv.Key, kv => kv.Value());

                // All settings — including the AI API key — are now stored in the DB.
                // AiAgentService.GetApiKey() reads AppSettingKeys.AiApiKey from the DB first.
                AppSettingsService.SetMany(values);
                AppSettingsService.Invalidate();
                _lblStatus.Text = $"Settings saved at {DateTime.Now:HH:mm:ss}.";
                _lblStatus.ForeColor = Color.SeaGreen;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Save failed.";
                _lblStatus.ForeColor = Color.Firebrick;
                MessageBox.Show($"Could not save settings:\n\n{ex.Message}", "Settings Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ResetCurrentTab()
        {
            var tabName = _tabs.SelectedTab?.Text ?? "this";
            if (MessageBox.Show(
                    $"Reset all settings on the \"{tabName}\" tab to their defaults?\n\n" +
                    "This only affects the current tab and takes effect when you save.",
                    "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // Re-running the seed logic writes defaults only where no row exists, so clear
            // the rows for this tab's keys first, then re-seed.
            var keysOnTab = _tabKeys.ToList();
            AppSettingsService.ResetKeys(keysOnTab);
            AppSettingsService.SeedDefaults();
            AppSettingsService.Invalidate();

            _tabs.TabPages.Clear();
            _readers.Clear();
            _writers.Clear();
            _tabKeys.Clear();
            BuildTabs();
            _lblStatus.Text = $"\"{tabName}\" reset to defaults.";
            _lblStatus.ForeColor = Color.DarkOrange;
        }
    }
}
