using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Forms
{
    public partial class ProductPicker : Form
    {
        public Product SelectedProduct { get; private set; }
        public string? SelectedBatchNumber { get; private set; }
        public DateTime? SelectedExpiry { get; private set; }
        public decimal? SelectedMrp { get; private set; }
        public decimal? SelectedSellingPrice { get; private set; }
        public decimal? SelectedCostPrice { get; private set; }
        private readonly int _customerId;
        private System.Collections.Generic.List<ProductRow> _allProducts = new System.Collections.Generic.List<ProductRow>();
        private class ProductRow
        {
            public int Id { get; set; }
            public string Barcode { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Hsn { get; set; }
            public string? Category { get; set; }
            public string? BatchNumber { get; set; }
            public DateTime? Expiry { get; set; }
            public decimal? Mrp { get; set; }
            public decimal? CostPrice { get; set; }
            public decimal? SellingPrice { get; set; }
            public decimal? Stock { get; set; }
            public string? Bonus { get; set; }
            public string? Pack { get; set; }
            public string? MarketedBy { get; set; }
            public decimal? LastSale { get; set; }
        }
        private DataGridView dataGridView1;
        private TextBox txtSearchBox;
        private Button btnSelect;
        private Button btnCancel;
        private Button btnAdd;
        private Button btnEdit;

        public ProductPicker(int customerId)
        {
            _customerId = customerId;
            InitializeComponents();
            LoadProducts();
        }

        private void InitializeComponents()
        {
            // Form setup
            this.Text = "Select Product (by Batch)";
            this.Size = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Search box
            var pnlSearch = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
            txtSearchBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Search products..." };
            txtSearchBox.TextChanged += TxtSearch_TextChanged;
            pnlSearch.Controls.Add(txtSearchBox);

            // DataGridView
            dataGridView1 = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                RowHeadersVisible = false
            };
            // Reduce flicker and lag on scroll
            try
            {
                typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(dataGridView1, true, null);
            }
            catch { }
            dataGridView1.VirtualMode = false;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dataGridView1.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
            dataGridView1.KeyDown += DataGridView1_KeyDown;
            txtSearchBox.KeyDown += TxtSearchBox_KeyDown;

            // Buttons panel
            var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            var flowLeft = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 260, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 10, 10, 10), WrapContents = false };
            var flowRight = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 230, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 10, 10, 10), WrapContents = false };
            btnAdd = new Button { Text = "Add Product", Size = new Size(110, 30) };
            btnEdit = new Button { Text = "Edit Product", Size = new Size(110, 30) };
            btnSelect = new Button { Text = "Select", DialogResult = DialogResult.OK, Size = new Size(100, 30) };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(100, 30) };
            
            btnSelect.Click += BtnSelect_Click;
            btnCancel.Click += BtnCancel_Click;
            btnAdd.Click += (s, e) => { using var dlg = new ProductEditForm(); if (dlg.ShowDialog(this) == DialogResult.OK) LoadProducts(); txtSearchBox.Focus(); txtSearchBox.SelectAll(); };
            btnEdit.Click += (s, e) => {
                if (dataGridView1.CurrentRow == null) return;
                var code = (string)dataGridView1.CurrentRow.Cells["Barcode"].Value;
                using var db = new AppDbContext();
                var prod = db.Products.FirstOrDefault(p => p.Barcode == code);
                if (prod == null) return;
                using var dlg = new ProductEditForm(prod);
                if (dlg.ShowDialog(this) == DialogResult.OK) LoadProducts();
                txtSearchBox.Focus(); txtSearchBox.SelectAll();
            };

            flowLeft.Controls.AddRange(new Control[] { btnAdd, btnEdit });
            flowRight.Controls.AddRange(new Control[] { btnCancel, btnSelect });
            pnlButtons.Controls.Add(flowLeft);
            pnlButtons.Controls.Add(flowRight);

            // Add controls to form
            this.Controls.Add(dataGridView1);
            this.Controls.Add(pnlSearch);
            this.Controls.Add(pnlButtons);

            this.Shown += (s, e) => { txtSearchBox.Focus(); txtSearchBox.SelectAll(); };
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.W)
                { e.Handled = true; this.DialogResult = DialogResult.Cancel; this.Close(); }
            };
        }

        private void LoadProducts()
        {
            try
            {
                using var db = new AppDbContext();
                db.ChangeTracker.QueryTrackingBehavior = Microsoft.EntityFrameworkCore.QueryTrackingBehavior.NoTracking;

                System.Collections.Generic.List<dynamic> batchRows;
                try
                {
                    batchRows = (from p in db.Products.AsNoTracking()
                                 join b in db.ProductBatches.AsNoTracking() on p.Id equals b.ProductIdRef into pb
                                 from b in pb.DefaultIfEmpty()
                                 orderby p.Name
                                 select new
                                 {
                                     p.Id,
                                     p.Barcode,
                                     p.Name,
                                     p.Hsn,
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
                                 }).ToList<dynamic>();
                }
                catch
                {
                    // Fallback to legacy old/new batch fields
                    var products = db.Products.AsNoTracking().OrderBy(p => p.Name).ToList();
                    batchRows = new System.Collections.Generic.List<dynamic>();
                    foreach (var p in products)
                    {
                        if (!string.IsNullOrWhiteSpace(p.OldBatch) || p.OldMrp.HasValue || p.OldSellingPrice.HasValue)
                        {
                            batchRows.Add(new { p.Id, p.Barcode, p.Name, p.Hsn, p.Category, BatchNumber = p.OldBatch, Expiry = p.OldExpiry, Mrp = p.OldMrp, CostPrice = p.OldCostPrice, SellingPrice = p.OldSellingPrice, Stock = p.OldStock, Bonus = (string?)null, Pack = (string?)null, MarketedBy = (string?)null });
                        }
                        if (!string.IsNullOrWhiteSpace(p.NewBatch) || p.NewMrp.HasValue || p.NewSellingPrice.HasValue)
                        {
                            batchRows.Add(new { p.Id, p.Barcode, p.Name, p.Hsn, p.Category, BatchNumber = p.NewBatch, Expiry = p.NewExpiry, Mrp = p.NewMrp, CostPrice = p.NewCostPrice, SellingPrice = p.NewSellingPrice, Stock = p.NewStock, Bonus = (string?)null, Pack = (string?)null, MarketedBy = (string?)null });
                        }
                    }
                }

                System.Collections.Generic.Dictionary<(int Ref, string Batch), decimal>? lastByRefBatch = null;
                if (_customerId > 0)
                {
                    var lastRef = (from ii in db.InvoiceItems.AsNoTracking()
                                    join inv in db.Invoices.AsNoTracking() on ii.InvoiceId equals inv.Id
                                    where inv.CustomerId == _customerId && ii.ProductIdRef != null
                                    orderby inv.InvoiceDate descending, ii.InvoiceId descending, ii.Id descending
                                    select new { Ref = ii.ProductIdRef!.Value, ii.Price, inv.InvoiceDate, ii.InvoiceId, ii.Id, ii.Description })
                                   .ToList();

                    // Last by (product ref, batch)
                    static string? ExtractBatch(string? desc)
                    {
                        if (string.IsNullOrEmpty(desc)) return null;
                        var idx = desc.IndexOf("BATCH:", StringComparison.OrdinalIgnoreCase);
                        if (idx < 0) return null;
                        var start = idx + 6;
                        // read until space or separator
                        int end = desc.IndexOfAny(new[] { ' ', '|', ',', ';' }, start);
                        var batch = (end > start ? desc.Substring(start, end - start) : desc.Substring(start)).Trim();
                        return string.IsNullOrWhiteSpace(batch) ? null : batch;
                    }

                    var withBatch = lastRef
                        .Where(x => !string.IsNullOrEmpty(x.Description) && x.Description!.IndexOf("BATCH:", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(x => new { x.Ref, Batch = ExtractBatch(x.Description), x.Price, x.InvoiceDate, x.Id })
                        .Where(x => !string.IsNullOrEmpty(x.Batch))
                        .ToList();

                    lastByRefBatch = withBatch
                        .GroupBy(x => (x.Ref, Batch: x.Batch!))
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(y => y.InvoiceDate).ThenByDescending(y => y.Id).First().Price);

                }

                _allProducts = batchRows.Select(r =>
                {
                    decimal? last = null;
                    if (_customerId > 0 && lastByRefBatch != null && !string.IsNullOrEmpty(r.BatchNumber))
                    {
                        if (lastByRefBatch.TryGetValue((r.Id, r.BatchNumber!), out var val)) last = val;
                    }
                    if (last == null) last = r.SellingPrice;
                    return new ProductRow
                    {
                        Id = r.Id,
                        Barcode = r.Barcode,
                        Name = r.Name,
                        Hsn = r.Hsn,
                        Category = r.Category,
                        BatchNumber = r.BatchNumber,
                        Expiry = r.Expiry,
                        Mrp = r.Mrp,
                        CostPrice = r.CostPrice,
                        SellingPrice = r.SellingPrice,
                        Stock = r.Stock,
                        Bonus = r.Bonus,
                        Pack = r.Pack,
                        MarketedBy = r.MarketedBy,
                        LastSale = last
                    };
                }).ToList();
                var bs = new BindingSource();
                bs.DataSource = _allProducts;
                dataGridView1.AutoGenerateColumns = false;
                dataGridView1.Columns.Clear();
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                // Define compact columns: Name, Scheme, Batch, Expiry, MRP, SP, LSP, Stock
                var colId = new DataGridViewTextBoxColumn { DataPropertyName = "Id", Name = "Id", Visible = false };
                var colBarcode = new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", Name = "Barcode", Visible = false };
                var colNameVis = new DataGridViewTextBoxColumn { DataPropertyName = "Name", Name = "Name", HeaderText = "Name", FillWeight = 36, MinimumWidth = 200 };
                var colScheme = new DataGridViewTextBoxColumn { DataPropertyName = "Bonus", Name = "Bonus", HeaderText = "Scheme", FillWeight = 12, MinimumWidth = 90 };
                var colBatch = new DataGridViewTextBoxColumn { DataPropertyName = "BatchNumber", Name = "BatchNumber", HeaderText = "Batch", FillWeight = 14, MinimumWidth = 90 };
                var colExpiry = new DataGridViewTextBoxColumn { DataPropertyName = "Expiry", Name = "Expiry", HeaderText = "Expiry", FillWeight = 12, MinimumWidth = 90, DefaultCellStyle = { Format = "dd/MM/yyyy" } };
                var colMrp = new DataGridViewTextBoxColumn { DataPropertyName = "Mrp", Name = "Mrp", HeaderText = "MRP", FillWeight = 12, MinimumWidth = 70, DefaultCellStyle = { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
                var colSell = new DataGridViewTextBoxColumn { DataPropertyName = "SellingPrice", Name = "SellingPrice", HeaderText = "Price", FillWeight = 12, MinimumWidth = 70, DefaultCellStyle = { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
                var colLast = new DataGridViewTextBoxColumn { DataPropertyName = "LastSale", Name = "LastSale", HeaderText = "Last Sale", FillWeight = 12, MinimumWidth = 70, DefaultCellStyle = { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
                var colStock = new DataGridViewTextBoxColumn { DataPropertyName = "Stock", Name = "Stock", HeaderText = "Stock", FillWeight = 14, MinimumWidth = 70, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } };
                var colCost = new DataGridViewTextBoxColumn { DataPropertyName = "CostPrice", Name = "CostPrice", Visible = false };
                dataGridView1.Columns.AddRange(new DataGridViewColumn[] { colId, colBarcode, colNameVis, colScheme, colBatch, colExpiry, colMrp, colSell, colLast, colStock, colCost });
            
                dataGridView1.SuspendLayout();
                dataGridView1.DataSource = bs;
                
                // Format columns (guard each access)
                if (dataGridView1.Columns["Id"] != null) dataGridView1.Columns["Id"].Visible = false;
                if (dataGridView1.Columns["Barcode"] != null) dataGridView1.Columns["Barcode"].Visible = false;
                // Auto size by FillWeights (no horizontal scroll)
                dataGridView1.ResumeLayout();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSelect_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow != null)
            {
                var row = dataGridView1.CurrentRow;
                var cellVal = row.Cells["Barcode"].Value;
                var code = cellVal == null ? null : cellVal.ToString();
                using var db = new AppDbContext();
                var prod = !string.IsNullOrWhiteSpace(code) ? db.Products.FirstOrDefault(p => p.Barcode == code) : null;
                if (prod != null)
                {
                    SelectedProduct = prod;
                    try { SelectedBatchNumber = row.Cells["BatchNumber"]?.Value?.ToString(); } catch { SelectedBatchNumber = null; }
                    try { SelectedMrp = row.Cells["Mrp"]?.Value is decimal d ? d : (decimal?)null; } catch { SelectedMrp = null; }
                    try { SelectedSellingPrice = row.Cells["SellingPrice"]?.Value is decimal d2 ? d2 : (decimal?)null; } catch { SelectedSellingPrice = null; }
                    try 
                    { 
                        var ev = row.Cells["Expiry"]?.Value; 
                        if (ev is DateTime dt) SelectedExpiry = dt; 
                        else if (ev != null && DateTime.TryParse(ev.ToString(), out var dt2)) SelectedExpiry = dt2; 
                        else SelectedExpiry = null; 
                    } 
                    catch { SelectedExpiry = null; }
                    try { SelectedCostPrice = row.Cells["CostPrice"]?.Value as decimal?; } catch { SelectedCostPrice = null; }
                    if (SelectedCostPrice == null) SelectedCostPrice = prod.Price;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            var term = tb?.Text?.Trim() ?? string.Empty;
            var bs = dataGridView1.DataSource as BindingSource;
            if (bs == null) { bs = new BindingSource(); dataGridView1.DataSource = bs; }
            if (string.IsNullOrEmpty(term))
            {
                bs.DataSource = _allProducts;
            }
            else
            {
                var lower = term.ToLowerInvariant();
                var filtered = _allProducts.Where(p => (p.Name ?? string.Empty).ToLowerInvariant().Contains(lower)
                                                     || (p.Barcode ?? string.Empty).ToLowerInvariant().Contains(lower))
                                            .ToList();
                bs.DataSource = filtered;
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                BtnSelect_Click(sender, e);
            }
        }

        // Keep grid navigation regardless of current control; handle Tab without altering selection
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down)
            {
                if (!dataGridView1.Focused)
                {
                    dataGridView1.Focus();
                    if (dataGridView1.CurrentCell == null && dataGridView1.Rows.Count > 0)
                        dataGridView1.CurrentCell = dataGridView1[0, 0];
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if (keyData == Keys.Tab)
            {
                // If grid has focus, cycle focus: Grid -> Add -> Edit -> Select -> Cancel -> Search -> Grid
                if (dataGridView1.Focused)
                {
                    TryFocus(btnAdd); return true;
                }
                if (btnAdd != null && btnAdd.Focused)
                { TryFocus(btnEdit); return true; }
                if (btnEdit != null && btnEdit.Focused)
                { TryFocus(btnSelect); return true; }
                if (btnSelect != null && btnSelect.Focused)
                { TryFocus(btnCancel); return true; }
                if (btnCancel != null && btnCancel.Focused)
                { TryFocus(txtSearchBox); return true; }
                if (txtSearchBox != null && txtSearchBox.Focused)
                { TryFocus(dataGridView1); return true; }
                // Default
                TryFocus(txtSearchBox); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TryFocus(Control? c)
        {
            if (c == null) return;
            try
            {
                c.Focus();
                if (c == txtSearchBox)
                {
                    txtSearchBox.SelectAll();
                }
                if (c == dataGridView1 && dataGridView1.CurrentCell == null && dataGridView1.Rows.Count > 0)
                {
                    dataGridView1.CurrentCell = dataGridView1[0, 0];
                }
            }
            catch { }
        }

        private void DataGridView1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                // Move focus to Select button, do not change row selection
                e.Handled = true; e.SuppressKeyPress = true;
                if (btnSelect != null) btnSelect.Focus();
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                // Ensure a current row before selecting
                if (dataGridView1.CurrentCell == null && dataGridView1.Rows.Count > 0)
                    dataGridView1.CurrentCell = dataGridView1[0, 0];
                BtnSelect_Click(sender!, EventArgs.Empty);
                e.Handled = true; e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                BtnCancel_Click(sender!, EventArgs.Empty);
                e.Handled = true; e.SuppressKeyPress = true;
                return;
            }
        }
        // Up/Down arrows use default selection movement

        private void TxtSearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && dataGridView1.Rows.Count > 0)
            {
                dataGridView1.Focus();
                if (dataGridView1.SelectedRows.Count == 0)
                {
                    dataGridView1.Rows[0].Selected = true;
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
    }
}
