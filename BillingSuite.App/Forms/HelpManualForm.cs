using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Professional In-App User Manual and Documentation Guide for Billease Pro.
    /// Provides comprehensive operational instructions, shortcut references,
    /// cloud sync guides, and official NexaAutomate developer branding.
    /// </summary>
    public class HelpManualForm : Form
    {
        private ListBox _navMenu = new();
        private Panel _contentPanel = new();
        private TextBox _searchBox = new();
        private Label _titleLabel = new();
        private RichTextBox _rtbContent = new();
        private Panel _headerPanel = new();

        private static readonly Color Primary = Color.FromArgb(15, 23, 42);       // #0F172A Slate 900
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);       // #2563EB Royal Blue
        private static readonly Color BgSide = Color.FromArgb(248, 250, 252);     // #F8FAFC Slate 50
        private static readonly Color BorderColor = Color.FromArgb(226, 232, 240); // #E2E8F0 Slate 200

        private class ManualTopic
        {
            public string Title { get; set; } = "";
            public string Category { get; set; } = "";
            public string RtfOrTextContent { get; set; } = "";
        }

        private readonly List<ManualTopic> _allTopics = new();

        public HelpManualForm(string? initialTopic = null)
        {
            InitializeComponent();
            LoadManualContent();
            PopulateNavigation();

            if (!string.IsNullOrEmpty(initialTopic))
            {
                for (int i = 0; i < _navMenu.Items.Count; i++)
                {
                    if (_navMenu.Items[i].ToString()?.IndexOf(initialTopic, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _navMenu.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (_navMenu.Items.Count > 0)
            {
                _navMenu.SelectedIndex = 0;
            }
        }

        private void InitializeComponent()
        {
            Text = "Billease Pro - Official User Manual & Operations Guide";
            Size = new Size(1100, 750);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            BackColor = Color.White;

            // 1. Top Header Bar
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Primary,
                Padding = new Padding(20, 10, 20, 10)
            };

            var appTitle = new Label
            {
                Text = "⚡ Billease Pro — Knowledge Base & User Manual",
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 12),
                AutoSize = true
            };

            var brandingLabel = new Label
            {
                Text = "Engineered by NexaAutomate  •  GitHub: thevirajdev  •  @thevirajrealm",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(22, 38),
                AutoSize = true
            };

            var btnClose = new Button
            {
                Text = "Close Guide",
                Size = new Size(100, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(970, 18),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();

            _headerPanel.Controls.AddRange(new Control[] { appTitle, brandingLabel, btnClose });

            // 2. Left Sidebar (Navigation & Search)
            var sidePanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 280,
                BackColor = BgSide,
                Padding = new Padding(12)
            };

            var sideHeader = new Label
            {
                Text = "DOCUMENTATION TOPICS",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(14, 14),
                AutoSize = true
            };

            _searchBox = new TextBox
            {
                Location = new Point(14, 38),
                Width = 250,
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = "🔍 Search guide topics..."
            };
            _searchBox.TextChanged += (s, e) => FilterTopics(_searchBox.Text.Trim());

            _navMenu = new ListBox
            {
                Location = new Point(14, 74),
                Size = new Size(250, 580),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.None,
                BackColor = BgSide,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ItemHeight = 32,
                DrawMode = DrawMode.OwnerDrawFixed
            };
            _navMenu.DrawItem += NavMenu_DrawItem;
            _navMenu.SelectedIndexChanged += NavMenu_SelectedIndexChanged;

            sidePanel.Controls.AddRange(new Control[] { sideHeader, _searchBox, _navMenu });

            // Separator between sidebar and content
            var splitBorder = new Panel
            {
                Dock = DockStyle.Left,
                Width = 1,
                BackColor = BorderColor
            };

            // 3. Right Content Area
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24)
            };

            var contentHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White
            };

            _titleLabel = new Label
            {
                Text = "Getting Started with Billease Pro",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Primary,
                Location = new Point(4, 10),
                AutoSize = true
            };
            contentHeader.Controls.Add(_titleLabel);

            _rtbContent = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 10.5F),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            _contentPanel.Controls.Add(_rtbContent);
            _contentPanel.Controls.Add(contentHeader);

            // Assembly controls
            Controls.Add(_contentPanel);
            Controls.Add(splitBorder);
            Controls.Add(sidePanel);
            Controls.Add(_headerPanel);
        }

        private void NavMenu_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _navMenu.Items.Count) return;
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            var bg = isSelected ? Color.FromArgb(239, 246, 255) : BgSide;
            var fg = isSelected ? Accent : Color.FromArgb(30, 41, 59);

            using var brushBg = new SolidBrush(bg);
            e.Graphics.FillRectangle(brushBg, e.Bounds);

            if (isSelected)
            {
                using var barBrush = new SolidBrush(Accent);
                e.Graphics.FillRectangle(barBrush, e.Bounds.X, e.Bounds.Y + 2, 4, e.Bounds.Height - 4);
            }

            var itemText = _navMenu.Items[e.Index].ToString() ?? "";
            using var brushFg = new SolidBrush(fg);
            using var font = new Font(_navMenu.Font.FontFamily, 9.5F, isSelected ? FontStyle.Bold : FontStyle.Regular);
            e.Graphics.DrawString(itemText, font, brushFg, e.Bounds.X + 12, e.Bounds.Y + 6);
        }

        private void NavMenu_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_navMenu.SelectedItem == null) return;
            var title = _navMenu.SelectedItem.ToString();
            var topic = _allTopics.Find(t => t.Title == title);
            if (topic != null)
            {
                _titleLabel.Text = topic.Title;
                DisplayTopicContent(topic.RtfOrTextContent);
            }
        }

        private void DisplayTopicContent(string text)
        {
            _rtbContent.Clear();
            _rtbContent.Text = text;

            // Highlight headings and sections in RichTextBox
            HighlightSections();
            _rtbContent.SelectionStart = 0;
            _rtbContent.ScrollToCaret();
        }

        private void HighlightSections()
        {
            string[] lines = _rtbContent.Lines;
            int charIndex = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("## ") || line.StartsWith("### ") || line.StartsWith("⭐ ") || line.StartsWith("💡 ") || line.StartsWith("🛡️ "))
                {
                    _rtbContent.Select(charIndex, line.Length);
                    _rtbContent.SelectionFont = new Font("Segoe UI", 11.5F, FontStyle.Bold);
                    _rtbContent.SelectionColor = Primary;
                }
                else if (line.StartsWith("● ") || line.StartsWith("▶ ") || line.StartsWith("✔ "))
                {
                    _rtbContent.Select(charIndex, line.Length);
                    _rtbContent.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
                    _rtbContent.SelectionColor = Accent;
                }
                else if (line.Contains("[F1]") || line.Contains("[F2]") || line.Contains("[F3]") || line.Contains("[F4]") || line.Contains("[Ctrl+P]"))
                {
                    _rtbContent.Select(charIndex, line.Length);
                    _rtbContent.SelectionFont = new Font("Segoe UI Semibold", 10F, FontStyle.Regular);
                }

                charIndex += line.Length + 1; // +1 for newline
            }
        }

        private void FilterTopics(string filter)
        {
            _navMenu.BeginUpdate();
            _navMenu.Items.Clear();

            foreach (var t in _allTopics)
            {
                if (string.IsNullOrWhiteSpace(filter) ||
                    t.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.RtfOrTextContent.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _navMenu.Items.Add(t.Title);
                }
            }

            _navMenu.EndUpdate();
            if (_navMenu.Items.Count > 0)
                _navMenu.SelectedIndex = 0;
        }

        private void PopulateNavigation()
        {
            _navMenu.BeginUpdate();
            _navMenu.Items.Clear();
            foreach (var topic in _allTopics)
            {
                _navMenu.Items.Add(topic.Title);
            }
            _navMenu.EndUpdate();
        }

        private void LoadManualContent()
        {
            _allTopics.Add(new ManualTopic
            {
                Title = "1. Introduction & Overview",
                Category = "Basics",
                RtfOrTextContent =
@"## Billease Pro — Complete Business Billing & Enterprise ERP

Billease Pro is an ultra-fast, offline-first billing, stock inventory, and khata management suite designed for retail stores, wholesalers, distributors, pharmacies, and service businesses.

⭐ Key Highlights:
● 100% Offline-First Architecture:
  Your local computer is the supreme source of truth. Billease Pro never pauses or lags when the internet drops. Full local SQLite engine guarantees instant checkout and printing.

● Automated Cloud Sync & Backup:
  When connected to the internet, business data securely syncs to the high-performance PostgreSQL/Supabase session pooler cloud vault in the background.

● Multi-Tenant Account Security:
  Each user has their own isolated tenant workspace. Invoices, customers, suppliers, inventory, and purchases belong strictly to the authenticated account.

● Gemini AI Document Processing:
  Upload supplier purchase invoices as PDFs or Excel spreadsheets and let Google Gemini AI instantly parse items, batches, quantities, purchase prices, MRP, and GST in seconds.

● Production Grade & High Availability:
  Includes automated local snapshots, recycle bin with 1-click restore, comprehensive settings customization, and zero plaintext cloud credential exposure.

----------------------------------------------------------------------------------
Developed with precision by NexaAutomate.
Developer: https://github.com/thevirajdev  |  Social: @thevirajrealm
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "2. Quick Start & Shortcuts",
                Category = "Basics",
                RtfOrTextContent =
@"## Keyboard Shortcuts & Rapid POS Navigation

Billease Pro is engineered for cashier speed. You can navigate almost the entire billing workflow without touching the mouse.

⭐ Universal Shortcuts:
[F1]        Open this User Manual & Knowledge Base
[F2]        Create New Invoice (Instant Billing POS)
[F3]        Quick Search Products / Barcode Scanner focus
[F4]        Focus Payment & Finalize Invoice
[Ctrl + P]  Print Current Document / Invoice
[Ctrl + S]  Save Current Document Draft
[Ctrl + N]  Add New Item Line
[Escape]    Close Current Dialog / Cancel Action

⭐ Cashier Workflow (3-Second Checkout):
1. Press [F2] anywhere in the app to open New Invoice.
2. Scan product barcode with handheld scanner or type product name.
3. Quantity defaults to 1; press Enter to move to next row.
4. Total tax and grand total calculate live.
5. Press [F4] to enter payment method (Cash / UPI / Card / Credit).
6. Press [Enter] to finalize and trigger printing.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "3. Billing & Invoicing (POS)",
                Category = "Sales",
                RtfOrTextContent =
@"## Invoicing & Point of Sale (POS) Engine

The Sales & Invoicing module handles retail cash sales, GST tax invoices, credit sales with customer khata balance tracking, and returns.

⭐ Creating an Invoice:
● Selecting Customer:
  Type existing customer name/phone or click '+' to quickly add a walk-in or new credit customer. If customer has outstanding balance, their 'Back Dues' are automatically visible on screen and invoice.

● Adding Items:
  Scan barcode or search by name. For pharmaceutical or batch-tracked goods, select the batch number to auto-fill MRP, Sale Price, and Expiry Date.

● Discounts & Taxes:
  Configure global or line-item discounts (percent % or flat currency). GST/Tax columns dynamically show CGST + SGST or IGST based on settings.

● Payment Types:
  Supports Cash, Credit Card, UPI / QR Code, Bank Transfer, and Split / Partial Payments. Any unpaid remainder is automatically posted to the customer's khata ledger.

⭐ Output Options:
● Thermal 80mm / 58mm: Direct high-speed POS receipt printer output.
● Standard A4 / A5: Full GST Tax Invoice with company logo, tax breakdown table, amount in words, and authorized signature.
● Instant PDF Export: Generates professional vector PDF files ready to WhatsApp or email.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "4. AI Document Extraction (PDF / Excel)",
                Category = "Purchases",
                RtfOrTextContent =
@"## AI-Powered Purchase Inwarding (Gemini AI)

Never type 50-item supplier purchase bills by hand again. Billease Pro integrates Google Gemini AI to read, extract, and inward raw purchase bills automatically.

⭐ How to Inward a Purchase Bill with AI:
1. Open Purchases module and click '+ New Purchase'.
2. Click the '✨ AI Document Inwarding' button.
3. Click 'Upload Invoice (PDF / Excel)' and choose your supplier's bill:
   - Formats supported: Scanned or digital PDF invoices (.pdf) and Excel spreadsheets (.xlsx, .xls).
4. The system automatically reads table structures, handles multiline items, and sends structured content to the Gemini API.
5. In seconds, the preview grid displays extracted rows:
   - Item Name & Barcode / SKU
   - Batch Number & Expiry Date (MM/yyyy)
   - Pack & Scheme (e.g. 10+1 free)
   - Purchase Rate, MRP & GST Percentage
6. Click 'Accept & Populate Purchase Form'.
7. Review the auto-populated bill and click 'Save Purchase'.
8. Inventory quantities, batch records, and supplier ledger are instantly updated!

💡 Tip: Ensure your Gemini API Key is saved in Settings > AI Assistant.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "5. Inventory & Batch Management",
                Category = "Inventory",
                RtfOrTextContent =
@"## Products, Batches, Stock Alerts & Expiries

Track stock across multiple batches, manufacturing dates, and warehouse locations.

⭐ Features & Controls:
● Batch Tracking:
  Each product can have multiple active batches with independent purchase costs, MRPs, and expiry dates. Billease Pro uses FIFO (First-In, First-Out) / FEFO (First-Expired, First-Out) recommendation during billing.

● Expiry Alerts:
  Configurable early-warning system (e.g., 30, 60, or 90 days before expiry). The dashboard highlights upcoming expiries in orange and expired items in red to prevent illegal sales.

● Low Stock Thresholds:
  Set minimum safety stock levels per product. When inventory dips below the threshold, the product appears on the 'Low Stock Alert' report for reordering.

● Damaged & Expired Item Returns:
  Log damaged or expired stock under Inventory > Damage Entry. Damaged quantities are deducted from active inventory without distorting sales revenue figures.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "6. Customer & Supplier Khata (Ledgers)",
                Category = "Ledgers",
                RtfOrTextContent =
@"## Party Ledgers, Dues & WhatsApp Payment Reminders

Maintain complete transparency of customer receivables and supplier payables with double-entry ledger tracking.

⭐ Customer Khata:
● View total sales, payments received, and net balance due per customer.
● Click 'Add Payment' to record cash/bank receipts and settle outstanding bills.
● 1-Click WhatsApp Payment Reminder:
  Click the WhatsApp icon beside any customer with dues. Billease Pro opens WhatsApp with a pre-filled, professional balance notification detailing their invoice numbers and total pending amount.

⭐ Supplier Payables:
● Track all inward purchases, credit periods, and pending supplier payments.
● Record supplier disbursements and track payment reference numbers.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "7. Recycle Bin & Data Restoration",
                Category = "Security",
                RtfOrTextContent =
@"## Audit Trail, Soft-Delete & Recycle Bin

Accidental deletions never result in permanent data loss in Billease Pro.

⭐ How the Recycle Bin Works:
● Soft Delete Protection:
  When an invoice, product, customer, supplier, purchase, or expense is deleted, it is moved to the Recycle Bin with a timestamp and user record.

● Formatted Inspection:
  Unlike older versions that showed unreadable JSON text, the Billease Pro Recycle Bin displays:
  - Entity Category (Invoice, Product, Customer, Supplier, etc.)
  - Display Name / Invoice Number
  - Amount / Value
  - Date & Time Deleted

● 1-Click Restore:
  Select any deleted record and click 'Restore'. The record is instantly recovered to active status and restored in sales and inventory calculations.

● Auto-Purge Setting:
  Configurable retention period (default 90 days) in Settings > Alerts & Defaults.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "8. Cloud Sync & Multi-Tenant Vault",
                Category = "Cloud",
                RtfOrTextContent =
@"## Enterprise Cloud Vault & 24/7 Background Sync

Billease Pro provides enterprise-level data protection using Supabase Cloud PostgreSQL with hardware-grade security.

⭐ Security & Anti-Reverse Engineering:
● Credential Vault:
  Cloud database credentials are encrypted using an AES-256 cipher coupled with pseudo-random runtime LCG key sharding. Credentials are NEVER stored as plain text inside binaries or configuration files.

● Multi-Tenant Isolation:
  Every record is tagged with an OwnerUserId. You can only view and manage your own organization's records, even when connecting to shared enterprise databases.

● Offline-First Resiliency:
  All daily reads and writes happen in milliseconds against the local SQLite engine. If network connectivity fails, changes queue locally and automatically sync the moment connection returns.

● 24/7 Background Windows Task:
  Click 'Enable 24/7 Background Sync' in Settings > Cloud Sync to register a native Windows Scheduled Task that backs up your database even when Billease Pro is closed.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "9. Backups & Disaster Recovery",
                Category = "Storage",
                RtfOrTextContent =
@"## Database Backups, Legacy Migrations & Disaster Recovery

⭐ Taking Manual Backups:
● Open Settings > Storage & Backup.
● Click 'Back Up Now' to generate an encrypted snapshot (.db or .bak) saved to your chosen folder or USB drive.

⭐ Restoring From Backup:
● Click 'Restore From Backup' and select any .bak or .db file.
● Automatic Legacy Migration:
  If restoring a legacy backup from an older version without user accounts, Billease Pro's automatic Claim Legacy Engine claims and connects all unowned data to your active login account without data loss.

⭐ Cloud Restore:
● Installed Billease Pro on a new computer?
● Simply log in with your credentials, go to Settings > Cloud Sync, and click 'Restore My Data from Cloud'. Your entire product catalog, customer list, and invoice history will populate in seconds.
"
            });

            _allTopics.Add(new ManualTopic
            {
                Title = "10. About & Developer Branding",
                Category = "About",
                RtfOrTextContent =
@"## About Billease Pro

● Product: Billease Pro Enterprise ERP & Billing Suite
● Version: 1.0.0 (Production Release)
● Technology: .NET 8.0 Windows Desktop, SQLite 3 Local Engine, Supabase PostgreSQL Cloud Vault, Google Gemini AI Engine
● Framework: Windows Forms High-DPI Compliant

⭐ Engineered by:
  NexaAutomate Studio

⭐ Developer & Social Profiles:
  ● Lead Developer: thevirajdev
  ● GitHub Repository: https://github.com/thevirajdev/Billease-pro
  ● Official GitHub: https://github.com/thevirajdev
  ● Social Handle: @thevirajrealm (Twitter / X, Instagram)

⭐ License & Support:
  For customizations, specialized hardware integrations, or enterprise deployments, contact NexaAutomate via GitHub or social channels.
"
            });
        }
    }
}
