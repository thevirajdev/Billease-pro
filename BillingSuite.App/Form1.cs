using System;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using BillingSuite.App.Forms;
using BillingSuite.App.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Drawing;
using QuestPDF.Fluent;
using System.Net.Mail;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace BillingSuite.App;

public partial class Form1 : Form
{
    // Invoices home UI runtime elements
    private ToolStrip? tsInvoices;
    private ToolStripButton? tsNewInvoice;
    private ToolStripButton? tsViewEditInvoice;
    private ToolStripButton? tsDeleteInvoice;
    private ToolStripButton? tsPreviewInvoice;
    private ToolStripButton? tsPrintInvoice;
    private ToolStripButton? tsEmailInvoice;
    private ToolStripButton? tsSmsInvoice;
    private ToolStripButton? tsMarkPaid;
    private ToolStripButton? tsVoidInvoice;
    private ToolStripButton? tsSuppliers;
    private ToolStripButton? tsSavePurchase;
    private DateTimePicker? dtpInvFrom;
    private DateTimePicker? dtpInvTo;
    private CheckBox? chkApplyDateFilter;
    private Button? btnRefreshInvoicesList;
    private SplitContainer? splitInvoices;
    private TabControl? tabsInvDetails;
    private DataGridView? gridInvItems;
    private DataGridView? gridInvPayments;
    private TextBox? txtInvPrivateNotes;
    private ListBox? lstInvSmsLog;
    // Invoices totals/footer
    private Panel? pnlInvTotals;
    private Label? lblInvCount;
    private Label? lblInvTotals;
    private ContextMenuStrip? cmsInvoices;
    // Products tab layout panels
    private Panel? pnlProductsLeft;
    private Panel? pnlProductsRight;
    private Panel? pnlProductsTop;
    private Panel? pnlProductsCenter;
    private Panel? pnlProductsSpacer;
    private TableLayoutPanel? tlpProducts;
    private SplitContainer? splitProducts;
    // New tabs
    private TabPage? tabSuppliers;
    private TabPage? tabPurchases;
    private Action? _globalActionPurchaseEdit;
    private Action? _globalActionPurchaseDelete;
    private DataGridView? _globalGridPurchases;
    private TabPage? tabRecycleBin;
    private TabPage? tabAiAssistant;
    private TabPage? tabReports;
    private TabPage? tabExpenses;
    private TabPage? tabHelpManual;
    private AiChatSidePanel? aiPanel;
    private ReportsForm? reportsForm;
    public Form1()
    {
        InitializeComponent();
        
        // Launch Floating Assistant
        try { new AiFloatingAssistant().Show(); } catch { }

        try { this.KeyPreview = true; } catch { }
        try { this.KeyDown += Form1_InvoicesShortcuts; } catch { }
        try { InitializeProductsGridIfNeeded(); } catch { }
        try { if (tabControl1 != null) tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged; } catch { }
        try { EnsureExtraTabs(); } catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try { UpdateWindowTitle(); } catch { }
    }

    // ================= Alerts Tab =================
    private enum AlertsFilterMode { DefaultDays, DateRange, Month, SpecificDate }

    private AlertsFilterMode CurrentAlertsMode()
    {
        try
        {
            var idx = cboAlertsFilterMode?.SelectedIndex ?? 0;
            return idx switch { 1 => AlertsFilterMode.DateRange, 2 => AlertsFilterMode.Month, 3 => AlertsFilterMode.SpecificDate, _ => AlertsFilterMode.DefaultDays };
        }
        catch { return AlertsFilterMode.DefaultDays; }
    }

    private void SetupAlertsUI()
    {
        try
        {
            if (cboAlertsFilterMode != null && cboAlertsFilterMode.Items.Count == 0)
            {
                cboAlertsFilterMode.Items.AddRange(new object[] { "Default Days", "Date Range", "Month", "Specific Date" });
            }
            if (cboAlertsFilterMode != null && cboAlertsFilterMode.SelectedIndex < 0) cboAlertsFilterMode.SelectedIndex = 0;
            if (numAlertsDefaultDays != null && numAlertsDefaultDays.Value <= 0) numAlertsDefaultDays.Value = 30;
            UpdateAlertsFilterInputs();
            // Smooth grids
            TrySmoothGrid(gridNearExpiry);
            TrySmoothGrid(gridLowStock);
        }
        catch { }
    }

    private class RecycleBinViewModel
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string CategoryDetail { get; set; } = string.Empty;
        public string AmountValue { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; }
        public string FormattedDetails { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }

    private static RecycleBinViewModel MapRecycleBinItem(BillingSuite.App.Models.RecycleBinItem item)
    {
        var vm = new RecycleBinViewModel
        {
            Id = item.Id,
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            DeletedAt = item.DeletedAt,
            JsonData = item.JsonData ?? string.Empty
        };

        var json = item.JsonData ?? string.Empty;
        var lines = new List<string>();
        lines.Add($"Item Type: {item.EntityType} (Original ID: {item.EntityId})");
        lines.Add($"Deleted On: {item.DeletedAt.ToLocalTime():dd-MMM-yyyy hh:mm tt}");
        lines.Add(new string('-', 55));

        try
        {
            var jobj = !string.IsNullOrWhiteSpace(json) ? Newtonsoft.Json.Linq.JObject.Parse(json) : null;
            if (jobj != null)
            {
                switch (item.EntityType)
                {
                    case "Product":
                        vm.DisplayName = jobj["Name"]?.ToString() ?? "Product";
                        var cat = jobj["Category"]?.ToString();
                        var hsn = jobj["Hsn"]?.ToString();
                        vm.CategoryDetail = !string.IsNullOrEmpty(cat) ? $"Category: {cat}" : (!string.IsNullOrEmpty(hsn) ? $"HSN: {hsn}" : "Product");
                        var mrp = jobj["Mrp"]?.ToString();
                        var price = jobj["Price"]?.ToString();
                        vm.AmountValue = !string.IsNullOrEmpty(mrp) ? $"MRP: ₹{mrp}" : (!string.IsNullOrEmpty(price) ? $"₹{price}" : "-");

                        lines.Add($"Product Name: {vm.DisplayName}");
                        if (!string.IsNullOrEmpty(cat)) lines.Add($"Category: {cat}");
                        if (!string.IsNullOrEmpty(hsn)) lines.Add($"HSN Code: {hsn}");
                        if (jobj["Barcode"] != null) lines.Add($"Barcode: {jobj["Barcode"]}");
                        if (jobj["Unit"] != null) lines.Add($"Unit: {jobj["Unit"]}");
                        if (!string.IsNullOrEmpty(price)) lines.Add($"Selling Price: ₹{price}");
                        if (!string.IsNullOrEmpty(mrp)) lines.Add($"MRP: ₹{mrp}");
                        if (jobj["CostPrice"] != null) lines.Add($"Cost Price: ₹{jobj["CostPrice"]}");
                        if (jobj["CurrentStock"] != null) lines.Add($"Stock at Deletion: {jobj["CurrentStock"]}");
                        break;

                    case "Customer":
                        vm.DisplayName = jobj["Name"]?.ToString() ?? "Customer";
                        var phone = jobj["Phone"]?.ToString();
                        var city = jobj["City"]?.ToString();
                        vm.CategoryDetail = !string.IsNullOrEmpty(phone) ? $"Phone: {phone}" : (!string.IsNullOrEmpty(city) ? $"City: {city}" : "Customer");
                        var gst = jobj["GstNumber"]?.ToString();
                        vm.AmountValue = !string.IsNullOrEmpty(gst) ? $"GST: {gst}" : "-";

                        lines.Add($"Customer Name: {vm.DisplayName}");
                        if (!string.IsNullOrEmpty(phone)) lines.Add($"Phone: {phone}");
                        if (jobj["Email"] != null) lines.Add($"Email: {jobj["Email"]}");
                        if (!string.IsNullOrEmpty(city)) lines.Add($"City: {city}");
                        if (jobj["State"] != null) lines.Add($"State: {jobj["State"]}");
                        if (!string.IsNullOrEmpty(gst)) lines.Add($"GSTIN: {gst}");
                        if (jobj["Balance"] != null) lines.Add($"Outstanding Balance: ₹{jobj["Balance"]}");
                        break;

                    case "Invoice":
                        var invNo = jobj["InvoiceNumber"]?.ToString() ?? item.EntityId.ToString();
                        vm.DisplayName = $"Invoice #{invNo}";
                        var cust = jobj["CustomerNameSnapshot"]?.ToString() ?? jobj["Customer"]?["Name"]?.ToString();
                        vm.CategoryDetail = !string.IsNullOrEmpty(cust) ? $"Customer: {cust}" : "Invoice";
                        var tot = jobj["Total"]?.ToString();
                        vm.AmountValue = !string.IsNullOrEmpty(tot) ? $"Total: ₹{tot}" : "-";

                        lines.Add($"Invoice Number: {invNo}");
                        lines.Add($"Date: {jobj["InvoiceDate"]?.ToString()}");
                        if (!string.IsNullOrEmpty(cust)) lines.Add($"Customer: {cust}");
                        lines.Add($"Subtotal: ₹{jobj["Subtotal"]?.ToString() ?? "0.00"}");
                        lines.Add($"Discount: ₹{jobj["Discount"]?.ToString() ?? "0.00"}");
                        lines.Add($"Tax: ₹{jobj["Tax"]?.ToString() ?? "0.00"}");
                        lines.Add($"Grand Total: ₹{tot ?? "0.00"}");
                        lines.Add($"Amount Paid: ₹{jobj["Paid"]?.ToString() ?? "0.00"}");
                        lines.Add($"Balance Due: ₹{jobj["Due"]?.ToString() ?? "0.00"}");

                        var items = jobj["Items"] as Newtonsoft.Json.Linq.JArray;
                        if (items != null && items.Count > 0)
                        {
                            lines.Add("");
                            lines.Add($"LINE ITEMS ({items.Count}):");
                            foreach (var it in items)
                            {
                                var iname = it["ProductName"]?.ToString() ?? it["Description"]?.ToString() ?? "Item";
                                var iqty = it["Quantity"]?.ToString() ?? "1";
                                var iprice = it["Price"]?.ToString() ?? it["Rate"]?.ToString() ?? "0";
                                var iline = it["LineTotal"]?.ToString() ?? it["Total"]?.ToString() ?? "-";
                                lines.Add($"  • {iname} — Qty: {iqty} @ ₹{iprice} (Total: ₹{iline})");
                            }
                        }
                        break;

                    case "Supplier":
                        vm.DisplayName = jobj["Name"]?.ToString() ?? "Supplier";
                        var sPhone = jobj["Phone"]?.ToString();
                        var sCity = jobj["City"]?.ToString();
                        vm.CategoryDetail = !string.IsNullOrEmpty(sPhone) ? $"Phone: {sPhone}" : (!string.IsNullOrEmpty(sCity) ? $"City: {sCity}" : "Supplier");
                        var sGst = jobj["GstNumber"]?.ToString();
                        var sDues = jobj["BackDues"]?.ToString();
                        vm.AmountValue = !string.IsNullOrEmpty(sDues) ? $"Dues: ₹{sDues}" : (!string.IsNullOrEmpty(sGst) ? $"GST: {sGst}" : "-");

                        lines.Add($"Supplier Name: {vm.DisplayName}");
                        if (!string.IsNullOrEmpty(sPhone)) lines.Add($"Phone: {sPhone}");
                        if (jobj["Email"] != null) lines.Add($"Email: {jobj["Email"]}");
                        if (!string.IsNullOrEmpty(sCity)) lines.Add($"City: {sCity}");
                        if (jobj["State"] != null) lines.Add($"State: {jobj["State"]}");
                        if (!string.IsNullOrEmpty(sGst)) lines.Add($"GSTIN: {sGst}");
                        if (!string.IsNullOrEmpty(sDues)) lines.Add($"Back Dues: ₹{sDues}");
                        break;

                    case "Expense":
                        vm.DisplayName = jobj["Description"]?.ToString() ?? "Expense";
                        var expCat = jobj["Category"]?.ToString();
                        vm.CategoryDetail = !string.IsNullOrEmpty(expCat) ? $"Category: {expCat}" : "Expense";
                        var expAmt = jobj["Amount"]?.ToString();
                        vm.AmountValue = !string.IsNullOrEmpty(expAmt) ? $"₹{expAmt}" : "-";

                        lines.Add($"Expense Description: {vm.DisplayName}");
                        if (!string.IsNullOrEmpty(expCat)) lines.Add($"Category: {expCat}");
                        lines.Add($"Amount: ₹{expAmt ?? "0.00"}");
                        if (jobj["Date"] != null) lines.Add($"Expense Date: {jobj["Date"]}");
                        if (jobj["Note"] != null) lines.Add($"Notes: {jobj["Note"]}");
                        break;

                    case "Purchase":
                        var pBill = jobj["InvoiceNumber"]?.ToString() ?? item.EntityId.ToString();
                        vm.DisplayName = $"Purchase #{pBill}";
                        var supName = jobj["Supplier"]?["Name"]?.ToString() ?? ($"Supplier #{jobj["SupplierId"]}");
                        vm.CategoryDetail = supName;
                        var pTot = jobj["Total"]?.ToString();
                        vm.AmountValue = !string.IsNullOrEmpty(pTot) ? $"₹{pTot}" : "-";

                        lines.Add($"Purchase Bill #: {pBill}");
                        lines.Add($"Supplier: {supName}");
                        lines.Add($"Purchase Date: {jobj["PurchaseDate"]?.ToString()}");
                        lines.Add($"Subtotal: ₹{jobj["Subtotal"]?.ToString() ?? "0.00"}");
                        lines.Add($"Tax: ₹{jobj["Tax"]?.ToString() ?? "0.00"}");
                        lines.Add($"Total: ₹{pTot ?? "0.00"}");
                        lines.Add($"Paid: ₹{jobj["Paid"]?.ToString() ?? "0.00"}");
                        lines.Add($"Due: ₹{jobj["Due"]?.ToString() ?? "0.00"}");

                        var pItems = jobj["Items"] as Newtonsoft.Json.Linq.JArray;
                        if (pItems != null && pItems.Count > 0)
                        {
                            lines.Add("");
                            lines.Add($"PURCHASE ITEMS ({pItems.Count}):");
                            foreach (var it in pItems)
                            {
                                var iname = it["ProductName"]?.ToString() ?? "Item";
                                var ibatch = it["BatchNumber"]?.ToString() ?? "-";
                                var iqty = it["Quantity"]?.ToString() ?? "1";
                                var icost = it["CostPrice"]?.ToString() ?? it["Rate"]?.ToString() ?? "0";
                                lines.Add($"  • {iname} [Batch: {ibatch}] — Qty: {iqty} @ ₹{icost}");
                            }
                        }
                        break;

                    default:
                        vm.DisplayName = $"{item.EntityType} #{item.EntityId}";
                        vm.CategoryDetail = item.EntityType;
                        vm.AmountValue = "-";
                        lines.Add($"Raw Data:");
                        lines.Add(json);
                        break;
                }
            }
        }
        catch
        {
            vm.DisplayName = $"{item.EntityType} #{item.EntityId}";
            vm.CategoryDetail = item.EntityType;
            vm.AmountValue = "-";
        }

        vm.FormattedDetails = string.Join(Environment.NewLine, lines);
        return vm;
    }

    private void BuildRecycleBinHomeUi()
    {
        if (tabRecycleBin == null) return;
        tabRecycleBin.Controls.Clear();

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 380,
            SplitterWidth = 6
        };

        var top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(248, 249, 250), Padding = new Padding(8, 6, 8, 6) };
        var btnRefresh = new Button { Text = "Refresh", Location = new Point(8, 8), Width = 75, Height = 28 };
        
        var cmbFilter = new ComboBox { Location = new Point(90, 9), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbFilter.Items.AddRange(new object[] { "All Items", "Invoices", "Products", "Customers", "Suppliers", "Expenses", "Purchases" });
        cmbFilter.SelectedIndex = 0;

        var txtSearch = new TextBox { Location = new Point(208, 9), Width = 170, Height = 26, PlaceholderText = "Search deleted items..." };

        var btnRestore = new Button 
        { 
            Text = "↺ Restore Selected", 
            Location = new Point(386, 8), 
            Width = 130, 
            Height = 28,
            BackColor = Color.FromArgb(25, 135, 84),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnRestore.FlatAppearance.BorderSize = 0;

        var btnDeletePermanent = new Button 
        { 
            Text = "✕ Delete Permanently", 
            Location = new Point(522, 8), 
            Width = 145, 
            Height = 28,
            BackColor = Color.FromArgb(220, 53, 69),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnDeletePermanent.FlatAppearance.BorderSize = 0;

        var btnEmpty = new Button { Text = "Empty Bin", Location = new Point(673, 8), Width = 85, Height = 28 };

        var lblHint = new Label 
        { 
            Text = "Items older than 30 days are automatically purged.", 
            AutoSize = true, 
            Location = new Point(770, 14), 
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8.5F)
        };

        top.Controls.Add(btnRefresh);
        top.Controls.Add(cmbFilter);
        top.Controls.Add(txtSearch);
        top.Controls.Add(btnRestore);
        top.Controls.Add(btnDeletePermanent);
        top.Controls.Add(btnEmpty);
        top.Controls.Add(lblHint);

        var grid = new DataGridView 
        { 
            Dock = DockStyle.Fill, 
            ReadOnly = true, 
            AllowUserToAddRows = false, 
            RowHeadersVisible = false, 
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, 
            AutoGenerateColumns = false,
            BackgroundColor = Color.White
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", HeaderText = "Id", Visible = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EntityType", HeaderText = "Item Type", Width = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisplayName", HeaderText = "Deleted Item Name / Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CategoryDetail", HeaderText = "Category / Details", Width = 220 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AmountValue", HeaderText = "Amount / Value", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DeletedAt", HeaderText = "Deleted On", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" } });

        // Preview Panel in Panel2
        var pnlPreview = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.FromArgb(245, 246, 248) };
        var pnlPreviewHeader = new Panel { Dock = DockStyle.Top, Height = 28 };
        var lblPreviewTitle = new Label { Text = "Item Details Preview", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), AutoSize = true, Location = new Point(4, 4) };
        var btnToggleRaw = new Button { Text = "Show Raw JSON", Dock = DockStyle.Right, Width = 120, Height = 26 };
        pnlPreviewHeader.Controls.Add(lblPreviewTitle);
        pnlPreviewHeader.Controls.Add(btnToggleRaw);

        var txtPreview = new TextBox 
        { 
            Dock = DockStyle.Fill, 
            Multiline = true, 
            ReadOnly = true, 
            ScrollBars = ScrollBars.Vertical, 
            Font = new Font("Consolas", 9.5F), 
            BackColor = Color.White 
        };

        pnlPreview.Controls.Add(txtPreview);
        pnlPreview.Controls.Add(pnlPreviewHeader);

        splitContainer.Panel1.Controls.Add(grid);
        splitContainer.Panel1.Controls.Add(top);
        splitContainer.Panel2.Controls.Add(pnlPreview);
        tabRecycleBin.Controls.Add(splitContainer);

        List<RecycleBinViewModel> allItems = new();
        bool showRaw = false;

        btnToggleRaw.Click += (s, e) =>
        {
            showRaw = !showRaw;
            btnToggleRaw.Text = showRaw ? "Show Formatted" : "Show Raw JSON";
            UpdatePreview();
        };

        void UpdatePreview()
        {
            if (grid.CurrentRow?.DataBoundItem is RecycleBinViewModel selected)
            {
                lblPreviewTitle.Text = $"{selected.EntityType} Details: {selected.DisplayName}";
                txtPreview.Text = showRaw ? selected.JsonData : selected.FormattedDetails;
            }
            else
            {
                lblPreviewTitle.Text = "Item Details Preview";
                txtPreview.Text = "Select an item above to preview its details, line items, and attributes.";
            }
        }

        grid.SelectionChanged += (s, e) => UpdatePreview();

        void Reload()
        {
            try
            {
                using var db = new AppDbContext();
                var rawList = db.RecycleBin.OrderByDescending(x => x.DeletedAt).ToList();
                allItems = rawList.Select(MapRecycleBinItem).ToList();

                ApplyFilter();
            }
            catch { grid.DataSource = null; }
        }

        void ApplyFilter()
        {
            var filterType = cmbFilter.SelectedItem?.ToString() ?? "All Items";
            var query = (txtSearch.Text ?? string.Empty).Trim().ToLowerInvariant();

            var filtered = allItems.AsEnumerable();

            if (filterType == "Invoices") filtered = filtered.Where(x => x.EntityType == "Invoice");
            else if (filterType == "Products") filtered = filtered.Where(x => x.EntityType == "Product");
            else if (filterType == "Customers") filtered = filtered.Where(x => x.EntityType == "Customer");
            else if (filterType == "Suppliers") filtered = filtered.Where(x => x.EntityType == "Supplier");
            else if (filterType == "Expenses") filtered = filtered.Where(x => x.EntityType == "Expense");
            else if (filterType == "Purchases") filtered = filtered.Where(x => x.EntityType == "Purchase");

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(x => 
                    x.DisplayName.ToLower().Contains(query) ||
                    x.CategoryDetail.ToLower().Contains(query) ||
                    x.AmountValue.ToLower().Contains(query) ||
                    x.FormattedDetails.ToLower().Contains(query));
            }

            grid.DataSource = filtered.ToList();
            UpdatePreview();
        }

        cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        btnRefresh.Click += (s, e) => Reload();

        btnRestore.Click += (s, e) =>
        {
            try
            {
                if (grid.CurrentRow?.DataBoundItem is not RecycleBinViewModel bound)
                {
                    MessageBox.Show("Please select an item to restore.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int id = bound.Id;
                string type = bound.EntityType;

                if (id <= 0 || string.IsNullOrWhiteSpace(type))
                {
                    MessageBox.Show("Invalid recycle bin item.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using var db = new AppDbContext();
                var item = db.RecycleBin.FirstOrDefault(x => x.Id == id);
                if (item == null)
                {
                    MessageBox.Show("Item not found in Recycle Bin.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var json = item.JsonData ?? "";
                bool restored = false;
                try
                {
                    switch (type.Trim())
                    {
                        case "Invoice":
                            {
                                var obj = System.Text.Json.JsonSerializer.Deserialize<BillingSuite.App.Models.Invoice>(json, new System.Text.Json.JsonSerializerOptions
                                {
                                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                                });
                                if (obj != null)
                                {
                                    obj.Id = 0;
                                    if (obj.Items != null)
                                    {
                                        foreach (var it in obj.Items)
                                        {
                                            it.Id = 0;
                                            it.InvoiceId = 0;
                                            it.Invoice = null;
                                        }
                                    }
                                    if (obj.Payments != null)
                                    {
                                        foreach (var p2 in obj.Payments)
                                        {
                                            p2.Id = 0;
                                            p2.InvoiceId = 0;
                                        }
                                    }
                                    db.Invoices.Add(obj);
                                    db.SaveChanges();
                                    restored = true;
                                }
                            }
                            break;

                        case "Product":
                            {
                                var obj = System.Text.Json.JsonSerializer.Deserialize<BillingSuite.App.Models.Product>(json);
                                if (obj != null)
                                {
                                    if (db.Products.Any(p => p.Id == obj.Id)) obj.Id = 0;
                                    db.Products.Add(obj);
                                    db.SaveChanges();
                                    restored = true;
                                }
                            }
                            break;

                        case "Customer":
                            {
                                var obj = System.Text.Json.JsonSerializer.Deserialize<BillingSuite.App.Models.Customer>(json);
                                if (obj != null)
                                {
                                    if (db.Customers.Any(c => c.Id == obj.Id)) obj.Id = 0;
                                    db.Customers.Add(obj);
                                    db.SaveChanges();
                                    restored = true;
                                }
                            }
                            break;

                        case "Supplier":
                            {
                                var obj = System.Text.Json.JsonSerializer.Deserialize<BillingSuite.App.Models.Supplier>(json);
                                if (obj != null)
                                {
                                    if (db.Suppliers.Any(s => s.Id == obj.Id)) obj.Id = 0;
                                    db.Suppliers.Add(obj);
                                    db.SaveChanges();
                                    restored = true;
                                }
                            }
                            break;

                        case "Expense":
                            {
                                var obj = System.Text.Json.JsonSerializer.Deserialize<BillingSuite.App.Models.Expense>(json);
                                if (obj != null)
                                {
                                    if (db.Expenses.Any(x => x.Id == obj.Id)) obj.Id = 0;
                                    db.Expenses.Add(obj);
                                    db.SaveChanges();
                                    restored = true;
                                }
                            }
                            break;

                        case "Purchase":
                            {
                                var obj = System.Text.Json.JsonSerializer.Deserialize<BillingSuite.App.Models.Purchase>(json, new System.Text.Json.JsonSerializerOptions
                                {
                                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                                });
                                if (obj != null)
                                {
                                    obj.Id = 0;
                                    if (obj.Items != null)
                                    {
                                        foreach (var pit in obj.Items)
                                        {
                                            pit.Id = 0;
                                            pit.PurchaseId = 0;
                                            pit.Purchase = null;
                                        }
                                    }
                                    db.Purchases.Add(obj);
                                    db.SaveChanges();
                                    restored = true;
                                }
                            }
                            break;

                        default:
                            MessageBox.Show($"Restore not implemented for type '{type}'.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to restore item: {ex.Message}", "Restore Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    restored = false;
                }

                if (restored)
                {
                    try 
                    { 
                        db.RecycleBin.Remove(item); 
                        db.SaveChanges(); 
                    } 
                    catch { }

                    MessageBox.Show($"{type} restored successfully.", "Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Reload();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore operation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        btnDeletePermanent.Click += (s, e) =>
        {
            try
            {
                if (grid.CurrentRow?.DataBoundItem is not RecycleBinViewModel bound)
                {
                    MessageBox.Show("Please select an item to delete permanently.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show($"Permanently delete '{bound.DisplayName}' from Recycle Bin?\n\nThis action cannot be undone.", "Confirm Permanent Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using var db = new AppDbContext();
                var item = db.RecycleBin.FirstOrDefault(x => x.Id == bound.Id);
                if (item != null)
                {
                    db.RecycleBin.Remove(item);
                    db.SaveChanges();
                }
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        btnEmpty.Click += (s, e) =>
        {
            try
            {
                if (MessageBox.Show("Are you sure you want to permanently delete ALL items in the Recycle Bin?\n\nThis action cannot be undone!", "Empty Recycle Bin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using var db = new AppDbContext();
                var all = db.RecycleBin.ToList();
                if (all.Count > 0)
                {
                    db.RecycleBin.RemoveRange(all);
                    db.SaveChanges();
                }
                Reload();
                MessageBox.Show("Recycle Bin emptied successfully.", "Recycle Bin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to empty Recycle Bin: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        Reload();
    }

    private void GridProducts_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        try
        {
            if (gridProducts == null) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = gridProducts.Columns[e.ColumnIndex];
            if (col == null || col.Name != "Name") return; // only custom paint Name column

            e.Handled = true;
            e.PaintBackground(e.CellBounds, true);
            e.Paint(e.CellBounds, DataGridViewPaintParts.Border);

            var text = e.FormattedValue?.ToString() ?? string.Empty;
            var term = (txtSearchProducts?.Text ?? string.Empty).Trim();
            
            if (!string.IsNullOrEmpty(term) && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var matchStart = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                using (var font = e.CellStyle.Font ?? gridProducts.Font)
                {
                    // Measure text before match
                    var beforeMatch = text.Substring(0, matchStart);
                    var matchText = text.Substring(matchStart, term.Length);
                    
                    var sizeBefore = TextRenderer.MeasureText(e.Graphics, beforeMatch, font);
                    var sizeMatch = TextRenderer.MeasureText(e.Graphics, matchText, font);
                    
                    // Draw highlight
                    int highlightX = e.CellBounds.X + 4 + sizeBefore.Width - (beforeMatch.Length > 0 ? 4 : 0);
                    var highlightRect = new Rectangle(highlightX, e.CellBounds.Y + 4, sizeMatch.Width - 6, e.CellBounds.Height - 8);
                    e.Graphics.FillRectangle(Brushes.Yellow, highlightRect);
                }
            }

            var rect = new Rectangle(e.CellBounds.X + 4, e.CellBounds.Y + 2, e.CellBounds.Width - 8, e.CellBounds.Height - 4);
            using var b = new SolidBrush(e.CellStyle.ForeColor);
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(text, e.CellStyle.Font ?? gridProducts.Font, b, rect, sf);
        }
        catch { }
    }

    private void TabControl1_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (tabControl1?.SelectedTab == tabProducts)
            {
                InitializeProductsGridIfNeeded();
                LoadProductsGrid();
                ApplyProductsGridFit(this, EventArgs.Empty);
                ApplyProductsPixelFit();
                // force a re-layout to guarantee header space
                try { tabProducts.PerformLayout(); pnlProductsCenter?.PerformLayout(); pnlProductsLeft?.PerformLayout(); gridProducts?.Invalidate(); } catch { }
            }
            else if (tabControl1?.SelectedTab == tabAlerts)
            {
                SetupAlertsUI();
                LoadAlerts();
            }
            else if (tabControl1?.SelectedTab == tabSuppliers)
            {
                BuildSuppliersHomeUi();
            }
            else if (tabControl1?.SelectedTab == tabPurchases)
            {
                BuildPurchasesHomeUi();
            }
            else if (tabControl1?.SelectedTab == tabRecycleBin)
            {
                BuildRecycleBinHomeUi();
            }
            else if (tabControl1?.SelectedTab == tabAiAssistant)
            {
                BuildAiAssistantHomeUi();
            }
            else if (tabControl1?.SelectedTab == tabReports)
            {
                EmbedReportsForm();
            }
            else if (tabControl1?.SelectedTab == tabExpenses)
            {
                EmbedExpensesForm();
            }
            else if (tabControl1?.SelectedTab == tabSettings)
            {
                EmbedSettingsForm();
            }
            else if (tabControl1?.SelectedTab == tabHelpManual)
            {
                EmbedHelpManualForm();
            }
        }
        catch { }
    }

    private void EnsureExtraTabs()
    {
        if (tabControl1 == null) return;
        // Suppliers tab
        bool hasSup = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text == "Suppliers") { tabSuppliers = p; hasSup = true; break; }
        if (!hasSup)
        {
            tabSuppliers = new TabPage("Suppliers");
            tabControl1.TabPages.Add(tabSuppliers);
        }
        // Purchases tab
        bool hasPur = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text == "Purchases") { tabPurchases = p; hasPur = true; break; }
        if (!hasPur)
        {
            tabPurchases = new TabPage("Purchases");
            tabControl1.TabPages.Add(tabPurchases);
        }
        // Recycle Bin tab
        bool hasRecycle = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text == "Recycle Bin") { tabRecycleBin = p; hasRecycle = true; break; }
        if (!hasRecycle)
        {
            tabRecycleBin = new TabPage("Recycle Bin");
            tabControl1.TabPages.Add(tabRecycleBin);
        }
        // AI Assistant tab
        bool hasAi = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text == "AI Assistant") { tabAiAssistant = p; hasAi = true; break; }
        if (!hasAi)
        {
            tabAiAssistant = new TabPage("AI Assistant");
            tabControl1.TabPages.Add(tabAiAssistant);
        }
        // Reports tab
        bool hasRep = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text == "Reports") { tabReports = p; hasRep = true; break; }
        if (!hasRep)
        {
            tabReports = new TabPage("Reports");
            tabControl1.TabPages.Add(tabReports);
        }
        // Expenses tab
        bool hasExp = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text == "Expenses") { tabExpenses = p; hasExp = true; break; }
        if (!hasExp)
        {
            tabExpenses = new TabPage("Expenses");
            tabControl1.TabPages.Add(tabExpenses);
        }
        // Settings tab
        bool hasSet = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text == "Settings") { tabSettings = p; hasSet = true; break; }
        if (!hasSet)
        {
            tabSettings = new TabPage("Settings");
            tabControl1.TabPages.Add(tabSettings);
        }
        // Help & User Manual tab
        bool hasHelp = false; foreach (TabPage p in tabControl1.TabPages) if (p.Text.StartsWith("Help")) { tabHelpManual = p; hasHelp = true; break; }
        if (!hasHelp)
        {
            tabHelpManual = new TabPage("Help & Manual (F1)");
            tabControl1.TabPages.Add(tabHelpManual);
        }
    }

    private void EmbedHelpManualForm()
    {
        try
        {
            if (tabHelpManual == null) return;
            tabHelpManual.Controls.Clear();
            var host = new Panel { Dock = DockStyle.Fill };
            tabHelpManual.Controls.Add(host);
            var f = new Forms.HelpManualForm()
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            host.Controls.Add(f);
            f.Show();
        }
        catch { }
    }

    private void EmbedSettingsForm()
    {
        try
        {
            if (tabSettings == null) return;
            tabSettings.Controls.Clear();
            var host = new Panel { Dock = DockStyle.Fill };
            tabSettings.Controls.Add(host);
            var f = new Forms.SettingsForm()
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            host.Controls.Add(f);
            f.Show();
        }
        catch { }
    }

    private void EmbedExpensesForm()
    {
        try
        {
            if (tabExpenses == null) return;
            tabExpenses.Controls.Clear();
            var host = new Panel { Dock = DockStyle.Fill };
            tabExpenses.Controls.Add(host);
            var f = new BillingSuite.App.Forms.ExpenseForm()
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            host.Controls.Add(f);
            f.Show();
        }
        catch { }
    }

    private void EmbedReportsForm()
    {
        try
        {
            if (tabReports == null) return;
            tabReports.Controls.Clear();
            var host = new Panel { Dock = DockStyle.Fill };
            tabReports.Controls.Add(host);
            var f = new ReportsForm()
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            host.Controls.Add(f);
            f.Show();
        }
        catch { }
    }

    private void EmbedSuppliersForm()
    {
        try
        {
            if (tabSuppliers == null) return;
            tabSuppliers.Controls.Clear();
            var host = new Panel { Dock = DockStyle.Fill };
            tabSuppliers.Controls.Add(host);
            var f = new BillingSuite.App.Forms.SuppliersForm()
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            host.Controls.Add(f);
            f.Show();
        }
        catch { }
    }

    private void BuildAiAssistantHomeUi()
    {
        try
        {
            if (tabAiAssistant == null) return;
            if (aiPanel != null) return; // already initialized

            tabAiAssistant.Controls.Clear();
            aiPanel = new AiChatSidePanel(isFloating: false);
            aiPanel.Dock = DockStyle.Fill;
            aiPanel.ActionApproved += HandleAiActionApproved;
            tabAiAssistant.Controls.Add(aiPanel);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load AI Assistant: " + ex.Message);
        }
    }

    private async void HandleAiActionApproved(AiActionResponse response)
    {
        using var db = new AppDbContext();
        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            switch (response.Action)
            {
                case "create_purchase":
                    await GeminiExecuteCreatePurchase(db, response.Data.ToObject<AiPurchaseData>());
                    break;
                case "create_bill":
                    await GeminiExecuteCreateBill(db, response.Data.ToObject<AiBillData>());
                    break;
                case "update_product":
                    await GeminiExecuteUpdateProduct(db, response.Data.ToObject<AiUpdateProductData>());
                    break;
                default:
                    MessageBox.Show("Execution not implemented for action: " + response.Action);
                    break;
            }
            await transaction.CommitAsync();
            MessageBox.Show("AI Action Executed Successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            MessageBox.Show("AI Action Failed and Rolled Back: " + ex.Message);
        }
    }

    private async Task GeminiExecuteCreatePurchase(AppDbContext db, AiPurchaseData? data)
    {
        if (data == null) return;
        var supplier = db.Suppliers.FirstOrDefault(s => s.Name == data.SupplierName);
        if (supplier == null)
        {
            supplier = new Supplier { Name = data.SupplierName ?? "AI Suggested Supplier", CreatedAt = DateTime.Now };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
        }

        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.Now,
            InvoiceNumber = data.InvoiceNumber,
            CreatedAt = DateTime.Now
        };
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();

        foreach (var item in data.Items)
        {
            var product = db.Products.FirstOrDefault(p => p.Name == item.ProductName);
            if (product == null)
            {
                product = new Product { Name = item.ProductName };
                db.Products.Add(product);
                await db.SaveChangesAsync();
            }

            var pItem = new PurchaseItem
            {
                PurchaseId = purchase.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                CostPrice = item.CostPrice ?? 0m,
                SellingPrice = item.SellingPrice ?? 0m,
                BatchNumber = item.BatchNumber,
                Expiry = !string.IsNullOrEmpty(item.Expiry) ? DateTime.TryParse(item.Expiry, out var dt) ? dt : null : null
            };
            db.PurchaseItems.Add(pItem);
            
            // Update stock
            var batch = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == product.Id && b.BatchNumber == item.BatchNumber);
            if (batch == null)
            {
                batch = new ProductBatch
                {
                    ProductIdRef = product.Id,
                    BatchNumber = item.BatchNumber ?? "AUTO",
                    Stock = 0,
                    CreatedAt = DateTime.Now
                };
                db.ProductBatches.Add(batch);
            }
            batch.Stock = (batch.Stock ?? 0m) + item.Quantity;
            batch.CostPrice = item.CostPrice;
            batch.SellingPrice = item.SellingPrice;
        }
        await db.SaveChangesAsync();
    }

    private async Task GeminiExecuteCreateBill(AppDbContext db, AiBillData? data)
    {
        if (data == null) return;
        var customer = db.Customers.FirstOrDefault(c => c.Name == (data.CustomerName ?? "AI Cash Customer")) ?? new Customer { Name = data.CustomerName ?? "AI Cash Customer", CreatedAt = DateTime.Now };
        if (customer.Id == 0) db.Customers.Add(customer);

        var invoice = new Invoice
        {
            Customer = customer,
            InvoiceDate = DateTime.Now,
            Status = InvoiceStatus.Paid,
            CreatedAt = DateTime.Now
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        foreach (var item in data.Items)
        {
            var invItem = new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = item.Price ?? 0m,
                LineTotal = (item.Price ?? 0m) * item.Quantity
            };
            db.InvoiceItems.Add(invItem);
        }
        await db.SaveChangesAsync();
    }

    private async Task GeminiExecuteUpdateProduct(AppDbContext db, AiUpdateProductData? data)
    {
        if (data == null) return;
        var product = db.Products.FirstOrDefault(p => p.Id == data.ProductId || p.Name == data.ProductName);
        if (product != null)
        {
            if (data.Price.HasValue) 
            {
                // Logic to update price in batches or main product (system dependent)
            }
            await db.SaveChangesAsync();
        }
    }

    private void EmbedSavePurchaseForm()
    {
        try
        {
            if (tabPurchases == null) return;
            tabPurchases.Controls.Clear();
        tabPurchases.Padding = new Padding(0);
            var host = new Panel { Dock = DockStyle.Fill };
            tabPurchases.Controls.Add(host);
            var f = new BillingSuite.App.Forms.SavePurchaseForm()
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill
            };
            host.Controls.Add(f);
            f.Show();
        }
        catch { }
    }

    private void ProductsHost_Resize(object? sender, EventArgs e)
    {
        LayoutProductsGrid();
        ApplyProductsGridFit(sender, EventArgs.Empty);
    }

    private void LayoutProductsGrid()
    {
        try
        {
            // With Dock panels in place, explicit bounds not required.
            // Keep method for compatibility; nothing to do here.
        }
        catch { }
    }

    private void UpdateAlertsFilterInputs()
    {
        try
        {
            var mode = CurrentAlertsMode();
            if (dtpAlertsFrom != null) dtpAlertsFrom.Visible = (mode == AlertsFilterMode.DateRange);
            if (dtpAlertsTo != null) dtpAlertsTo.Visible = (mode == AlertsFilterMode.DateRange);
            if (dtpAlertsMonth != null) dtpAlertsMonth.Visible = (mode == AlertsFilterMode.Month);
            if (dtpAlertsDate != null) dtpAlertsDate.Visible = (mode == AlertsFilterMode.SpecificDate);
        }
        catch { }
    }

    private (DateTime from, DateTime to, int? defaultDays) GetAlertsFilter()
    {
        var today = DateTime.Today;
        var mode = CurrentAlertsMode();
        if (mode == AlertsFilterMode.DateRange && dtpAlertsFrom != null && dtpAlertsTo != null)
        {
            var f = dtpAlertsFrom.Value.Date;
            var t = dtpAlertsTo.Value.Date;
            if (t < f) t = f;
            return (f, t, null);
        }
        if (mode == AlertsFilterMode.Month && dtpAlertsMonth != null)
        {
            var f = new DateTime(dtpAlertsMonth.Value.Year, dtpAlertsMonth.Value.Month, 1);
            var t = f.AddMonths(1).AddDays(-1);
            return (f, t, null);
        }
        if (mode == AlertsFilterMode.SpecificDate && dtpAlertsDate != null)
        {
            var d = dtpAlertsDate.Value.Date;
            return (d, d, null);
        }
        int def = 30; try { def = (int)(numAlertsDefaultDays?.Value ?? 30); } catch { }
        return (today, today.AddDays(def), def);
    }

    // ================= Alerts Tab Event Handlers =================
    private void btnAlerts_Click(object? sender, EventArgs e)
    {
        try
        {
            if (tabControl1 != null && tabAlerts != null)
            {
                tabControl1.SelectedTab = tabAlerts;
            }
        }
        catch { }
    }

    private void btnBackup_Click(object? sender, EventArgs e)
    {
        DatabaseUtils.BackupDatabase();
    }

    private void btnRestore_Click(object? sender, EventArgs e)
    {
        DatabaseUtils.RestoreDatabase();
    }

    private void btnSettingsNav_Click(object? sender, EventArgs e)
    {
        try
        {
            if (tabControl1 != null && tabSettings != null)
            {
                tabControl1.SelectedTab = tabSettings;
            }
        }
        catch { }
    }

    private void btnSettings_Click(object? sender, EventArgs e)
    {
        if (tabControl1 != null && tabSettings != null)
        {
            tabControl1.SelectedTab = tabSettings;
            return;
        }

        using var dlg = new Forms.SettingsForm();
        dlg.ShowDialog(this);
        // Settings can change invoice layout and company identity — invalidate so the
        // next read reflects changes without requiring an app restart.
        AppSettingsService.Invalidate();
        // Refresh window title in case the company name changed.
        UpdateWindowTitle();
    }

    private void signOutToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Sign out and return to the login screen?",
                "Sign Out", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        AuthService.SignOut();
        this.Hide();

        using var login = new Forms.LoginForm(firstRun: false);
        if (login.ShowDialog() == DialogResult.OK)
        {
            // User signed back in — refresh the window and show it again.
            AppSettingsService.Invalidate();
            UpdateWindowTitle();
            this.Show();
        }
        else
        {
            // User closed the login screen — close the app cleanly.
            Application.Exit();
        }
    }

    /// <summary>
    /// Updates the main window title to show the signed-in user's email and company name.
    /// </summary>
    private void UpdateWindowTitle()
    {
        var company = AppSettingsService.CompanyName;
        var user = AuthService.CurrentUser?.Email;
        var brand = "Billease Pro • Developed by NexaAutomate";
        Text = string.IsNullOrWhiteSpace(user)
            ? $"{brand} – {company}"
            : $"{brand} – {company} ({user})";
    }

    private void cboAlertsFilterMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            UpdateAlertsFilterInputs();
        }
        catch { }
    }

    private void btnAlertsApply_Click(object? sender, EventArgs e)
    {
        try
        {
            LoadAlerts();
        }
        catch { }
    }

    private void btnAlertsRefresh_Click(object? sender, EventArgs e)
    {
        try
        {
            if (cboAlertsFilterMode != null && cboAlertsFilterMode.Items.Count > 0)
                cboAlertsFilterMode.SelectedIndex = 0;
            if (numAlertsDefaultDays != null)
                numAlertsDefaultDays.Value = 30;
            UpdateAlertsFilterInputs();
            LoadAlerts();
        }
        catch { }
    }

    private void LoadAlerts()
    {
        try
        {
            using var db = new AppDbContext();
            
            // Perform global expiry check before loading alerts
            InventoryService.SyncExpiredStock(db);

            var mode = CurrentAlertsMode();
            var (from, to, defaultDays) = GetAlertsFilter();
            int defDays = defaultDays ?? AppSettingsService.DefaultExpiryAlertDays;
            var today = DateTime.Today;

            // ---- Near Expiry: Query ProductBatches (primary data source) ----
            var batchRows = db.ProductBatches
                .Include(b => b.Product)
                .Where(b => b.Expiry != null && b.Stock > 0)
                .AsEnumerable()
                .Select(b => new
                {
                    Name = b.Product?.Name ?? "N/A",
                    Mrp = b.Mrp ?? 0m,
                    Batch = b.BatchNumber,
                    Stock = b.Stock ?? 0m,
                    Expiry = b.Expiry!.Value.Date,
                    DaysLeft = (b.Expiry!.Value.Date - today).TotalDays,
                    EffectiveAlertDays = b.Product?.ExpiryAlertDays ?? defDays
                });

            // Also pull from Product denormalized fields as fallback
            var products = db.Products.ToList();
            var prodRows = products
                .SelectMany(p => new[]
                {
                    new { p.Name, Mrp = p.NewMrp ?? 0m, Batch = p.NewBatch ?? "", Stock = p.NewStock ?? 0m, Expiry = p.NewExpiry, AlertDays = p.ExpiryAlertDays },
                    new { p.Name, Mrp = p.OldMrp ?? 0m, Batch = p.OldBatch ?? "", Stock = p.OldStock ?? 0m, Expiry = p.OldExpiry, AlertDays = p.ExpiryAlertDays },
                    new { p.Name, Mrp = p.VeryOldMrp ?? 0m, Batch = p.VeryOldBatch ?? "", Stock = p.VeryOldStock ?? 0m, Expiry = p.VeryOldExpiry, AlertDays = p.ExpiryAlertDays }
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Batch) && x.Expiry.HasValue)
                .Select(x => new
                {
                    x.Name,
                    Mrp = x.Mrp,
                    Batch = x.Batch,
                    Stock = x.Stock,
                    Expiry = x.Expiry!.Value.Date,
                    DaysLeft = (x.Expiry!.Value.Date - today).TotalDays,
                    EffectiveAlertDays = x.AlertDays ?? defDays
                });

            // Merge both sources, deduplicate by Name+Batch
            // Only show near-expiry (DaysLeft > 0), NOT already expired
            var allExpiry = batchRows.Concat(prodRows)
                .Where(x => x.DaysLeft > 0) // exclude already expired
                .GroupBy(x => (x.Name, x.Batch))
                .Select(g => g.First());

            if (mode == AlertsFilterMode.DefaultDays)
            {
                allExpiry = allExpiry.Where(x => x.DaysLeft <= x.EffectiveAlertDays);
            }
            else
            {
                allExpiry = allExpiry.Where(x => x.Expiry >= from && x.Expiry <= to);
            }

            var expList = allExpiry
                .OrderBy(x => x.DaysLeft)
                .Select(x => new { x.Name, x.Mrp, x.Batch, x.Stock, Expiry = x.Expiry.ToString("dd/MM/yyyy"), DaysLeft = (int)x.DaysLeft })
                .ToList();

            if (gridNearExpiry != null)
            {
                gridNearExpiry.DataSource = expList;
                gridNearExpiry.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }

            // ---- Low Stock: Query Aggregated Stock for all products with a threshold ----
            var lowStockSummary = (from p in db.Products.AsNoTracking().ToList()
                                   where (p.LowStockThreshold ?? 0m) > 0
                                   let batches = db.ProductBatches.AsNoTracking().Where(b => b.ProductIdRef == p.Id).ToList()
                                   let totalStock = batches.Sum(x => x.Stock ?? 0m)
                                   where totalStock <= (p.LowStockThreshold ?? 0m)
                                   select new
                                   {
                                       Name = p.Name,
                                       Batch = "AGGREGATED STOCK",
                                       Stock = totalStock,
                                       Threshold = p.LowStockThreshold ?? 0m
                                   })
                                   .OrderBy(x => x.Stock)
                                   .ToList();

            if (gridLowStock != null)
            {
                gridLowStock.DataSource = null;
                gridLowStock.DataSource = lowStockSummary;
                gridLowStock.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                gridLowStock.Refresh();
            }

        }
        catch { }
    }

    private void ApplyProductsPixelFit()
    {
        if (gridProducts == null) return;
        try
        {
            // Fix autosizing and set exact widths to fit available client width
            gridProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            gridProducts.ScrollBars = ScrollBars.Vertical;
            // Use left panel width; if not ready, use center minus right width
            int hostWidth = gridProducts.ClientSize.Width;
            try
            {
                if (pnlProductsLeft != null && pnlProductsLeft.ClientSize.Width > 0)
                    hostWidth = pnlProductsLeft.ClientSize.Width;
                else if (pnlProductsCenter != null)
                    hostWidth = Math.Max(0, pnlProductsCenter.ClientSize.Width - (pnlProductsRight?.Width ?? 0));
            }
            catch { }
            // Reserve scrollbar width so last column doesn't clip under it
            int reserveScroll = SystemInformation.VerticalScrollBarWidth;
            int available = hostWidth - reserveScroll - (gridProducts.RowHeadersVisible ? gridProducts.RowHeadersWidth : 0) - 2; // small padding
            if (available < 220) available = 220;

            // Equal widths for all except Name and Category; keep Select small
            // Define metadata: name, minSoft, minHard, units for width sharing
            var meta = new System.Collections.Generic.List<(string name, int minSoft, int minHard, float units)>
            {
                ("colSelect", 32, 30, 0.0f), // fixed small
                ("Id", 0, 0, 0.0f), // hidden/fixed
                ("BatchId", 0, 0, 0.0f), // hidden/fixed
                ("Hsn", 34, 30, 1f),
                ("Name", 150, 120, 2.5f),
                ("BatchNumber", 60, 56, 1f),
                ("Category", 90, 70, 1.6f),
                ("Expiry", 60, 50, 1f),
                ("Mrp", 55, 45, 1f),
                ("CostPrice", 55, 45, 1f),
                ("SellingPrice", 55, 45, 1f),
                ("MarketedBy", 74, 70, 1f),
                ("Stock", 42, 40, 1f),
                ("Bonus", 36, 34, 1f),
                ("Pack", 42, 40, 1f)
            };

            int fixedUsed = 0;
            foreach (var m in meta)
            {
                if (m.name == "colSelect")
                {
                    var col = gridProducts.Columns[m.name]; if (col != null) { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; col.Width = Math.Max(m.minHard, m.minSoft); fixedUsed += col.Width; }
                }
                else if (m.name == "Id")
                {
                    var col = gridProducts.Columns[m.name]; if (col != null) { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; col.Width = 0; }
                }
                else if (m.name == "BatchId")
                {
                    var col = gridProducts.Columns[m.name]; if (col != null) { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; col.Width = 0; }
                }
            }

            int remAvail = Math.Max(60, available - fixedUsed);
            float totalUnits = 0f; foreach (var m in meta) if (m.units > 0) totalUnits += m.units;
            var targets = new System.Collections.Generic.Dictionary<string, int>();
            int sum = 0;
            foreach (var m in meta)
            {
                if (m.units == 0) { targets[m.name] = Math.Max(m.minHard, m.minSoft); continue; }
                int t = (int)Math.Floor(remAvail * (m.units / totalUnits));
                if (t < m.minSoft) t = m.minSoft;
                targets[m.name] = t; sum += t;
            }
            if (sum > remAvail)
            {
                int deficit = sum - remAvail;
                // reduce proportionally down to minHard
                int reducible = 0; foreach (var m in meta) if (m.units > 0) reducible += Math.Max(0, targets[m.name] - m.minHard);
                if (reducible > 0)
                {
                    foreach (var m in meta)
                    {
                        if (m.units == 0) continue;
                        if (deficit <= 0) break;
                        int can = Math.Max(0, targets[m.name] - m.minHard);
                        if (can == 0) continue;
                        int take = Math.Min(can, (int)Math.Ceiling(deficit * (targets[m.name] - m.minHard) / (double)reducible));
                        targets[m.name] -= take; deficit -= take;
                    }
                    // sweep remainder
                    foreach (var m in meta)
                    {
                        if (m.units == 0) continue;
                        if (deficit <= 0) break;
                        int can = Math.Max(0, targets[m.name] - m.minHard);
                        int take = Math.Min(can, deficit); targets[m.name] -= take; deficit -= take;
                    }
                }
                foreach (var m in meta) if (m.units > 0 && targets[m.name] < m.minHard) targets[m.name] = m.minHard;
            }

            // assign widths, last column absorbs rounding
            int used = 0;
            for (int i = 0; i < meta.Count; i++)
            {
                var m = meta[i]; var col = gridProducts.Columns[m.name]; if (col == null) continue;
                int w = targets.TryGetValue(m.name, out var tv) ? tv : Math.Max(m.minHard, m.minSoft);
                if (i == meta.Count - 1)
                {
                    int free = available - used; if (free > 0) w = Math.Max(m.minHard, free);
                }
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; col.Width = w; used += w;
            }

            // Make only Select column editable; others locked
            foreach (DataGridViewColumn c in gridProducts.Columns)
            {
                c.ReadOnly = c.Name != "colSelect";
            }
        }
        catch { }
    }



    private void ApplyInvoicesGridColumnLayout()
    {
        if (gridInvoices == null || gridInvoices.Columns.Count == 0) return;
        gridInvoices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        foreach (DataGridViewColumn col in gridInvoices.Columns)
        {
            switch (col.Name)
            {
                case "InvoiceNumber": col.FillWeight = 18; break;
                case "InvoiceDate": col.FillWeight = 12; col.DefaultCellStyle.Format = "dd/MM/yyyy"; break;
                case "DueDate": col.FillWeight = 12; col.DefaultCellStyle.Format = "dd/MM/yyyy"; break;
                case "Customer": col.FillWeight = 28; break;
                case "Status": col.FillWeight = 10; break;
                case "Total": col.FillWeight = 10; col.DefaultCellStyle.Format = "0.00"; break;
                case "TotalPaid": col.FillWeight = 10; col.DefaultCellStyle.Format = "0.00"; break;
                case "Balance": col.FillWeight = 10; col.DefaultCellStyle.Format = "0.00"; break;
                default: col.FillWeight = 10; break;
            }
        }
        if (gridInvoices.Columns["Id"] != null) gridInvoices.Columns["Id"].Visible = false;
    }

    // ================= Products/Services Page =================
    private void InitializeProductsGridIfNeeded()
    {
        try
        {
            if (gridProducts == null) return;
            gridProducts.ReadOnly = false; // we'll lock columns except Select
            gridProducts.RowHeadersVisible = false;
            gridProducts.AllowUserToAddRows = false;
            gridProducts.AllowUserToDeleteRows = false;
            gridProducts.AutoGenerateColumns = false;
            gridProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridProducts.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            gridProducts.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            gridProducts.ScrollBars = ScrollBars.Vertical; // keep vertical scroll only
            // Dock-based layout reset: Top (actions), Center as SplitContainer (Left grid, Right category)
            if (tabProducts != null)
            // Dock-based layout reset: Master TableLayoutPanel to prevent ANY overlap
            if (tabProducts != null && tabProducts.Tag == null)
            {
                tabProducts.Tag = "Initialized";
                
                TableLayoutPanel tlpMaster = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
                tlpMaster.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tlpMaster.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                TableLayoutPanel containerTop = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
                containerTop.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                containerTop.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                containerTop.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                FlowLayoutPanel rowSearch = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
                FlowLayoutPanel rowActions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
                FlowLayoutPanel rowExpiry = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };

                void MoveControl(Control c, Panel host) {
                    try {
                        if (c != null && tabProducts.Controls.Contains(c)) {
                            tabProducts.Controls.Remove(c); c.Margin = new Padding(6); host.Controls.Add(c);
                        }
                    } catch {}
                }

                MoveControl(lblProductFilterText, rowSearch); MoveControl(txtSearchProducts, rowSearch);
                MoveControl(btnSearchProducts, rowSearch); MoveControl(lblProductFilterColumn, rowSearch);
                MoveControl(cboProductFilterColumn, rowSearch); MoveControl(chkShowOutOfStock, rowSearch);

                MoveControl(btnRefreshProducts, rowActions); MoveControl(btnAddProduct, rowActions);
                MoveControl(btnEditProduct, rowActions); MoveControl(btnDeleteProduct, rowActions);
                MoveControl(btnExportProducts, rowActions); MoveControl(btnExportProductsPdf, rowActions); MoveControl(btnImportProducts, rowActions);

                MoveControl(lblExpiryFilterMode, rowExpiry); MoveControl(cboExpiryFilterMode, rowExpiry);
                MoveControl(dtpExpiryFrom, rowExpiry); MoveControl(dtpExpiryTo, rowExpiry);
                MoveControl(dtpExpiryMonth, rowExpiry); MoveControl(dtpExpiryDate, rowExpiry);
                MoveControl(btnApplyExpiryFilter, rowExpiry); MoveControl(btnResetExpiryFilter, rowExpiry);

                containerTop.Controls.Add(rowSearch, 0, 0);
                containerTop.Controls.Add(rowActions, 0, 1);
                containerTop.Controls.Add(rowExpiry, 0, 2);

                pnlProductsTop = new Panel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0) };
                pnlProductsTop.Controls.Add(containerTop);

                try
                {
                    if (chkProductsSelectAll != null)
                    {
                        try { pnlProductsTop.Controls.Remove(chkProductsSelectAll); } catch { }
                        chkProductsSelectAll.Text = "Select All"; chkProductsSelectAll.AutoSize = true; chkProductsSelectAll.Margin = new Padding(6);
                        rowActions.Controls.Add(chkProductsSelectAll);
                        chkProductsSelectAll.CheckedChanged -= (s, e) => { };
                        chkProductsSelectAll.CheckedChanged += (s, e) => {
                            try { if (gridProducts == null) return; gridProducts.EndEdit(); foreach (DataGridViewRow row in gridProducts.Rows) { if (row.IsNewRow) continue; if (row.Cells["colSelect"] is DataGridViewCheckBoxCell c) c.Value = chkProductsSelectAll.Checked; } } catch { }
                        };
                    }
                } catch { }

                splitProducts = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel2, IsSplitterFixed = false, SplitterWidth = 5 };
                try { splitProducts.Panel2MinSize = 150; splitProducts.SplitterDistance = Math.Max(0, tabProducts.ClientSize.Width - 150); } catch { }

                TableLayoutPanel tlpCat = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
                tlpCat.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tlpCat.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                try { tabProducts.Controls.Remove(lblCategoryFilterHeader); lblCategoryFilterHeader.Dock = DockStyle.Fill; lblCategoryFilterHeader.Margin = new Padding(2); tlpCat.Controls.Add(lblCategoryFilterHeader, 0, 0); } catch { }
                try { tabProducts.Controls.Remove(lstProductCategories); lstProductCategories.Dock = DockStyle.Fill; lstProductCategories.Margin = new Padding(0, 5, 0, 0); tlpCat.Controls.Add(lstProductCategories, 0, 1); } catch { }
                splitProducts.Panel2.Controls.Add(tlpCat);

                try { tabProducts.Controls.Remove(gridProducts); } catch { }
                splitProducts.Panel1.Padding = new Padding(0); // ZERO padding needed with TableLayoutPanel
                splitProducts.Panel1.Controls.Add(gridProducts);
                gridProducts.Dock = DockStyle.Fill;

                tlpMaster.Controls.Add(pnlProductsTop, 0, 0);
                tlpMaster.Controls.Add(splitProducts, 0, 1);
                tabProducts.Controls.Add(tlpMaster);
            }
            // dense but readable fonts
            try
            {
                var body = new Font(gridProducts.Font.FontFamily, 8.5f, FontStyle.Regular);
                var header = new Font(gridProducts.Font.FontFamily, 9.0f, FontStyle.Bold);
                gridProducts.Font = body;
                gridProducts.ColumnHeadersDefaultCellStyle.Font = header;
                gridProducts.DefaultCellStyle.Padding = new Padding(2);
            }
            catch { }
            gridProducts.Columns.Clear();
            // Columns: Select, (hidden Id), (hidden BatchId), HSN, Name, Batch Number, Category, Expiry, MRP, Cost Price, Selling Price, Marketed By, Stock, Bonus, Pack
            var colSel = new DataGridViewCheckBoxColumn { Name = "colSelect", HeaderText = "Select", Width = 34, MinimumWidth = 32, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ThreeState = false, ReadOnly = false };
            gridProducts.Columns.Add(colSel);
            // Hidden Id for edit/view actions
            var colId = new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Id", DataPropertyName = "Id", Visible = false };
            gridProducts.Columns.Add(colId);
            var colBatchId = new DataGridViewTextBoxColumn { Name = "BatchId", HeaderText = "BatchId", DataPropertyName = "BatchId", Visible = false };
            gridProducts.Columns.Add(colBatchId);
            gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Hsn", HeaderText = "HSN", DataPropertyName = "Hsn", FillWeight = 6.2f, MinimumWidth = 34 });
            gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", DataPropertyName = "Name", FillWeight = 22f, MinimumWidth = 150 });
            gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "BatchNumber", HeaderText = "Batch Number", DataPropertyName = "BatchNumber", FillWeight = 8.2f, MinimumWidth = 60 });
            gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", DataPropertyName = "Category", FillWeight = 9.6f, MinimumWidth = 72 });
            var colExp = new DataGridViewTextBoxColumn { Name = "Expiry", HeaderText = "Expiry", DataPropertyName = "Expiry", FillWeight = 7.2f, MinimumWidth = 60 }; colExp.DefaultCellStyle.Format = "MM/yy"; gridProducts.Columns.Add(colExp);
            var colMrp = new DataGridViewTextBoxColumn { Name = "Mrp", HeaderText = "MRP", DataPropertyName = "Mrp", FillWeight = 6, MinimumWidth = 55 }; colMrp.DefaultCellStyle.Format = "0.00"; colMrp.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; gridProducts.Columns.Add(colMrp);
            var colCp = new DataGridViewTextBoxColumn { Name = "CostPrice", HeaderText = "Cost Price", DataPropertyName = "CostPrice", FillWeight = 6, MinimumWidth = 55 }; colCp.DefaultCellStyle.Format = "0.00"; colCp.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; gridProducts.Columns.Add(colCp);
            var colSp = new DataGridViewTextBoxColumn { Name = "SellingPrice", HeaderText = "Selling Price", DataPropertyName = "SellingPrice", FillWeight = 6, MinimumWidth = 55 }; colSp.DefaultCellStyle.Format = "0.00"; colSp.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; gridProducts.Columns.Add(colSp);
            gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "MarketedBy", HeaderText = "Marketed By", DataPropertyName = "MarketedBy", FillWeight = 8.0f, MinimumWidth = 74 });
            var colStock = new DataGridViewTextBoxColumn { Name = "Stock", HeaderText = "Stock", DataPropertyName = "Stock", FillWeight = 4.4f, MinimumWidth = 42 }; colStock.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; gridProducts.Columns.Add(colStock);
            gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bonus", HeaderText = "Bonus", DataPropertyName = "Bonus", FillWeight = 3.6f, MinimumWidth = 36 });
            gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pack", HeaderText = "Pack", DataPropertyName = "Pack", FillWeight = 4.4f, MinimumWidth = 42 });

            // Fit on resize
            gridProducts.Resize -= ApplyProductsGridFit;
            // Removed raw grid resize hook to prevent jitter loops

            // Ensure headers are visible with a fixed height
            try { gridProducts.ColumnHeadersVisible = true; gridProducts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing; gridProducts.ColumnHeadersHeight = 44; } catch { }
            // Also ensure pixel fit applies after size changes and after handle created
            gridProducts.HandleCreated += (s, e) => { try { ApplyProductsPixelFit(); } catch { } };
            // Removed raw grid sizechanged hook to prevent jitter loops
            
            gridProducts.DataBindingComplete += (s, e) => { try { ApplyProductsGridFit(this, EventArgs.Empty); ApplyProductsPixelFit(); if (gridProducts.Rows.Count > 0) { gridProducts.FirstDisplayedScrollingRowIndex = 0; gridProducts.ClearSelection(); var first = gridProducts.Rows[0]; var visCol = 0; for (int c=0;c<gridProducts.Columns.Count;c++){ if (gridProducts.Columns[c].Visible){ visCol=c; break;} } gridProducts.CurrentCell = first.Cells[visCol]; } } catch { } };
            // Commit checkbox on click
            gridProducts.CurrentCellDirtyStateChanged += (s, e) => { try { if (gridProducts.CurrentCell is DataGridViewCheckBoxCell) gridProducts.CommitEdit(DataGridViewDataErrorContexts.Commit); } catch { } };
            // Ellipsis paint for Name column to avoid text overflow
            gridProducts.CellPainting -= GridProducts_CellPainting;
            gridProducts.CellPainting += GridProducts_CellPainting;
            
            if (tabProducts != null && tabProducts.Tag == null)
            {
                // Unused in TableLayout Master layout, skipped
            }
            gridProducts.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) btnEditProduct_Click(this, EventArgs.Empty); };
            // apply fit immediately
            try { ApplyProductsPixelFit(); } catch { }
        }
        catch { }
    }

    private void LoadProductsGrid()
    {
        try
        {
            if (gridProducts == null) return;
            using var db = new AppDbContext();
            
            // Sync expired stock before loading
            InventoryService.SyncExpiredStock(db);

            var term = (txtSearchProducts?.Text ?? string.Empty).Trim().ToLowerInvariant();
            System.Collections.Generic.List<dynamic> rows;
            try
            {
                var q = from p in db.Products
                        join b in db.ProductBatches on p.Id equals b.ProductIdRef into pb
                        from b in pb.DefaultIfEmpty()
                        select new {
                            Id = p.Id,
                            BatchId = (int?) (b != null ? b.Id : (int?)null),
                            Hsn = (b != null && b.Hsn != null && b.Hsn != "") ? b.Hsn : (p.Hsn ?? p.Barcode),
                            p.Name,
                            p.Category,
                            BatchNumber = b != null ? b.BatchNumber : null,
                            Expiry = b != null ? b.Expiry : null,
                            Mrp = b != null ? b.Mrp : null,
                            CostPrice = b != null ? b.CostPrice : null,
                            SellingPrice = b != null ? b.SellingPrice : null,
                            Stock = b != null ? b.Stock : null,
                            Bonus = b != null ? b.Bonus : null,
                            Pack = b != null ? b.Pack : null,
                            MarketedBy = b != null ? b.MarketedBy : null
                        };
                if (!string.IsNullOrWhiteSpace(term))
                {
                    q = q.Where(x => (x.Name != null && x.Name.ToLower().Contains(term)) ||
                                     (x.Hsn != null && x.Hsn.ToLower().Contains(term)) ||
                                     (x.Category != null && x.Category.ToLower().Contains(term)) ||
                                     (x.BatchNumber != null && x.BatchNumber.ToLower().Contains(term)));
                }

                var catText = lstProductCategories?.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(catText) && catText != "All categories")
                {
                    q = q.Where(x => x.Category == catText);
                }

                if (chkShowOutOfStock != null && !chkShowOutOfStock.Checked)
                {
                    q = q.Where(x => x.Stock != null && x.Stock > 0);
                }

                rows = new System.Collections.Generic.List<dynamic>(q.OrderBy(x => x.Name).ThenBy(x => x.BatchNumber).ToList());
            }
            catch
            {
                // Fallback to legacy fields
                var pquery = db.Products.AsNoTracking();
                if (!string.IsNullOrWhiteSpace(term))
                {
                    pquery = pquery.Where(p => (p.Name != null && p.Name.ToLower().Contains(term)) || (p.Hsn != null && p.Hsn.ToLower().Contains(term)) || (p.Category != null && p.Category.ToLower().Contains(term)) || (p.OldBatch != null && p.OldBatch.ToLower().Contains(term)) || (p.NewBatch != null && p.NewBatch.ToLower().Contains(term)));
                }
                rows = pquery
                    .SelectMany(p => new[]
                    {
                        new { Id = p.Id, BatchId = (int?)null, Hsn = (p.Hsn ?? p.Barcode), p.Name, p.Category, BatchNumber = p.OldBatch, Expiry = p.OldExpiry, Mrp = p.OldMrp, CostPrice = p.OldCostPrice, SellingPrice = p.OldSellingPrice, Stock = p.OldStock, Bonus = (string)null, Pack = (string)null, MarketedBy = p.MarketedBy },
                        new { Id = p.Id, BatchId = (int?)null, Hsn = (p.Hsn ?? p.Barcode), p.Name, p.Category, BatchNumber = p.NewBatch, Expiry = p.NewExpiry, Mrp = p.NewMrp, CostPrice = p.NewCostPrice, SellingPrice = p.NewSellingPrice, Stock = p.NewStock, Bonus = (string)null, Pack = (string)null, MarketedBy = p.MarketedBy }
                    })
                    .Where(r => r.BatchNumber != null)
                    .OrderBy(r => r.Name)
                    .ThenBy(r => r.BatchNumber)
                    .ToList<dynamic>();
            }
            gridProducts.DataSource = rows;
            ApplyProductsGridFit(this, EventArgs.Empty);
            ApplyProductsPixelFit();

            // Auto-scroll to match if searching
            if (!string.IsNullOrWhiteSpace(term) && gridProducts.Rows.Count > 0)
            {
                BeginInvoke(new Action(() => {
                    try {
                        if (gridProducts.Rows.Count > 0) {
                            gridProducts.ClearSelection();
                            gridProducts.Rows[0].Selected = true;
                            gridProducts.FirstDisplayedScrollingRowIndex = 0;
                        }
                    } catch { }
                }));
            }
        }
        catch { }
    }

    private void ApplyProductsGridFit(object? sender, EventArgs e)
    {
        if (gridProducts == null) return;
        try
        {
            gridProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            void SetCol(string name, float weight, int min)
            {
                var c = gridProducts.Columns[name];
                if (c == null) return;
                try { c.FillWeight = weight; } catch { }
                try { if (c.MinimumWidth != min) c.MinimumWidth = min; } catch { }
            }
            SetCol("Hsn", 8f, 50);
            SetCol("Name", 22f, 140);
            SetCol("BatchNumber", 10f, 80);
            SetCol("Category", 12f, 100);
            SetCol("Expiry", 9f, 70);
            SetCol("Mrp", 7f, 55);
            SetCol("CostPrice", 7f, 55);
            SetCol("SellingPrice", 7f, 55);
            SetCol("Stock", 6f, 50);
            SetCol("Bonus", 5f, 50);
            SetCol("Pack", 6f, 55);
            SetCol("MarketedBy", 10f, 95);

            void AlignRight(string name) { var c = gridProducts.Columns[name]; if (c != null) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
            AlignRight("Mrp"); AlignRight("CostPrice"); AlignRight("SellingPrice"); AlignRight("Stock");
        }
        catch { }
    }

    // NOTE: Event handlers for Products page already exist later in this file.
    // We keep a single set to avoid duplicates.

    private void Form1_InvoicesShortcuts(object? sender, KeyEventArgs e)
    {
        try
        {
            if (Form.ActiveForm != this) return;

            // F1: Open User Manual & Documentation Guide
            if (e.KeyCode == Keys.F1)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                new Forms.HelpManualForm().ShowDialog(this);
                return;
            }

            // F2: Fast New Invoice (Global POS trigger)
            if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnInvoiceAdd_Click(this, EventArgs.Empty);
                return;
            }

            // Determine active tab and route shortcuts
            var active = tabControl1?.SelectedTab;
            bool onInvoices = active == tabInvoices;
            bool onProducts = active == tabProducts;
            bool onCustomers = active == tabCustomers;
            bool onPurchases = active == tabPurchases;

            // Global add new based on tab
            if (e.Control && e.KeyCode == Keys.N)
            {
                e.Handled = true;
                if (onInvoices) { btnInvoiceAdd_Click(this, EventArgs.Empty); return; }
                if (onProducts) { btnAddProduct_Click(this, EventArgs.Empty); return; }
                if (onCustomers) { btnCustomerAdd_Click(this, EventArgs.Empty); return; }
                if (onPurchases) {
                    btnPurchases_Click(this, EventArgs.Empty);
                    if (tabPurchases != null && tabPurchases.Tag is Action refresh) refresh();
                    return;
                }
            }

            // Up/Down selection movement and Enter action per tab
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter)
            {
                if (onInvoices && gridInvoices != null)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    TryFocusGrid(gridInvoices);
                    if (e.KeyCode == Keys.Enter) { btnInvoiceEdit_Click(this, EventArgs.Empty); return; }
                    MoveSelection(gridInvoices, e.KeyCode == Keys.Up); return;
                }
                if (onProducts && gridProducts != null)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    TryFocusGrid(gridProducts);
                    if (e.KeyCode == Keys.Enter) { btnEditProduct_Click(this, EventArgs.Empty); return; }
                    MoveSelection(gridProducts, e.KeyCode == Keys.Up); return;
                }
                if (onCustomers && gridCustomers != null)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    TryFocusGrid(gridCustomers);
                    if (e.KeyCode == Keys.Enter) { var cid = GetSelectedCustomerId(); if (cid != null) ShowCustomerHistoryDialog(cid.Value); return; }
                    MoveSelection(gridCustomers, e.KeyCode == Keys.Up); return;
                }
                if (onPurchases && _globalGridPurchases != null)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    TryFocusGrid(_globalGridPurchases);
                    if (e.KeyCode == Keys.Enter) { _globalActionPurchaseEdit?.Invoke(); return; }
                    MoveSelection(_globalGridPurchases, e.KeyCode == Keys.Up); return;
                }
            }

            // Ctrl+E edit per tab
            if (e.Control && e.KeyCode == Keys.E)
            {
                e.Handled = true;
                if (onInvoices) { btnInvoiceEdit_Click(this, EventArgs.Empty); return; }
                if (onProducts) { btnEditProduct_Click(this, EventArgs.Empty); return; }
                if (onCustomers) { btnCustomerEdit_Click(this, EventArgs.Empty); return; }
                if (onPurchases) { _globalActionPurchaseEdit?.Invoke(); return; }
            }
            // Ctrl+D or Delete delete per tab
            if ((e.Control && e.KeyCode == Keys.D) || e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                if (onInvoices) { btnInvoiceDelete_Click(this, EventArgs.Empty); return; }
                if (onProducts) { btnDeleteProduct_Click(this, EventArgs.Empty); return; }
                if (onCustomers) { btnCustomerDelete_Click(this, EventArgs.Empty); return; }
                if (onPurchases) { _globalActionPurchaseDelete?.Invoke(); return; }
            }
            // Ctrl+W ignore at main level
            if (e.Control && e.KeyCode == Keys.W)
            { e.Handled = true; return; }
        }
        catch { }
    }

    private async void SendSmsSelectedInvoice()
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var db = new AppDbContext();
        var inv = db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Customer)
            .FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        var phone = inv.Customer?.Phone;
        if (string.IsNullOrWhiteSpace(phone)) { MessageBox.Show("Customer has no phone number."); return; }
        MessageBox.Show("SMS functionality is not available. Settings have been removed.");
        return;
    }

    private void VoidSelectedInvoice()
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        var confirm = MessageBox.Show("Are you sure you want to void this invoice?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;
        using var db = new AppDbContext();
        var entity = db.Invoices.FirstOrDefault(i => i.Id == id);
        if (entity == null) { MessageBox.Show("Invoice not found."); return; }
        try
        {
            entity.Status = InvoiceStatus.Void;
            db.SaveChanges();
            LoadInvoices(txtInvoiceSearch.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to void invoice: {ex.Message}");
        }
    }

    private void EnsureInvoicesGridTopVisible()
    {
        try
        {
            if (gridInvoices == null) return;
            gridInvoices.SuspendLayout();
            if (gridInvoices.Rows.Count > 0)
            {
                gridInvoices.FirstDisplayedScrollingRowIndex = 0;
            }
            gridInvoices.ClearSelection();
            gridInvoices.ResumeLayout(false);
        }
        catch { }
    }

    private void GridInvoices_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (gridInvoices.Columns[e.ColumnIndex].Name != "Status" || e.Value == null) return;
        var status = e.Value.ToString();
        if (string.Equals(status, "Paid", System.StringComparison.OrdinalIgnoreCase))
        {
            e.CellStyle.ForeColor = System.Drawing.Color.DarkGreen;
            e.CellStyle.Font = new Font(gridInvoices.Font, FontStyle.Bold);
        }
        else if (string.Equals(status, "PastDue", System.StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Past Due", System.StringComparison.OrdinalIgnoreCase))
        {
            e.CellStyle.ForeColor = System.Drawing.Color.Red;
            e.CellStyle.Font = new Font(gridInvoices.Font, FontStyle.Bold);
        }
        else if (string.Equals(status, "Partial", System.StringComparison.OrdinalIgnoreCase))
        {
            e.CellStyle.ForeColor = System.Drawing.Color.SteelBlue;
            e.CellStyle.Font = new Font(gridInvoices.Font, FontStyle.Bold);
        }
        else if (string.Equals(status, "Unpaid", System.StringComparison.OrdinalIgnoreCase))
        {
            e.CellStyle.ForeColor = System.Drawing.Color.DarkOrange;
            e.CellStyle.Font = new Font(gridInvoices.Font, FontStyle.Bold);
        }
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        // Check for WebView2 Runtime
        if (!IsWebView2Installed())
        {
            var res = MessageBox.Show(
                "Microsoft WebView2 Runtime is required for the Dashboard and AI features but was not found on this device.\n\n" +
                "Would you like to open the download page now?",
                "WebView2 Missing",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (res == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo("https://developer.microsoft.com/en-us/microsoft-edge/webview2/") { UseShellExecute = true }); } catch { }
            }
        }

        // initialize product filters UI
        if (cboProductFilterColumn.Items.Count == 0)
        {
            cboProductFilterColumn.Items.AddRange(new object[] { "Product/Service Name", "ID/SKU" });
        }
        if (cboProductFilterColumn.SelectedIndex < 0) cboProductFilterColumn.SelectedIndex = 0;
        LoadProductCategories();
        LoadProducts();
        if (txtSearchProducts != null)
        {
            txtSearchProducts.TextChanged += (s, ev) => { LoadProducts(txtSearchProducts.Text); };
        }
        // hide duplicate buttons on the main toolbar (keep Invoices tab toolbar only)
        if (btnCreateInvoice != null) btnCreateInvoice.Visible = false;
        if (btnProducts != null) btnProducts.Visible = false;
        if (btnCustomers != null) btnCustomers.Visible = false;
        // also hide the whole main toolstrip to remove blank space
        if (toolStrip1 != null) toolStrip1.Visible = false;
        BuildInvoicesHomeUi();
        LoadCustomers();
        LoadInvoices();
        // Smooth grids to avoid laggy scroll perception
        TrySmoothGrid(gridProducts);
        TrySmoothGrid(gridCustomers);
        ApplyProductsGridLayout();
        WireCustomersPane();
        EnsureProductsFooter();
        UpdateProductsCount();
        UpdateCustomersCount();
        // Live search for customers
        if (txtCustomerSearch != null)
            txtCustomerSearch.TextChanged += (s, ev) => { LoadCustomers(txtCustomerSearch.Text); };
        // Wire Enter on customers grid to jump to invoice history
        WireCustomersGridKeys();
        // Responsive layout on resize
        this.Resize += (s, ev) => { TryResizeProducts(); TryResizeCustomers(); };
        TryResizeProducts();
        TryResizeCustomers();
        // Alerts tab init
        SetupAlertsUI();
        LoadAlerts();
        EnsureProductsFooter();
        UpdateProductsCount();
        UpdateCustomersCount();
        // Add 'Select All' checkbox for products grid (runtime to avoid designer churn)
        if (tabProducts != null && chkProductsSelectAll == null)
        {
            chkProductsSelectAll = new CheckBox();
            chkProductsSelectAll.Text = "Select All";
            chkProductsSelectAll.AutoSize = true;
            chkProductsSelectAll.Location = new System.Drawing.Point(10, 70);
            chkProductsSelectAll.CheckedChanged += (s2, e2) => ToggleSelectAllProducts(chkProductsSelectAll.Checked);
            tabProducts.Controls.Add(chkProductsSelectAll);

            // Add 'Log Damage' button to the product toolbar area if available
            var btnLogDamage = new Button();
            btnLogDamage.Text = "Log Damage";
            btnLogDamage.BackColor = Color.FromArgb(214, 48, 49);
            btnLogDamage.ForeColor = Color.White;
            btnLogDamage.FlatStyle = FlatStyle.Flat;
            btnLogDamage.Size = new Size(110, 30);
            btnLogDamage.Click += btnLogDamage_Click;
            
            // Try adding to the flow layout containing Add/Edit/Delete
            if (pnlProductsTop != null) 
            {
                btnLogDamage.Location = new Point(810, 44);
                pnlProductsTop.Controls.Add(btnLogDamage);
                btnLogDamage.BringToFront();
            }
            else
            {
                btnLogDamage.Location = new Point(780, 44);
                tabProducts.Controls.Add(btnLogDamage);
            }
        }
        if (gridProducts != null)
        {
            gridProducts.MultiSelect = true;
            gridProducts.EditMode = DataGridViewEditMode.EditOnEnter;
            gridProducts.CurrentCellDirtyStateChanged += (s3, e3) =>
            {
                if (gridProducts.IsCurrentCellDirty)
                    gridProducts.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }
    }

    // Runtime field for select-all checkbox
    private CheckBox? chkProductsSelectAll;

    private bool IsWebView2Installed()
    {
        try
        {
            string version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(version);
        }
        catch { return false; }
    }

    private void EnsureProductsSelectColumn()
    {
        if (gridProducts == null) return;
        const string colName = "colSelect";
        if (gridProducts.Columns[colName] == null)
        {
            var col = new DataGridViewCheckBoxColumn
            {
                Name = colName,
                HeaderText = "Select",
                Width = 60,
                ReadOnly = false,
                Frozen = false
            };
            gridProducts.Columns.Insert(0, col);
        }
    }

    private void ToggleSelectAllProducts(bool isChecked)
    {
        if (gridProducts == null) return;
        const string colName = "colSelect";
        if (gridProducts.Columns[colName] == null) return;
        foreach (DataGridViewRow row in gridProducts.Rows)
        {
            if (!row.IsNewRow)
            {
                row.Cells[colName].Value = isChecked;
            }
        }
    }

    private void btnProducts_Click(object sender, EventArgs e)
    {
        try { if (tabControl1 != null && tabProducts != null) tabControl1.SelectedTab = tabProducts; } catch { }
        InitializeProductsGridIfNeeded();
        LoadProductsGrid();
    }

    private void btnRefreshProducts_Click(object sender, EventArgs e)
    {
        LoadProductsGrid();
    }

    private void exitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void LoadProducts() { LoadProductsGrid(); }
    private void LoadProducts(string? search) { LoadProductsGrid(); }
    private void LoadProducts(string? search, string? filterColumn, bool includeOutOfStock, string? category)
    {
        LoadProductsGrid();
    }

    private void LoadProductCategories()
    {
        using var db = new AppDbContext();
        var cats = db.Products
            .Select(p => p.Category)
            .Where(c => c != null && c != "")
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        lstProductCategories.Items.Clear();
        lstProductCategories.Items.Add("All categories");
        foreach (var c in cats) lstProductCategories.Items.Add(c!);
        if (lstProductCategories.Items.Count > 0 && lstProductCategories.SelectedIndex < 0)
            lstProductCategories.SelectedIndex = 0;
    }

    private int? GetSelectedProductId()
    {
        if (gridProducts.CurrentRow == null) return null;
        var row = gridProducts.CurrentRow;
        if (row.Cells["Id"] != null && row.Cells["Id"].Value is int id) return id;
        return null;
    }

    private void btnAddProduct_Click(object sender, EventArgs e)
    {
        var dlg = new ProductEditForm();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            // ProductEditForm already persisted changes. Just reload.
            LoadProductsGrid();
        }
    }

    private void btnEditProduct_Click(object sender, EventArgs e)
    {
        var id = GetSelectedProductId();
        if (id == null) { MessageBox.Show("Select a product."); return; }
        using var db = new AppDbContext();
        var entity = db.Products.FirstOrDefault(p => p.Id == id);
        if (entity == null) { MessageBox.Show("Product not found."); return; }
        // Try to resolve selected batch (by BatchId hidden column or BatchNumber)
        ProductBatch? selBatch = null;
        try
        {
            if (gridProducts?.CurrentRow != null)
            {
                int? batchId = null;
                try { if (gridProducts.CurrentRow.Cells["BatchId"]?.Value is int bid) batchId = bid; } catch { }
                if (batchId.HasValue)
                {
                    selBatch = db.ProductBatches.FirstOrDefault(b => b.Id == batchId.Value);
                }
                else
                {
                    var bn = gridProducts.CurrentRow.Cells["BatchNumber"]?.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(bn)) selBatch = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == entity.Id && b.BatchNumber == bn);
                }
            }
        }
        catch { }
        var dlg = new ProductEditForm(entity, selBatch);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            // ProductEditForm already persisted changes. Just reload.
            LoadProductsGrid();
        }
    }

    private void btnLogDamage_Click(object? sender, EventArgs e)
    {
        var id = GetSelectedProductId();
        if (id == null) { MessageBox.Show("Select a product to log damage."); return; }
        using var db = new AppDbContext();
        var entity = db.Products.FirstOrDefault(p => p.Id == id);
        if (entity == null) { MessageBox.Show("Product not found."); return; }
        
        // Try to resolve selected batch
        ProductBatch? selBatch = null;
        try {
            if (gridProducts?.CurrentRow != null) {
                int? batchId = null;
                if (gridProducts.CurrentRow.Cells["BatchId"]?.Value is int bid) batchId = bid;
                if (batchId.HasValue) selBatch = db.ProductBatches.FirstOrDefault(b => b.Id == batchId.Value);
            }
        } catch { }

        var dlg = new DamageEntryForm(entity, selBatch);
        if (dlg.ShowDialog(this) == DialogResult.OK) {
            LoadProductsGrid();
        }
    }

    private void btnDeleteProduct_Click(object sender, EventArgs e)
    {
        if (gridProducts == null)
        {
            var id = GetSelectedProductId();
            if (id == null) { MessageBox.Show("Select a product."); return; }
            if (MessageBox.Show("Delete selected product?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            using var db0 = new AppDbContext();
            var entity0 = db0.Products.FirstOrDefault(p => p.Id == id);
            if (entity0 == null) { MessageBox.Show("Product not found."); return; }
            try { db0.RecycleBin.Add(new RecycleBinItem { EntityType = "Product", EntityId = entity0.Id, JsonData = System.Text.Json.JsonSerializer.Serialize(entity0), DeletedAt = System.DateTime.UtcNow }); } catch { }
            db0.Products.Remove(entity0);
            db0.SaveChanges();
            LoadProducts(txtSearchProducts.Text);
            return;
        }

        const string colSelect = "colSelect";
        var hasSelectCol = gridProducts.Columns[colSelect] != null;
        var checkedIds = new System.Collections.Generic.List<int>();
        if (hasSelectCol)
        {
            foreach (DataGridViewRow row in gridProducts.Rows)
            {
                if (row.IsNewRow) continue;
                var isChecked = false;
                if (row.Cells[colSelect] is DataGridViewCheckBoxCell cb && cb.Value is bool b) isChecked = b;
                if (isChecked && row.Cells["Id"]?.Value is int rid) checkedIds.Add(rid);
            }
        }

        using var db = new AppDbContext();
        // Case 1: some rows explicitly checked -> delete those
        if (checkedIds.Count > 0)
        {
            if (MessageBox.Show($"Delete {checkedIds.Count} selected product(s)?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            var toDelete = db.Products.Where(p => checkedIds.Contains(p.Id)).ToList();
            try
            {
                foreach (var ent in toDelete)
                    db.RecycleBin.Add(new RecycleBinItem { EntityType = "Product", EntityId = ent.Id, JsonData = System.Text.Json.JsonSerializer.Serialize(ent), DeletedAt = System.DateTime.UtcNow });
            }
            catch { }
            db.Products.RemoveRange(toDelete);
            db.SaveChanges();
            LoadProductsGrid();
            return;
        }

        // Case 2: Select All checked -> delete all filtered, or entire DB if no filters
        bool isSelectAll = (chkProductsSelectAll?.Checked ?? false);
        if (isSelectAll)
        {
            var hasSearch = !string.IsNullOrWhiteSpace(txtSearchProducts?.Text);
            var category = lstProductCategories?.SelectedItem?.ToString();
            var isAllCategory = string.IsNullOrWhiteSpace(category) || category == "All categories";
            var mode = cboExpiryFilterMode?.SelectedItem?.ToString() ?? "None";
            var noExpiryFilter = mode == "None";

            if (!hasSearch && isAllCategory && noExpiryFilter)
            {
                var total = db.Products.Count();
                if (MessageBox.Show($"Delete ALL products ({total})? This cannot be undone.", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                try
                {
                    foreach (var ent in db.Products.ToList())
                        db.RecycleBin.Add(new RecycleBinItem { EntityType = "Product", EntityId = ent.Id, JsonData = System.Text.Json.JsonSerializer.Serialize(ent), DeletedAt = System.DateTime.UtcNow });
                }
                catch { }
                db.Products.RemoveRange(db.Products);
                db.SaveChanges();
                LoadProductCategories();
                LoadProductsGrid();
                return;
            }
            else
            {
                // Delete all currently visible (filtered) rows in the grid
                var visibleIds = new System.Collections.Generic.List<int>();
                foreach (DataGridViewRow row in gridProducts.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (row.Cells["Id"]?.Value is int rid) visibleIds.Add(rid);
                }
                if (visibleIds.Count == 0) { MessageBox.Show("No products to delete for current filter."); return; }
                if (MessageBox.Show($"Delete {visibleIds.Count} filtered product(s)?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                var toDelete = db.Products.Where(p => visibleIds.Contains(p.Id)).ToList();
                try
                {
                    foreach (var ent in toDelete)
                        db.RecycleBin.Add(new RecycleBinItem { EntityType = "Product", EntityId = ent.Id, JsonData = System.Text.Json.JsonSerializer.Serialize(ent), DeletedAt = System.DateTime.UtcNow });
                }
                catch { }
                db.Products.RemoveRange(toDelete);
                db.SaveChanges();
                LoadProductsGrid();
                return;
            }
        }

        // Fallback: single current row delete
        {
            var id = GetSelectedProductId();
            if (id == null) { MessageBox.Show("Select a product or use checkboxes."); return; }
            if (MessageBox.Show("Delete selected product?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            var entity = db.Products.FirstOrDefault(p => p.Id == id);
            if (entity == null) { MessageBox.Show("Product not found."); return; }
            try { db.RecycleBin.Add(new RecycleBinItem { EntityType = "Product", EntityId = entity.Id, JsonData = System.Text.Json.JsonSerializer.Serialize(entity), DeletedAt = System.DateTime.UtcNow }); } catch { }
            db.Products.Remove(entity);
            db.SaveChanges();
            LoadProductsGrid();
        }
    }

    private void btnSearchProducts_Click(object sender, System.EventArgs e)
    {
        LoadProductsGrid();
    }

    private void btnExportProducts_Click(object sender, System.EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = "Products.xlsx" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var db = new AppDbContext();
            var batches = db.ProductBatches
                .Include(b => b.Product)
                .OrderBy(b => b.Product!.Name).ThenBy(b => b.BatchNumber)
                .ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Products");

            // --- Header row ---
            string[] headers = {
                "SKU", "Name", "HSN", "Category",
                "Batch Number", "Expiry", "MRP", "Cost Price", "Selling Price", "Stock",
                "Pack", "Bonus", "Marketed By",
                "Margin", "Margin %", "CP %", "SP %",
                "Total Value (CP)", "Total Value (SP)",
                "Expiry Alert Days", "Low Stock Threshold"
            };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 2;
            foreach (var b in batches)
            {
                var p = b.Product;
                decimal mrp = b.Mrp ?? 0m;
                decimal cp = b.CostPrice ?? 0m;
                decimal sp = b.SellingPrice ?? 0m;
                decimal stock = b.Stock ?? 0m;
                decimal margin = sp - cp;
                decimal marginPct = cp > 0 ? Math.Round(margin / cp * 100, 2) : 0m;
                decimal cpPct = mrp > 0 ? Math.Round(cp / mrp * 100, 2) : 0m;
                decimal spPct = mrp > 0 ? Math.Round(sp / mrp * 100, 2) : 0m;
                decimal totalCP = stock * cp;
                decimal totalSP = stock * sp;

                int c = 1;
                ws.Cell(row, c++).Value = p?.Barcode ?? "";
                ws.Cell(row, c++).Value = p?.Name ?? "";
                ws.Cell(row, c++).Value = b.Hsn ?? p?.Hsn ?? "";
                ws.Cell(row, c++).Value = p?.Category ?? "";
                ws.Cell(row, c++).Value = b.BatchNumber;
                if (b.Expiry.HasValue)
                    ws.Cell(row, c).Value = b.Expiry.Value;
                c++;
                ws.Cell(row, c++).Value = mrp;
                ws.Cell(row, c++).Value = cp;
                ws.Cell(row, c++).Value = sp;
                ws.Cell(row, c++).Value = stock;
                ws.Cell(row, c++).Value = b.Pack ?? "";
                ws.Cell(row, c++).Value = b.Bonus ?? "";
                ws.Cell(row, c++).Value = b.MarketedBy ?? "";
                ws.Cell(row, c++).Value = margin;
                ws.Cell(row, c++).Value = marginPct;
                ws.Cell(row, c++).Value = cpPct;
                ws.Cell(row, c++).Value = spPct;
                ws.Cell(row, c++).Value = totalCP;
                ws.Cell(row, c++).Value = totalSP;
                ws.Cell(row, c++).Value = p?.ExpiryAlertDays;
                ws.Cell(row, c++).Value = p?.LowStockThreshold;
                row++;
            }

            // Format columns
            ws.Column(6).Style.DateFormat.Format = "dd/MM/yyyy"; // Expiry
            for (int ci = 7; ci <= 10; ci++) ws.Column(ci).Style.NumberFormat.Format = "#,##0.00"; // MRP,CP,SP,Stock
            for (int ci = 14; ci <= 19; ci++) ws.Column(ci).Style.NumberFormat.Format = "#,##0.00"; // Calculated
            ws.Columns().AdjustToContents();

            // Freeze header row
            ws.SheetView.FreezeRows(1);

            wb.SaveAs(sfd.FileName);
            MessageBox.Show($"Exported {row - 2} batch rows.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnExportProductsPdf_Click(object sender, System.EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", FileName = "Products.pdf" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var db = new AppDbContext();
            var batches = db.ProductBatches
                .Include(b => b.Product)
                .OrderBy(b => b.Product!.Name).ThenBy(b => b.BatchNumber)
                .ToList();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.ContentFromLeftToRight();
                    page.Margin(10);
                    page.Header().Text("Products & Batches Report").SemiBold().FontSize(14);
                    page.Content().Column(col =>
                    {
                        col.Spacing(5);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);   // SKU
                                columns.RelativeColumn(2);    // Name
                                columns.ConstantColumn(65);   // Batch
                                columns.ConstantColumn(65);   // Expiry
                                columns.ConstantColumn(50);   // MRP
                                columns.ConstantColumn(50);   // CP
                                columns.ConstantColumn(50);   // SP
                                columns.ConstantColumn(45);   // Stock
                                columns.ConstantColumn(50);   // Margin
                                columns.ConstantColumn(50);   // Total(CP)
                            });
                            table.Header(header =>
                            {
                                header.Cell().Text("SKU").Bold().FontSize(8);
                                header.Cell().Text("Name").Bold().FontSize(8);
                                header.Cell().Text("Batch").Bold().FontSize(8);
                                header.Cell().Text("Expiry").Bold().FontSize(8);
                                header.Cell().Text("MRP").Bold().FontSize(8);
                                header.Cell().Text("CP").Bold().FontSize(8);
                                header.Cell().Text("SP").Bold().FontSize(8);
                                header.Cell().Text("Stock").Bold().FontSize(8);
                                header.Cell().Text("Margin").Bold().FontSize(8);
                                header.Cell().Text("Total(CP)").Bold().FontSize(8);
                            });

                            foreach (var b in batches)
                            {
                                var p = b.Product;
                                decimal cp = b.CostPrice ?? 0m;
                                decimal sp = b.SellingPrice ?? 0m;
                                decimal stock = b.Stock ?? 0m;
                                table.Cell().Text(p?.Barcode ?? "").FontSize(7);
                                table.Cell().Text(p?.Name ?? "").FontSize(7);
                                table.Cell().Text(b.BatchNumber).FontSize(7);
                                table.Cell().Text(b.Expiry?.ToString("dd/MM/yyyy") ?? "").FontSize(7);
                                table.Cell().Text((b.Mrp ?? 0m).ToString("0.00")).FontSize(7);
                                table.Cell().Text(cp.ToString("0.00")).FontSize(7);
                                table.Cell().Text(sp.ToString("0.00")).FontSize(7);
                                table.Cell().Text(stock.ToString("0.##")).FontSize(7);
                                table.Cell().Text((sp - cp).ToString("0.00")).FontSize(7);
                                table.Cell().Text((stock * cp).ToString("0.00")).FontSize(7);
                            }
                        });
                    });
                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Generated: ");
                        x.Span(System.DateTime.Now.ToString("g"));
                    });
                });
            }).GeneratePdf(sfd.FileName);

            MessageBox.Show($"Exported {batches.Count} rows.", "PDF Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnImportProducts_Click(object sender, System.EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        using var db = new AppDbContext();
        int imported = 0, skipped = 0;
        var ext = System.IO.Path.GetExtension(ofd.FileName).ToLowerInvariant();

        // Parse rows into a common list of dictionaries (header -> value)
        var rows = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();

        if (ext == ".xlsx")
        {
            using var fs = new System.IO.FileStream(ofd.FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            using var wb = new XLWorkbook(fs);
            var ws = wb.Worksheets.First();

            // Build header map from row 1
            var headerMap = new System.Collections.Generic.Dictionary<int, string>();
            foreach (var cell in ws.Row(1).CellsUsed())
            {
                var key = cell.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(key))
                    headerMap[cell.Address.ColumnNumber] = key!;
            }

            for (int r = 2; r <= ws.LastRowUsed()?.RowNumber(); r++)
            {
                var dict = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                bool hasAny = false;
                foreach (var kvp in headerMap)
                {
                    var val = ws.Cell(r, kvp.Key).GetString()?.Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(val)) hasAny = true;
                    dict[kvp.Value] = val;
                }
                if (!hasAny) break;
                rows.Add(dict);
            }
        }
        else // CSV
        {
            var lines = System.IO.File.ReadAllLines(ofd.FileName);
            if (lines.Length < 2) { MessageBox.Show("Empty or header-only file."); return; }
            var headerNames = lines[0].Split(',').Select(h => h.Trim()).ToArray();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                var dict = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < headerNames.Length; j++)
                {
                    dict[headerNames[j]] = j < parts.Length ? parts[j].Trim() : "";
                }
                rows.Add(dict);
            }
        }

        // Helper functions to read from dictionary
        string? GetStr(System.Collections.Generic.Dictionary<string, string> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }
        decimal? GetDec(System.Collections.Generic.Dictionary<string, string> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && decimal.TryParse(v, out var r)) return r;
            return null;
        }
        DateTime? GetDate(System.Collections.Generic.Dictionary<string, string> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                {
                    if (DateTime.TryParse(v, out var dt)) return dt;
                    // Try dd/MM/yyyy format
                    if (DateTime.TryParseExact(v, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" },
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt2)) return dt2;
                }
            return null;
        }
        int? GetInt(System.Collections.Generic.Dictionary<string, string> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && int.TryParse(v, out var r)) return r;
            return null;
        }

        foreach (var row in rows)
        {
            var name = GetStr(row, "Name", "Product Name", "Product");
            var barcode = GetStr(row, "SKU", "Barcode", "Product Code");
            if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

            var category = GetStr(row, "Category", "Cat");
            var hsn = GetStr(row, "HSN", "HSN Code");
            var batchNumber = GetStr(row, "Batch Number", "Batch", "BatchNumber", "Batch No");
            var expiry = GetDate(row, "Expiry", "Expiry Date", "Exp Date", "ExpiryDate");
            var mrp = GetDec(row, "MRP", "M.R.P", "Maximum Retail Price");
            var costPrice = GetDec(row, "Cost Price", "CostPrice", "CP", "Purchase Price");
            var sellingPrice = GetDec(row, "Selling Price", "SellingPrice", "SP", "Sale Price");
            var stock = GetDec(row, "Stock", "Qty", "Quantity");
            var pack = GetStr(row, "Pack", "Pack Size");
            var bonus = GetStr(row, "Bonus", "Free", "Scheme");
            var marketedBy = GetStr(row, "Marketed By", "MarketedBy", "Manufacturer", "Company");
            var alertDays = GetInt(row, "Expiry Alert Days", "ExpiryAlertDays", "Alert Days");
            var lowThreshold = GetDec(row, "Low Stock Threshold", "LowStockThreshold", "Min Stock");

            // Find or create Product
            Product? product = null;
            if (!string.IsNullOrWhiteSpace(barcode))
                product = db.Products.FirstOrDefault(p => p.Barcode == barcode);
            if (product == null && !string.IsNullOrWhiteSpace(name))
                product = db.Products.FirstOrDefault(p => p.Name == name);

            if (product == null)
            {
                product = new Product
                {
                    Barcode = barcode ?? "",
                    Name = name!,
                    Category = category,
                    Hsn = hsn,
                    ExpiryAlertDays = alertDays ?? Services.AppSettingsService.DefaultExpiryAlertDays,
                    LowStockThreshold = lowThreshold
                };
                db.Products.Add(product);
                db.SaveChanges(); // get the Id
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(category)) product.Category = category;
                if (!string.IsNullOrWhiteSpace(hsn)) product.Hsn = hsn;
                if (alertDays.HasValue) product.ExpiryAlertDays = alertDays.Value;
                if (lowThreshold.HasValue) product.LowStockThreshold = lowThreshold.Value;
                product.UpdatedAt = DateTime.UtcNow;
            }

            // Find or create ProductBatch
            if (!string.IsNullOrWhiteSpace(batchNumber))
            {
                var existingBatch = db.ProductBatches
                    .FirstOrDefault(pb => pb.ProductIdRef == product.Id && pb.BatchNumber == batchNumber);

                if (existingBatch == null)
                {
                    db.ProductBatches.Add(new ProductBatch
                    {
                        ProductIdRef = product.Id,
                        BatchNumber = batchNumber!,
                        Hsn = hsn ?? product.Hsn,
                        Expiry = expiry,
                        Mrp = mrp,
                        CostPrice = costPrice,
                        SellingPrice = sellingPrice,
                        Stock = stock,
                        Pack = pack,
                        Bonus = bonus,
                        MarketedBy = marketedBy
                    });
                }
                else
                {
                    if (mrp.HasValue) existingBatch.Mrp = mrp.Value;
                    if (costPrice.HasValue) existingBatch.CostPrice = costPrice.Value;
                    if (sellingPrice.HasValue) existingBatch.SellingPrice = sellingPrice.Value;
                    if (stock.HasValue) existingBatch.Stock = stock.Value;
                    if (expiry.HasValue) existingBatch.Expiry = expiry.Value;
                    if (!string.IsNullOrWhiteSpace(hsn)) existingBatch.Hsn = hsn;
                    if (!string.IsNullOrWhiteSpace(pack)) existingBatch.Pack = pack;
                    if (!string.IsNullOrWhiteSpace(bonus)) existingBatch.Bonus = bonus;
                    if (!string.IsNullOrWhiteSpace(marketedBy)) existingBatch.MarketedBy = marketedBy;
                    existingBatch.UpdatedAt = DateTime.UtcNow;
                }
            }
            else if (mrp.HasValue || costPrice.HasValue || stock.HasValue)
            {
                // No batch number provided — create a default batch
                var defaultBatch = "BATCH-" + (product.Id).ToString("D4");
                var existingBatch = db.ProductBatches
                    .FirstOrDefault(pb => pb.ProductIdRef == product.Id && pb.BatchNumber == defaultBatch);
                if (existingBatch == null)
                {
                    db.ProductBatches.Add(new ProductBatch
                    {
                        ProductIdRef = product.Id,
                        BatchNumber = defaultBatch,
                        Hsn = hsn ?? product.Hsn,
                        Expiry = expiry,
                        Mrp = mrp,
                        CostPrice = costPrice,
                        SellingPrice = sellingPrice,
                        Stock = stock,
                        Pack = pack,
                        Bonus = bonus,
                        MarketedBy = marketedBy
                    });
                }
                else
                {
                    if (mrp.HasValue) existingBatch.Mrp = mrp.Value;
                    if (costPrice.HasValue) existingBatch.CostPrice = costPrice.Value;
                    if (sellingPrice.HasValue) existingBatch.SellingPrice = sellingPrice.Value;
                    if (stock.HasValue) existingBatch.Stock = stock.Value;
                    if (expiry.HasValue) existingBatch.Expiry = expiry.Value;
                    existingBatch.UpdatedAt = DateTime.UtcNow;
                }
            }
            imported++;
        }

        db.SaveChanges();
        LoadProductCategories();
        LoadProducts(txtSearchProducts.Text);
        var msg = $"Imported/updated {imported} product rows.";
        if (skipped > 0) msg += $"\nSkipped {skipped} rows (no Name found).";
        MessageBox.Show(msg, "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void cboProductFilterColumn_SelectedIndexChanged(object sender, System.EventArgs e)
    {
        LoadProducts(txtSearchProducts.Text);
    }

    private void chkShowOutOfStock_CheckedChanged(object sender, System.EventArgs e)
    {
        LoadProducts(txtSearchProducts.Text);
    }

    private void lstProductCategories_SelectedIndexChanged(object sender, System.EventArgs e)
    {
        LoadProducts(txtSearchProducts.Text);
    }

    // Expiry filter UI handlers
    private void cboExpiryFilterMode_SelectedIndexChanged(object sender, System.EventArgs e)
    {
        ToggleExpiryControls();
    }

    private void btnApplyExpiryFilter_Click(object sender, System.EventArgs e)
    {
        LoadProducts(txtSearchProducts.Text);
    }

    private void btnResetExpiryFilter_Click(object sender, System.EventArgs e)
    {
        if (cboExpiryFilterMode != null) cboExpiryFilterMode.SelectedItem = "None";
        LoadProducts(txtSearchProducts.Text);
    }

    private void ToggleExpiryControls()
    {
        var mode = cboExpiryFilterMode?.SelectedItem?.ToString() ?? "None";
        if (dtpExpiryFrom == null || dtpExpiryTo == null || dtpExpiryMonth == null || dtpExpiryDate == null) return;
        // Hide all first
        dtpExpiryFrom.Visible = false; dtpExpiryTo.Visible = false; dtpExpiryMonth.Visible = false; dtpExpiryDate.Visible = false;
        if (mode == "Date Range") { dtpExpiryFrom.Visible = true; dtpExpiryTo.Visible = true; }
        else if (mode == "Month") { dtpExpiryMonth.Visible = true; }
        else if (mode == "Specific Date") { dtpExpiryDate.Visible = true; }
    }

    private void GridProducts_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (gridProducts == null || e.RowIndex < 0) return;
        // If new per-batch columns are used (no legacy columns), skip legacy formatting
        bool hasLegacy = gridProducts.Columns.Contains("NewExpiry") || gridProducts.Columns.Contains("OldExpiry") || gridProducts.Columns.Contains("VeryOldExpiry");
        if (!hasLegacy) return;
        var row = gridProducts.Rows[e.RowIndex];
        DateTime? GetDate(string col)
        {
            if (row.Cells[col] == null) return null;
            var v = row.Cells[col].Value;
            if (v == null) return null;
            if (v is DateTime dt) return dt.Date;
            if (DateTime.TryParse(v.ToString(), out var parsed)) return parsed.Date;
            return null;
        }
        decimal GetDec(string col)
        {
            if (row.Cells[col] == null) return 0m;
            var v = row.Cells[col].Value;
            if (v == null) return 0m;
            if (v is decimal d) return d;
            if (decimal.TryParse(v.ToString(), out var parsed)) return parsed;
            return 0m;
        }
        int GetInt(string col)
        {
            if (row.Cells[col] == null) return 0;
            var v = row.Cells[col].Value;
            if (v == null) return 0;
            if (v is int i) return i;
            if (int.TryParse(v.ToString(), out var parsed)) return parsed;
            return 0;
        }

        var today = DateTime.Today;
        var expNew = GetDate("NewExpiry");
        var expOld = GetDate("OldExpiry");
        var expVeryOld = GetDate("VeryOldExpiry");
        int alertDays = GetInt("ExpiryAlertDays"); if (alertDays <= 0) alertDays = Services.AppSettingsService.DefaultExpiryAlertDays;
        decimal lowThresh = GetDec("LowStockThreshold");
        decimal sNew = GetDec("NewStock");
        decimal sOld = GetDec("OldStock");
        decimal sVeryOld = GetDec("VeryOldStock");

        // Determine state
        double minDaysLeft = double.PositiveInfinity;
        foreach (var d in new[] { expNew, expOld, expVeryOld })
        {
            if (d.HasValue)
            {
                var days = (d.Value.Date - today).TotalDays;
                if (days < minDaysLeft) minDaysLeft = days;
            }
        }
        bool expired = minDaysLeft <= 0 && minDaysLeft != double.PositiveInfinity;
        bool nearExpiry = !expired && minDaysLeft <= alertDays;
        bool lowStock = (lowThresh > 0) && (sNew <= lowThresh || sOld <= lowThresh || sVeryOld <= lowThresh);

        // Apply coloring with priority: expired > near-expiry > low-stock
        var style = row.DefaultCellStyle;
        if (expired)
        {
            style.BackColor = System.Drawing.Color.MistyRose;
            style.ForeColor = System.Drawing.Color.DarkRed;
        }
        else if (nearExpiry)
        {
            style.BackColor = System.Drawing.Color.LemonChiffon;
            style.ForeColor = System.Drawing.Color.DarkOrange;
        }
        else if (lowStock)
        {
            style.BackColor = System.Drawing.Color.AliceBlue;
            style.ForeColor = System.Drawing.Color.MidnightBlue;
        }
        else
        {
            style.BackColor = System.Drawing.Color.White;
            style.ForeColor = System.Drawing.Color.Black;
        }
    }

    // Customers
    private void LoadCustomers(string? search = null)
    {
        using var db = new AppDbContext();
        var q = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(c => (c.Name != null && c.Name.ToLower().Contains(s))
                         || (c.Email != null && c.Email.ToLower().Contains(s))
                         || (c.Phone != null && c.Phone.ToLower().Contains(s)));
        }
        var baseList = q.OrderBy(c => c.Name).ToList();
        var rows = new System.Collections.Generic.List<object>();
        foreach (var c in baseList)
        {
            // totals
            var invs = db.Invoices.Where(i => i.CustomerId == c.Id);
            decimal totalPurchase = invs.Select(i => (decimal?)i.Total).Sum() ?? 0m;
            decimal backDues = invs.Where(i => i.Status != Models.InvoiceStatus.Paid && i.Status != Models.InvoiceStatus.Void)
                                    .Select(i => (decimal?)(i.Total - i.TotalPaid)).Sum() ?? 0m;
            DateTime? lastDate = invs.OrderByDescending(i => i.InvoiceDate).Select(i => (DateTime?)i.InvoiceDate).FirstOrDefault();
            // profit: sum((sell - cost)*qty) across all items for this customer
            decimal profit = 0m;
            try
            {
                var custInvIds = invs.Select(i => i.Id).ToList();
                var items = db.InvoiceItems.Where(ii => custInvIds.Contains(ii.InvoiceId)).ToList();
                var prodIds = items.Where(ii => ii.ProductIdRef.HasValue).Select(ii => ii.ProductIdRef!.Value).Distinct().ToList();
                var prods = db.Products.Where(p => prodIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
                foreach (var it in items)
                {
                    Models.Product? p = null;
                    if (it.ProductIdRef.HasValue) prods.TryGetValue(it.ProductIdRef.Value, out p);
                    if (p == null && !string.IsNullOrWhiteSpace(it.ProductId)) p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                    decimal cost = 0m;
                    if (p != null)
                    {
                        string? batch = ExtractBatch(it.Description);
                        if (!string.IsNullOrWhiteSpace(batch))
                        {
                            if (string.Equals(batch, p.NewBatch, StringComparison.OrdinalIgnoreCase)) cost = p.NewCostPrice ?? 0m;
                            else if (string.Equals(batch, p.OldBatch, StringComparison.OrdinalIgnoreCase)) cost = p.OldCostPrice ?? 0m;
                            else if (string.Equals(batch, p.VeryOldBatch, StringComparison.OrdinalIgnoreCase)) cost = p.VeryOldCostPrice ?? 0m;
                        }
                        if (cost == 0m) cost = p.NewCostPrice ?? p.OldCostPrice ?? p.VeryOldCostPrice ?? 0m;
                    }
                    profit += (it.Price - cost) * it.Quantity;
                }
            }
            catch { }
            rows.Add(new {
                c.Id, c.Name, c.Email, c.Phone, c.City, c.State, c.GstVatNumber,
                TotalPurchase = totalPurchase,
                Profit = profit,
                BackDues = backDues,
                LastDate = lastDate
            });
        }
        gridCustomers.DataSource = rows;
        if (gridCustomers.Columns["Id"] != null) gridCustomers.Columns["Id"].Visible = false;
        TrySmoothGrid(gridCustomers);
        // Ensure columns stretch to fill available width
        gridCustomers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Reasonable minimums and fill weights so text columns take more width
        void SetCol(string name, int minW, float weight, DataGridViewContentAlignment? align = null, string? fmt = null)
        {
            var c = gridCustomers.Columns[name];
            if (c == null) return;
            try { c.FillWeight = weight; } catch { }
            try { c.MinimumWidth = minW; } catch { /* some frameworks throw if not attached yet */ }
            if (align.HasValue) { try { c.DefaultCellStyle.Alignment = align.Value; } catch { } }
            if (!string.IsNullOrEmpty(fmt)) { try { c.DefaultCellStyle.Format = fmt; } catch { } }
        }
        SetCol("Name", 160, 220f);
        SetCol("Email", 160, 180f);
        SetCol("Phone", 120, 120f);
        SetCol("City", 120, 120f);
        SetCol("State", 90, 90f);
        SetCol("GstVatNumber", 120, 120f);
        SetCol("TotalPurchase", 110, 110f, DataGridViewContentAlignment.MiddleRight, "0.00");
        SetCol("Profit", 100, 100f, DataGridViewContentAlignment.MiddleRight, "0.00");
        SetCol("BackDues", 110, 110f, DataGridViewContentAlignment.MiddleRight, "0.00");
        SetCol("LastDate", 110, 110f);
        // Make every visible column fill so no right-side blank area remains
        foreach (DataGridViewColumn col in gridCustomers.Columns)
        {
            if (col.Visible)
            {
                try { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; } catch { }
                // Keep a small minimum to avoid zero-width if window is tiny
                if (col.MinimumWidth < 60) col.MinimumWidth = Math.Max(60, col.MinimumWidth);
            }
        }
        // Format new computed columns
        if (gridCustomers.Columns["TotalPurchase"] != null) { gridCustomers.Columns["TotalPurchase"].HeaderText = "Total Purchase"; gridCustomers.Columns["TotalPurchase"].DefaultCellStyle.Format = "0.00"; gridCustomers.Columns["TotalPurchase"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
        if (gridCustomers.Columns["Profit"] != null) { gridCustomers.Columns["Profit"].DefaultCellStyle.Format = "0.00"; gridCustomers.Columns["Profit"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
        if (gridCustomers.Columns["BackDues"] != null) { gridCustomers.Columns["BackDues"].HeaderText = "Back Dues"; gridCustomers.Columns["BackDues"].DefaultCellStyle.Format = "0.00"; gridCustomers.Columns["BackDues"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
        if (gridCustomers.Columns["LastDate"] != null) { gridCustomers.Columns["LastDate"].HeaderText = "Last Date"; gridCustomers.Columns["LastDate"].DefaultCellStyle.Format = "dd/MM/yyyy"; }
        // Keep selection change wired once
        gridCustomers.SelectionChanged -= GridCustomers_SelectionChanged;
        gridCustomers.SelectionChanged += GridCustomers_SelectionChanged;
        // Double-click opens Bill History popup
        gridCustomers.CellDoubleClick -= GridCustomers_CellDoubleClick;
        gridCustomers.CellDoubleClick += GridCustomers_CellDoubleClick;
        // Load details for current selection
        GridCustomers_SelectionChanged(this, EventArgs.Empty);
        UpdateCustomersCount();
    }

    private int? GetSelectedCustomerId()
    {
        if (gridCustomers.CurrentRow == null) return null;
        var row = gridCustomers.CurrentRow;
        if (row.Cells["Id"] != null && row.Cells["Id"].Value is int id) return id;
        return null;
    }

    private void btnCustomerRefresh_Click(object sender, EventArgs e)
    {
        LoadCustomers(txtCustomerSearch.Text);
    }

    private void btnCustomerSearch_Click(object sender, EventArgs e)
    {
        LoadCustomers(txtCustomerSearch.Text);
    }

    private void btnCustomerAdd_Click(object sender, EventArgs e)
    {
        var dlg = new CustomerEditForm();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            // CustomerEditForm already saves to DB; just reload the list
            LoadCustomers(txtCustomerSearch.Text);
        }
    }

    private void btnCustomerEdit_Click(object sender, EventArgs e)
    {
        var id = GetSelectedCustomerId();
        if (id == null) { MessageBox.Show("Select a customer."); return; }
        using var db = new AppDbContext();
        var entity = db.Customers.FirstOrDefault(c => c.Id == id);
        if (entity == null) { MessageBox.Show("Customer not found."); return; }
        var dlg = new CustomerEditForm(new Customer
        {
            Id = entity.Id,
            Name = entity.Name,
            Email = entity.Email,
            Phone = entity.Phone,
            Address1 = entity.Address1,
            Address2 = entity.Address2,
            City = entity.City,
            State = entity.State,
            PostalCode = entity.PostalCode,
            Country = entity.Country,
            GstVatNumber = entity.GstVatNumber
        });
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            entity.Name = dlg.Model.Name;
            entity.Email = dlg.Model.Email;
            entity.Phone = dlg.Model.Phone;
            entity.Address1 = dlg.Model.Address1;
            entity.Address2 = dlg.Model.Address2;
            entity.City = dlg.Model.City;
            entity.State = dlg.Model.State;
            entity.PostalCode = dlg.Model.PostalCode;
            entity.Country = dlg.Model.Country;
            entity.GstVatNumber = dlg.Model.GstVatNumber;
            entity.UpdatedAt = System.DateTime.UtcNow;
            db.SaveChanges();
            LoadCustomers(txtCustomerSearch.Text);
        }
    }

    private void btnCustomerDelete_Click(object sender, EventArgs e)
    {
        var id = GetSelectedCustomerId();
        if (id == null) { MessageBox.Show("Select a customer."); return; }
        if (MessageBox.Show("Delete selected customer?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        using var db = new AppDbContext();
        var entity = db.Customers.FirstOrDefault(c => c.Id == id);
        if (entity == null) { MessageBox.Show("Customer not found."); return; }
        try { db.RecycleBin.Add(new RecycleBinItem { EntityType = "Customer", EntityId = entity.Id, JsonData = System.Text.Json.JsonSerializer.Serialize(entity), DeletedAt = System.DateTime.UtcNow }); } catch { }
        db.Customers.Remove(entity);
        db.SaveChanges();
        LoadCustomers(txtCustomerSearch.Text);
    }

    // --- Smooth grid helpers and layouts ---
    private void TrySmoothGrid(DataGridView? g)
    {
        if (g == null) return;
        try { typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(g, true, null); } catch { }
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        g.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        g.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
    }

    // Resize helpers to keep grids responsive within tabs
    private void TryResizeProducts()
    {
        try
        {
            if (tabProducts == null || gridProducts == null) return;
            // leave top controls area ~88px and bottom footer 22px
            gridProducts.Location = new System.Drawing.Point(10, 88);
            gridProducts.Size = new System.Drawing.Size(Math.Max(200, tabProducts.ClientSize.Width - 20), Math.Max(100, tabProducts.ClientSize.Height - 88 - 24));
            UpdateProductsCount();
        }
        catch { }
    }

    private void TryResizeCustomers()
    {
        try
        {
            if (tabCustomers == null || gridCustomers == null) return;
            // Dock customers grid to fill, only summary bar at bottom
            gridCustomers.Dock = DockStyle.Fill;
            UpdateCustomersCount();
        }
        catch { }
    }

    // Focus and selection helpers
    private void TryFocusGrid(DataGridView? g)
    {
        if (g == null) return;
        try
        {
            if (!g.Focused) g.Focus();
            if (g.CurrentCell == null && g.Rows.Count > 0)
                g.CurrentCell = g[0, 0];
        }
        catch { }
    }

    private void MoveSelection(DataGridView g, bool up)
    {
        try
        {
            if (g.CurrentCell == null && g.Rows.Count > 0) { g.CurrentCell = g[0, 0]; return; }
            int row = g.CurrentCell.RowIndex;
            row = up ? Math.Max(0, row - 1) : Math.Min(g.Rows.Count - 1, row + 1);
            g.CurrentCell = g[g.CurrentCell.ColumnIndex, row];
            g.Rows[row].Selected = true;
            g.Focus();
        }
        catch { }
    }

    private void ApplyProductsGridLayout()
    {
        if (gridProducts == null || gridProducts.Columns.Count == 0) return;
        TrySmoothGrid(gridProducts);
    }

    // --- Customers: summary + invoice history ---
    private Panel? pnlCustSummary;
    private Label? lblCustTotal;
    private Label? lblCustProfit;
    private Label? lblCustBackDues;
    private Label? lblCustLastDate;
    private DataGridView? gridCustInvoices;
    private Button? btnCustInvView;
    private Button? btnCustInvEdit;
    private Button? btnCustInvDelete;

    private void WireCustomersPane()
    {
        if (tabCustomers == null || gridCustomers == null) return;
        // Build summary panel if not built
        if (pnlCustSummary == null)
        {
            // Slim footer: only show total customers, minimize wasted space at bottom
            pnlCustSummary = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = SystemColors.Control, Padding = new Padding(6, 4, 6, 4) };
            lblCustomersCount = new Label { Dock = DockStyle.Left, AutoSize = false, Width = 220, TextAlign = ContentAlignment.MiddleLeft };
            pnlCustSummary.Controls.Add(lblCustomersCount);
            tabCustomers.Controls.Add(pnlCustSummary);

            // Do not create extra summary labels or buttons here to avoid reserving height
            lblCustTotal = null; lblCustProfit = null; lblCustBackDues = null; lblCustLastDate = null;
            btnCustInvDelete = null; btnCustInvEdit = null; btnCustInvView = null;
        }
        // Do not create any fixed history grid here; popup will handle history.
    }

    private void GridCustomers_SelectionChanged(object? sender, EventArgs e)
    {
        try
        {
            var id = GetSelectedCustomerId();
            if (id == null) { UpdateCustomerSummary(0, 0, 0, null); return; }
            // Only recompute summary (grid shows row totals already); no fixed history binding
            LoadCustomerSummaryAndHistory(id.Value);
        }
        catch { }
    }

    private void LoadCustomerSummaryAndHistory(int customerId)
    {
        using var db = new AppDbContext();
        var invs = db.Invoices
            .Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.Status, i.Total, i.TotalPaid, Balance = i.Total - i.TotalPaid })
            .ToList();
        decimal totalPurchase = invs.Sum(i => i.Total);
        decimal backDues = invs.Where(i => i.Status != Models.InvoiceStatus.Paid && i.Status != Models.InvoiceStatus.Void).Sum(i => i.Balance);
        DateTime? lastDate = invs.Count > 0 ? invs.Max(i => (DateTime?)i.InvoiceDate) : null;

        // Profit calculation: sum((sell - cost)*qty) across all items of this customer
        decimal profit = 0m;
        try
        {
            var items = db.InvoiceItems
                .Where(ii => db.Invoices.Any(i => i.Id == ii.InvoiceId && i.CustomerId == customerId))
                .ToList();
            // prefetch products
            var prodIds = items.Where(ii => ii.ProductIdRef.HasValue).Select(ii => ii.ProductIdRef!.Value).Distinct().ToList();
            var prods = db.Products.Where(p => prodIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
            foreach (var it in items)
            {
                Models.Product? p = null;
                if (it.ProductIdRef.HasValue) prods.TryGetValue(it.ProductIdRef.Value, out p);
                if (p == null && !string.IsNullOrWhiteSpace(it.ProductId)) p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                decimal cost = 0m;
                if (p != null)
                {
                    string? batch = ExtractBatch(it.Description);
                    if (!string.IsNullOrWhiteSpace(batch))
                    {
                        if (string.Equals(batch, p.NewBatch, StringComparison.OrdinalIgnoreCase)) cost = p.NewCostPrice ?? 0m;
                        else if (string.Equals(batch, p.OldBatch, StringComparison.OrdinalIgnoreCase)) cost = p.OldCostPrice ?? 0m;
                        else if (string.Equals(batch, p.VeryOldBatch, StringComparison.OrdinalIgnoreCase)) cost = p.VeryOldCostPrice ?? 0m;
                    }
                    if (cost == 0m) cost = p.NewCostPrice ?? p.OldCostPrice ?? p.VeryOldCostPrice ?? 0m;
                }
                profit += (it.Price - cost) * it.Quantity;
            }
        }
        catch { }

        UpdateCustomerSummary(totalPurchase, profit, backDues, lastDate);
        BindCustomerHistory(invs);
    }

    private static string? ExtractBatch(string? desc)
    {
        if (string.IsNullOrWhiteSpace(desc)) return null;
        var idx = desc.IndexOf("BATCH:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + 6;
        int end = desc.IndexOfAny(new[] { ' ', '|', ',', ';' }, start);
        return (end > start ? desc.Substring(start, end - start) : desc.Substring(start)).Trim();
    }

    private void UpdateCustomerSummary(decimal total, decimal profit, decimal dues, DateTime? lastDate)
    {
        if (lblCustTotal == null || lblCustProfit == null || lblCustBackDues == null || lblCustLastDate == null) return;
        lblCustTotal.Text = $"Total Purchase: {total:0.00}";
        lblCustProfit.Text = $"Profit: {profit:0.00}";
        lblCustBackDues.Text = $"Back Dues: {dues:0.00}";
        lblCustLastDate.Text = $"Last Date: {(lastDate.HasValue ? lastDate.Value.ToString("dd/MM/yyyy") : "-")}";
        UpdateCustomersCount();
    }

    private void BindCustomerHistory(object data) { /* no-op: history shown in popup now */ }

    // Make Enter on customers grid jump to history grid (first row)
    private void WireCustomersGridKeys()
    {
        if (gridCustomers == null) return;
        gridCustomers.KeyDown -= GridCustomers_KeyDown;
        gridCustomers.KeyDown += GridCustomers_KeyDown;
    }

    private void GridCustomers_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true; e.SuppressKeyPress = true; var cid = GetSelectedCustomerId(); if (cid != null) ShowCustomerHistoryDialog(cid.Value);
            }
        }
        catch { }
    }

    private void GridCustomers_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var cid = GetSelectedCustomerId(); if (cid != null) ShowCustomerHistoryDialog(cid.Value);
    }

    // --- Products footer (total count) ---
    private Panel? pnlProductsFooter;
    private Label? lblProductsCount;

    private void EnsureProductsFooter()
    {
        if (tabProducts == null) return;
        if (pnlProductsFooter != null) return;
        pnlProductsFooter = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = SystemColors.Control };
        // Totals bottom-left on products tab
        lblProductsCount = new Label { Dock = DockStyle.Left, AutoSize = false, Width = 220, TextAlign = ContentAlignment.MiddleLeft };
        pnlProductsFooter.Controls.Add(lblProductsCount);
        tabProducts.Controls.Add(pnlProductsFooter);
    }

    private void UpdateProductsCount()
    {
        if (lblProductsCount == null || gridProducts == null) return;
        int n = 0; try { n = gridProducts.Rows.Count; if (gridProducts.AllowUserToAddRows) n = Math.Max(0, n - 1); } catch { }
        lblProductsCount.Text = $"Total products: {n}";
    }

    private Label? lblCustomersCount;
    private void UpdateCustomersCount()
    {
        if (lblCustomersCount == null || gridCustomers == null) return;
        int n = 0; try { n = gridCustomers.Rows.Count; if (gridCustomers.AllowUserToAddRows) n = Math.Max(0, n - 1); } catch { }
        lblCustomersCount.Text = $"Total customers: {n}";
    }

    private int? GetSelectedCustomerInvoiceId()
    {
        if (gridCustInvoices?.CurrentRow == null) return null;
        var row = gridCustInvoices.CurrentRow;
        if (row.DataBoundItem == null) return null;
        try
        {
            var prop = row.DataBoundItem.GetType().GetProperty("Id");
            if (prop != null)
            {
                var val = prop.GetValue(row.DataBoundItem);
                if (val is int id) return id;
            }
        }
        catch { }
        return null;
    }

    private void PreviewSelectedCustomerInvoice()
    {
        var id = GetSelectedCustomerInvoiceId(); if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var db = new AppDbContext();
        var inv = db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .Include(i => i.Customer)
            .FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        var html = Forms.InvoicePreviewForm.BuildHtmlFor(inv);
        using var pv = new Forms.InvoicePreviewForm(inv, html);
        pv.ShowDialog(this);
        // After preview, refresh in case of side effects
        var cid = GetSelectedCustomerId(); if (cid != null) LoadCustomerSummaryAndHistory(cid.Value);
    }

    // Show bill history as a popup dialog for the selected customer
    private void ShowCustomerHistoryDialog(int customerId)
    {
        try
        {
            using var db = new AppDbContext();
            var cust = db.Customers.FirstOrDefault(c => c.Id == customerId);
            var rows = db.Invoices
                .Where(i => i.CustomerId == customerId)
                .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
                .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.Status, i.Total, i.TotalPaid, Balance = i.Total - i.TotalPaid })
                .ToList();

            var f = new Form
            {
                Text = $"Bill History - {(cust?.Name ?? customerId.ToString())}",
                StartPosition = FormStartPosition.CenterParent,
                Size = new System.Drawing.Size(900, 520),
                KeyPreview = true
            };
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };
            TrySmoothGrid(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Invoice#", DataPropertyName = "InvoiceNumber", Width = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "InvoiceDate", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", DataPropertyName = "Total", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Paid", DataPropertyName = "TotalPaid", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Balance", DataPropertyName = "Balance", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 100 });
            grid.DataSource = rows;

            var pnl = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 6) };
            var btnClose = new Button { Text = "Close", Width = 90 };
            var btnDelete = new Button { Text = "Delete", Width = 90 };
            var btnEdit = new Button { Text = "Edit", Width = 90 };
            var btnPrev = new Button { Text = "Preview", Width = 90 };
            pnl.Controls.AddRange(new Control[] { btnClose, btnDelete, btnEdit, btnPrev });

            int? SelectedInvId()
            {
                if (grid.CurrentRow?.DataBoundItem == null) return null;
                var prop = grid.CurrentRow.DataBoundItem.GetType().GetProperty("Id");
                var val = prop?.GetValue(grid.CurrentRow.DataBoundItem);
                return val is int iid ? iid : (int?)null;
            }

            void DoEdit()
            {
                var id = SelectedInvId(); if (id == null) { MessageBox.Show("Select an invoice."); return; }
                using var dlg = new Forms.InvoiceEditForm(id);
                dlg.ShowDialog(f);
                // refresh data after potential edits
                var refreshed = db.Invoices.Where(i => i.CustomerId == customerId)
                    .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
                    .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.Status, i.Total, i.TotalPaid, Balance = i.Total - i.TotalPaid }).ToList();
                grid.DataSource = refreshed;
            }

            void DoPreview()
            {
                var id = SelectedInvId(); if (id == null) { MessageBox.Show("Select an invoice."); return; }
                var inv = db.Invoices.Include(i => i.Items).Include(i => i.Payments).Include(i => i.Customer).FirstOrDefault(i => i.Id == id);
                if (inv == null) { MessageBox.Show("Invoice not found."); return; }
                var html = Forms.InvoicePreviewForm.BuildHtmlFor(inv);
                using var pv = new Forms.InvoicePreviewForm(inv, html);
                pv.ShowDialog(f);
            }

            void DoDelete()
            {
                var id = SelectedInvId(); if (id == null) { MessageBox.Show("Select an invoice."); return; }
                if (MessageBox.Show("Delete selected invoice?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                var inv = db.Invoices.FirstOrDefault(i => i.Id == id);
                if (inv == null) { MessageBox.Show("Not found."); return; }
                db.Invoices.Remove(inv); db.SaveChanges();
                var refreshed = db.Invoices.Where(i => i.CustomerId == customerId)
                    .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
                    .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.Status, i.Total, i.TotalPaid, Balance = i.Total - i.TotalPaid }).ToList();
                grid.DataSource = refreshed;
            }

            btnEdit.Click += (s, e) => DoEdit();
            btnPrev.Click += (s, e) => DoPreview();
            btnDelete.Click += (s, e) => DoDelete();
            btnClose.Click += (s, e) => f.Close();

            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) DoEdit(); };
            grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; DoEdit(); }
                else if (e.Control && e.KeyCode == Keys.P) { e.Handled = true; DoPreview(); }
                else if (e.KeyCode == Keys.Delete || (e.Control && e.KeyCode == Keys.D)) { e.Handled = true; DoDelete(); }
                else if (e.KeyCode == Keys.Escape) { e.Handled = true; f.Close(); }
            };

            f.Controls.Add(grid);
            f.Controls.Add(pnl);
            f.ShowDialog(this);
        }
        catch { }
    }

    private void EditSelectedCustomerInvoice()
    {
        var id = GetSelectedCustomerInvoiceId(); if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var dlg = new Forms.InvoiceEditForm(id);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            var cid = GetSelectedCustomerId(); if (cid != null) LoadCustomerSummaryAndHistory(cid.Value);
        }
    }

    private void DeleteSelectedCustomerInvoice()
    {
        var id = GetSelectedCustomerInvoiceId(); if (id == null) { MessageBox.Show("Select an invoice."); return; }
        if (MessageBox.Show("Delete selected invoice?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        using var db = new AppDbContext();
        var inv = db.Invoices.FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        db.Invoices.Remove(inv);
        db.SaveChanges();
        var cid = GetSelectedCustomerId(); if (cid != null) LoadCustomerSummaryAndHistory(cid.Value);
    }

    // Invoices
    private void LoadInvoices(string? search = null)
    {
        try
        {
            if (gridInvoices == null) return;
            using var db = new AppDbContext();
            var q = db.Invoices.AsNoTracking().AsQueryable();
            // date filter
            if (chkApplyDateFilter?.Checked == true && dtpInvFrom != null && dtpInvTo != null)
            {
                var from = dtpInvFrom.Value.Date;
                var to = dtpInvTo.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(i => i.InvoiceNumber.ToLower().Contains(s)
                              || (i.Customer != null && i.Customer.Name != null && i.Customer.Name.ToLower().Contains(s)));
            }
            var data = q
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .Select(i => new
                {
                    i.Id,
                    i.InvoiceNumber,
                    i.InvoiceDate,
                    i.DueDate,
                    Customer = i.Customer != null ? i.Customer.Name : "",
                    Status = i.Status.ToString(),
                    BillAmt = i.Total - i.BackDues,
                    i.BackDues,
                    i.Total,
                    i.TotalPaid,
                    Balance = i.Total - i.TotalPaid
                })
                .ToList();
            gridInvoices.DataSource = data;
            ApplyInvoicesGridColumnLayout();
            if (gridInvoices.Columns["Id"] != null) gridInvoices.Columns["Id"].Visible = false;
            // formatting and status coloring
            if (gridInvoices.Columns["InvoiceDate"] != null) gridInvoices.Columns["InvoiceDate"].DefaultCellStyle.Format = "dd/MM/yyyy";
            if (gridInvoices.Columns["DueDate"] != null) gridInvoices.Columns["DueDate"].DefaultCellStyle.Format = "dd/MM/yyyy";
            if (gridInvoices.Columns["BillAmt"] != null) gridInvoices.Columns["BillAmt"].DefaultCellStyle.Format = "0.00";
            if (gridInvoices.Columns["BackDues"] != null) gridInvoices.Columns["BackDues"].DefaultCellStyle.Format = "0.00";
            if (gridInvoices.Columns["Total"] != null) gridInvoices.Columns["Total"].DefaultCellStyle.Format = "0.00";
            if (gridInvoices.Columns["TotalPaid"] != null) gridInvoices.Columns["TotalPaid"].DefaultCellStyle.Format = "0.00";
            if (gridInvoices.Columns["Balance"] != null) gridInvoices.Columns["Balance"].DefaultCellStyle.Format = "0.00";
            gridInvoices.CellFormatting -= GridInvoices_CellFormatting;
            gridInvoices.CellFormatting += GridInvoices_CellFormatting;
            // totals footer
            var count = data.Count;
            var sumBill = data.Sum(x => (decimal)x.BillAmt);
            var sumPaid = data.Sum(x => (decimal)x.TotalPaid);
            var sumBal = data.Sum(x => (decimal)x.Balance);
            if (lblInvCount != null) lblInvCount.Text = $"{count} Invoice(s)";
            if (lblInvTotals != null) lblInvTotals.Text = $"Fresh Sale Total {sumBill:0.00}    Total Paid {sumPaid:0.00}    Final Balance {sumBal:0.00}";
            EnsureInvoicesGridTopVisible();
            LoadInvoiceDetailsFromSelection();
        }
        catch (Exception ex)
        {
            try { MessageBox.Show(this, $"Failed to load invoices.\n\n{ex.Message}", "Load Invoices", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }

    private int? GetSelectedInvoiceId()
    {
        if (gridInvoices.CurrentRow == null) return null;
        var row = gridInvoices.CurrentRow;
        if (row.Cells["Id"] != null && row.Cells["Id"].Value is int id) return id;
        return null;
    }

    private void btnInvoiceRefresh_Click(object sender, EventArgs e)
    {
        LoadInvoices(txtInvoiceSearch.Text);
    }

    private void btnInvoiceSearch_Click(object sender, EventArgs e)
    {
        LoadInvoices(txtInvoiceSearch.Text);
    }

    private void BuildInvoicesHomeUi()
    {
        // only once
        if (tsInvoices != null) return;
        // hide old quick buttons on the tab
        btnInvoiceRefresh.Visible = false;
        btnInvoiceAdd.Visible = false;
        btnInvoiceEdit.Visible = false;
        btnInvoiceDelete.Visible = false;
        btnInvoiceAddPayment.Visible = false;
        txtInvoiceSearch.Visible = false;
        btnInvoiceSearch.Visible = false;

        // toolbar
        tsInvoices = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        tsNewInvoice = new ToolStripButton("Create new Invoice"); tsNewInvoice.Click += (s,e)=> btnInvoiceAdd_Click(this, e);
        tsViewEditInvoice = new ToolStripButton("View/Edit Invoice"); tsViewEditInvoice.Click += (s,e)=> btnInvoiceEdit_Click(this, e);
        tsDeleteInvoice = new ToolStripButton("Delete Selected"); tsDeleteInvoice.Click += (s,e)=> btnInvoiceDelete_Click(this, e);
        tsPreviewInvoice = new ToolStripButton("Print Preview"); tsPreviewInvoice.Click += (s,e)=> PreviewSelectedInvoice(false);
        tsPrintInvoice = new ToolStripButton("Print Selected"); tsPrintInvoice.Click += (s,e)=> PreviewSelectedInvoice(true);
        tsEmailInvoice = new ToolStripButton("E-mail Invoice"); tsEmailInvoice.Click += (s,e)=> EmailSelectedInvoice();
        tsSmsInvoice = new ToolStripButton("Send SMS notification"); tsSmsInvoice.Click += (s,e)=> SendSmsSelectedInvoice();
        tsMarkPaid = new ToolStripButton("Mark invoice as 'Paid'"); tsMarkPaid.Click += (s,e)=> btnInvoiceAddPayment_Click(this, e);
        tsVoidInvoice = new ToolStripButton("Void Invoice"); tsVoidInvoice.Click += (s,e)=> MessageBox.Show("Void not implemented yet");
        tsSuppliers = new ToolStripButton("Suppliers"); tsSuppliers.Click += (s,e)=> { using var f = new SuppliersForm(); f.ShowDialog(this); };
        tsSavePurchase = new ToolStripButton("Save Purchase"); tsSavePurchase.Click += (s,e)=> { using var f = new SavePurchaseForm(); f.ShowDialog(this); };
        tsInvoices.Items.AddRange(new ToolStripItem[] { tsNewInvoice, tsViewEditInvoice, tsDeleteInvoice, tsPreviewInvoice, tsPrintInvoice, tsEmailInvoice, tsSmsInvoice, tsMarkPaid, tsVoidInvoice, new ToolStripSeparator(), tsSuppliers, tsSavePurchase });
        tabInvoices.Controls.Add(tsInvoices);

        // filter panel
        var pnlFilter = new Panel { Dock = DockStyle.Top, Height = 42 };
        var lblFrom = new Label { Text = "Invoice date from:", Location = new Point(10, 12), AutoSize = true };
        dtpInvFrom = new DateTimePicker { Location = new Point(120, 9), Width = 140 };
        var lblTo = new Label { Text = "Invoice date to:", Location = new Point(270, 12), AutoSize = true };
        dtpInvTo = new DateTimePicker { Location = new Point(370, 9), Width = 140 };
        chkApplyDateFilter = new CheckBox { Text = "Apply filter", Location = new Point(520, 12), AutoSize = true, Checked = true };
        btnRefreshInvoicesList = new Button { Text = "Refresh Invoices list", Location = new Point(630, 7), Width = 160, Height = 28 };
        btnRefreshInvoicesList.Click += (s, e) => LoadInvoices();
        pnlFilter.Controls.AddRange(new Control[] { lblFrom, dtpInvFrom, lblTo, dtpInvTo, chkApplyDateFilter, btnRefreshInvoicesList });
        tabInvoices.Controls.Add(pnlFilter);
        tabInvoices.Padding = new Padding(0);

        // split container for grid + details
        tabInvoices.Padding = new Padding(0);
        splitInvoices = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Margin = new Padding(0) };
        splitInvoices.Panel1MinSize = 220;
        splitInvoices.SplitterWidth = 5;
        splitInvoices.SplitterDistance = Math.Max(220, (int)(tabInvoices.Height * 0.6));
        // move grid to top panel using a table layout to avoid overlap/clipping
        splitInvoices.Panel1.Margin = new Padding(0);
        splitInvoices.Panel1.Padding = new Padding(0);
        var tlpTop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
        tlpTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpTop.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlpTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        // footer panel for totals
        pnlInvTotals = new Panel { Dock = DockStyle.Fill, Height = 20, BackColor = SystemColors.Control, Margin = new Padding(0) };
        lblInvCount = new Label { AutoSize = true, Location = new Point(8, 3) };
        lblInvTotals = new Label { AutoSize = true, Anchor = AnchorStyles.Right | AnchorStyles.Top, Location = new Point(splitInvoices.Panel1.Width - 400, 3) };
        pnlInvTotals.Controls.Add(lblInvCount);
        pnlInvTotals.Controls.Add(lblInvTotals);
        // configure grid inside a host panel to provide reliable top padding
        var pnlGridHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(0, 64, 0, 0) };
        gridInvoices.Parent = null;
        gridInvoices.Location = new Point(0,0);
        gridInvoices.Margin = new Padding(0);
        gridInvoices.Dock = DockStyle.Fill;
        gridInvoices.EnableHeadersVisualStyles = false;
        gridInvoices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        gridInvoices.ColumnHeadersHeight = 36;
        gridInvoices.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        gridInvoices.AllowUserToResizeRows = false;
        gridInvoices.RowTemplate.Height = 30;
        gridInvoices.ScrollBars = ScrollBars.Vertical;
        gridInvoices.Top = 0;
        gridInvoices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // add to layout
        pnlGridHost.Controls.Add(gridInvoices);
        tlpTop.Controls.Add(pnlGridHost, 0, 0);
        tlpTop.Controls.Add(pnlInvTotals, 0, 1);
        splitInvoices.Panel1.Controls.Add(tlpTop);
        gridInvoices.SelectionChanged += (s,e)=> LoadInvoiceDetailsFromSelection();
        gridInvoices.DataBindingComplete += (s,e)=> { ApplyInvoicesGridColumnLayout(); EnsureInvoicesGridTopVisible(); };
        gridInvoices.Resize += (s,e)=> EnsureInvoicesGridTopVisible();
        // double-click to edit
        gridInvoices.CellDoubleClick += (s,e)=> { if (e.RowIndex >= 0) btnInvoiceEdit_Click(this, EventArgs.Empty); };
        // context menu
        cmsInvoices = new ContextMenuStrip();
        var miViewEdit = new ToolStripMenuItem("View / Edit", null, (s,e)=> btnInvoiceEdit_Click(this, e));
        var miDelete = new ToolStripMenuItem("Delete", null, (s,e)=> btnInvoiceDelete_Click(this, e));
        var miMarkPaid = new ToolStripMenuItem("Mark Paid", null, (s,e)=> btnInvoiceAddPayment_Click(this, e));
        var miPreview = new ToolStripMenuItem("Print Preview", null, (s,e)=> PreviewSelectedInvoice(false));
        var miPrint = new ToolStripMenuItem("Print", null, (s,e)=> PreviewSelectedInvoice(true));
        var miSavePdf = new ToolStripMenuItem("Save as PDF", null, (s,e)=> SaveSelectedInvoiceAsPdf());
        var miExportExcel = new ToolStripMenuItem("Export to Excel", null, (s,e)=> ExportSelectedInvoiceToCsv());
        var miVoid = new ToolStripMenuItem("Void", null, (s,e)=> VoidSelectedInvoice());
        cmsInvoices.Items.AddRange(new ToolStripItem[] { miViewEdit, miDelete, miMarkPaid, new ToolStripSeparator(), miPreview, miPrint, miSavePdf, miExportExcel, new ToolStripSeparator(), miVoid });
        gridInvoices.ContextMenuStrip = cmsInvoices;
        splitInvoices.Panel1.PerformLayout();
        splitInvoices.PerformLayout();
        EnsureInvoicesGridTopVisible();

        // Global shortcuts on the invoices page
        this.KeyPreview = true;
        this.KeyDown -= Form1_InvoicesShortcuts;
        this.KeyDown += Form1_InvoicesShortcuts;
        splitInvoices.Resize += (s, e) => 
        {
            var minTop = 220;
            if (splitInvoices.SplitterDistance < minTop) splitInvoices.SplitterDistance = minTop;
        };

        // bottom tabs
        tabsInvDetails = new TabControl { Dock = DockStyle.Fill };
        var tpItems = new TabPage("Invoice Items");
        gridInvItems = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        var cPid = new DataGridViewTextBoxColumn { HeaderText = "Product/Service ID", DataPropertyName = "ProductId", FillWeight = 18 };
        var cName = new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", FillWeight = 32 };
        var cDesc = new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description", FillWeight = 30 };
        var cPrice = new DataGridViewTextBoxColumn { HeaderText = "Price", DataPropertyName = "Price", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } };
        var cQty = new DataGridViewTextBoxColumn { HeaderText = "QTY", DataPropertyName = "Quantity", FillWeight = 6, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } };
        var cLine = new DataGridViewTextBoxColumn { HeaderText = "Line Total", DataPropertyName = "LineTotal", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } };
        gridInvItems.Columns.AddRange(new DataGridViewColumn[] { cPid, cName, cDesc, cPrice, cQty, cLine });
        tpItems.Controls.Add(gridInvItems);

        var tpPayments = new TabPage("Payments");
        gridInvPayments = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AutoGenerateColumns = false };
        gridInvPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "Date", Width = 120 });
        gridInvPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Method", DataPropertyName = "Method", Width = 150 });
        gridInvPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Amount", DataPropertyName = "Amount", Width = 120 });
        tpPayments.Controls.Add(gridInvPayments);

        var tpNotes = new TabPage("Invoice Private Notes");
        txtInvPrivateNotes = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        tpNotes.Controls.Add(txtInvPrivateNotes);

        var tpSms = new TabPage("SMS Log");
        lstInvSmsLog = new ListBox { Dock = DockStyle.Fill };
        tpSms.Controls.Add(lstInvSmsLog);

        tabsInvDetails.TabPages.AddRange(new TabPage[] { tpItems, tpPayments, tpNotes, tpSms });
        splitInvoices.Panel2.Controls.Add(tabsInvDetails);
        tabInvoices.Controls.Add(splitInvoices);

        // ensure order: toolbar, filter, split
        tabInvoices.Controls.SetChildIndex(tsInvoices, 0);
        tabInvoices.Controls.SetChildIndex(pnlFilter, 1);
        tabInvoices.Controls.SetChildIndex(splitInvoices, 2);
    }

    private void LoadInvoiceDetailsFromSelection()
    {
        var id = GetSelectedInvoiceId();
        if (id == null || gridInvItems == null || gridInvPayments == null) return;
        using var db = new AppDbContext();
        var inv = db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .FirstOrDefault(i => i.Id == id);
        if (inv == null) { gridInvItems.DataSource = null; gridInvPayments.DataSource = null; if (txtInvPrivateNotes!=null) txtInvPrivateNotes.Text = string.Empty; return; }
        var itemRows = inv.Items.Select(x => new
        {
            x.ProductId,
            Name = (db.Products.FirstOrDefault(p => p.Barcode == x.ProductId)?.Name)
                   ?? (x.ProductIdRef.HasValue ? db.Products.FirstOrDefault(p => p.Id == x.ProductIdRef.Value)?.Name : null)
                   ?? x.ProductId,
            x.Description,
            x.Price,
            x.Quantity,
            x.LineTotal
        }).ToList();
        gridInvItems.DataSource = itemRows;
        gridInvPayments.DataSource = inv.Payments.Select(p => new { Date = p.Date.ToString("MM/dd/yyyy"), p.Method, p.Amount }).ToList();
        if (txtInvPrivateNotes != null) txtInvPrivateNotes.Text = inv.PrivateNotes ?? string.Empty;
    }

    private string GenerateInvoicePdfFile(BillingSuite.App.Models.Invoice inv)
    {
        var html = Forms.InvoicePreviewForm.BuildHtmlFor(inv);
        var bytes = Forms.InvoicePreviewForm.GeneratePdfFor(inv, html);
        var fileName = Services.AppPaths.InvoicePdfFileName(inv.InvoiceNumber, inv.InvoiceDate);
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
        System.IO.File.WriteAllBytes(tmp, bytes);
        return tmp;
    }

    private void PreviewSelectedInvoice(bool print)
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var db = new AppDbContext();
        var inv = db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Customer)
            .FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        // Build HTML matching the editor preview
        string html = BillingSuite.App.Forms.InvoicePreviewForm.BuildHtmlFor(inv);
        if (print)
        {
            // Open native Print dialog and print without showing the preview window
            PrintInvoiceUsingDialog(html);
        }
        else
        {
            // Open the preview window
            using (var preview = new BillingSuite.App.Forms.InvoicePreviewForm(inv, html))
            {
                preview.ShowDialog(this);
            }
        }
    }

    private void PrintInvoiceUsingDialog(string html)
    {
        // Use a temporary hidden form with WebBrowser to invoke the native Print dialog
        using (var f = new Form())
        using (var wb = new WebBrowser())
        {
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            f.Opacity = 0.01; // nearly invisible
            f.Width = 200; f.Height = 100;
            wb.ScriptErrorsSuppressed = true;
            wb.AllowWebBrowserDrop = false;
            wb.IsWebBrowserContextMenuEnabled = false;
            wb.Dock = DockStyle.Fill;

            wb.DocumentCompleted += (s, e) =>
            {
                try { wb.ShowPrintDialog(); }
                catch { }
                finally { f.Close(); }
            };

            f.Controls.Add(wb);
            // Assign after handler to ensure event triggers
            wb.DocumentText = html;
            f.ShowDialog(this);
        }
    }

    private void SaveSelectedInvoiceAsPdf()
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var db = new AppDbContext();
        var inv = db.Invoices.Include(i => i.Items).Include(i => i.Customer).FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        var html = BillingSuite.App.Forms.InvoicePreviewForm.BuildHtmlFor(inv);
        using var sfd = new SaveFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", FileName = $"Invoice_{inv.InvoiceNumber}_{DateTime.Now:yyyyMMdd}.pdf" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        var bytes = BillingSuite.App.Forms.InvoicePreviewForm.GeneratePdfFor(inv, html);
        System.IO.File.WriteAllBytes(sfd.FileName, bytes);
        MessageBox.Show("Saved.");
    }

    private void ExportSelectedInvoiceToCsv()
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var db = new AppDbContext();
        var inv = db.Invoices.Include(i => i.Items).Include(i => i.Customer).FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        using var sfd = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = $"Invoice_{inv.InvoiceNumber}_{DateTime.Now:yyyyMMdd}.csv" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#,Code,Name,Expiry,MRP,Unit Price,Qty,Amount");
        int idx = 1;
        foreach (var it in inv.Items)
        {
            string code = !string.IsNullOrWhiteSpace(it.ProductId) ? it.ProductId : (it.ProductIdRef?.ToString() ?? "");
            string name;
            using (var db2 = new AppDbContext())
            {
                name = db2.Products.FirstOrDefault(p => (p.Barcode == it.ProductId) || (it.ProductIdRef.HasValue && p.Id == it.ProductIdRef.Value))?.Name ?? (it.ProductId ?? "");
            }
            string exp = string.Empty;
            if (!string.IsNullOrWhiteSpace(it.Description))
            {
                foreach (var part in it.Description.Split('|'))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && kv[0].Trim().Equals("EXP", StringComparison.OrdinalIgnoreCase)) { if (DateTime.TryParse(kv[1], out var dt)) exp = dt.ToString("dd/MM/yyyy"); break; }
                }
            }
            string mrp = string.Empty;
            if (!string.IsNullOrWhiteSpace(it.Description))
            {
                foreach (var part in it.Description.Split('|'))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && kv[0].Trim().Equals("MRP", StringComparison.OrdinalIgnoreCase)) { if (decimal.TryParse(kv[1], out var d)) mrp = d.ToString("0.00"); break; }
                }
            }
            sb.AppendLine(string.Join(",", new [] { idx++.ToString(), EscapeCsv(code), EscapeCsv(name), EscapeCsv(exp), EscapeCsv(mrp), it.Price.ToString("0.00"), it.Quantity.ToString("0.##"), (it.Price*it.Quantity).ToString("0.00") }));
        }
        System.IO.File.WriteAllText(sfd.FileName, sb.ToString());
        MessageBox.Show("Exported.");
    }

    private static string EscapeCsv(string s)
    {
        s ??= string.Empty;
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
            return '"' + s.Replace("\"", "\"\"") + '"';
        return s;
    }

    private void EmailSelectedInvoice()
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var db = new AppDbContext();
        var inv = db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Customer)
            .FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        var toEmail = inv.Customer?.Email;
        if (string.IsNullOrWhiteSpace(toEmail)) { MessageBox.Show("Customer has no email."); return; }

        var pdfPath = GenerateInvoicePdfFile(inv);
        MessageBox.Show("Email functionality is not available. Settings have been removed.");
        return;
    }

    private void btnInvoiceAdd_Click(object sender, EventArgs e)
    {
        var dlg = new InvoiceEditForm();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            // Editor persists invoice itself; just reload list to reflect changes
            LoadInvoices(txtInvoiceSearch.Text);
        }
    }

    private void btnInvoiceEdit_Click(object sender, EventArgs e)
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using (var dlg = new InvoiceEditForm(id))
        {
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // Editor persists changes; just refresh the list
                LoadInvoices(txtInvoiceSearch.Text);
            }
        }
    }

    private void btnInvoiceDelete_Click(object sender, EventArgs e)
    {
        if (sender == null) return;
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        if (MessageBox.Show("Delete selected invoice?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        using var db = new AppDbContext();
        var inv = db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(inv, new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                WriteIndented = false
            });
            db.RecycleBin.Add(new RecycleBinItem { EntityType = "Invoice", EntityId = inv.Id, JsonData = json, DeletedAt = System.DateTime.UtcNow });
        }
        catch { }
        db.Invoices.Remove(inv);
        db.SaveChanges();
        LoadInvoices(txtInvoiceSearch.Text);
    }

    private void btnInvoiceAddPayment_Click(object sender, EventArgs e)
    {
        var id = GetSelectedInvoiceId();
        if (id == null) { MessageBox.Show("Select an invoice."); return; }
        using var db = new AppDbContext();
        var inv = db.Invoices.Include(i => i.Payments).Include(i => i.Customer).FirstOrDefault(i => i.Id == id);
        if (inv == null) { MessageBox.Show("Invoice not found."); return; }
        var balance = inv.Total - inv.TotalPaid;
        if (balance <= 0) balance = inv.Total - inv.Payments.Sum(p => p.Amount);
        var dlg = new PaymentForm(balance < 0 ? 0 : balance);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var pmt = dlg.Model;
        decimal remaining = pmt.Amount;
        // If customer exists, allocate to their oldest unpaid invoices first (back dues), excluding current invoice
        if (inv.CustomerId != null)
        {
            var customerId = inv.CustomerId.Value;
            var oldInvoices = db.Invoices
                .Include(x => x.Payments)
                .Where(x => x.CustomerId == customerId && x.Id != inv.Id)
                .OrderBy(x => x.InvoiceDate)
                .ThenBy(x => x.Id)
                .ToList();
            foreach (var oi in oldInvoices)
            {
                var paid = oi.Payments.Sum(pp => pp.Amount);
                oi.TotalPaid = paid; // sync if stale
                var bal = Math.Max(0m, oi.Total - oi.TotalPaid);
                if (bal <= 0m) continue;
                if (remaining <= 0m) break;
                var apply = Math.Min(remaining, bal);
                db.Payments.Add(new Payment
                {
                    InvoiceId = oi.Id,
                    Date = pmt.Date,
                    Method = pmt.Method,
                    Amount = apply,
                    Notes = string.IsNullOrWhiteSpace(pmt.Notes) ? "Auto-applied to previous balance" : pmt.Notes
                });
                oi.TotalPaid += apply;
                // status for old invoices
                if (oi.TotalPaid >= oi.Total)
                    oi.Status = InvoiceStatus.Paid;
                else if (oi.TotalPaid > 0m && oi.DueDate.HasValue && oi.DueDate.Value.Date < System.DateTime.Today)
                    oi.Status = InvoiceStatus.PastDue; // still past due if not fully paid
                else if (oi.TotalPaid > 0m)
                    oi.Status = InvoiceStatus.Partial;
                else
                    oi.Status = InvoiceStatus.Unpaid;
                remaining -= apply;
            }
        }
        // Apply remaining to current invoice
        if (remaining > 0m)
        {
            db.Payments.Add(new Payment
            {
                InvoiceId = inv.Id,
                Date = pmt.Date,
                Method = pmt.Method,
                Amount = remaining,
                Notes = pmt.Notes
            });
            inv.TotalPaid = (inv.Payments.Sum(pp => pp.Amount) + remaining);
        }
        else
        {
            inv.TotalPaid = inv.Payments.Sum(pp => pp.Amount);
        }
        inv.UpdatedAt = System.DateTime.UtcNow;
        // auto status for current
        if (inv.TotalPaid >= inv.Total)
            inv.Status = InvoiceStatus.Paid;
        else if (inv.DueDate.HasValue && inv.DueDate.Value.Date < System.DateTime.Today)
            inv.Status = InvoiceStatus.PastDue;
        else if (inv.TotalPaid > 0m)
            inv.Status = InvoiceStatus.Partial;
        else
            inv.Status = InvoiceStatus.Unpaid;
        db.SaveChanges();
        LoadInvoices(txtInvoiceSearch.Text);
    }

    private void btnPurchases_Click(object? sender, EventArgs e)
    {
        using var dlg = new BillingSuite.App.Forms.PurchaseEditForm();
        dlg.ShowDialog(this);
    }



    private void BuildPurchasesHomeUi()
    {
        if (tabPurchases == null) return;
        tabPurchases.Controls.Clear();

        // Master Layout
        var tlpMaster = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0), Padding = new Padding(0) };
        tlpMaster.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlpMaster.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlpMaster.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlpMaster.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlpMaster.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tabPurchases.Controls.Add(tlpMaster);

        // Header panel with title (left) and hint (right)
        var pnlHeader = new Panel { Dock = DockStyle.Fill, Height = 28, Padding = new Padding(6, 4, 6, 2), Margin = new Padding(0) };
        var lblTitle = new Label { Text = "Purchases (All)", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft };
        var lblHint = new Label { Text = "Right click on datagrid row for more options.", AutoSize = false, Dock = DockStyle.Right, Width = 360, ForeColor = SystemColors.GrayText, TextAlign = ContentAlignment.MiddleRight };
        pnlHeader.Controls.Add(lblHint);
        pnlHeader.Controls.Add(lblTitle);
        tlpMaster.Controls.Add(pnlHeader, 0, 0);

        // Filter panel
        var pnlFilter = new Panel { Dock = DockStyle.Fill, Height = 40, Margin = new Padding(0) };
        var txtProductSearch = new TextBox { PlaceholderText = "Filter by Product Name", Width = 160 };
        var btnFilter = new Button { Text = "Filter", Width = 70 };
        // NOTE: btnFilter.Click is wired later, after LoadPurchases is defined
        pnlFilter.Controls.Add(new Label { Text = "Product:", Location = new Point(10, 10), AutoSize = true });
        txtProductSearch.Location = new Point(70, 7); pnlFilter.Controls.Add(txtProductSearch);
        btnFilter.Location = new Point(235, 6); pnlFilter.Controls.Add(btnFilter);
        tlpMaster.Controls.Add(pnlFilter, 0, 1);

        // Toolbar
        var ts = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Fill };
        var btnNew = new ToolStripButton("New") { ToolTipText = "Create Purchase" };
        var btnEdit = new ToolStripButton("Edit") { ToolTipText = "View/Edit Purchase" };
        var btnDelete = new ToolStripButton("Delete") { ToolTipText = "Delete Selected" };
        var btnPreview = new ToolStripButton("Preview") { ToolTipText = "Print Preview" };
        var btnPrint = new ToolStripButton("Print") { ToolTipText = "Print Selected" };
        var btnAddPayment = new ToolStripButton("+Payment") { ToolTipText = "Add Payment" };
        var btnDeletePayment = new ToolStripButton("-Payment") { ToolTipText = "Delete Payment" };
        var btnHistory = new ToolStripButton("History") { ToolTipText = "Supplier History" };
        var btnRefresh = new ToolStripButton("Refresh") { ToolTipText = "Refresh Purchases list" };
        ts.Items.AddRange(new ToolStripItem[] { btnNew, btnEdit, btnDelete, btnPreview, btnPrint, btnAddPayment, btnDeletePayment, btnHistory, btnRefresh });
        tlpMaster.Controls.Add(ts, 0, 2);

        // Split with grid + footer, and bottom details (items)
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Margin = new Padding(0) };
        tlpMaster.Controls.Add(split, 0, 3);
        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var pnlFooter = new Panel { Dock = DockStyle.Fill, AutoSize = true, BackColor = SystemColors.Control, Padding = new Padding(2) };
        var lblCount = new Label { AutoSize = true, Location = new Point(8, 3) };
        var lblTotals = new Label { AutoSize = true, Anchor = AnchorStyles.Right | AnchorStyles.Top, Location = new Point(400, 3) };
        pnlFooter.Controls.Add(lblCount); pnlFooter.Controls.Add(lblTotals);

        var gridPurchases = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AutoGenerateColumns = false, Margin = new Padding(0), AllowUserToAddRows = false };
        try { typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(gridPurchases, true, null); } catch { }
        gridPurchases.ColumnHeadersVisible = true;
        gridPurchases.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridPurchases.ColumnHeadersHeight = 26;
        gridPurchases.EnableHeadersVisualStyles = false;
        gridPurchases.Padding = new Padding(0);
        gridPurchases.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridPurchases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Id", DataPropertyName = "Id", Name = "Id", Visible = false });
        gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "PurchaseDate", Name = "PurchaseDate", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bill #", DataPropertyName = "InvoiceNumber", Name = "InvoiceNumber", FillWeight = 14 });
        gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Supplier", DataPropertyName = "Supplier", Name = "Supplier", FillWeight = 28 });
        gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Subtotal", DataPropertyName = "Subtotal", Name = "Subtotal", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
        gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", DataPropertyName = "Total", Name = "Total", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
        gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SupplierId", DataPropertyName = "SupplierId", Name = "SupplierId", Visible = false });

        var pnlHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(0, 0, 0, 0) };
        pnlHost.Controls.Add(gridPurchases);
        tlp.Controls.Add(pnlHost, 0, 0);
        tlp.Controls.Add(pnlFooter, 0, 1);
        split.Panel1.Controls.Add(tlp);

        // Bottom: Items tab
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var tabItems = new TabPage("Purchase Items");
        var gridItems = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AutoGenerateColumns = false };
        gridItems.ColumnHeadersVisible = true;
        gridItems.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridItems.ColumnHeadersHeight = 26;
        gridItems.EnableHeadersVisualStyles = false;
        gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product", DataPropertyName = "ProductName", FillWeight = 28 });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch", DataPropertyName = "BatchNumber", FillWeight = 12 });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry", DataPropertyName = "Expiry", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MRP", DataPropertyName = "Mrp", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cost", DataPropertyName = "CostPrice", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Selling", DataPropertyName = "SellingPrice", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty", DataPropertyName = "Quantity", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight } });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pack", DataPropertyName = "Pack", FillWeight = 8 });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bonus", DataPropertyName = "Bonus", FillWeight = 8 });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Marketed By", DataPropertyName = "MarketedBy", FillWeight = 15 });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HSN", DataPropertyName = "Hsn", FillWeight = 10 });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Category", DataPropertyName = "Category", FillWeight = 12 });
        gridItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Line Total", DataPropertyName = "LineTotal", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
        tabItems.Controls.Add(gridItems);
        tabs.TabPages.Add(tabItems);
        var tabPayments = new TabPage("Payments");
        var gridPayments = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AutoGenerateColumns = false };
        gridPayments.ColumnHeadersVisible = true;
        gridPayments.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridPayments.ColumnHeadersHeight = 26;
        gridPayments.EnableHeadersVisualStyles = false;
        gridPayments.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridPayments.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Id", DataPropertyName = "Id", Name = "Id", Visible = false });
        gridPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "Date", Name = "Date", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        gridPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Method", DataPropertyName = "Method", Name = "Method", FillWeight = 18 });
        gridPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Amount", DataPropertyName = "Amount", Name = "Amount", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
        gridPayments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", DataPropertyName = "Notes", Name = "Notes", FillWeight = 40 });
        tabPayments.Controls.Add(gridPayments);
        tabs.TabPages.Add(tabPayments);
        split.Panel2.Controls.Add(tabs);

        void UpdateFooter()
        {
            if (gridPurchases.DataSource is System.Collections.IEnumerable ie)
            {
                int count = 0; decimal sum = 0m;
                foreach (var row in ie)
                {
                    count++;
                    try { var t = (decimal?)row.GetType().GetProperty("Total")?.GetValue(row) ?? 0m; sum += t; } catch { }
                }
                lblCount.Text = $"{count} Purchase(s)";
                lblTotals.Text = $"Total: {sum:0.00}";
            }
        }

        int? SelectedPurchaseId()
        {
            if (gridPurchases.CurrentRow == null) return null;
            try { return (int?)gridPurchases.CurrentRow.Cells["Id"].Value; } catch { return null; }
        }

        void LoadItemsForSelected()
        {
            try { var col = gridItems.Columns["Expiry"]; if (col != null) col.DefaultCellStyle.Format = "dd/MM/yyyy"; } catch { }
            var id = SelectedPurchaseId(); if (id == null) { gridItems.DataSource = null; return; }
            using var db = new AppDbContext();
            
            var rows = (from i in db.PurchaseItems
                        where i.PurchaseId == id
                        join p in db.Products on i.ProductId equals p.Id into prodGrp
                        from p in prodGrp.DefaultIfEmpty()
                        select new {
                            i.ProductName, i.BatchNumber, i.Expiry, i.Mrp, 
                            i.CostPrice, i.SellingPrice, i.Quantity, i.Pack, 
                            i.Bonus, i.MarketedBy, i.LineTotal,
                            Hsn = p != null ? p.Hsn : null,
                            Category = p != null ? p.Category : null
                        }).ToList();
                        
            gridItems.DataSource = rows;

            try
            {
                var term = txtProductSearch?.Text?.Trim().ToLowerInvariant() ?? "";
                if (term.Length > 0)
                {
                    foreach (DataGridViewRow r in gridItems.Rows)
                    {
                        var nm = r.Cells["ProductName"]?.Value?.ToString()?.Trim().ToLowerInvariant() ?? "";
                        if (nm.Contains(term))
                        {
                            r.DefaultCellStyle.BackColor = Color.LightYellow;
                            r.DefaultCellStyle.SelectionBackColor = Color.Goldenrod;
                            r.DefaultCellStyle.ForeColor = Color.Black;
                        }
                    }
                }
            }
            catch { }
        }

        int? SelectedPaymentId()
        {
            if (gridPayments.CurrentRow == null) return null;
            try { return (int?)gridPayments.CurrentRow.Cells["Id"].Value; } catch { return null; }
        }

        void LoadPaymentsForSelected()
        {
            var id = SelectedPurchaseId(); if (id == null) { gridPayments.DataSource = null; return; }
            using var db = new AppDbContext();
            var rows = db.PurchasePayments.Where(p => p.PurchaseId == id)
                .OrderBy(p => p.Date)
                .Select(p => new { p.Id, p.Date, p.Method, p.Amount, p.Notes })
                .ToList();
            gridPayments.DataSource = rows;
        }

        void RecalcPaidDue(int purchaseId)
        {
            using var db = new AppDbContext();
            var p = db.Purchases.FirstOrDefault(x => x.Id == purchaseId); if (p == null) return;
            var paid = db.PurchasePayments.Where(x => x.PurchaseId == purchaseId).Sum(x => (decimal?)x.Amount) ?? 0m;
            p.Paid = paid;
            var total = p.Total ?? p.Subtotal ?? 0m;
            p.Due = total - paid;
            db.SaveChanges();
        }

        void LoadPurchases()
        {
            using var db = new AppDbContext();
            var data = (
                from p in db.Purchases
                join s in db.Suppliers on p.SupplierId equals s.Id into gs
                from s in gs.DefaultIfEmpty()
                orderby p.PurchaseDate descending, p.Id descending
                select new
                {
                    p.Id,
                    p.PurchaseDate,
                    p.InvoiceNumber,
                    Supplier = s != null ? s.Name : string.Empty,
                    p.Subtotal,
                    p.Total,
                    p.SupplierId
                }
            ).ToList();
            var prodSearch = (txtProductSearch?.Text ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(prodSearch))
            {
                var pIds = db.PurchaseItems.Where(pi => pi.ProductName.ToLower().Contains(prodSearch)).Select(pi => pi.PurchaseId).Distinct().ToList();
                data = data.Where(p => pIds.Contains(p.Id)).ToList();
            }
            gridPurchases.DataSource = data;
            UpdateFooter();
            LoadItemsForSelected();
            LoadPaymentsForSelected();
            // Ensure a row is selected so bottom panels populate without manual click
            try { if (gridPurchases.Rows.Count > 0) { gridPurchases.ClearSelection(); gridPurchases.Rows[0].Selected = true; gridPurchases.CurrentCell = gridPurchases.Rows[0].Cells[gridPurchases.Columns["PurchaseDate"].Index]; } } catch { }
            // After binding and layout, finalize splitter distance to avoid header clipping
            try { split.PerformLayout(); tabPurchases.PerformLayout(); } catch { }
            try
            {
                var usable = split.Height;
                split.SplitterDistance = Math.Max(140, Math.Min(usable - 180, (int)(usable * 0.6)));
            }
            catch { }
        }

        void CreatePurchase()
        {
            using var dlg = new BillingSuite.App.Forms.PurchaseEditForm();
            var _ = dlg.ShowDialog(this);
            // Regardless of OK/Cancel, editor may autosave on close; refresh list
            LoadPurchases();
        }

        void EditPurchase()
        {
            var id = SelectedPurchaseId(); if (id == null) return;
            using var db = new AppDbContext();
            if (db.Purchases.FirstOrDefault(x => x.Id == id) == null) return;
            using var dlg = new BillingSuite.App.Forms.PurchaseEditForm(id.Value);
            var _ = dlg.ShowDialog(this);
            // Refresh even if closed without OK (editor autosave on close)
            LoadPurchases();
        }

        void DeletePurchase()
        {
            var id = SelectedPurchaseId(); if (id == null) return;
            if (MessageBox.Show("Delete selected purchase?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            // Ask if stock should be subtracted as well
            var adj = MessageBox.Show("Also subtract purchased item quantities from stock?", "Adjust Stock", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (adj == DialogResult.Cancel) return;
            using var db = new AppDbContext();
            var p = db.Purchases.FirstOrDefault(x => x.Id == id); if (p == null) return;
            var items = db.PurchaseItems.Where(i => i.PurchaseId == p.Id).ToList();
            // Always handle batch detail rollback logic irrespective of stock choice
            try
            {
                foreach (var it in items)
                {
                    int? prodId = it.ProductId;
                    Models.Product? prod = null;
                    if (prodId.HasValue) prod = db.Products.FirstOrDefault(pr => pr.Id == prodId.Value);
                    if (prod == null && !string.IsNullOrWhiteSpace(it.ProductName))
                        prod = db.Products.FirstOrDefault(pr => pr.Name.ToLower() == (it.ProductName ?? string.Empty).ToLower());
                    if (prod == null) continue;

                    var batchCode = it.BatchNumber?.Trim();
                    if (string.IsNullOrWhiteSpace(batchCode)) continue;

                    // Determine if there exists any later purchase for this product+batch
                    var laterExists = (from pi in db.PurchaseItems
                                       join pu in db.Purchases on pi.PurchaseId equals pu.Id
                                       where ((pi.ProductId != null && pi.ProductId == prod.Id) || (pi.ProductId == null && (pi.ProductName ?? "") == (prod.Name ?? "")))
                                             && (pi.BatchNumber ?? "") == batchCode
                                             && (pu.PurchaseDate > p.PurchaseDate || (pu.PurchaseDate == p.PurchaseDate && pu.Id > p.Id))
                                       select pi.Id).Any();
                    if (laterExists)
                    {
                        // A later purchase exists; keep latest details as-is
                        continue;
                    }

                    // Find previous purchase (before current) for same product+batch
                    var prev = (from pi in db.PurchaseItems
                                join pu in db.Purchases on pi.PurchaseId equals pu.Id
                                where ((pi.ProductId != null && pi.ProductId == prod.Id) || (pi.ProductId == null && (pi.ProductName ?? "") == (prod.Name ?? "")))
                                      && (pi.BatchNumber ?? "") == batchCode
                                      && (pu.PurchaseDate < p.PurchaseDate || (pu.PurchaseDate == p.PurchaseDate && pu.Id < p.Id))
                                orderby pu.PurchaseDate descending, pu.Id descending
                                select new { pi.Mrp, pi.CostPrice, pi.SellingPrice, pi.Expiry, pi.Pack, pi.MarketedBy, pi.Bonus }).FirstOrDefault();

                    var pb = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == prod.Id && b.BatchNumber == batchCode);
                    if (prev != null)
                    {
                        if (pb == null)
                        {
                            pb = new Models.ProductBatch
                            {
                                ProductIdRef = prod.Id,
                                BatchNumber = batchCode,
                                UpdatedAt = DateTime.UtcNow
                            };
                            db.ProductBatches.Add(pb);
                        }
                        pb.Mrp = prev.Mrp;
                        pb.CostPrice = prev.CostPrice;
                        pb.SellingPrice = prev.SellingPrice ?? pb.SellingPrice;
                        pb.Expiry = prev.Expiry;
                        if (!string.IsNullOrWhiteSpace(prev.Pack)) pb.Pack = prev.Pack;
                        if (!string.IsNullOrWhiteSpace(prev.MarketedBy)) pb.MarketedBy = prev.MarketedBy;
                        if (!string.IsNullOrWhiteSpace(prev.Bonus)) pb.Bonus = prev.Bonus;
                        pb.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // No previous purchase: clear batch detail fields but keep record
                        if (pb != null)
                        {
                            pb.Mrp = null; pb.CostPrice = null; pb.SellingPrice = pb.SellingPrice; // keep selling if used elsewhere
                            pb.Expiry = null; pb.Pack = pb.Pack; pb.MarketedBy = pb.MarketedBy; pb.Bonus = pb.Bonus;
                            pb.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                db.SaveChanges();
            }
            catch { }

            if (adj == DialogResult.Yes)
            {
                try
                {
                    foreach (var it in items)
                    {
                        int? prodId = it.ProductId;
                        Models.Product? prod = null;
                        if (prodId.HasValue) prod = db.Products.FirstOrDefault(pr => pr.Id == prodId.Value);
                        if (prod == null && !string.IsNullOrWhiteSpace(it.ProductName))
                            prod = db.Products.FirstOrDefault(pr => pr.Name.ToLower() == (it.ProductName ?? string.Empty).ToLower());
                        if (prod == null) continue;

                        var batchCode = it.BatchNumber?.Trim();
                        if (!string.IsNullOrWhiteSpace(batchCode))
                        {
                            var pb = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == prod.Id && b.BatchNumber == batchCode);
                            if (pb != null)
                            {
                                var prev = pb.Stock ?? 0m;
                                pb.Stock = Math.Max(0m, prev - (it.Quantity));
                                pb.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                // Legacy fallback: adjust matching legacy stock fields
                                if (string.Equals(batchCode, prod.NewBatch, StringComparison.OrdinalIgnoreCase) && prod.NewStock.HasValue)
                                    prod.NewStock = Math.Max(0m, (prod.NewStock ?? 0m) - it.Quantity);
                                else if (string.Equals(batchCode, prod.OldBatch, StringComparison.OrdinalIgnoreCase) && prod.OldStock.HasValue)
                                    prod.OldStock = Math.Max(0m, (prod.OldStock ?? 0m) - it.Quantity);
                                else if (string.Equals(batchCode, prod.VeryOldBatch, StringComparison.OrdinalIgnoreCase) && prod.VeryOldStock.HasValue)
                                    prod.VeryOldStock = Math.Max(0m, (prod.VeryOldStock ?? 0m) - it.Quantity);
                            }
                        }
                    }
                    db.SaveChanges();
                }
                catch { }
            }
            db.PurchaseItems.RemoveRange(items);
            db.Purchases.Remove(p);
            db.SaveChanges();
            LoadPurchases();
        }

        void PreviewPurchase(bool print = false)
        {
            var id = SelectedPurchaseId(); if (id == null) return;
            using var db = new AppDbContext();
            var p = db.Purchases.FirstOrDefault(x => x.Id == id); if (p == null) return;
            p.Items = db.PurchaseItems.Where(i => i.PurchaseId == p.Id).ToList();
            var html = BillingSuite.App.Forms.PurchasePreviewForm.BuildHtmlFor(p);
            using var prev = new BillingSuite.App.Forms.PurchasePreviewForm(p, html);
            if (print)
            {
                prev.Show(this);
                BeginInvoke(new Action(() => { try { prev.Close(); } catch { } }));
            }
            else prev.ShowDialog(this);
        }

        void AddPayment()
        {
            var id = SelectedPurchaseId(); if (id == null) return;
            using var dlg = new Form { Text = "Add Payment", StartPosition = FormStartPosition.CenterParent, Size = new Size(360, 220), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var lblDate = new Label { Text = "Date", Location = new Point(12, 18), AutoSize = true };
            var dt = new DateTimePicker { Location = new Point(80, 14), Width = 250, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            var lblMethod = new Label { Text = "Method", Location = new Point(12, 52), AutoSize = true };
            var txtMethod = new TextBox { Location = new Point(80, 48), Width = 250 };
            var lblAmt = new Label { Text = "Amount", Location = new Point(12, 86), AutoSize = true };
            var txtAmt = new TextBox { Location = new Point(80, 82), Width = 120 };
            var lblNotes = new Label { Text = "Notes", Location = new Point(12, 120), AutoSize = true };
            var txtNotes = new TextBox { Location = new Point(80, 116), Width = 250 };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(170, 150), Width = 70 };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(260, 150), Width = 70 };
            dlg.Controls.AddRange(new Control[] { lblDate, dt, lblMethod, txtMethod, lblAmt, txtAmt, lblNotes, txtNotes, btnOk, btnCancel });
            dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (!decimal.TryParse(txtAmt.Text, out var amount) || amount <= 0) { MessageBox.Show("Enter valid amount."); return; }
            using var db = new AppDbContext();
            db.PurchasePayments.Add(new PurchasePayment { PurchaseId = id.Value, Date = dt.Value.Date, Method = txtMethod.Text?.Trim(), Amount = amount, Notes = txtNotes.Text?.Trim() });
            db.SaveChanges();
            RecalcPaidDue(id.Value);
            LoadPaymentsForSelected();
            LoadPurchases();
        }

        void DeletePayment()
        {
            var pid = SelectedPurchaseId(); if (pid == null) return;
            var payId = SelectedPaymentId(); if (payId == null) return;
            if (MessageBox.Show("Delete selected payment?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using var db = new AppDbContext();
            var p = db.PurchasePayments.FirstOrDefault(x => x.Id == payId); if (p == null) return;
            db.PurchasePayments.Remove(p);
            db.SaveChanges();
            RecalcPaidDue(pid.Value);
            LoadPaymentsForSelected();
            LoadPurchases();
        }

        void ShowSupplierHistory()
        {
            var id = SelectedPurchaseId(); if (id == null) return;
            int supId = 0; try { supId = (int)(gridPurchases.CurrentRow?.Cells["SupplierId"].Value ?? 0); } catch { }
            if (supId <= 0) return;
            using var f = new BillingSuite.App.Forms.SupplierHistoryForm(supId);
            f.ShowDialog(this);
        }

        // Hook events
        tabPurchases.Tag = (Action)LoadPurchases;
        _globalGridPurchases = gridPurchases;
        _globalActionPurchaseEdit = EditPurchase;
        _globalActionPurchaseDelete = DeletePurchase;
        btnNew.Click += (s, e) => CreatePurchase();
        btnEdit.Click += (s, e) => EditPurchase();
        btnDelete.Click += (s, e) => DeletePurchase();
        btnPreview.Click += (s, e) => PreviewPurchase(false);
        btnPrint.Click += (s, e) => PreviewPurchase(true);
        btnHistory.Click += (s, e) => ShowSupplierHistory();
        btnAddPayment.Click += (s, e) => AddPayment();
        btnDeletePayment.Click += (s, e) => DeletePayment();
        btnRefresh.Click += (s, e) => LoadPurchases();
        btnFilter.Click += (s, e) => LoadPurchases();
        if (txtProductSearch != null)
        {
            txtProductSearch.TextChanged += (s, e) => LoadPurchases();
            txtProductSearch.PreviewKeyDown += (s, e) => {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up) {
                    e.IsInputKey = true;
                    gridPurchases.Focus();
                }
            };
        }
        gridPurchases.SelectionChanged += (s, e) => { LoadItemsForSelected(); LoadPaymentsForSelected(); };
        gridPurchases.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditPurchase(); };
        gridPurchases.PreviewKeyDown += (s, e) => {
            if (e.KeyCode == Keys.Enter) e.IsInputKey = true;
        };
        gridPurchases.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; EditPurchase(); }
            if (e.Control && e.KeyCode == Keys.N) { e.Handled = true; e.SuppressKeyPress = true; CreatePurchase(); }
            if (e.Control && e.KeyCode == Keys.E) { e.Handled = true; e.SuppressKeyPress = true; EditPurchase(); }
            if ((e.Control && e.KeyCode == Keys.D) || e.KeyCode == Keys.Delete) { e.Handled = true; e.SuppressKeyPress = true; DeletePurchase(); }
        };

        // Context menu
        var cms = new ContextMenuStrip();
        cms.Items.Add("View/Edit", null, (s, e) => EditPurchase());
        cms.Items.Add("Delete", null, (s, e) => DeletePurchase());
        cms.Items.Add("Preview", null, (s, e) => PreviewPurchase(false));
        cms.Items.Add("Print", null, (s, e) => PreviewPurchase(true));
        cms.Items.Add("Supplier History", null, (s, e) => ShowSupplierHistory());
        cms.Items.Add("Add Payment", null, (s, e) => AddPayment());
        cms.Items.Add("Delete Payment", null, (s, e) => DeletePayment());
        gridPurchases.ContextMenuStrip = cms;

        // Initial load and layout tuning
        LoadPurchases();
        void AdjustSplit()
        {
            try
            {
                var h = split.Height;
                split.SplitterDistance = Math.Max(180, (int)(h * 0.56));
            }
            catch { }
        }
        split.Resize += (s, e) => AdjustSplit();
        AdjustSplit();
    }

    private void BuildSuppliersHomeUi()
    {
        if (tabSuppliers == null) return;
        tabSuppliers.Controls.Clear();
        try { tabSuppliers.Padding = new Padding(0); } catch { }

        // Top panel with search and actions
        var top = new Panel { Dock = DockStyle.Top, Height = 42 };
        var txtSearch = new TextBox { PlaceholderText = "Enter filter text", Location = new Point(8, 9), Width = 260 };
        var btnSearch = new Button { Text = "Search", Location = new Point(276, 8), Width = 80 };
        var btnRefresh = new Button { Text = "Refresh", Location = new Point(362, 8), Width = 90 };
        var btnAdd = new Button { Text = "Add", Location = new Point(458, 8), Width = 80 };
        var btnEdit = new Button { Text = "Edit", Location = new Point(542, 8), Width = 80 };
        var btnDelete = new Button { Text = "Delete", Location = new Point(626, 8), Width = 80 };
        var btnPurchase = new Button { Text = "New Purchase", Location = new Point(710, 8), Width = 120 };
        var btnHistory = new Button { Text = "History", Location = new Point(834, 8), Width = 90 };
        top.Controls.AddRange(new Control[] { txtSearch, btnSearch, btnRefresh, btnAdd, btnEdit, btnDelete, btnPurchase, btnHistory });

        // Grid hosted in a padded panel to avoid top overlap
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false };
        var supHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 46, 0, 0) };
        supHost.Controls.Add(grid);

        // Footer with count
        var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = SystemColors.Control };
        var lblCount = new Label { Dock = DockStyle.Left, AutoSize = false, Width = 220, TextAlign = ContentAlignment.MiddleLeft };
        pnlFooter.Controls.Add(lblCount);
        
        // Add controls in docking-friendly order: Fill (padded host) first, then Bottom, then Top (last) to ensure top is visible
        tabSuppliers.Controls.Add(supHost);
        tabSuppliers.Controls.Add(pnlFooter);
        tabSuppliers.Controls.Add(top);
        try { top.BringToFront(); pnlFooter.BringToFront(); } catch { }

        void UpdateCount()
        {
            int n = 0; try { n = grid.Rows.Count; if (grid.AllowUserToAddRows) n = Math.Max(0, n - 1); } catch { }
            lblCount.Text = $"Total suppliers: {n}";
        }

        void LoadSuppliers()
        {
            using var db = new AppDbContext();
            var t = (txtSearch.Text ?? string.Empty).Trim().ToLowerInvariant();
            var q = db.Suppliers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(t))
            {
                q = q.Where(s => (s.Name != null && s.Name.ToLower().Contains(t))
                                 || (s.Email != null && s.Email.ToLower().Contains(t))
                                 || (s.Phone != null && s.Phone.ToLower().Contains(t))
                                 || (s.City != null && s.City.ToLower().Contains(t))
                                 || (s.State != null && s.State.ToLower().Contains(t))
                                 || (s.GstNumber != null && s.GstNumber.ToLower().Contains(t)));
            }
            var list = q
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Email,
                    s.Phone,
                    s.City,
                    s.State,
                    GST = s.GstNumber,
                    TotalPurchase = (decimal?)db.Purchases.Where(p => p.SupplierId == s.Id).Sum(p => (decimal?)p.Total) ?? 0m,
                    BackDues = s.BackDues ?? ((decimal?)db.Purchases.Where(p => p.SupplierId == s.Id).Sum(p => (decimal?)(p.Due))) ?? 0m,
                    LastPurchase = db.Purchases.Where(p => p.SupplierId == s.Id).OrderByDescending(p => p.PurchaseDate).Select(p => (DateTime?)p.PurchaseDate).FirstOrDefault()
                })
                .ToList();

            grid.Columns.Clear();
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", Name = "Id", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name", FillWeight = 22, MinimumWidth = 140 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Email", HeaderText = "Email", FillWeight = 18, MinimumWidth = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Phone", HeaderText = "Phone", FillWeight = 12, MinimumWidth = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "City", HeaderText = "City", FillWeight = 12, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "State", HeaderText = "State", FillWeight = 10, MinimumWidth = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "GST", HeaderText = "GST", FillWeight = 14, MinimumWidth = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalPurchase", HeaderText = "Total Purchase", FillWeight = 12, MinimumWidth = 110, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BackDues", HeaderText = "Back Dues", FillWeight = 10, MinimumWidth = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastPurchase", HeaderText = "Last Purchase", FillWeight = 12, MinimumWidth = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd-MMM-yyyy" } });
            grid.DataSource = list;
            UpdateCount();
        }

        int? SelectedSupplierId()
        {
            if (grid.CurrentRow == null) return null;
            try { return (int?)grid.CurrentRow.Cells["Id"].Value; } catch { return null; }
        }

        void AddOrEditSupplier(int? id)
        {
            using var db = new AppDbContext();
            Supplier? entity = null;
            if (id != null) entity = db.Suppliers.FirstOrDefault(s => s.Id == id);
            // editor dialog
            var dlg = new Form { Text = id == null ? "Add Supplier" : "Edit Supplier", StartPosition = FormStartPosition.CenterParent, Size = new Size(420, 300), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var lblName = new Label { Text = "Name*", Location = new Point(12, 18), AutoSize = true };
            var txtName = new TextBox { Location = new Point(100, 14), Width = 280, Text = entity?.Name ?? string.Empty };
            var lblEmail = new Label { Text = "Email", Location = new Point(12, 52), AutoSize = true };
            var txtEmail = new TextBox { Location = new Point(100, 48), Width = 280, Text = entity?.Email ?? string.Empty };
            var lblPhone = new Label { Text = "Phone", Location = new Point(12, 86), AutoSize = true };
            var txtPhone = new TextBox { Location = new Point(100, 82), Width = 280, Text = entity?.Phone ?? string.Empty };
            var lblCity = new Label { Text = "City", Location = new Point(12, 120), AutoSize = true };
            var txtCity = new TextBox { Location = new Point(100, 116), Width = 280, Text = entity?.City ?? string.Empty };
            var lblState = new Label { Text = "State", Location = new Point(12, 154), AutoSize = true };
            var txtState = new TextBox { Location = new Point(100, 150), Width = 280, Text = entity?.State ?? string.Empty };
            var lblGst = new Label { Text = "GST", Location = new Point(12, 188), AutoSize = true };
            var txtGst = new TextBox { Location = new Point(100, 184), Width = 280, Text = entity?.GstNumber ?? string.Empty };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(220, 220), Width = 70 };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(310, 220), Width = 70 };
            dlg.Controls.AddRange(new Control[] { lblName, txtName, lblEmail, txtEmail, lblPhone, txtPhone, lblCity, txtCity, lblState, txtState, lblGst, txtGst, btnOk, btnCancel });
            dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var name = txtName.Text?.Trim(); if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("Name is required."); return; }
            if (entity == null)
            {
                entity = new Supplier { Name = name, Email = txtEmail.Text?.Trim(), Phone = txtPhone.Text?.Trim(), City = txtCity.Text?.Trim(), State = txtState.Text?.Trim(), GstNumber = txtGst.Text?.Trim(), CreatedAt = DateTime.UtcNow };
                db.Suppliers.Add(entity);
            }
            else
            {
                entity.Name = name; entity.Email = txtEmail.Text?.Trim(); entity.Phone = txtPhone.Text?.Trim(); entity.City = txtCity.Text?.Trim(); entity.State = txtState.Text?.Trim(); entity.GstNumber = txtGst.Text?.Trim(); entity.UpdatedAt = DateTime.UtcNow;
            }
            db.SaveChanges();
            LoadSuppliers();
        }

        void DeleteSupplier()
        {
            var id = SelectedSupplierId(); if (id == null) { MessageBox.Show("Select a supplier."); return; }
            if (MessageBox.Show("Delete selected supplier?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            using var db = new AppDbContext();
            var entity = db.Suppliers.FirstOrDefault(s => s.Id == id); if (entity == null) return;
            db.Suppliers.Remove(entity); db.SaveChanges();
            LoadSuppliers();
        }

        void StartNewPurchase()
        {
            using var dlg = new BillingSuite.App.Forms.PurchaseEditForm();
            if (dlg.ShowDialog(this) == DialogResult.OK) LoadSuppliers();
        }

        void OpenSupplierHistory()
        {
            var id = SelectedSupplierId(); if (id == null) return;
            using var dlg = new BillingSuite.App.Forms.SupplierHistoryForm(id.Value);
            dlg.ShowDialog(this);
        }

        // Events
        btnSearch.Click += (s, e) => LoadSuppliers();
        txtSearch.TextChanged += (s, e) => LoadSuppliers();
        btnRefresh.Click += (s, e) => LoadSuppliers();
        btnAdd.Click += (s, e) => AddOrEditSupplier(null);
        btnEdit.Click += (s, e) => { var id = SelectedSupplierId(); if (id != null) AddOrEditSupplier(id); };
        btnDelete.Click += (s, e) => DeleteSupplier();
        btnPurchase.Click += (s, e) => StartNewPurchase();
        btnHistory.Click += (s, e) => OpenSupplierHistory();
        grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenSupplierHistory(); };

        // Context menu
        var cms = new ContextMenuStrip();
        cms.Items.Add("Add", null, (s, e) => AddOrEditSupplier(null));
        cms.Items.Add("Edit", null, (s, e) => { var id = SelectedSupplierId(); if (id != null) AddOrEditSupplier(id); });
        cms.Items.Add("Delete", null, (s, e) => DeleteSupplier());
        cms.Items.Add("New Purchase", null, (s, e) => StartNewPurchase());
        cms.Items.Add("History", null, (s, e) => OpenSupplierHistory());
        grid.ContextMenuStrip = cms;

        // Initial load
        LoadSuppliers();
    }
}
