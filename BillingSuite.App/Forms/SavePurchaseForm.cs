using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;

namespace BillingSuite.App.Forms
{
    public class SavePurchaseForm : Form
    {
        // Toolbar and layout (clone-like)
        private readonly Panel pnlToolbar = new Panel();
        private readonly ToolStrip tool = new ToolStrip();
        private readonly ToolStripButton tsSelectSupplier = new ToolStripButton();
        private readonly ToolStripButton tsAddLine = new ToolStripButton();
        private readonly ToolStripButton tsDeleteLine = new ToolStripButton();
        private readonly ToolStripButton tsSave = new ToolStripButton();

        private readonly Panel contentPanel = new Panel();
        private readonly GroupBox gbSupplier = new GroupBox();
        private readonly GroupBox gbPurchase = new GroupBox();
        private readonly ComboBox cmbSupplier = new ComboBox();
        private readonly TextBox txtNotes = new TextBox();
        private readonly TextBox txtInvoiceNo = new TextBox();
        private readonly DateTimePicker dtPurchase = new DateTimePicker();

        private readonly DataGridView gridItems = new DataGridView();
        private readonly Panel pnlItemsFooter = new Panel();
        private readonly Label lblItemsCount = new Label();
        private readonly Panel pnlCompactFooter = new Panel();
        private readonly Label lblLeftCompact = new Label();
        private readonly Label lblRightCompact = new Label();

        private class Line
        {
            public int? ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string? Pack { get; set; }
            public string? MarketedBy { get; set; }
            public string? Batch { get; set; }
            public string? Expiry { get; set; } // MM/YYYY
            public decimal? Mrp { get; set; }
            public decimal? Cp { get; set; }
            public decimal? Sp { get; set; }
            public decimal Qty { get; set; } = 1m;
            public string? Bonus { get; set; }
            public decimal LineTotal => (Cp ?? 0m) * Qty;
        }

        private readonly BindingSource bs = new BindingSource();

        public SavePurchaseForm(int? presetSupplierId = null)
        {
            Text = "Save Purchase";
            StartPosition = FormStartPosition.CenterParent;
            var wa = Screen.FromHandle(this.Handle).WorkingArea;
            int targetW = Math.Min(1100, Math.Max(900, wa.Width - 40));
            int targetH = Math.Min(720, Math.Max(580, wa.Height - 40));
            Size = new Size(targetW, targetH);
            MinimumSize = new Size(900, 580);

            // Toolbar host
            pnlToolbar.Dock = DockStyle.Top; pnlToolbar.Height = 30; Controls.Add(pnlToolbar);
            tool.GripStyle = ToolStripGripStyle.Hidden; tool.Dock = DockStyle.Fill; tool.AutoSize = false; tool.Height = 30; tool.Padding = new Padding(4,2,4,2);
            tsSelectSupplier.Text = "Select Supplier";
            tsAddLine.Text = "Add line item";
            tsDeleteLine.Text = "Delete line";
            tsSave.Text = "Save";
            tool.Items.AddRange(new ToolStripItem[] { tsSelectSupplier, new ToolStripSeparator(), tsAddLine, tsDeleteLine, new ToolStripSeparator(), tsSave });
            pnlToolbar.Controls.Add(tool);

            // Content
            contentPanel.Dock = DockStyle.Fill; contentPanel.Padding = new Padding(0); Controls.Add(contentPanel);

            // Compact bottom footer (left: item metrics, right: totals)
            pnlCompactFooter.Dock = DockStyle.Bottom; pnlCompactFooter.Height = 24; pnlCompactFooter.BackColor = SystemColors.ControlLightLight; pnlCompactFooter.Padding = new Padding(8,2,8,2);
            lblLeftCompact.AutoSize = false; lblLeftCompact.TextAlign = ContentAlignment.MiddleLeft; lblLeftCompact.Dock = DockStyle.Left; lblLeftCompact.Width = 600; lblLeftCompact.Font = new Font(Font.FontFamily, 8.5f);
            lblRightCompact.AutoSize = false; lblRightCompact.TextAlign = ContentAlignment.MiddleRight; lblRightCompact.Dock = DockStyle.Fill; lblRightCompact.Font = new Font(Font.FontFamily, 8.5f);
            pnlCompactFooter.Controls.Add(lblRightCompact); pnlCompactFooter.Controls.Add(lblLeftCompact);
            Controls.Add(pnlCompactFooter);

            // Supplier box
            gbSupplier.Text = "Supplier *";
            gbSupplier.SetBounds(10, 10, 700, 155);
            gbSupplier.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var lblSupTo = new Label { Text = "Purchase from *", Location = new Point(10, 25), AutoSize = true };
            cmbSupplier.DropDownStyle = ComboBoxStyle.DropDown; cmbSupplier.AutoCompleteMode = AutoCompleteMode.SuggestAppend; cmbSupplier.AutoCompleteSource = AutoCompleteSource.ListItems; cmbSupplier.DisplayMember = "Name"; cmbSupplier.ValueMember = "Id";
            cmbSupplier.SetBounds(120, 22, 230, 23);
            var btnAddSupplier = new Button { Text = "+", Size = new Size(25,23), Location = new Point(120 + 230 + 5, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            var lblNotes = new Label { Text = "Notes", Location = new Point(10, 55), AutoSize = true };
            txtNotes.SetBounds(120, 52, 285, 64); txtNotes.Multiline = true;
            gbSupplier.Controls.AddRange(new Control[] { lblSupTo, cmbSupplier, btnAddSupplier, lblNotes, txtNotes });
            contentPanel.Controls.Add(gbSupplier);

            // Purchase box (right)
            gbPurchase.Text = "Purchase";
            gbPurchase.SetBounds(720, 10, 360, 160); gbPurchase.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var lblInvNo = new Label { Text = "Invoice #", Location = new Point(10, 25), AutoSize = true };
            txtInvoiceNo.SetBounds(90, 22, 250, 23);
            var lblDate = new Label { Text = "Date", Location = new Point(10, 55), AutoSize = true };
            dtPurchase.SetBounds(90, 52, 250, 23); dtPurchase.Format = DateTimePickerFormat.Custom; dtPurchase.CustomFormat = "dd/MM/yyyy";
            gbPurchase.Controls.AddRange(new Control[] { lblInvNo, txtInvoiceNo, lblDate, dtPurchase });
            contentPanel.Controls.Add(gbPurchase);

            // Center with grid and footer
            var pnlTopHost = new Panel { Dock = DockStyle.Top, Height = 180, Padding = new Padding(0) };
            try { contentPanel.Controls.Remove(gbSupplier); pnlTopHost.Controls.Add(gbSupplier); } catch { }
            try { contentPanel.Controls.Remove(gbPurchase); pnlTopHost.Controls.Add(gbPurchase); } catch { }
            contentPanel.Controls.Add(pnlTopHost);

            var pnlCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BorderStyle = BorderStyle.FixedSingle };
            pnlItemsFooter.Dock = DockStyle.Bottom; pnlItemsFooter.Height = 22; pnlItemsFooter.BackColor = SystemColors.Control;
            var pnlBottomLine = new Panel { Height = 1, BackColor = Color.Black, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            pnlItemsFooter.Controls.Add(pnlBottomLine);
            pnlItemsFooter.Resize += (s, e) => { try { pnlBottomLine.SetBounds(1, 0, Math.Max(0, pnlItemsFooter.ClientSize.Width - 2), 1); } catch { } };
            lblItemsCount.AutoSize = true; lblItemsCount.Text = "Items: 0"; lblItemsCount.Location = new Point(6, 5);
            pnlItemsFooter.Controls.Add(lblItemsCount);

            gridItems.Dock = DockStyle.Fill; gridItems.AllowUserToAddRows = false; gridItems.RowHeadersVisible = false; gridItems.AutoGenerateColumns = false; gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; gridItems.ScrollBars = ScrollBars.Both;
            ConfigureColumns();
            pnlCenter.Controls.Add(gridItems);
            pnlCenter.Controls.Add(pnlItemsFooter);
            contentPanel.Controls.Add(pnlCenter);

            // Events
            Load += (s, e) => Init(presetSupplierId);
            tsAddLine.Click += (s, e) => AddExistingProduct();
            tsDeleteLine.Click += (s, e) => DeleteCurrentLine();
            tsSave.Click += (s, e) => Save();
            btnAddSupplier.Click += (s, e) => QuickAddSupplier();
            tsSelectSupplier.Click += (s, e) => EnsureSupplierSelectedWithQuickDialog(true);

            // Keyboard shortcuts
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.N || e.KeyCode == Keys.Insert) { e.Handled = true; tsAddLine.PerformClick(); return; }
                if ((e.Control && e.KeyCode == Keys.D) || e.KeyCode == Keys.Delete) { e.Handled = true; tsDeleteLine.PerformClick(); return; }
                if (e.Control && e.KeyCode == Keys.S) { e.Handled = true; tsSave.PerformClick(); return; }
            };

            gridItems.RowsAdded += (s, e) => { try { lblItemsCount.Text = $"Items: {gridItems.Rows.Count}"; UpdateCompactTotals(); } catch { } };
            gridItems.RowsRemoved += (s, e) => { try { lblItemsCount.Text = $"Items: {Math.Max(0, gridItems.Rows.Count)}"; UpdateCompactTotals(); } catch { } };
            gridItems.CellEndEdit += (s, e) => { try { RecalcTotals(); UpdateCompactTotals(); } catch { } };
        }

        private void Init(int? presetSupplierId)
        {
            try
            {
                using var db = new AppDbContext();
                var sups = db.Suppliers.OrderBy(s => s.Name).ToList();
                cmbSupplier.DataSource = new BindingSource(sups, null);
                cmbSupplier.DisplayMember = "Name";
                cmbSupplier.ValueMember = "Id";
                if (presetSupplierId.HasValue)
                {
                    cmbSupplier.SelectedValue = presetSupplierId.Value;
                }
            }
            catch { }

            bs.DataSource = new System.Collections.Generic.List<Line>();
            gridItems.DataSource = bs;
            RecalcTotals();
            UpdateCompactTotals();
        }

        private void ConfigureColumns()
        {
            gridItems.Columns.Clear();
            var colSerial = new DataGridViewTextBoxColumn { HeaderText = "Sl", Name = "colSerial", ReadOnly = true, FillWeight = 6, MinimumWidth = 40 };
            var colScheme = new DataGridViewTextBoxColumn { HeaderText = "Scheme", Name = "colScheme", ReadOnly = false, FillWeight = 10, MinimumWidth = 80 };
            var colName = new DataGridViewTextBoxColumn { HeaderText = "Name", Name = "colName", DataPropertyName = nameof(Line.ProductName), ReadOnly = false, FillWeight = 24, MinimumWidth = 200 };
            var colPack = new DataGridViewTextBoxColumn { HeaderText = "Pack", Name = "colPack", DataPropertyName = nameof(Line.Pack), ReadOnly = false, FillWeight = 8, MinimumWidth = 70 };
            var colMkt = new DataGridViewTextBoxColumn { HeaderText = "Mkt.", Name = "colMkt", DataPropertyName = nameof(Line.MarketedBy), ReadOnly = false, FillWeight = 10, MinimumWidth = 90 };
            var colBatch = new DataGridViewTextBoxColumn { HeaderText = "Batch No.", Name = "colBatch", DataPropertyName = nameof(Line.Batch), ReadOnly = false, FillWeight = 12, MinimumWidth = 100 };
            var colExpiry = new DataGridViewTextBoxColumn { HeaderText = "Expiry", Name = "colExpiry", DataPropertyName = nameof(Line.Expiry), ReadOnly = false, FillWeight = 10, MinimumWidth = 90 };
            var colMrp = new DataGridViewTextBoxColumn { HeaderText = "MRP", Name = "colMrp", DataPropertyName = nameof(Line.Mrp), ReadOnly = false, FillWeight = 10, MinimumWidth = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, NullValue = "" } };
            var colCp = new DataGridViewTextBoxColumn { HeaderText = "CP", Name = "colCp", DataPropertyName = nameof(Line.Cp), FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
            var colSp = new DataGridViewTextBoxColumn { HeaderText = "SP", Name = "colSp", DataPropertyName = nameof(Line.Sp), FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
            var colQty = new DataGridViewTextBoxColumn { HeaderText = "Quantity", Name = "colQty", DataPropertyName = nameof(Line.Qty), FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } };
            var colAmount = new DataGridViewTextBoxColumn { HeaderText = "Amount (CP)", Name = "colAmount", DataPropertyName = nameof(Line.LineTotal), ReadOnly = true, FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
            gridItems.Columns.Add(colSerial);
            gridItems.Columns.Add(colScheme);
            gridItems.Columns.Add(colName);
            gridItems.Columns.Add(colPack);
            gridItems.Columns.Add(colMkt);
            gridItems.Columns.Add(colBatch);
            gridItems.Columns.Add(colExpiry);
            gridItems.Columns.Add(colMrp);
            gridItems.Columns.Add(colCp);
            gridItems.Columns.Add(colSp);
            gridItems.Columns.Add(colQty);
            gridItems.Columns.Add(colAmount);

            gridItems.CellFormatting += (s, e) =>
            {
                try
                {
                    if (e.RowIndex < 0) return; if (e.ColumnIndex < 0) return; if (e.ColumnIndex >= gridItems.Columns.Count) return; if (e.RowIndex >= gridItems.Rows.Count) return;
                    var row = gridItems.Rows[e.RowIndex]; if (row == null) return;
                    var it = row.DataBoundItem as Line; if (it == null) return;
                    var colNameSafe = gridItems.Columns[e.ColumnIndex].Name;
                    if (colNameSafe == "colSerial") { e.Value = (e.RowIndex + 1).ToString(); e.FormattingApplied = true; }
                }
                catch { }
            };
        }

        private void AddExistingProduct()
        {
            var term = Microsoft.VisualBasic.Interaction.InputBox("Enter product name to add:", "Add Product", "");
            if (string.IsNullOrWhiteSpace(term)) return;
            using var db = new AppDbContext();
            var p = db.Products.Where(x => x.Name.ToLower().Contains(term.Trim().ToLower())).OrderBy(x => x.Name).FirstOrDefault();
            if (p == null) { MessageBox.Show("No matching product found. Use Add Blank from context."); return; }
            (bs.DataSource as System.Collections.Generic.List<Line>)!.Add(new Line { ProductId = p.Id, ProductName = p.Name, Qty = 1 });
            bs.ResetBindings(false); RecalcTotals(); UpdateCompactTotals();
        }

        private void DeleteCurrentLine()
        {
            try
            {
                if (gridItems.CurrentRow?.DataBoundItem is Line l)
                {
                    var list = (System.Collections.Generic.List<Line>)bs.DataSource;
                    list.Remove(l);
                    bs.ResetBindings(false);
                }
            }
            catch { }
        }

        private void QuickAddSupplier()
        {
            try
            {
                var name = Microsoft.VisualBasic.Interaction.InputBox("Supplier name:", "Add Supplier", "").Trim();
                if (string.IsNullOrWhiteSpace(name)) return;
                using var db = new AppDbContext();
                var ex = db.Suppliers.FirstOrDefault(s => s.Name.ToLower() == name.ToLower());
                if (ex == null)
                {
                    var s = new Supplier { Name = name, CreatedAt = DateTime.UtcNow };
                    db.Suppliers.Add(s); db.SaveChanges();
                    var sups = db.Suppliers.OrderBy(x => x.Name).ToList();
                    cmbSupplier.DataSource = new BindingSource(sups, null);
                    cmbSupplier.DisplayMember = "Name"; cmbSupplier.ValueMember = "Id"; cmbSupplier.SelectedValue = s.Id;
                }
                else
                {
                    var sups = db.Suppliers.OrderBy(x => x.Name).ToList();
                    cmbSupplier.DataSource = new BindingSource(sups, null);
                    cmbSupplier.DisplayMember = "Name"; cmbSupplier.ValueMember = "Id"; cmbSupplier.SelectedValue = ex.Id;
                }
            }
            catch { }
        }

        private void EnsureSupplierSelectedWithQuickDialog(bool forcePrompt = false)
        {
            try
            {
                if (!forcePrompt && cmbSupplier.SelectedValue is int) return;
                var name = Microsoft.VisualBasic.Interaction.InputBox("Select or add supplier (name):", "Supplier", "");
                if (string.IsNullOrWhiteSpace(name)) return;
                using var db = new AppDbContext();
                var existing = db.Suppliers.FirstOrDefault(s => s.Name.ToLower() == name.ToLower());
                int id;
                if (existing == null)
                {
                    var s = new Supplier { Name = name.Trim(), CreatedAt = DateTime.UtcNow };
                    db.Suppliers.Add(s); db.SaveChanges(); id = s.Id;
                }
                else id = existing.Id;
                var sups = db.Suppliers.OrderBy(s => s.Name).ToList();
                cmbSupplier.DataSource = new BindingSource(sups, null);
                cmbSupplier.DisplayMember = "Name"; cmbSupplier.ValueMember = "Id"; cmbSupplier.SelectedValue = id;
            }
            catch { }
        }

        private DateTime? ParseMonthYear(string? t)
        {
            if (string.IsNullOrWhiteSpace(t)) return null;
            t = t.Trim(); if (!t.Contains('/')) return null; var parts = t.Split('/');
            if (parts.Length != 2) return null; if (!int.TryParse(parts[0], out var mm)) return null; if (!int.TryParse(parts[1], out var yy)) return null;
            if (yy < 100) yy += 2000; if (mm < 1 || mm > 12) return null; return new DateTime(yy, mm, 1);
        }

        private void RecalcTotals()
        {
            try
            {
                var lines = (bs.DataSource as System.Collections.Generic.List<Line>)!;
                var sub = lines.Sum(l => l.LineTotal);
                lblRightCompact.Text = $"Subtotal: {sub:0.00}    Total: {sub:0.00}";
            }
            catch { }
        }

        private void UpdateCompactTotals()
        {
            try
            {
                var lines = (bs.DataSource as System.Collections.Generic.List<Line>)!;
                var items = lines.Count;
                var qty = lines.Sum(l => l.Qty);
                lblLeftCompact.Text = $"Items: {items}   Qty: {qty:0.##}";
                var sub = lines.Sum(l => l.LineTotal);
                lblRightCompact.Text = $"Subtotal: {sub:0.00}    Total: {sub:0.00}";
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                if (cmbSupplier.SelectedValue is not int supplierId)
                {
                    MessageBox.Show("Select a supplier."); return;
                }
                var lines = (bs.DataSource as System.Collections.Generic.List<Line>)!;
                if (lines.Count == 0) { MessageBox.Show("Add at least one product line."); return; }

                using var db = new AppDbContext();
                var purchase = new Purchase
                {
                    SupplierId = supplierId,
                    PurchaseDate = dtPurchase.Value.Date,
                    InvoiceNumber = string.IsNullOrWhiteSpace(txtInvoiceNo.Text) ? null : txtInvoiceNo.Text.Trim(),
                    Subtotal = lines.Sum(l => l.LineTotal),
                    Total = lines.Sum(l => l.LineTotal),
                    Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim()
                };
                db.Purchases.Add(purchase);
                db.SaveChanges();

                foreach (var l in lines)
                {
                    int productId;
                    Product product;
                    if (l.ProductId.HasValue)
                    {
                        product = db.Products.First(p => p.Id == l.ProductId.Value);
                        product.UpdatedAt = DateTime.UtcNow;
                        productId = product.Id;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(l.ProductName)) continue;
                        product = new Product { Name = l.ProductName.Trim(), Barcode = l.ProductName.Trim(), Hsn = l.ProductName.Trim(), CreatedAt = DateTime.UtcNow };
                        db.Products.Add(product); db.SaveChanges();
                        productId = product.Id;
                    }

                    var bn = string.IsNullOrWhiteSpace(l.Batch) ? "New" : l.Batch.Trim();
                    var batch = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == productId && b.BatchNumber == bn);
                    if (batch == null)
                    {
                        batch = new ProductBatch
                        {
                            ProductIdRef = productId,
                            BatchNumber = bn,
                            Expiry = ParseMonthYear(l.Expiry),
                            Mrp = l.Mrp,
                            CostPrice = l.Cp,
                            SellingPrice = l.Sp,
                            Stock = l.Qty,
                            Pack = string.IsNullOrWhiteSpace(l.Pack) ? null : l.Pack.Trim(),
                            Bonus = string.IsNullOrWhiteSpace(l.Bonus) ? null : l.Bonus.Trim(),
                            MarketedBy = string.IsNullOrWhiteSpace(l.MarketedBy) ? null : l.MarketedBy.Trim(),
                            Hsn = product.Hsn,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.ProductBatches.Add(batch);
                    }
                    else
                    {
                        batch.Expiry = ParseMonthYear(l.Expiry);
                        batch.Mrp = l.Mrp;
                        batch.CostPrice = l.Cp;
                        batch.SellingPrice = l.Sp;
                        batch.Pack = string.IsNullOrWhiteSpace(l.Pack) ? null : l.Pack.Trim();
                        batch.Bonus = string.IsNullOrWhiteSpace(l.Bonus) ? null : l.Bonus.Trim();
                        batch.MarketedBy = string.IsNullOrWhiteSpace(l.MarketedBy) ? null : l.MarketedBy.Trim();
                        batch.Stock = (batch.Stock ?? 0m) + l.Qty;
                        batch.UpdatedAt = DateTime.UtcNow;
                    }
                    db.SaveChanges();

                    db.PurchaseItems.Add(new PurchaseItem
                    {
                        PurchaseId = purchase.Id,
                        ProductId = productId,
                        ProductName = product.Name,
                        BatchNumber = bn,
                        Expiry = ParseMonthYear(l.Expiry),
                        Mrp = l.Mrp,
                        CostPrice = l.Cp,
                        SellingPrice = l.Sp,
                        Quantity = l.Qty,
                        Pack = string.IsNullOrWhiteSpace(l.Pack) ? null : l.Pack.Trim(),
                        Bonus = string.IsNullOrWhiteSpace(l.Bonus) ? null : l.Bonus.Trim(),
                        MarketedBy = string.IsNullOrWhiteSpace(l.MarketedBy) ? null : l.MarketedBy.Trim(),
                        LineTotal = l.LineTotal
                    });
                    db.SaveChanges();

                    var batches = db.ProductBatches.Where(b => b.ProductIdRef == productId).OrderByDescending(b => (b.Stock ?? 0)).ToList();
                    if (batches.Count > 3)
                    {
                        foreach (var b in batches.Where(b => (b.Stock ?? 0m) == 0m).ToList())
                            db.ProductBatches.Remove(b);
                        db.SaveChanges();
                    }
                }

                var sup = db.Suppliers.First(s => s.Id == supplierId);
                sup.LastPurchaseAt = DateTime.UtcNow;
                sup.BackDues = (sup.BackDues ?? 0m) + (purchase.Total ?? 0m);
                sup.UpdatedAt = DateTime.UtcNow;
                db.SaveChanges();

                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save purchase: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
