 using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using BillingSuite.App.Services;

using BillingSuite.App.Interfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BillingSuite.App.Forms
{
    public class PurchaseEditForm : Form, IAiControllable
    {
        private readonly ToolStrip tool = new ToolStrip();
        private readonly Panel pnlToolbar = new Panel();
        private readonly Panel contentPanel = new Panel();

        private readonly ToolStripButton tsSelectSupplier = new ToolStripButton();
        private readonly ToolStripButton tsAddLine = new ToolStripButton();
        private readonly ToolStripButton tsDeleteLine = new ToolStripButton();
        private readonly ToolStripButton tsPreview = new ToolStripButton();
        private readonly ToolStripButton tsPrint = new ToolStripButton();
        private readonly ToolStripDropDownButton tsTemplate = new ToolStripDropDownButton();
        private readonly ToolStripButton tsEmail = new ToolStripButton();
        private readonly ToolStripButton tsMarkPaid = new ToolStripButton();
        private readonly ToolStripButton tsVoid = new ToolStripButton();
        private readonly ToolStripButton tsAiImport = new ToolStripButton();
        private readonly ToolStripButton tsSave = new ToolStripButton();

        private readonly GroupBox gbSupplier = new GroupBox();
        private readonly GroupBox gbPurchase = new GroupBox();
        private readonly ComboBox cmbSupplier = new ComboBox();
        private readonly TextBox txtInvoiceNo = new TextBox();
        private readonly DateTimePicker dtPurchase = new DateTimePicker();
        private readonly TextBox txtNotes = new TextBox();
        private List<Supplier> _allSuppliers = new List<Supplier>();

        private readonly DataGridView gridItems = new DataGridView();
        private readonly Panel pnlBottom = new Panel();
        private readonly Label lblTotals = new Label();

        private Panel pnlCompactFooter = new Panel();
        private Label lblLeftCompact = new Label();
        private Label lblRightCompact = new Label();
        private Label lblStatus = new Label();
        private string currentTemplate = "Classic";

        // Side info panel for selected product
        private Panel? pnlRightInfo;
        private DataGridView? gridBatchInfo;
        private Label? lblTotalStock;

        // Tabs and payments (clone of Invoice layout)
        private TabControl? tabControl;
        private DataGridView? paymentsGrid;
        private ContextMenuStrip paymentsMenu = new ContextMenuStrip();
        private Panel? paymentsPanel;
        private Label lblSubtotalVal = new Label();
        private Label lblTotalVal = new Label();
        private Label lblPaidVal = new Label();
        private Label lblBalanceVal = new Label();
        
        private Label lblTotalDiscVal = new Label();
        private Label lblTotalCgstVal = new Label();
        private Label lblTotalSgstVal = new Label();
        private Label lblTotalIgstVal = new Label();

        // Adjustments controls (mirror Invoice)
        private TextBox txtExtraCostName = new TextBox();
        private NumericUpDown numExtraCost = new NumericUpDown();
        private CheckBox chkExtraPercent = new CheckBox();
        private NumericUpDown numDiscount = new NumericUpDown();
        private CheckBox chkDiscountPercent = new CheckBox();
        private NumericUpDown numTaxRate = new NumericUpDown();

        public Purchase Model { get; private set; }
        private bool autoSaving = false;
        private bool _savedAndClosing = false;

        // IAiControllable Implementation
        public object GetAiContext()
        {
            var items = new List<object>();
            foreach (DataGridViewRow row in gridItems.Rows)
            {
                if (row.DataBoundItem is PurchaseItem item)
                    items.Add(new { item.ProductName, item.Quantity, item.CostPrice, item.SellingPrice, item.BatchNumber });
            }

            return new
            {
                Form = "PurchaseEditForm",
                InvoiceNumber = txtInvoiceNo.Text,
                Supplier = cmbSupplier.Text,
                Items = items,
                Total = lblTotalVal.Text
            };
        }

        public async Task<bool> PerformAiAction(string action, object data)
        {
            try
            {
                var input = JObject.FromObject(data);
                if (action == "ui_update" || action == "add_product")
                {
                    var products = input["products"] as JArray;
                    if (products == null && input["ProductName"] != null) products = new JArray { input };

                    if (products != null)
                    {
                        foreach (var p in products)
                        {
                            string name = p["ProductName"]?.ToString() ?? "";
                            decimal qty = p["Quantity"]?.Value<decimal>() ?? 1;
                            decimal cost = p["CostPrice"]?.Value<decimal>() ?? 0;
                            decimal sell = p["SellingPrice"]?.Value<decimal>() ?? 0;
                            string batch = p["BatchNumber"]?.ToString() ?? "AUTO";

                            AddProductToPurchase(name, qty, cost, sell, batch);
                        }
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private void AddProductToPurchase(string name, decimal qty, decimal cost, decimal sell, string batch)
        {
            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.Name.ToLower().Contains(name.ToLower()));
            if (product == null)
            {
                product = new Product { Name = name };
                db.Products.Add(product);
                db.SaveChanges();
            }

            this.Invoke((MethodInvoker)delegate {
                if (gridItems.DataSource is BindingSource bs)
                {
                    var pi = new PurchaseItem
                    {
                        PurchaseId = Model.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = qty,
                        CostPrice = cost,
                        SellingPrice = sell,
                        BatchNumber = batch,
                        Expiry = DateTime.Now.AddYears(2) // Default AI expiry
                    };
                    bs.Add(pi);
                    // Recalc logic usually in Recalc() method if exists
                    try { 
                        var method = this.GetType().GetMethod("Recalc", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                        method?.Invoke(this, null);
                    } catch { }
                }
            });
        }

        public PurchaseEditForm(int? purchaseId = null)
        {
            Text = purchaseId == null ? "Create Purchase" : "Edit Purchase";
            StartPosition = FormStartPosition.CenterParent;
            var wa = Screen.FromHandle(this.Handle).WorkingArea;
            int targetW = Math.Min(1100, Math.Max(900, wa.Width - 40));
            int targetH = Math.Min(720, Math.Max(580, wa.Height - 40));
            Size = new System.Drawing.Size(targetW, targetH);
            MinimumSize = new Size(900, 580);

            using var db = new AppDbContext();
            _allSuppliers = db.Suppliers.OrderBy(s => s.Name).ToList();

            // Toolbar host
            pnlToolbar.Dock = DockStyle.Top; pnlToolbar.Height = 30; Controls.Add(pnlToolbar);
            
            // Register with AI Context Manager
            this.Load += (s, e) => AiContextManager.RegisterActiveForm(this);
            this.FormClosing += (s, e) => AiContextManager.UnregisterActiveForm(this);

            tool.GripStyle = ToolStripGripStyle.Hidden; tool.Dock = DockStyle.Fill; tool.AutoSize = false; tool.Height = 30; tool.Stretch = true; tool.Padding = new Padding(4,2,4,2);
            tsSelectSupplier.Text = "Select Supplier";
            tsAddLine.Text = "Add line item";
            tsDeleteLine.Text = "Delete line";
            tsPreview.Text = "Preview";
            tsPrint.Text = "Print";
            tsEmail.Text = "E-mail";
            tsMarkPaid.Text = "Mark Paid";
            tsVoid.Text = "Void";
            tsTemplate.Text = "Template: Classic";
            tsTemplate.DropDownItems.Add("Classic", null, (s, e) => { currentTemplate = "Classic"; tsTemplate.Text = "Template: Classic"; });
            tsTemplate.DropDownItems.Add("Compact", null, (s, e) => { currentTemplate = "Compact"; tsTemplate.Text = "Template: Compact"; });
            tsTemplate.DropDownItems.Add("Wide", null, (s, e) => { currentTemplate = "Wide"; tsTemplate.Text = "Template: Wide"; });
            tsSave.Text = "Save";
            var tsSearch = new ToolStripButton { Text = "Search Product" };
            tsSearch.Click += (s, e) => {
                try { using var f = new PurchaseSearchForm(); f.ShowDialog(this); } catch { }
            };
            tsAiImport.Text = "PDF Import";
            tsAiImport.Click += (s, e) => DoAiImport();
            tool.Items.AddRange(new ToolStripItem[] { tsSelectSupplier, new ToolStripSeparator(), tsAddLine, tsDeleteLine, tsSearch, tsAiImport, new ToolStripSeparator(), tsPreview, tsPrint, tsTemplate, tsEmail, tsMarkPaid, tsVoid, tsSave });
            pnlToolbar.Controls.Add(tool);

            // Content host
            contentPanel.Dock = DockStyle.Fill; contentPanel.AutoScroll = true; Controls.Add(contentPanel);

            // Compact bottom footer (left: item metrics, right: totals)
            pnlCompactFooter.Dock = DockStyle.Bottom;
            pnlCompactFooter.Height = 24;
            pnlCompactFooter.BackColor = SystemColors.ControlLightLight;
            pnlCompactFooter.Padding = new Padding(8, 2, 8, 2);
            lblLeftCompact.AutoSize = false; lblLeftCompact.TextAlign = ContentAlignment.MiddleLeft;
            lblLeftCompact.Dock = DockStyle.Left; lblLeftCompact.Width = 600; lblLeftCompact.Font = new Font(Font.FontFamily, 8.5f);
            lblRightCompact.AutoSize = false; lblRightCompact.TextAlign = ContentAlignment.MiddleRight;
            lblRightCompact.Dock = DockStyle.Fill; lblRightCompact.Font = new Font(Font.FontFamily, 8.5f);
            pnlCompactFooter.Controls.Add(lblRightCompact);
            pnlCompactFooter.Controls.Add(lblLeftCompact);
            Controls.Add(pnlCompactFooter);
            pnlCompactFooter.BringToFront();
            // Status label (bottom-left above compact footer)
            lblStatus.AutoSize = true; lblStatus.Text = "Status: Unpaid"; lblStatus.Location = new Point(10, this.ClientSize.Height - 46);
            lblStatus.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(lblStatus);
            lblStatus.BringToFront();

            // Supplier box
            gbSupplier.Text = "Supplier *"; gbSupplier.SetBounds(10, 10, 700, 110); gbSupplier.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmbSupplier.DropDownStyle = ComboBoxStyle.DropDown; cmbSupplier.AutoCompleteMode = AutoCompleteMode.SuggestAppend; cmbSupplier.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmbSupplier.DisplayMember = "Name"; cmbSupplier.ValueMember = "Id"; cmbSupplier.DataSource = _allSuppliers;
            
            // Add company name autocomplete functionality
            cmbSupplier.TextChanged += (s, e) => {
                if (!cmbSupplier.Focused) return;
                if (cmbSupplier.Text.Length > 1 && !cmbSupplier.DroppedDown)
                {
                    try
                    {
                        var text = cmbSupplier.Text.ToLower();
                        var matches = _allSuppliers.Where(s => s.Name.ToLower().Contains(text)).ToList();
                        if (matches.Count > 0)
                        {
                            var originalText = cmbSupplier.Text;
                            cmbSupplier.DroppedDown = true;
                            // Keep the typed text while showing suggestions
                            if (!string.IsNullOrWhiteSpace(originalText))
                            {
                                cmbSupplier.Text = originalText;
                                cmbSupplier.Select(originalText.Length, 0);
                            }
                        }
                    }
                    catch { }
                }
            };
            
            // Enter: close the suggestion dropdown but KEEP the typed text (do not let autocomplete overwrite it)
            cmbSupplier.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && cmbSupplier.DroppedDown)
                {
                    e.SuppressKeyPress = true; e.Handled = true;
                    cmbSupplier.DroppedDown = false;
                    // If typed text exactly matches a supplier, select it (name stays exactly as typed)
                    var typed = cmbSupplier.Text?.Trim();
                    if (!string.IsNullOrEmpty(typed))
                    {
                        var exact = _allSuppliers.FirstOrDefault(x => string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase));
                        if (exact != null) cmbSupplier.SelectedItem = exact;
                    }
                }
            };

            var lblSup = new Label { Text = "Supplier *", Location = new Point(10, 25), AutoSize = true };
            cmbSupplier.SetBounds(80, 22, 260, 23);
            var btnNewSup = new Button { Text = "New ", Size = new Size(50, 23), Location = new Point(80 + 260 + 8, 22) };
            btnNewSup.Click += (s, e) => {
                try {
                    using var f = new SupplierEditForm();
                    if (f.ShowDialog(this) == DialogResult.OK) {
                        using var db = new AppDbContext();
                        db.Suppliers.Add(f.Model);
                        db.SaveChanges();
                        var list = db.Suppliers.OrderBy(x => x.Name).ToList();
                        cmbSupplier.DataSource = list;
                        cmbSupplier.SelectedValue = f.Model.Id;
                    }
                } catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            };
            var lblNotes = new Label { Text = "Notes", Location = new Point(10, 55), AutoSize = true };
            txtNotes.SetBounds(80, 52, 460, 50); txtNotes.Multiline = true;
            gbSupplier.Controls.Add(lblSup);
            gbSupplier.Controls.Add(cmbSupplier);
            gbSupplier.Controls.Add(btnNewSup);
            gbSupplier.Controls.Add(lblNotes);
            gbSupplier.Controls.Add(txtNotes);
            contentPanel.Controls.Add(gbSupplier);

            // Purchase box (right)
            gbPurchase.Text = "Purchase"; gbPurchase.SetBounds(720, 10, 360, 110); gbPurchase.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var lblInvNo = new Label { Text = "Bill #", Location = new Point(10, 25), AutoSize = true };
            txtInvoiceNo.SetBounds(90, 22, 250, 23);
            var lblDate = new Label { Text = "Date", Location = new Point(10, 55), AutoSize = true };
            dtPurchase.SetBounds(90, 52, 250, 23); dtPurchase.Format = DateTimePickerFormat.Custom; dtPurchase.CustomFormat = "dd/MM/yyyy";
            gbPurchase.Controls.Add(lblInvNo);
            gbPurchase.Controls.Add(txtInvoiceNo);
            gbPurchase.Controls.Add(lblDate);
            gbPurchase.Controls.Add(dtPurchase);
            contentPanel.Controls.Add(gbPurchase);

            // Items grid
            gridItems.Dock = DockStyle.Fill; gridItems.AllowUserToAddRows = false; gridItems.RowHeadersVisible = false; gridItems.AutoGenerateColumns = false; 
            gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            gridItems.ScrollBars = ScrollBars.Both; // Ensure horizontal scrollbar appears when columns exceed width
            
            var colSerial = new DataGridViewTextBoxColumn { HeaderText = "Sl", Name = "colSerial", ReadOnly = true, Width = 40 };
            var colSku = new DataGridViewTextBoxColumn { HeaderText = "SKU", Name = "colSku", ReadOnly = false, Width = 110 };
            var colName = new DataGridViewTextBoxColumn { HeaderText = "Name", Name = "colName", ReadOnly = false, Width = 280 };
            var colBatch = new DataGridViewTextBoxColumn { HeaderText = "Batch No.", Name = "colBatch", ReadOnly = false, Width = 110 };
            var colExpiry = new DataGridViewTextBoxColumn { HeaderText = "Expiry", Name = "colExpiry", ReadOnly = false, Width = 90 };
            var colMrp = new DataGridViewTextBoxColumn { HeaderText = "MRP", Name = "colMrp", ReadOnly = false, Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, NullValue = "" } };
            var colSell = new DataGridViewTextBoxColumn { HeaderText = "SP", Name = "colSell", ReadOnly = false, Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, NullValue = "", Format = "0.00" } };
            var colRate = new DataGridViewTextBoxColumn { HeaderText = "Rate", Name = "colRate", ReadOnly = false, Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } };
            var colDisc = new DataGridViewTextBoxColumn { HeaderText = "Disc %", Name = "colDisc", ReadOnly = false, Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.##" } };
            var colCgst = new DataGridViewTextBoxColumn { HeaderText = "CGST %", Name = "colCgst", ReadOnly = false, Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.##" } };
            var colSgst = new DataGridViewTextBoxColumn { HeaderText = "SGST %", Name = "colSgst", ReadOnly = false, Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.##" } };
            var colIgst = new DataGridViewTextBoxColumn { HeaderText = "IGST %", Name = "colIgst", ReadOnly = false, Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.##" } };
            var colCost = new DataGridViewTextBoxColumn { HeaderText = "Cost", Name = "colCost", ReadOnly = false, Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, NullValue = "", Format = "0.00" } };
            var colQty = new DataGridViewTextBoxColumn { HeaderText = "Qty", Name = "colQty", ReadOnly = false, Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } };
            var colPack = new DataGridViewTextBoxColumn { HeaderText = "Pack", Name = "colPack", ReadOnly = false, Width = 80 };
            var colMkt = new DataGridViewTextBoxColumn { HeaderText = "Mkt", Name = "colMkt", ReadOnly = false, Width = 140 };
            var colBonus = new DataGridViewTextBoxColumn { HeaderText = "Bonus", Name = "colBonus", ReadOnly = false, Width = 80 };
            var colHsn = new DataGridViewTextBoxColumn { HeaderText = "HSN", Name = "colHsn", ReadOnly = false, Width = 90 };
            var colCategory = new DataGridViewTextBoxColumn { HeaderText = "Category", Name = "colCategory", ReadOnly = false, Width = 130 };
            var colAmount = new DataGridViewTextBoxColumn { HeaderText = "Amount", Name = "colAmount", ReadOnly = true, Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight, BackColor = Color.FromArgb(245, 245, 245) } };
            
            gridItems.Columns.Add(colSerial);
            gridItems.Columns.Add(colSku);
            gridItems.Columns.Add(colName);
            gridItems.Columns.Add(colBatch);
            gridItems.Columns.Add(colExpiry);
            gridItems.Columns.Add(colMrp);
            gridItems.Columns.Add(colSell);
            gridItems.Columns.Add(colRate);
            gridItems.Columns.Add(colDisc);
            gridItems.Columns.Add(colCgst);
            gridItems.Columns.Add(colSgst);
            gridItems.Columns.Add(colIgst);
            gridItems.Columns.Add(colCost);
            gridItems.Columns.Add(colQty);
            gridItems.Columns.Add(colPack);
            gridItems.Columns.Add(colMkt);
            gridItems.Columns.Add(colBonus);
            gridItems.Columns.Add(colHsn);
            gridItems.Columns.Add(colCategory);
            gridItems.Columns.Add(colAmount);
            gridItems.EditingControlShowing += (s, e) =>
            {
                if (gridItems.CurrentCell == null) return;
                var colName = gridItems.Columns[gridItems.CurrentCell.ColumnIndex].Name;

                if (colName == "colMkt")
                {
                    if (e.Control is TextBox tb)
                    {
                        tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                        tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
                        var sc = new AutoCompleteStringCollection();
                        try
                        {
                            using var db = new AppDbContext();
                            var bats = db.ProductBatches.Where(p => p.MarketedBy != null && p.MarketedBy != "").Select(p => p.MarketedBy).Distinct().ToList();
                            foreach (var c in bats.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                                sc.Add(c!);
                        }
                        catch { }
                        tb.AutoCompleteCustomSource = sc;
                    }
                }
                else if (colName == "colSku")
                {
                    if (e.Control is TextBox tb)
                    {
                        tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                        tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
                        var sc = new AutoCompleteStringCollection();
                        try
                        {
                            using var db = new AppDbContext();
                            sc.Add("[+] GENERATE NEW PRODUCT"); // Manual trigger for new SKU
                            var prods = db.Products.Select(p => p.Name).ToList();
                            foreach (var p in prods) sc.Add(p);
                        }
                        catch { }
                        tb.AutoCompleteCustomSource = sc;
                    }
                }
                else if (e.Control is TextBox tb2)
                {
                    tb2.AutoCompleteMode = AutoCompleteMode.None;
                }
            };
            gridItems.CellFormatting += (s, e) =>
            {
                try
                {
                    if (e.RowIndex < 0) return; if (e.ColumnIndex < 0) return; var name = gridItems.Columns[e.ColumnIndex].Name;
                    if (name == "colSerial") { e.Value = (e.RowIndex + 1).ToString(); e.FormattingApplied = true; }
                    if (name == "colAmount")
                    {
                        var r = gridItems.Rows[e.RowIndex];
                        var cost = ParseDecimal(r.Cells["colCost"].Value) ?? 0m;
                        var qty = ParseDecimal(r.Cells["colQty"].Value) ?? 0m;
                        e.Value = (cost * qty).ToString("0.00"); e.FormattingApplied = true;
                    }
                }
                catch { }
            };

            // Method definitions follow after handlers
            
            void UpdateLeftInfoForSelection()
            {
                try
                {
                    if (gridItems.CurrentRow == null) return;
                    var name = gridItems.CurrentRow.Cells["colName"].Value?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) { if (gridBatchInfo != null) gridBatchInfo.DataSource = null; if (lblTotalStock != null) lblTotalStock.Text = "Total Stock: 0"; return; }
                    using var db = new AppDbContext();
                    var prod = db.Products.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
                    if (prod == null) { if (gridBatchInfo != null) gridBatchInfo.DataSource = null; if (lblTotalStock != null) lblTotalStock.Text = "Total Stock: 0"; return; }
                    // Fill HSN/Category into row if empty
                    try
                    {
                        if (string.IsNullOrWhiteSpace(gridItems.CurrentRow.Cells["colHsn"].Value?.ToString())) gridItems.CurrentRow.Cells["colHsn"].Value = prod.Hsn;
                        if (string.IsNullOrWhiteSpace(gridItems.CurrentRow.Cells["colCategory"].Value?.ToString())) gridItems.CurrentRow.Cells["colCategory"].Value = prod.Category;
                    }
                    catch { }
                    
                    decimal selectedLineTotal = 0m;
                    try
                    {
                        var scost = ParseDecimal(gridItems.CurrentRow.Cells["colCost"].Value) ?? 0m;
                        var sqty = ParseDecimal(gridItems.CurrentRow.Cells["colQty"].Value) ?? 0m;
                        selectedLineTotal = scost * sqty;
                        lblLeftCompact.Text = $"Row Item Total: {selectedLineTotal:0.00} | Product: {name} | Qty: {sqty} | CP: {scost:0.00}";
                    }
                    catch { }

                    var batches = db.ProductBatches.Where(b => b.ProductIdRef == prod.Id).OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id)
                        .Select(b => new
                        {
                            b.BatchNumber,
                            b.Expiry,
                            Mrp = b.Mrp ?? 0m,
                            CP = b.CostPrice ?? 0m,
                            SP = b.SellingPrice ?? 0m,
                            CPpct = (b.Mrp ?? 0m) > 0m ? Math.Round(((b.CostPrice ?? 0m) / (b.Mrp ?? 1m)) * 100m, 1) : 0m,
                            MarginPct = (b.SellingPrice ?? 0m) > 0m ? Math.Round((((b.SellingPrice ?? 0m) - (b.CostPrice ?? 0m)) / (b.SellingPrice ?? 1m)) * 100m, 1) : 0m,
                            Margin = Math.Round((b.SellingPrice ?? 0m) - (b.CostPrice ?? 0m), 2),
                            Stock = b.Stock ?? 0m
                        }).ToList();
                    var totalStock = batches.Sum(x => x.Stock);
                    if (lblTotalStock != null) lblTotalStock.Text = $"Total Stock: {totalStock:0.##}";
                    if (gridBatchInfo != null)
                    {
                        gridBatchInfo.AutoGenerateColumns = true;
                        gridBatchInfo.DataSource = batches;
                    }
                }
                catch { }
            }
            gridItems.EditingControlShowing += (s, e) =>
            {
                if (e.Control is TextBox tb)
                {
                    try
                    {
                        tb.KeyPress -= NumericKeyPress;
                        var col = gridItems.Columns[gridItems.CurrentCell.ColumnIndex].Name;
                        bool numeric = col == "colMrp" || col == "colSell" || col == "colCost" || col == "colQty";
                        if (numeric)
                        {
                            tb.KeyPress += NumericKeyPress;
                        }
                    }
                    catch { }
                }
            };
            
            gridItems.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var colName = gridItems.Columns[e.ColumnIndex].Name;
                if (colName == "colSku" || colName == "colCost" || colName == "colQty" || colName == "colMrp" || colName == "colSell" || colName == "colExpiry" || colName == "colRate" || colName == "colDisc" || colName == "colCgst" || colName == "colSgst" || colName == "colIgst")
                {
                    try {
                        var r = gridItems.Rows[e.RowIndex];
                        if (autoSaving) return; // ignore during mass updates

                        if (colName == "colSku")
                        {
                            var val = r.Cells["colSku"].Value?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(val))
                            {
                                using var db = new AppDbContext();
                                if (val == "[+] GENERATE NEW PRODUCT")
                                {
                                    // Calculate next sequential SKU
                                    long nextSku = 10001;
                                    var codes = db.Products.Select(p => p.Barcode).ToList();
                                    foreach (var c in codes)
                                    {
                                        if (long.TryParse(c, out long num) && num >= nextSku) nextSku = num + 1;
                                    }
                                    autoSaving = true;
                                    r.Cells["colSku"].Value = nextSku.ToString();
                                    // If name is already there (from import), keep it so they can use it as the new name
                                    if (string.IsNullOrWhiteSpace(r.Cells["colName"].Value?.ToString()))
                                    {
                                        r.Cells["colName"].Value = "";
                                    }
                                    autoSaving = false;
                                }
                                else
                                {
                                    // Try match by name OR barcode
                                    var prod = db.Products.FirstOrDefault(x => x.Name == val || x.Barcode == val);
                                    if (prod != null)
                                    {
                                        autoSaving = true;
                                        r.Cells["colSku"].Value = prod.Barcode;
                                        r.Cells["colName"].Value = prod.Name;
                                        r.Cells["colHsn"].Value = prod.Hsn;
                                        r.Cells["colCategory"].Value = prod.Category;
                                        r.Cells["colMkt"].Value = prod.MarketedBy;
                                        autoSaving = false;
                                    }
                                }
                            }
                        }

                        if (colName == "colIgst")
                        {
                            if (ParseDecimal(r.Cells["colIgst"].Value) > 0)
                            {
                                r.Cells["colCgst"].Value = 0m;
                                r.Cells["colSgst"].Value = 0m;
                            }
                        }
                        else if (colName == "colCgst" || colName == "colSgst")
                        {
                            if (ParseDecimal(r.Cells["colCgst"].Value) > 0 || ParseDecimal(r.Cells["colSgst"].Value) > 0)
                            {
                                r.Cells["colIgst"].Value = 0m;
                            }
                        }

                        if (colName == "colRate" || colName == "colDisc" || colName == "colCgst" || colName == "colSgst" || colName == "colIgst")
                        {
                            decimal rate = ParseDecimal(r.Cells["colRate"].Value) ?? 0;
                            decimal disc = ParseDecimal(r.Cells["colDisc"].Value) ?? 0;
                            decimal cgst = ParseDecimal(r.Cells["colCgst"].Value) ?? 0;
                            decimal sgst = ParseDecimal(r.Cells["colSgst"].Value) ?? 0;
                            decimal igst = ParseDecimal(r.Cells["colIgst"].Value) ?? 0;
                            
                            decimal taxable = rate * (1 - disc / 100);
                            decimal cost = taxable * (1 + (cgst + sgst + igst) / 100);
                            r.Cells["colCost"].Value = Math.Round(cost, 2);
                        }
                        else if (colName == "colCost")
                        {
                            decimal cost = ParseDecimal(r.Cells["colCost"].Value) ?? 0;
                            decimal disc = ParseDecimal(r.Cells["colDisc"].Value) ?? 0;
                            decimal cgst = ParseDecimal(r.Cells["colCgst"].Value) ?? 0;
                            decimal sgst = ParseDecimal(r.Cells["colSgst"].Value) ?? 0;
                            decimal igst = ParseDecimal(r.Cells["colIgst"].Value) ?? 0;
                            
                            if (disc < 100)
                            {
                                decimal taxable = cost / (1 + (cgst + sgst + igst) / 100);
                                decimal rate = taxable / (1 - disc / 100);
                                r.Cells["colRate"].Value = Math.Round(rate, 2);
                            }
                        }

                        if (colName == "colExpiry")
                        {
                            var val = r.Cells["colExpiry"].Value?.ToString();
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                var parsed = DateUtils.ParseExpiryDate(val);
                                if (parsed != null)
                                {
                                    r.Cells["colExpiry"].Value = parsed.Value.ToString("MM/yyyy");
                                }
                            }
                        }
                        UpdateTotals();
                        UpdateLeftInfoForSelection();
                    } catch {}
                }
            };

            gridItems.SelectionChanged += (s, e) => { try { UpdateLeftInfoForSelection(); } catch { } };

            // Bottom-left info panel (batches) under items
            var pnlBottomInfo = new Panel { Dock = DockStyle.Bottom, Height = 140, Padding = new Padding(4), BorderStyle = BorderStyle.FixedSingle };
            var gbInfo = new GroupBox { Text = "Selected Product Batches", Location = new Point(4, 4), Size = new Size(740, 130), Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Top };
            gridBatchInfo = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            var pnlInfoTop = new Panel { Dock = DockStyle.Top, Height = 22 };
            lblTotalStock = new Label { Text = "Total Stock: 0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            pnlInfoTop.Controls.Add(lblTotalStock);
            gbInfo.Controls.Add(gridBatchInfo);
            gbInfo.Controls.Add(pnlInfoTop);
            pnlBottomInfo.Controls.Add(gbInfo);

            // Center panel to host items grid (full width above bottom info)
            var pnlCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BorderStyle = BorderStyle.FixedSingle };
            pnlCenter.Controls.Add(gridItems);

            // Bottom totals
            pnlBottom.Dock = DockStyle.Bottom; pnlBottom.Height = 28; pnlBottom.BackColor = SystemColors.ControlLight; lblTotals.Dock = DockStyle.Right; lblTotals.TextAlign = ContentAlignment.MiddleRight; lblTotals.Width = 400; pnlBottom.Controls.Add(lblTotals);

            var pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
            pnlMain.Controls.Add(pnlCenter);
            pnlMain.Controls.Add(pnlBottomInfo);
            pnlMain.Controls.Add(pnlBottom);
            // Host inside Items tab of TabControl to mirror Invoice
            tabControl = new TabControl { Dock = DockStyle.None };
            var tabItems = new TabPage("Items");
            tabItems.Controls.Add(pnlMain);
            // Payments & Summary tab
            var tabPayments = new TabPage("Payments & Summary") { Padding = new Padding(5) };
            paymentsPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            // Payments grid
            paymentsGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 180,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            paymentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", Name = "colDate", DataPropertyName = "Date", FillWeight = 20 });
            paymentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Method", Name = "colMethod", DataPropertyName = "Method", FillWeight = 20 });
            paymentsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", Name = "colId", Visible = false }); // Hidden ID column if needed
            paymentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", Name = "colNotes", DataPropertyName = "Notes", FillWeight = 40 });
            paymentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Amount", Name = "colAmount", DataPropertyName = "Amount", FillWeight = 20, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            // Context menu
            var miEditPay = new ToolStripMenuItem("Edit", null, (s, e) => EditSelectedPayment());
            var miDeletePay = new ToolStripMenuItem("Delete", null, (s, e) => DeleteSelectedPayment());
            paymentsMenu.Items.Add(miEditPay);
            paymentsMenu.Items.Add(miDeletePay);
            paymentsGrid.ContextMenuStrip = paymentsMenu;
            paymentsGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelectedPayment(); };
            paymentsGrid.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && paymentsGrid != null)
                {
                    var hit = paymentsGrid.HitTest(e.X, e.Y);
                    if (hit.RowIndex >= 0)
                    {
                        paymentsGrid.ClearSelection();
                        try { paymentsGrid.Rows[hit.RowIndex].Selected = true; } catch { }
                    }
                }
            };

            // Summary group (right aligned like Invoice)
            var gbSummary = new GroupBox { Text = "Summary", Dock = DockStyle.Top, Height = 145, Margin = new Padding(0, 10, 0, 0) };
            var lblSubtotalSum = new Label { Text = "Subtotal:", Location = new Point(10, 25), AutoSize = true, Font = new Font(DefaultFont.FontFamily, DefaultFont.Size, FontStyle.Bold) };
            lblSubtotalVal.Location = new Point(100, 25); lblSubtotalVal.AutoSize = true; lblSubtotalVal.Text = "0.00";
            
            var lblDiscSumTotal = new Label { Text = "Disc:", Location = new Point(180, 25), AutoSize = true };
            lblTotalDiscVal.Location = new Point(230, 25); lblTotalDiscVal.AutoSize = true; lblTotalDiscVal.Text = "0.00";
            lblTotalDiscVal.ForeColor = Color.Firebrick;

            var lblCgstSumTotal = new Label { Text = "CGST:", Location = new Point(10, 50), AutoSize = true };
            lblTotalCgstVal.Location = new Point(100, 50); lblTotalCgstVal.AutoSize = true; lblTotalCgstVal.Text = "0.00";
            
            var lblSgstSumTotal = new Label { Text = "SGST:", Location = new Point(180, 50), AutoSize = true };
            lblTotalSgstVal.Location = new Point(230, 50); lblTotalSgstVal.AutoSize = true; lblTotalSgstVal.Text = "0.00";

            var lblIgstSumTotal = new Label { Text = "IGST:", Location = new Point(10, 75), AutoSize = true };
            lblTotalIgstVal.Location = new Point(100, 75); lblTotalIgstVal.AutoSize = true; lblTotalIgstVal.Text = "0.00";

            var lblTotalSum = new Label { Text = "Total Bill:", Location = new Point(10, 100), AutoSize = true, Font = new Font(DefaultFont.FontFamily, 10, FontStyle.Bold) };
            lblTotalVal.Location = new Point(100, 100); lblTotalVal.AutoSize = true; lblTotalVal.Font = new Font(DefaultFont.FontFamily, 10, FontStyle.Bold); lblTotalVal.Text = "0.00";
            lblTotalVal.ForeColor = Color.DarkBlue;

            var lblPaidSum = new Label { Text = "Paid:", Location = new Point(10, 125), AutoSize = true };
            lblPaidVal.Location = new Point(100, 125); lblPaidVal.AutoSize = true; lblPaidVal.Text = "0.00";
            
            var lblBalanceSum = new Label { Text = "Balance:", Location = new Point(180, 125), AutoSize = true, Font = new Font(DefaultFont.FontFamily, DefaultFont.Size, FontStyle.Bold) };
            lblBalanceVal.Location = new Point(240, 125); lblBalanceVal.AutoSize = true; lblBalanceVal.Font = new Font(DefaultFont.FontFamily, DefaultFont.Size, FontStyle.Bold); lblBalanceVal.Text = "0.00";
            
            gbSummary.Controls.AddRange(new Control[] { lblSubtotalSum, lblSubtotalVal, lblDiscSumTotal, lblTotalDiscVal, lblCgstSumTotal, lblTotalCgstVal, lblSgstSumTotal, lblTotalSgstVal, lblIgstSumTotal, lblTotalIgstVal, lblTotalSum, lblTotalVal, lblPaidSum, lblPaidVal, lblBalanceSum, lblBalanceVal });

            // Adjustments group (left side)
            var gbAdjust = new GroupBox { Text = "Adjustments", Dock = DockStyle.Top, Height = 150 };
            var lblExtra = new Label { Text = "Extra:", Location = new Point(10, 25), AutoSize = true };
            txtExtraCostName.Location = new Point(60, 22); txtExtraCostName.Width = 140; txtExtraCostName.PlaceholderText = "Name";
            numExtraCost.Location = new Point(205, 22); numExtraCost.DecimalPlaces = 2; numExtraCost.Maximum = 9999999; numExtraCost.Minimum = 0; numExtraCost.Width = 100;
            chkExtraPercent.Location = new Point(310, 24); chkExtraPercent.AutoSize = true; chkExtraPercent.Text = "%";
            var lblDisc = new Label { Text = "Discount:", Location = new Point(10, 55), AutoSize = true };
            numDiscount.Location = new Point(80, 52); numDiscount.DecimalPlaces = 2; numDiscount.Maximum = 9999999; numDiscount.Minimum = 0; numDiscount.Width = 100;
            chkDiscountPercent.Location = new Point(185, 54); chkDiscountPercent.AutoSize = true; chkDiscountPercent.Text = "%";
            var lblTax = new Label { Text = "Tax %:", Location = new Point(10, 85), AutoSize = true };
            numTaxRate.Location = new Point(60, 82); numTaxRate.DecimalPlaces = 2; numTaxRate.Maximum = 100; numTaxRate.Minimum = 0; numTaxRate.Width = 80;
            gbAdjust.Controls.AddRange(new Control[] { lblExtra, txtExtraCostName, numExtraCost, chkExtraPercent, lblDisc, numDiscount, chkDiscountPercent, lblTax, numTaxRate });

            // Add Payment button
            var btnAddPayment = new Button { Text = "Add Payment", Size = new Size(110, 28) };
            btnAddPayment.Click += (s, e) => { AddPayment(); };

            // Structured layout
            paymentsGrid.Dock = DockStyle.Fill;
            var pnlBottomArea = new Panel { Dock = DockStyle.Bottom, Height = 220, Padding = new Padding(10) };
            
            var pnlPayButtons = new Panel { Dock = DockStyle.Top, Height = 40 };
            btnAddPayment.Location = new Point(0, 5); btnAddPayment.Size = new Size(120, 30);
            pnlPayButtons.Controls.Add(btnAddPayment);

            var pnlTotals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            
            gbAdjust.Dock = DockStyle.Fill;
            gbSummary.Dock = DockStyle.Fill;
            pnlTotals.Controls.Add(gbAdjust, 0, 0);
            pnlTotals.Controls.Add(gbSummary, 1, 0);

            pnlBottomArea.Controls.Add(pnlTotals);
            pnlBottomArea.Controls.Add(pnlPayButtons);

            paymentsPanel.Controls.Add(paymentsGrid);
            paymentsPanel.Controls.Add(pnlBottomArea);
            tabPayments.Controls.Add(paymentsPanel);

            tabControl.TabPages.Add(tabItems);
            tabControl.TabPages.Add(tabPayments);
            contentPanel.Controls.Add(tabControl);
            // Position TabControl same as previous pnlMain area
            tabControl.SetBounds(10, 130, contentPanel.ClientSize.Width - 20, contentPanel.ClientSize.Height - 140);
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            // Initialize model
            Model = purchaseId == null ? new Purchase { PurchaseDate = DateTime.Today } : LoadExisting(purchaseId.Value);
            if (Model == null) Model = new Purchase { PurchaseDate = DateTime.Today };
            txtInvoiceNo.Text = Model.InvoiceNumber ?? string.Empty;
            dtPurchase.Value = (Model.PurchaseDate == default ? DateTime.Today : Model.PurchaseDate).Date;
            txtNotes.Text = Model.Notes ?? string.Empty;
            // Delay supplier selection until handle is safely created
            this.Load += (s, e) => {
                try 
                { 
                    if (Model?.SupplierId > 0) 
                    {
                        var list = cmbSupplier.Items.Cast<Supplier>().ToList();
                        var match = list.FirstOrDefault(sup => sup.Id == Model.SupplierId);
                        if (match != null) cmbSupplier.SelectedItem = match;
                    } 
                } catch { }
            };
            // Load adjustments
            txtExtraCostName.Text = Model.ExtraCostName ?? string.Empty;
            numExtraCost.Value = (Model.ExtraCostAmount ?? 0m);
            chkExtraPercent.Checked = Model.ExtraCostIsPercent ?? false;
            numDiscount.Value = (Model.Discount ?? 0m);
            chkDiscountPercent.Checked = Model.DiscountIsPercent ?? false;
            numTaxRate.Value = (Model.Tax ?? 0m);

            // Global keyboard shortcuts (match Invoice)
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.N || e.KeyCode == Keys.Insert)
                { e.Handled = true; tsAddLine.PerformClick(); return; }
                if ((e.Control && e.KeyCode == Keys.D) || e.KeyCode == Keys.Delete)
                { e.Handled = true; tsDeleteLine.PerformClick(); return; }
                if (e.Control && e.KeyCode == Keys.R)
                { e.Handled = true; tsPreview.PerformClick(); return; }
                if (e.Control && e.KeyCode == Keys.P)
                { e.Handled = true; tsPrint.PerformClick(); return; }
                // Add line: Ctrl+N
                if (e.Control && e.KeyCode == Keys.N)
                {
                    e.Handled = true;
                    if (tabControl != null && tabControl.SelectedIndex == 1) AddPayment();
                    else DoAddLineViaPicker();
                }
                // Delete line: Ctrl+D
                else if (e.Control && e.KeyCode == Keys.D)
                {
                    e.Handled = true;
                    if (tabControl != null && tabControl.SelectedIndex == 1) DeleteSelectedPayment();
                    else DeleteCurrentRow();
                }
                else if (e.Control && e.KeyCode == Keys.W)
                { e.Handled = true; this.Close(); return; }
            };

            // Handlers
            tsSelectSupplier.Click += (s, e) => EnsureSupplierSelected();
            tsAddLine.Click += (s, e) => {
                if (tabControl != null && tabControl.SelectedIndex == 1) AddPayment();
                else DoAddLineViaPicker();
            };
            tsDeleteLine.Click += (s, e) => {
                if (tabControl != null && tabControl.SelectedIndex == 1) DeleteSelectedPayment();
                else DeleteCurrentRow();
            };
            tsPreview.Click += (s, e) => DoPreview();
            tsPrint.Click += (s, e) => DoPreview(true);
            tsSave.Click += (s, e) => { if (SavePurchase()) { _savedAndClosing = true; DialogResult = DialogResult.OK; Close(); } };
            tsVoid.Click += (s, e) => DoVoid();

            // After show, update payments grid and summary
            this.Shown += (s, e) => { try { UpdateLeftInfoForSelection(); RefreshPaymentsAndSummary(); } catch { } };

            // Recalc on adjustment changes
            numExtraCost.ValueChanged += (s, e) => { UpdateTotals(); RefreshPaymentsAndSummary(); };
            chkExtraPercent.CheckedChanged += (s, e) => { UpdateTotals(); RefreshPaymentsAndSummary(); };
            numDiscount.ValueChanged += (s, e) => { UpdateTotals(); RefreshPaymentsAndSummary(); };
            chkDiscountPercent.CheckedChanged += (s, e) => { UpdateTotals(); RefreshPaymentsAndSummary(); };
            numTaxRate.ValueChanged += (s, e) => { UpdateTotals(); RefreshPaymentsAndSummary(); };

            this.FormClosing += (s, e) =>
            {
                if (autoSaving) return;
                if (_savedAndClosing) return; // already saved via Save button
                // Ask the user: Save now (with stock), Save as Draft (no stock), or Cancel (discard)
                var choice = MessageBox.Show(this,
                    "Do you want to SAVE this purchase?\n\nYes = Save (stock will be updated)\nNo = Save as Draft (NOT added to stock, can be edited later)\nCancel = Do not save",
                    "Save Purchase", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) { e.Cancel = true; return; }
                bool asDraft = (choice == DialogResult.No);
                try { autoSaving = true; if (!SavePurchase(asDraft)) e.Cancel = true; }
                catch { }
                finally { autoSaving = false; }
            };

            UpdateTotals();
        }

        private void NumericKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.') e.Handled = true;
            if (e.KeyChar == '.' && (sender as TextBox)?.Text.IndexOf('.') > -1) e.Handled = true;
        }

        private void EnsureSupplierSelected()
        {
            try
            {
                if (cmbSupplier.SelectedItem != null) return;
                MessageBox.Show("Please select supplier.");
                cmbSupplier.DroppedDown = true;
            }
            catch { }
        }

        private void TrySelectSupplier(int supplierId)
        {
            try 
            { 
                if (supplierId <= 0) return;
                var list = cmbSupplier.Items.Cast<Supplier>().ToList();
                var match = list.FirstOrDefault(s => s.Id == supplierId);
                if (match != null)
                {
                    cmbSupplier.SelectedItem = match;
                }
            } 
            catch { }
        }

        private Purchase LoadExisting(int id)
        {
            autoSaving = true; // Use as loading guard
            try
            {
                using var db = new AppDbContext();
                var p = db.Purchases.FirstOrDefault(x => x.Id == id) ?? new Purchase();
                p.Items = db.PurchaseItems.Where(i => i.PurchaseId == p.Id).ToList();
                foreach (var it in p.Items) AddRowFromItem(it);
                return p;
            }
            finally { autoSaving = false; }
        }

        private int AddEmptyRow()
        {
            int rowIndex = gridItems.Rows.Add();
            return rowIndex;
        }

        private void AddRowFromItem(PurchaseItem it)
        {
            int idx = gridItems.Rows.Add();
            var r = gridItems.Rows[idx];
            r.Cells["colName"].Value = it.ProductName;
            r.Cells["colBatch"].Value = it.BatchNumber;
            r.Cells["colExpiry"].Value = it.Expiry?.ToString("dd/MM/yyyy");
            r.Cells["colMrp"].Value = it.Mrp;
            r.Cells["colSell"].Value = it.SellingPrice;
            r.Cells["colCost"].Value = it.CostPrice;
            r.Cells["colRate"].Value = it.Rate;
            r.Cells["colDisc"].Value = it.DiscountPercent;
            r.Cells["colCgst"].Value = it.CgstPercent;
            r.Cells["colSgst"].Value = it.SgstPercent;
            r.Cells["colIgst"].Value = it.IgstPercent;
            r.Cells["colQty"].Value = it.Quantity;
            r.Cells["colPack"].Value = it.Pack;
            r.Cells["colMkt"].Value = it.MarketedBy;
            r.Cells["colBonus"].Value = it.Bonus;
            
            // Link metadata if possible
            try
            {
                using var db = new AppDbContext();
                var prod = db.Products.FirstOrDefault(x => x.Id == (it.ProductId ?? 0) || x.Name == it.ProductName);
                if (prod != null)
                {
                    r.Cells["colHsn"].Value = prod.Hsn;
                    r.Cells["colCategory"].Value = prod.Category;
                }
            } catch { }
        }

        private void DeleteCurrentRow()
        {
            if (gridItems.CurrentCell == null) return;
            if (gridItems.CurrentCell.RowIndex < 0) return;
            if (gridItems.CurrentRow?.IsNewRow == true) return;
            gridItems.Rows.RemoveAt(gridItems.CurrentCell.RowIndex);
            UpdateTotals();
        }

        private void DoAddLineViaPicker()
        {
            try
            {
                using var picker = new ProductPicker(0);
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    int idx = gridItems.Rows.Add();
                    var r = gridItems.Rows[idx];
                    var name = picker.SelectedProduct?.Name ?? picker.SelectedProduct?.Barcode ?? string.Empty;
                    r.Cells["colName"].Value = name;
                    r.Cells["colBatch"].Value = picker.SelectedBatchNumber;
                    r.Cells["colExpiry"].Value = picker.SelectedExpiry?.ToString("dd/MM/yyyy");
                    r.Cells["colMrp"].Value = picker.SelectedMrp;

                    decimal? cost = null; decimal? rate = null; decimal? disc = null; decimal? cgst = null; decimal? sgst = null; decimal? igst = null;
                    string? pack = null; string? mkt = null; string? bonus = null;
                    try
                    {
                        using var db = new AppDbContext();
                        if (picker.SelectedProduct != null && !string.IsNullOrWhiteSpace(picker.SelectedBatchNumber))
                        {
                            var pb = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == picker.SelectedProduct.Id && b.BatchNumber == picker.SelectedBatchNumber);
                            if (pb != null)
                            {
                                cost = pb.CostPrice; rate = pb.Rate; disc = pb.DiscountPercent; cgst = pb.CgstPercent; sgst = pb.SgstPercent; igst = pb.IgstPercent;
                                pack = pb.Pack; mkt = pb.MarketedBy; bonus = pb.Bonus;
                            }
                        }
                    }
                    catch { }
                    if (!cost.HasValue) cost = picker.SelectedSellingPrice ?? picker.SelectedMrp ?? 0m;
                    r.Cells["colCost"].Value = cost;
                    r.Cells["colRate"].Value = rate ?? cost; 
                    r.Cells["colDisc"].Value = disc ?? 0m;
                    r.Cells["colCgst"].Value = cgst ?? 0m;
                    r.Cells["colSgst"].Value = sgst ?? 0m;
                    r.Cells["colIgst"].Value = igst ?? 0m;
                    r.Cells["colQty"].Value = 1m;
                    if (!string.IsNullOrWhiteSpace(pack)) r.Cells["colPack"].Value = pack;
                    if (!string.IsNullOrWhiteSpace(mkt)) r.Cells["colMkt"].Value = mkt;
                    if (!string.IsNullOrWhiteSpace(bonus)) r.Cells["colBonus"].Value = bonus;

                    // Focus quantity for quick edit
                    try { gridItems.ClearSelection(); gridItems.CurrentCell = r.Cells["colQty"]; gridItems.BeginEdit(true); } catch { }
                    UpdateTotals();
                }
            }
            catch { }
        }

        private void UpdateTotals()
        {
            if (autoSaving) return;
            try
            {
                decimal subtotal = 0m;
                decimal totalDisc = 0m;
                decimal totalCgst = 0m;
                decimal totalSgst = 0m;
                decimal totalIgst = 0m;

                foreach (DataGridViewRow row in gridItems.Rows)
                {
                    if (row.IsNewRow) continue;
                    var cost = ParseDecimal(row.Cells["colCost"].Value) ?? 0m;
                    var qty = ParseDecimal(row.Cells["colQty"].Value) ?? 0m;
                    
                    var rate = ParseDecimal(row.Cells["colRate"].Value) ?? 0m;
                    var dPct = ParseDecimal(row.Cells["colDisc"].Value) ?? 0m;
                    var cPct = ParseDecimal(row.Cells["colCgst"].Value) ?? 0m;
                    var sPct = ParseDecimal(row.Cells["colSgst"].Value) ?? 0m;
                    var iPct = ParseDecimal(row.Cells["colIgst"].Value) ?? 0m;

                    var taxableValue = rate * (1 - dPct / 100) * qty;
                    var cgstAmt = taxableValue * (cPct / 100);
                    var sgstAmt = taxableValue * (sPct / 100);
                    var igstAmt = taxableValue * (iPct / 100);
                    var discAmt = (rate * (dPct / 100)) * qty;

                    totalDisc += discAmt;
                    totalCgst += cgstAmt;
                    totalSgst += sgstAmt;
                    totalIgst += igstAmt;

                    var amt = Math.Round(cost * qty, 2);
                    row.Cells["colAmount"].Value = amt;
                    subtotal += amt;
                }
                // Global Adjustments
                var extra = (decimal)numExtraCost.Value;
                if (chkExtraPercent.Checked) extra = Math.Round(subtotal * (extra / 100m), 2);
                var globalDiscount = (decimal)numDiscount.Value;
                if (chkDiscountPercent.Checked) globalDiscount = Math.Round(subtotal * (globalDiscount / 100m), 2);
                
                var finalTaxable = Math.Max(0m, subtotal + extra - globalDiscount);
                var globalTax = Math.Round(finalTaxable * ((decimal)numTaxRate.Value / 100m), 2);
                var total = finalTaxable + globalTax;

                lblTotals.Text = $"Subtotal: {subtotal:0.00}    Total: {total:0.00}";
                
                // Update Detailed Summary Labels
                if (lblSubtotalVal != null) lblSubtotalVal.Text = subtotal.ToString("0.00");
                if (lblTotalDiscVal != null) lblTotalDiscVal.Text = totalDisc.ToString("0.00");
                if (lblTotalCgstVal != null) lblTotalCgstVal.Text = totalCgst.ToString("0.00");
                if (lblTotalSgstVal != null) lblTotalSgstVal.Text = totalSgst.ToString("0.00");
                if (lblTotalIgstVal != null) lblTotalIgstVal.Text = totalIgst.ToString("0.00");
                if (lblTotalVal != null) lblTotalVal.Text = total.ToString("0.00");
                // Update summary labels if visible
                if (lblSubtotalVal != null) lblSubtotalVal.Text = subtotal.ToString("0.00");
                if (lblTotalVal != null) lblTotalVal.Text = total.ToString("0.00");
                
                // Update compact footers
                lblRightCompact.Text = $"Subtotal: {subtotal:0.00} | Total: {total:0.00}";
                try
                {
                    using var db = new AppDbContext();
                    var pid = Model?.Id ?? 0;
                    decimal paid = 0m;
                    if (pid > 0)
                        paid = db.PurchasePayments.Where(pp => pp.PurchaseId == pid).Sum(x => (decimal)x.Amount);
                    var balance = total - paid;
                    lblRightCompact.Text = $"Subtotal: {subtotal:0.00} | Total Payable: {total:0.00} | Paid: {paid:0.00} | Balance: {balance:0.00}";
                }
                catch { }
            }
            catch { }
        }

        private static decimal? ParseDecimal(object? v)
        {
            if (v == null) return null; if (decimal.TryParse(v.ToString(), out var d)) return d; return null;
        }
        private static DateTime? ParseDate(object? v)
        {
            if (v == null) return null;
            var s = v.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;
            try
            {
                // Try common purchase formats first (Indian style)
                var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/yyyy", "M/yyyy", "MM/yy", "M/yy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy" };
                if (DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var exact))
                    return exact.Date;
                // Fallback to culture parse
                if (DateTime.TryParse(s, out var any)) return any.Date;
            }
            catch { }
            return null;
        }

        private bool SavePurchase(bool asDraft = false)
        {
            try
            {
                int supId = 0;
                if (cmbSupplier.SelectedValue is int sv && sv > 0)
                    supId = sv;
                else
                {
                    // Fallback: match supplier by the exact typed text
                    var typed = cmbSupplier.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(typed))
                    {
                        var match = _allSuppliers.FirstOrDefault(x => string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase));
                        if (match != null) supId = match.Id;
                    }
                }
                if (supId <= 0)
                { MessageBox.Show("Select supplier."); return false; }

                using var db = new AppDbContext();
                var p = Model?.Id > 0 ? db.Purchases.FirstOrDefault(x => x.Id == Model.Id) : null;
                if (p == null) { p = new Purchase(); db.Purchases.Add(p); }
                p.SupplierId = supId; p.PurchaseDate = dtPurchase.Value.Date; p.InvoiceNumber = txtInvoiceNo.Text?.Trim(); p.Notes = txtNotes.Text?.Trim();
                p.ExtraCostName = txtExtraCostName.Text?.Trim();
                p.ExtraCostAmount = numExtraCost.Value;
                p.ExtraCostIsPercent = chkExtraPercent.Checked;
                p.Discount = numDiscount.Value;
                p.DiscountIsPercent = chkDiscountPercent.Checked;
                p.Tax = numTaxRate.Value;
                p.TotalDiscount = ParseDecimal(lblTotalDiscVal.Text);
                p.TotalCgst = ParseDecimal(lblTotalCgstVal.Text);
                p.TotalSgst = ParseDecimal(lblTotalSgstVal.Text);

                // Ensure Purchase has an Id before adding items
                if (p.Id == 0)
                {
                    db.SaveChanges();
                }
                // Remove existing items if editing, but first reverse their stock effect to avoid double counting on edits
                if (p.Id > 0)
                {
                    var old = db.PurchaseItems.Where(i => i.PurchaseId == p.Id).ToList();
                    if (old.Count > 0)
                    {
                        try
                        {
                            foreach (var oi in old)
                            {
                                if (oi.ProductId.HasValue)
                                {
                                    var pb = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == oi.ProductId.Value && b.BatchNumber == oi.BatchNumber);
                                    if (pb != null)
                                    {
                                        var prev = pb.Stock ?? 0m;
                                        pb.Stock = Math.Max(0m, prev - oi.Quantity);
                                        pb.UpdatedAt = DateTime.UtcNow;
                                    }
                                }
                            }
                            db.SaveChanges();
                        }
                        catch { }
                    }
                    if (old.Count > 0)
                    {
                        db.PurchaseItems.RemoveRange(old);
                        db.SaveChanges();
                    }
                }

                decimal subtotal = 0m;
                bool hasAnyRow = false;
                foreach (DataGridViewRow row in gridItems.Rows)
                {
                    if (row.IsNewRow) continue;
                    hasAnyRow = true;
                    var name = row.Cells["colName"].Value?.ToString()?.Trim(); if (string.IsNullOrWhiteSpace(name)) continue;
                    var batch = row.Cells["colBatch"].Value?.ToString()?.Trim();
                    var mrp = ParseDecimal(row.Cells["colMrp"].Value);
                    var sell = ParseDecimal(row.Cells["colSell"].Value);
                    var cost = ParseDecimal(row.Cells["colCost"].Value) ?? 0m;
                    var qty = ParseDecimal(row.Cells["colQty"].Value) ?? 0m;
                    var exp = ParseDate(row.Cells["colExpiry"].Value);
                    var pack = row.Cells["colPack"].Value?.ToString()?.Trim();
                    var mkt = row.Cells["colMkt"].Value?.ToString()?.Trim();
                    var bonus = row.Cells["colBonus"].Value?.ToString()?.Trim();
                    var hsn = row.Cells["colHsn"].Value?.ToString()?.Trim();
                    var category = row.Cells["colCategory"].Value?.ToString()?.Trim();
                    
                    var rate = ParseDecimal(row.Cells["colRate"].Value);
                    var disc = ParseDecimal(row.Cells["colDisc"].Value);
                    var cgst = ParseDecimal(row.Cells["colCgst"].Value);
                    var sgst = ParseDecimal(row.Cells["colSgst"].Value);
                    var igst = ParseDecimal(row.Cells["colIgst"].Value);

                    // ensure product exists (auto create if missing)
                    var prod = db.Products.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
                    if (prod == null)
                    {
                        prod = new Product { Name = name };
                        db.Products.Add(prod);
                        // Save items + updated purchase totals
                db.SaveChanges();
                    }
                    // update product HSN/Category if provided
                    if (!string.IsNullOrWhiteSpace(hsn)) prod.Hsn = hsn;
                    if (!string.IsNullOrWhiteSpace(category)) prod.Category = category;

                    if (!asDraft)
                    {
                        ApplyPurchaseToProduct(db, prod, new TempRow(batch, mrp, exp, cost, qty, pack, mkt, bonus, sell, rate, disc, cgst, sgst, igst));
                    }
                    // Draft: purchase + items saved but stock/batches NOT touched until final save
                    
                    // Sync updated metadata (MarketedBy) ONLY IF NOT EMPTY and check expiry globally
                    if (!string.IsNullOrWhiteSpace(mkt))
                    {
                        InventoryService.SyncProductMetadata(db, prod.Id, mkt);
                    }
                    InventoryService.SyncExpiredStock(db);

                    var it = new PurchaseItem
                    {
                        PurchaseId = p.Id,
                        ProductId = prod.Id,
                        ProductName = prod.Name,
                        BatchNumber = batch,
                        Mrp = mrp,
                        CostPrice = cost,
                        SellingPrice = sell,
                        Quantity = qty,
                        Pack = pack,
                        Bonus = bonus,
                        MarketedBy = mkt,
                        Expiry = exp,
                        Rate = rate,
                        DiscountPercent = disc,
                        CgstPercent = cgst,
                        SgstPercent = sgst,
                        IgstPercent = igst,
                        LineTotal = cost * qty
                    };
                    db.PurchaseItems.Add(it);
                    subtotal += it.LineTotal ?? 0m;
                }
                // If editing and no rows present, avoid changing totals to zero silently; but proceed as user intent if grid cleared
                // Also track summary taxes for reporting
                p.TotalDiscount = ParseDecimal(lblTotalDiscVal.Text);
                p.TotalCgst = ParseDecimal(lblTotalCgstVal.Text);
                p.TotalSgst = ParseDecimal(lblTotalSgstVal.Text);
                p.TotalIgst = ParseDecimal(lblTotalIgstVal.Text);
                p.Total = ParseDecimal(lblTotalVal.Text) ?? 0m;
                db.SaveChanges();
                Model = p;
                try { RefreshPaymentsAndSummary(); } catch { }

                // Batch limit rule: a product can hold a MAX of 4 batches. Only when a 5th arrives,
                // remove oldest zero-stock batches until back to 4. Never delete while count <= 4.
                try
                {
                    var productsToCheck = db.Products.Select(x => x.Id).ToList();
                    foreach (var pid in productsToCheck)
                    {
                        var batches = db.ProductBatches.Where(b => b.ProductIdRef == pid).OrderBy(b => b.UpdatedAt).ThenBy(b => b.Id).ToList();
                        while (batches.Count > 4)
                        {
                            // Prefer deleting the oldest batch with zero stock; otherwise delete the oldest
                            var victim = batches.FirstOrDefault(b => (b.Stock ?? 0m) <= 0m) ?? batches.First();
                            db.ProductBatches.Remove(victim);
                            batches.Remove(victim);
                        }
                    }
                    db.SaveChanges();
                }
                catch { }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving purchase: {ex.Message}");
                return false;
            }
            finally { UpdateTotals(); }
        }

        private record TempRow(string? Batch, decimal? Mrp, DateTime? Expiry, decimal Cost, decimal Qty, string? Pack, string? Mkt, string? Bonus, decimal? Selling, decimal? Rate, decimal? Disc, decimal? Cgst, decimal? Sgst, decimal? Igst);
        private void ApplyPurchaseToProduct(AppDbContext db, Product prod, TempRow it)
        {
            // Use ProductBatches table: if batch exists -> add stock; else create batch
            var existing = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == prod.Id && b.BatchNumber == it.Batch);
            if (existing != null)
            {
                existing.Mrp = it.Mrp ?? existing.Mrp;
                existing.CostPrice = it.Cost;
                existing.Expiry = it.Expiry ?? existing.Expiry;
                existing.Stock = (existing.Stock ?? 0m) + it.Qty;
                if (!string.IsNullOrWhiteSpace(it.Pack)) existing.Pack = it.Pack;
                if (!string.IsNullOrWhiteSpace(it.Mkt)) existing.MarketedBy = it.Mkt;
                if (!string.IsNullOrWhiteSpace(it.Bonus)) existing.Bonus = it.Bonus;
                if (it.Selling.HasValue) existing.SellingPrice = it.Selling;
                existing.Rate = it.Rate;
                existing.DiscountPercent = it.Disc;
                existing.CgstPercent = it.Cgst;
                existing.SgstPercent = it.Sgst;
                existing.IgstPercent = it.Igst;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Always create a separate batch when name differs; do not merge/rename a placeholder batch automatically
                var nb = new ProductBatch
                {
                    ProductIdRef = prod.Id,
                    BatchNumber = it.Batch,
                    Expiry = it.Expiry,
                    Mrp = it.Mrp,
                    CostPrice = it.Cost,
                    SellingPrice = it.Selling ?? it.Cost,
                    Stock = it.Qty,
                    Pack = it.Pack,
                    MarketedBy = it.Mkt,
                    Bonus = it.Bonus,
                    Rate = it.Rate,
                    DiscountPercent = it.Disc,
                    CgstPercent = it.Cgst,
                    SgstPercent = it.Sgst,
                    IgstPercent = it.Igst,
                    Hsn = prod.Hsn,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ProductBatches.Add(nb);
            }
        }
        private void MoveNewToOld(Product p)
        {
            p.OldBatch = p.NewBatch; p.OldMrp = p.NewMrp; p.OldExpiry = p.NewExpiry; p.OldCostPrice = p.NewCostPrice; p.OldSellingPrice = p.NewSellingPrice; p.OldStock = p.NewStock;
            p.NewBatch = null; p.NewMrp = null; p.NewExpiry = null; p.NewCostPrice = null; p.NewSellingPrice = null; p.NewStock = null;
        }
        private void MoveOldToVeryOld(Product p)
        {
            p.VeryOldBatch = p.OldBatch; p.VeryOldMrp = p.OldMrp; p.VeryOldExpiry = p.OldExpiry; p.VeryOldCostPrice = p.OldCostPrice; p.VeryOldSellingPrice = p.OldSellingPrice; p.VeryOldStock = p.OldStock;
            p.OldBatch = null; p.OldMrp = null; p.OldExpiry = null; p.OldCostPrice = null; p.OldSellingPrice = null; p.OldStock = null;
        }
        private void SetNew(Product p, TempRow it)
        {
            p.NewBatch = it.Batch; p.NewMrp = it.Mrp; p.NewExpiry = it.Expiry; p.NewCostPrice = it.Cost; p.NewSellingPrice = p.NewSellingPrice ?? it.Cost; p.NewStock = (p.NewStock ?? 0m) + it.Qty;
        }
        private static bool NullableDateEq(DateTime? a, DateTime? b)
        { if (!a.HasValue && !b.HasValue) return true; if (a.HasValue != b.HasValue) return false; return a!.Value.Date == b!.Value.Date; }

        private void DoPreview(bool printDirect = false)
        {
            try
            {
                if (!SavePurchase()) return;
                using var db = new AppDbContext();
                var p = db.Purchases.FirstOrDefault(x => x.Id == Model.Id);
                if (p == null) return;
                p.Items = db.PurchaseItems.Where(i => i.PurchaseId == p.Id).ToList();
                var html = PurchasePreviewForm.BuildHtmlFor(p, currentTemplate);
                using var prev = new PurchasePreviewForm(p, html, currentTemplate);
                if (printDirect)
                {
                    prev.Show(this);
                    BeginInvoke(new Action(() => { try { prev.Close(); } catch { } }));
                    return;
                }
                prev.ShowDialog(this);
            }
            catch { }
        }

        private void SearchProductHistory()
        {
            var term = Microsoft.VisualBasic.Interaction.InputBox("Enter Product Name/Barcode to search purchase history:", "Search Product", "");
            if (string.IsNullOrWhiteSpace(term)) return;

            using var db = new AppDbContext();
            var termLower = term.ToLower();
            var matches = (from pi in db.PurchaseItems
                          join pu in db.Purchases on pi.PurchaseId equals pu.Id
                          join s in db.Suppliers on pu.SupplierId equals s.Id into gs
                          from s in gs.DefaultIfEmpty()
                          where (pi.ProductName != null && pi.ProductName.ToLower().Contains(termLower))
                          orderby pu.PurchaseDate descending
                          select new
                          {
                              Date = pu.PurchaseDate,
                              Supplier = s != null ? s.Name : "-",
                              BillNo = pu.InvoiceNumber,
                              Batch = pi.BatchNumber,
                              Qty = pi.Quantity,
                              Price = pi.CostPrice
                          }).ToList();

            if (matches.Count == 0)
            {
                MessageBox.Show("No purchase history found for this product.");
                return;
            }

            var report = string.Join("\n", matches.Select(m => $"{m.Date:dd/MM/yyyy} | {m.Supplier} | Bill: {m.BillNo} | Qty: {m.Qty} | Price: {m.Price:0.00}"));
            MessageBox.Show($"Purchase History for '{term}':\n\nDate | Supplier | Bill | Qty | Price\n" + report, "Product Purchase History");
        }

        private void SetupSupplierCombo()
        {
            cmbSupplier.DropDownStyle = ComboBoxStyle.DropDown;
            cmbSupplier.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbSupplier.AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        private void RefreshPaymentsAndSummary()
        {
            try
            {
                using var db = new AppDbContext();
                var pid = Model?.Id ?? 0;
                decimal totalPaid = 0m;
                if (pid > 0)
                {
                    var pays = db.PurchasePayments.Where(pp => pp.PurchaseId == pid).OrderBy(pp => pp.Date).ThenBy(pp => pp.Id)
                        .Select(pp => new { pp.Id, pp.Date, pp.Method, pp.Notes, pp.Amount }).ToList();
                    if (paymentsGrid != null)
                    {
                        paymentsGrid.DataSource = null;
                        paymentsGrid.DataSource = pays;
                    }
                    totalPaid = pays.Sum(x => x.Amount);
                }
                var subtotal = 0m;
                foreach (DataGridViewRow row in gridItems.Rows)
                {
                    if (row.IsNewRow) continue;
                    var cost = ParseDecimal(row.Cells["colCost"].Value) ?? 0m;
                    var qty = ParseDecimal(row.Cells["colQty"].Value) ?? 0m;
                    subtotal += cost * qty;
                }
                // Apply adjustments for accurate totals
                var extra = (decimal)numExtraCost.Value;
                if (chkExtraPercent.Checked) extra = Math.Round(subtotal * (extra / 100m), 2);
                var discount = (decimal)numDiscount.Value;
                if (chkDiscountPercent.Checked) discount = Math.Round(subtotal * (discount / 100m), 2);
                var taxable = Math.Max(0m, subtotal + extra - discount);
                var tax = Math.Round(taxable * ((decimal)numTaxRate.Value / 100m), 2);
                var total = taxable + tax;
                if (lblSubtotalVal != null) lblSubtotalVal.Text = subtotal.ToString("0.00");
                if (lblTotalVal != null) lblTotalVal.Text = total.ToString("0.00");
                if (lblPaidVal != null) lblPaidVal.Text = totalPaid.ToString("0.00");
                if (lblBalanceVal != null) lblBalanceVal.Text = (total - totalPaid).ToString("0.00");
                // Update status label
                try
                {
                    var status = "Unpaid";
                    if (total <= 0m) status = "Unpaid";
                    else if (totalPaid <= 0m) status = "Unpaid";
                    else if (totalPaid < total) status = "Partial";
                    else status = "Paid";
                    lblStatus.Text = $"Status: {status}";
                }
                catch { }
                // Update compact right
                lblRightCompact.Text = $"Subtotal: {subtotal:0.00}   Total: {total:0.00}   Paid: {totalPaid:0.00}   Balance: {(total-totalPaid):0.00}";

                // Sync with DB if Model is present
                if (Model != null && Model.Id > 0)
                {
                    using var sdb = new AppDbContext();
                    var p = sdb.Purchases.FirstOrDefault(x => x.Id == Model.Id);
                    if (p != null)
                    {
                        p.Subtotal = subtotal;
                        p.Total = total;
                        p.Paid = totalPaid;
                        p.Due = total - totalPaid;
                        sdb.SaveChanges();
                    }
                }
            }
            catch { }
        }

        // Removed duplicate compact-only UpdateLeftInfoForSelection; consolidated above

        private void AddPayment() { ShowPaymentDialog(); }
        private void EditSelectedPayment()
        {
            try
            {
                if (paymentsGrid == null || paymentsGrid.CurrentRow == null) return;
                var rowObj = paymentsGrid.CurrentRow.DataBoundItem;
                if (rowObj == null) return;
                var pid = (int)rowObj.GetType().GetProperty("Id").GetValue(rowObj);
                using var db = new AppDbContext();
                var pay = db.PurchasePayments.FirstOrDefault(x => x.Id == pid);
                if (pay != null) ShowPaymentDialog(pay);
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void ShowPaymentDialog(PurchasePayment existing = null)
        {
            try
            {
                if (Model == null || Model.Id <= 0) { if (!SavePurchase()) return; }
                using var dlg = new Form { Text = existing == null ? "Add Payment" : "Edit Payment", Size = new Size(360, 220), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
                var lblDate = new Label { Text = "Date", Location = new Point(10, 20), AutoSize = true };
                var dt = new DateTimePicker { Location = new Point(80, 16), Width = 250, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = existing?.Date ?? DateTime.Today };
                var lblMethod = new Label { Text = "Method", Location = new Point(10, 55), AutoSize = true };
                var txtMethod = new TextBox { Location = new Point(80, 52), Width = 250, Text = existing?.Method ?? "" };
                var lblAmt = new Label { Text = "Amount", Location = new Point(10, 90), AutoSize = true };
                var numAmt = new NumericUpDown { Location = new Point(80, 86), Width = 120, DecimalPlaces = 2, Maximum = 9999999, Minimum = 0, Value = existing?.Amount ?? 0m };
                var lblNote = new Label { Text = "Notes", Location = new Point(10, 125), AutoSize = true };
                var txtNote = new TextBox { Location = new Point(80, 122), Width = 250, Text = existing?.Notes ?? "" };
                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(160, 155), Size = new Size(80, 28) };
                var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(250, 155), Size = new Size(80, 28) };
                dlg.Controls.Add(lblDate); dlg.Controls.Add(dt); dlg.Controls.Add(lblMethod); dlg.Controls.Add(txtMethod); dlg.Controls.Add(lblAmt); dlg.Controls.Add(numAmt); dlg.Controls.Add(lblNote); dlg.Controls.Add(txtNote); dlg.Controls.Add(btnOk); dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                using var db = new AppDbContext();
                if (existing == null)
                {
                    var pay = new PurchasePayment { PurchaseId = Model.Id, Date = dt.Value.Date, Method = txtMethod.Text?.Trim(), Amount = numAmt.Value, Notes = txtNote.Text?.Trim() };
                    db.PurchasePayments.Add(pay);
                }
                else
                {
                    var pay = db.PurchasePayments.FirstOrDefault(x => x.Id == existing.Id);
                    if (pay != null) { pay.Date = dt.Value.Date; pay.Method = txtMethod.Text?.Trim(); pay.Amount = numAmt.Value; pay.Notes = txtNote.Text?.Trim(); }
                }
                db.SaveChanges();
                RefreshPaymentsAndSummary();
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void DeleteSelectedPayment()
        {
            try
            {
                if (paymentsGrid == null || paymentsGrid.CurrentRow == null) return;
                var rowObj = paymentsGrid.CurrentRow.DataBoundItem;
                if (rowObj == null) return;
                var payId = (int)rowObj.GetType().GetProperty("Id").GetValue(rowObj);

                using var db = new AppDbContext();
                var pay = db.PurchasePayments.FirstOrDefault(pp => pp.Id == payId);
                if (pay != null)
                {
                    db.PurchasePayments.Remove(pay);
                    db.SaveChanges();
                    RefreshPaymentsAndSummary();
                }
            }
            catch { }
        }
        private async void DoAiImport()
        {
            try
            {
                using var dlg = new AiImportDialog();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var json = dlg.JsonText;
                    if (string.IsNullOrWhiteSpace(json)) return;

                    var root = JObject.Parse(json);
                    
                    // Header
                    var supplierName = root["SUPPLIER"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(supplierName))
                    {
                        var sup = _allSuppliers.FirstOrDefault(s => string.Equals(s.Name, supplierName, StringComparison.OrdinalIgnoreCase));
                        if (sup != null) cmbSupplier.SelectedValue = sup.Id;
                        else cmbSupplier.Text = supplierName;
                    }

                    txtInvoiceNo.Text = root["BILL NO"]?.ToString() ?? "";
                    var dateStr = root["DATE"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, out var dt))
                    {
                        dtPurchase.Value = dt;
                    }

                    // Products Collection & Matching
                    var productsJson = root["products"] as JArray;
                    if (productsJson == null) return;

                    var reviewItems = new List<AiPurchaseItem>();
                    using (var db = new AppDbContext())
                    {
                        var allProducts = db.Products.ToList(); // Load into memory to avoid SQL translation errors
                        var allDbProductNames = allProducts.Select(p => p.Name).ToList();
                        var inputNames = productsJson.Select(p => p["NAME"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();

                        // Call AI for robust Matching
                        var aiService = new AiAgentService();
                        var aiMatches = await aiService.PerformProductMatchingAsync(inputNames, allDbProductNames);

                        foreach (var p in productsJson)
                        {
                            var inputName = p["NAME"]?.ToString() ?? "";
                            
                            // Use AI result if it successfully matched something
                            string? aiSuggestedName = null;
                            if (aiMatches.TryGetValue(inputName, out var suggestion)) aiSuggestedName = suggestion;

                            Product? matchedProd = null;
                            if (!string.IsNullOrEmpty(aiSuggestedName))
                            {
                                // In-memory search (case-insensitive)
                                matchedProd = allProducts.FirstOrDefault(x => string.Equals(x.Name, aiSuggestedName, StringComparison.OrdinalIgnoreCase));
                            }
                            
                            // fallback logic if AI didn't return a match or the match was invalid
                            if (matchedProd == null)
                            {
                                matchedProd = allProducts.FirstOrDefault(x => string.Equals(x.Name, inputName, StringComparison.OrdinalIgnoreCase));
                            }
                            
                            if (matchedProd == null)
                            {
                                // Intelligent Keyword Fallback
                                var words = inputName.Split(new[] { ' ', '-', '/', '.' }, StringSplitOptions.RemoveEmptyEntries)
                                                     .Where(w => w.Length > 2).ToList();
                                
                                if (words.Count > 0)
                                {
                                    var firstWord = words[0];
                                    // Find all products starting with the same brand name
                                    var candidates = allProducts.Where(p => p.Name.StartsWith(firstWord, StringComparison.OrdinalIgnoreCase)).ToList();
                                    
                                    if (candidates.Count == 1)
                                    {
                                        matchedProd = candidates[0];
                                    }
                                    else if (candidates.Count > 1)
                                    {
                                        // Pick the one with the most matching keywords
                                        matchedProd = candidates
                                            .OrderByDescending(c => words.Count(w => c.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
                                            .ThenBy(c => Math.Abs(c.Name.Length - inputName.Length))
                                            .FirstOrDefault();
                                    }
                                }
                            }
                            
                            if (matchedProd == null)
                            {
                                // Last resort simple contains
                                matchedProd = allProducts.FirstOrDefault(x => x.Name.Contains(inputName, StringComparison.OrdinalIgnoreCase) || inputName.Contains(x.Name, StringComparison.OrdinalIgnoreCase));
                            }

                            // Robust Parsing & Auto-Calculation
                            var mrp = ParseDecimal(p["MRP"]);
                            var rate = ParseDecimal(p["RATE"]);
                            var cost = ParseDecimal(p["COST"] ?? p["CP"]);
                            var sell = ParseDecimal(p["SELL_PRICE"] ?? p["SP"]);
                            var qty = ParseDecimal(p["QTY"]) ?? 0;
                            var disc = ParseDecimal(p["DISC"] ?? p["DISCOUNT"]) ?? 0;
                            var cgst = ParseDecimal(p["CGST"]) ?? 0;
                            var sgst = ParseDecimal(p["SGST"]) ?? 0;
                            var igst = ParseDecimal(p["IGST"]) ?? 0;

                            decimal totalTaxFactor = 1 + (cgst + sgst + igst) / 100m;
                            decimal discFactor = 1 - disc / 100m;

                            if (cost == null && rate != null)
                            {
                                cost = Math.Round(rate.Value * discFactor * totalTaxFactor, 2);
                            }
                            else if (rate == null && cost != null)
                            {
                                if (discFactor * totalTaxFactor > 0)
                                    rate = Math.Round(cost.Value / (discFactor * totalTaxFactor), 2);
                            }

                            // Keep the EXACT name as pasted in the JSON; do not rename/assume
                            reviewItems.Add(new AiPurchaseItem
                            {
                                Sku = string.Empty, // Manual confirmation required as per prior directive
                                ProductName = inputName,
                                BatchNumber = p["BATCH NO"]?.ToString() ?? p["BATCH"]?.ToString(),
                                Expiry = p["EXPIRY"]?.ToString(),
                                Mrp = mrp,
                                SellingPrice = sell,
                                CostPrice = cost,
                                Quantity = qty,
                                Rate = rate,
                                Hsn = p["HSN"]?.ToString() ?? matchedProd?.Hsn,
                                Pack = p["PACK"]?.ToString(),
                                Mkt = p["MKT"]?.ToString() ?? p["MARKETED BY"]?.ToString() ?? matchedProd?.MarketedBy,
                                Bonus = p["BONUS"]?.ToString(),
                                Disc = disc,
                                Cgst = cgst,
                                Sgst = sgst,
                                Igst = igst,
                                Category = p["CATEGORY"]?.ToString() ?? matchedProd?.Category
                            });
                        }
                    }

                    // Show Confirmation Dialog
                    var aiResp = new AiActionResponse
                    {
                        Action = "create_purchase",
                        Message = $"AI matched {reviewItems.Count} items. All details (Pack, Mkt, Taxes, etc.) have been imported. Please verify.",
                        Data = JObject.FromObject(new AiPurchaseData { Items = reviewItems })
                    };

                    using (var reviewDlg = new AiActionReviewForm(aiResp))
                    {
                        if (reviewDlg.ShowDialog(this) != DialogResult.OK) return;
                        
                        // Approved! Add to Grid
                        autoSaving = true;
                        gridItems.Rows.Clear();
                        
                        var updatedData = reviewDlg.ResultResponse.Data.ToObject<AiPurchaseData>();
                        if (updatedData == null) return;

                        foreach (var item in updatedData.Items)
                        {
                            int idx = gridItems.Rows.Add();
                            var r = gridItems.Rows[idx];
                            r.Cells["colSku"].Value = item.Sku; // Use SKU from dialog (blank or manual)
                            r.Cells["colName"].Value = item.ProductName;
                            r.Cells["colBatch"].Value = item.BatchNumber;
                            r.Cells["colExpiry"].Value = item.Expiry;
                            r.Cells["colMrp"].Value = item.Mrp;
                            r.Cells["colSell"].Value = item.SellingPrice;
                            r.Cells["colRate"].Value = item.Rate;
                            r.Cells["colCost"].Value = item.CostPrice;
                            r.Cells["colQty"].Value = item.Quantity;
                            r.Cells["colHsn"].Value = item.Hsn;
                            r.Cells["colPack"].Value = item.Pack;
                            r.Cells["colMkt"].Value = item.Mkt;
                            r.Cells["colBonus"].Value = item.Bonus;
                            r.Cells["colDisc"].Value = item.Disc;
                            r.Cells["colCgst"].Value = item.Cgst;
                            r.Cells["colSgst"].Value = item.Sgst;
                            r.Cells["colIgst"].Value = item.Igst;
                            r.Cells["colCategory"].Value = item.Category;
                        }
                        autoSaving = false;
                        UpdateTotals();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("AI Import failed: " + ex.Message);
            }
        }

        private void DoVoid()
        {
            if (Model == null || Model.Id <= 0) { MessageBox.Show("Cannot void unsaved purchase."); return; }
            if (MessageBox.Show("Are you sure you want to VOID this purchase? This action cannot be undone.", "Confirm Void", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            bool reduceStock = MessageBox.Show("Do you want to REDUCE/DEDUCT the items in this purchase from your current stock?", "Reduce Stock?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            try
            {
                using var db = new AppDbContext();
                var p = db.Purchases.FirstOrDefault(x => x.Id == Model.Id);
                if (p == null) return;

                if (reduceStock)
                {
                    var items = db.PurchaseItems.Where(i => i.PurchaseId == p.Id).ToList();
                    foreach (var it in items)
                    {
                        if (it.ProductId.HasValue)
                        {
                            var pb = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == it.ProductId.Value && b.BatchNumber == it.BatchNumber);
                            if (pb != null)
                            {
                                pb.Stock = Math.Max(0m, (pb.Stock ?? 0m) - it.Quantity);
                                pb.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }

                p.Notes = (p.Notes ?? "") + " [VOIDED AT " + DateTime.Now.ToString() + "]";
                p.Total = 0;
                p.Subtotal = 0;
                // We keep the items for history but the purchase itself is effectively zeroed
                db.SaveChanges();
                MessageBox.Show("Purchase voided successfully.");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MessageBox.Show("Error voiding: " + ex.Message); }
        }

        private class AiImportDialog : Form
        {
            private TextBox txtJson = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10) };
            private Button btnImport = new Button { Text = "Next: Review Items", DialogResult = DialogResult.OK, Width = 150 };
            private Button btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            private Button btnUploadDoc = new Button { Text = "📁 Upload Invoice (PDF / Excel)", Width = 230, Height = 32 };
            private Label lblStatus = new Label { AutoSize = true, Text = "Upload a PDF or Excel invoice to extract items automatically with AI, or paste JSON directly:", Location = new Point(10, 48), ForeColor = Color.FromArgb(70, 70, 70) };

            public string JsonText => txtJson.Text;

            public AiImportDialog()
            {
                Text = "Smart Invoice Import — PDF / Excel / JSON"; 
                Size = new Size(720, 540); 
                StartPosition = FormStartPosition.CenterParent;

                Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(10, 8, 10, 6), BackColor = Color.FromArgb(248, 249, 250) };
                btnUploadDoc.Location = new Point(10, 10);
                btnUploadDoc.BackColor = Color.FromArgb(13, 110, 253);
                btnUploadDoc.ForeColor = Color.White;
                btnUploadDoc.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                btnUploadDoc.FlatStyle = FlatStyle.Flat;
                btnUploadDoc.FlatAppearance.BorderSize = 0;
                btnUploadDoc.Cursor = Cursors.Hand;

                var lblHint = new Label 
                { 
                    Text = "Supported: PDF, XLSX, XLS. AI will parse Header, Items, Batch, Expiry, MRP, Cost & GST.", 
                    Location = new Point(250, 16), 
                    AutoSize = true, 
                    ForeColor = Color.DimGray,
                    Font = new Font("Segoe UI", 8.5F)
                };

                pnlTop.Controls.Add(btnUploadDoc);
                pnlTop.Controls.Add(lblHint);
                pnlTop.Controls.Add(lblStatus);

                btnUploadDoc.Click += async (s, e) =>
                {
                    using var ofd = new OpenFileDialog
                    {
                        Filter = "Invoice Documents (*.pdf;*.xlsx;*.xls)|*.pdf;*.xlsx;*.xls|PDF Files (*.pdf)|*.pdf|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                        Title = "Select Purchase Invoice Document to Extract"
                    };
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            btnUploadDoc.Enabled = false;
                            btnImport.Enabled = false;
                            lblStatus.Text = $"Processing '{System.IO.Path.GetFileName(ofd.FileName)}' with AI engine... please wait...";
                            lblStatus.ForeColor = Color.DarkBlue;
                            Cursor = Cursors.WaitCursor;
                            Application.DoEvents();

                            var aiProcessor = new AiDocumentProcessor(new AiAgentService());
                            var extractedJson = await aiProcessor.ProcessInvoiceDocumentAsync(ofd.FileName);
                            txtJson.Text = extractedJson;
                            lblStatus.Text = "Invoice data successfully extracted! Verify or click 'Next: Review Items' to proceed.";
                            lblStatus.ForeColor = Color.ForestGreen;
                        }
                        catch (Exception ex)
                        {
                            lblStatus.Text = $"Error: {ex.Message}";
                            lblStatus.ForeColor = Color.Firebrick;
                            MessageBox.Show($"Failed to process document: {ex.Message}", "AI Extraction Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        finally
                        {
                            btnUploadDoc.Enabled = true;
                            btnImport.Enabled = true;
                            Cursor = Cursors.Default;
                        }
                    }
                };

                Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(10, 8, 10, 8) };
                btnImport.Dock = DockStyle.Right; 
                btnCancel.Dock = DockStyle.Right;
                var btnSettings = new Button { Text = "AI Settings", Dock = DockStyle.Left, Width = 110 };
                
                btnImport.BackColor = Color.FromArgb(0, 122, 204); 
                btnImport.ForeColor = Color.White; 
                btnImport.FlatStyle = FlatStyle.Flat;
                btnImport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                btnCancel.FlatStyle = FlatStyle.Flat;
                btnSettings.FlatStyle = FlatStyle.Flat;

                btnSettings.Click += (s, e) => {
                    using var sDlg = new AiSettingsForm();
                    sDlg.ShowDialog(this);
                };

                pnlBottom.Controls.Add(btnImport);
                pnlBottom.Controls.Add(new Label { Dock = DockStyle.Right, Width = 10 });
                pnlBottom.Controls.Add(btnCancel);
                pnlBottom.Controls.Add(btnSettings);

                Controls.Add(txtJson); 
                Controls.Add(pnlTop);
                Controls.Add(pnlBottom);
                AcceptButton = btnImport; 
                CancelButton = btnCancel;
            }
        }
    }
}
