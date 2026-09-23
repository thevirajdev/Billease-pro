using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Data;
using BillingSuite.App.Services;
using System.Collections.Generic;

namespace BillingSuite.App.Forms
{
    public class ProductEditForm : Form
    {
        // ------------------ Product Identity ------------------
        private readonly TextBox txtBarcode = new TextBox();
        private readonly TextBox txtName = new TextBox();
        private readonly TextBox txtDescription = new TextBox();
        private readonly TextBox txtHsn = new TextBox();
        private readonly TextBox txtCategory = new TextBox();
        private readonly TextBox txtAlert = new TextBox();
        private readonly TextBox txtLowStock = new TextBox();
        private readonly TextBox txtMarketedBy = new TextBox();

        // ------------------ Data Grid ------------------
        private DataGridView gridBatches = new DataGridView();
        private Button btnBatchAdd = new Button();
        private Button btnBatchEdit = new Button();
        private Button btnBatchDelete = new Button();

        // ------------------ Form Actions ------------------
        private readonly Button btnOk = new Button();
        private readonly Button btnCancel = new Button();

        private readonly List<ProductBatch> _pendingBatches = new List<ProductBatch>();
        public Product Model { get; private set; }
        private readonly int? _batchId;

        public ProductEditForm(Product? model = null, ProductBatch? batch = null)
        {
            Text = model == null ? "Add Product" : "Edit Product";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(950, 800);
            MinimumSize = new Size(800, 650);
            FormBorderStyle = FormBorderStyle.Sizable;
            AutoScroll = true;
            KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.W) { e.Handled = true; Close(); } };

            Model = model ?? new Product();
            _batchId = batch?.Id;

            // Migrate any legacy data to ProductBatches table immediately
            MigrateLegacyData();

            // Build beautiful UI securely using standard containers
            BuildUI();

            // Bind data to the layout
            BindData();
            LoadBatches();
        }

        private void MigrateLegacyData()
        {
            if (Model.Id == 0) return;
            if (string.IsNullOrWhiteSpace(Model.OldBatch) && string.IsNullOrWhiteSpace(Model.NewBatch) && string.IsNullOrWhiteSpace(Model.VeryOldBatch)) return;

            try
            {
                using var db = new AppDbContext();
                var p = db.Products.FirstOrDefault(x => x.Id == Model.Id);
                if (p == null) return;

                void AddIfSet(string? b, decimal? m, DateTime? e, decimal? c, decimal? s, decimal? st)
                {
                    if (string.IsNullOrWhiteSpace(b)) return;
                    // Check if already exists to avoid duplicates
                    if (db.ProductBatches.Any(x => x.ProductIdRef == p.Id && x.BatchNumber == b)) return;
                    
                    db.ProductBatches.Add(new ProductBatch {
                        ProductIdRef = p.Id, BatchNumber = b, Mrp = m, Expiry = e, CostPrice = c, SellingPrice = s, Stock = (decimal?)st, 
                        Hsn = p.Hsn, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    });
                }

                AddIfSet(p.OldBatch, p.OldMrp, p.OldExpiry, p.OldCostPrice, p.OldSellingPrice, p.OldStock);
                AddIfSet(p.NewBatch, p.NewMrp, p.NewExpiry, p.NewCostPrice, p.NewSellingPrice, p.NewStock);
                AddIfSet(p.VeryOldBatch, p.VeryOldMrp, p.VeryOldExpiry, p.VeryOldCostPrice, p.VeryOldSellingPrice, p.VeryOldStock);

                // Clear legacy fields
                p.OldBatch = null; p.OldMrp = null; p.OldExpiry = null; p.OldCostPrice = null; p.OldSellingPrice = null; p.OldStock = null;
                p.NewBatch = null; p.NewMrp = null; p.NewExpiry = null; p.NewCostPrice = null; p.NewSellingPrice = null; p.NewStock = null;
                p.VeryOldBatch = null; p.VeryOldMrp = null; p.VeryOldExpiry = null; p.VeryOldCostPrice = null; p.VeryOldSellingPrice = null; p.VeryOldStock = null;
                
                db.SaveChanges();
                // Update local model too
                Model = p;
            }
            catch { }
        }

        private void BuildUI()
        {
            this.Controls.Clear();
            this.BackColor = Color.FromArgb(245, 246, 250);

            TableLayoutPanel tlpMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(15)
            };
            tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Section 1: Product Details GroupBox
            GroupBox gbDetails = CreateModernGroupBox("Identity & Categorization");
            gbDetails.Controls.Add(CreateDetailsPanel());
            tlpMain.Controls.Add(gbDetails, 0, 0);

            // Section 2: Alerts & Inventory Rules
            GroupBox gbAlerts = CreateModernGroupBox("Inventory Thresholds & Alerts");
            gbAlerts.Controls.Add(CreateAlertsPanel());
            tlpMain.Controls.Add(gbAlerts, 0, 1);

            // Section 3: Batches Grid
            GroupBox gbBatchesGrid = CreateModernGroupBox("Active Batches");
            gbBatchesGrid.Controls.Add(CreateGridPanel());
            tlpMain.Controls.Add(gbBatchesGrid, 0, 2);

            // Section 5: Buttons
            FlowLayoutPanel flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 50,
                Padding = new Padding(0, 10, 0, 0)
            };
            btnCancel.Text = "Cancel"; btnCancel.Size = new Size(100, 35); btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Cursor = Cursors.Hand; btnCancel.BackColor = Color.White; btnCancel.FlatStyle = FlatStyle.Flat;

            btnOk.Text = "Save Product"; btnOk.Size = new Size(130, 35); btnOk.Click += BtnOk_Click;
            btnOk.Cursor = Cursors.Hand; btnOk.BackColor = Color.FromArgb(0, 122, 204); btnOk.ForeColor = Color.White; btnOk.FlatStyle = FlatStyle.Flat;

            flpButtons.Controls.Add(btnCancel);
            flpButtons.Controls.Add(btnOk);
            tlpMain.Controls.Add(flpButtons, 0, 4);

            // Assign Row Styles dynamically
            tlpMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Details
            tlpMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Alerts
            tlpMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Batches Grid (Flex)
            tlpMain.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

            this.Controls.Add(tlpMain);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private GroupBox CreateModernGroupBox(string text)
        {
            return new GroupBox
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(15),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 15)
            };
        }

        private TableLayoutPanel CreateDetailsPanel()
        {
            TableLayoutPanel p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true };
            p.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            txtDescription.Multiline = true; txtDescription.Height = 55;

            AddRowLayout(p, 0, "SKU / Barcode", txtBarcode, "Name", txtName);
            AddRowLayout(p, 1, "Category", txtCategory, "HSN", txtHsn);
            AddRowLayout(p, 2, "Marketed By", txtMarketedBy, "", new Label());
            
            p.Controls.Add(new Label { Text = "Description", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            p.Controls.Add(txtDescription, 1, 3);
            p.SetColumnSpan(txtDescription, 3);
            txtDescription.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            return p;
        }

        private TableLayoutPanel CreateAlertsPanel()
        {
            TableLayoutPanel p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true };
            p.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            AddRowLayout(p, 0, "Expiry Alert (Days)", txtAlert, "Low Stock Threshold", txtLowStock);
            return p;
        }

        private TableLayoutPanel CreateGridPanel()
        {
            TableLayoutPanel p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            p.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 0, 0, 10) };
            
            btnBatchAdd.Text = "Add Batch"; btnBatchAdd.Size = new Size(100, 30); btnBatchAdd.BackColor = Color.White;
            btnBatchEdit.Text = "Edit Batch"; btnBatchEdit.Size = new Size(100, 30); btnBatchEdit.BackColor = Color.White;
            btnBatchDelete.Text = "Delete Batch"; btnBatchDelete.Size = new Size(100, 30); btnBatchDelete.BackColor = Color.White;
            
            toolbar.Controls.Add(btnBatchAdd); toolbar.Controls.Add(btnBatchEdit); toolbar.Controls.Add(btnBatchDelete);
            
            gridBatches = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                MinimumSize = new Size(0, 150),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            try { typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(gridBatches, true, null); } catch { }
            gridBatches.CellFormatting += GridBatches_CellFormatting;

            p.Controls.Add(toolbar, 0, 0);
            p.Controls.Add(gridBatches, 0, 1);
            return p;
        }

        private void AddRowLayout(TableLayoutPanel p, int row, string l1, Control c1, string l2, Control c2)
        {
            p.Controls.Add(new Label { Text = l1, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 5) }, 0, row);
            c1.Anchor = AnchorStyles.Left | AnchorStyles.Right; p.Controls.Add(c1, 1, row);
            
            p.Controls.Add(new Label { Text = l2, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 5, 0, 5) }, 2, row);
            c2.Anchor = AnchorStyles.Left | AnchorStyles.Right; p.Controls.Add(c2, 3, row);
        }

        private void BindData()
        {
            txtBarcode.Text = Model.Barcode;
            txtName.Text = Model.Name;
            txtDescription.Text = Model.Description;
            txtHsn.Text = Model.Hsn;
            txtCategory.Text = Model.Category;
            txtAlert.Text = (Model.ExpiryAlertDays ?? BillingSuite.App.Services.AppSettingsService.DefaultExpiryAlertDays).ToString();
            txtLowStock.Text = (Model.LowStockThreshold ?? 0m).ToString();
            txtMarketedBy.Text = Model.MarketedBy ?? string.Empty;

            try
            {
                using var dbCat = new AppDbContext();
                var cats = dbCat.Products.Where(p => p.Category != null && p.Category != "").Select(p => p.Category!).Distinct().OrderBy(s => s).ToArray();
                txtCategory.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtCategory.AutoCompleteSource = AutoCompleteSource.CustomSource;
                var src = new AutoCompleteStringCollection(); src.AddRange(cats);
                txtCategory.AutoCompleteCustomSource = src;

                var mkts = dbCat.Products.Where(p => p.MarketedBy != null && p.MarketedBy != "").Select(p => p.MarketedBy!).Distinct()
                    .Union(dbCat.ProductBatches.Where(b => b.MarketedBy != null && b.MarketedBy != "").Select(b => b.MarketedBy!))
                    .Distinct().OrderBy(s => s).ToArray();
                txtMarketedBy.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtMarketedBy.AutoCompleteSource = AutoCompleteSource.CustomSource;
                var mktSrc = new AutoCompleteStringCollection(); mktSrc.AddRange(mkts);
                txtMarketedBy.AutoCompleteCustomSource = mktSrc;
            } catch { }

            void NumericOnly(object? s, KeyPressEventArgs e)
            {
                if (char.IsControl(e.KeyChar)) return;
                var tb = s as TextBox; if (tb == null) return;
                if (e.KeyChar == '.' && tb.Text.Contains('.')) { e.Handled = true; return; }
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.') e.Handled = true;
            }

            txtAlert.KeyPress += NumericOnly; txtLowStock.KeyPress += NumericOnly;

            btnBatchAdd.Click += (s, e) => TryAddBatch();
            btnBatchEdit.Click += (s, e) => TryEditBatch();
            btnBatchDelete.Click += (s, e) => TryDeleteBatch();

            BuildBatchGridColumns();
        }


        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("Name is required."); return; }
            if (string.IsNullOrWhiteSpace(txtBarcode.Text)) { MessageBox.Show("SKU / Barcode is required."); return; }

            Model.Barcode = txtBarcode.Text.Trim(); Model.Hsn = Model.Barcode;
            Model.Name = txtName.Text.Trim(); Model.Description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim();
            Model.Category = string.IsNullOrWhiteSpace(txtCategory.Text) ? null : txtCategory.Text.Trim();
            if (int.TryParse(txtAlert.Text, out var days)) Model.ExpiryAlertDays = days; else Model.ExpiryAlertDays = null;
            if (decimal.TryParse(txtLowStock.Text, out var low)) Model.LowStockThreshold = low; else Model.LowStockThreshold = null;

            using (var db = new AppDbContext())
            {
                Model.MarketedBy = txtMarketedBy.Text.Trim();
                if (Model.Id == 0) db.Products.Add(Model);
                else
                {
                    var existing = db.Products.FirstOrDefault(p => p.Id == Model.Id);
                    if (existing != null)
                    {
                        var today = DateTime.Today;
                        existing.Barcode = Model.Barcode; existing.Name = Model.Name; existing.Description = Model.Description; existing.Category = Model.Category; existing.Hsn = Model.Hsn; 
                        existing.ExpiryAlertDays = Model.ExpiryAlertDays; existing.LowStockThreshold = Model.LowStockThreshold;
                        existing.MarketedBy = txtMarketedBy.Text.Trim();
                        existing.UpdatedAt = DateTime.UtcNow;

                        // Sync MarketedBy to all batches and check EXPIRY logic via Service
                        var mktValue = txtMarketedBy.Text.Trim();
                        if (!string.IsNullOrWhiteSpace(mktValue))
                        {
                            InventoryService.SyncProductMetadata(db, existing.Id, mktValue);
                        }
                        InventoryService.SyncExpiredStock(db);
                    }
                    else db.Products.Add(Model);
                }
                db.SaveChanges();

                if (_pendingBatches.Count > 0)
                {
                    var today = DateTime.Today;
                    var mkt = txtMarketedBy.Text.Trim();
                    foreach (var pb in _pendingBatches)
                    {
                        var newBatch = new ProductBatch { 
                            ProductIdRef = Model.Id, 
                            BatchNumber = pb.BatchNumber, 
                            Expiry = pb.Expiry, 
                            Mrp = pb.Mrp, 
                            CostPrice = pb.CostPrice, 
                            SellingPrice = pb.SellingPrice, 
                            Stock = pb.Stock, 
                            Pack = pb.Pack, 
                            MarketedBy = mkt, 
                            Bonus = pb.Bonus, 
                            Hsn = Model.Hsn, 
                            UpdatedAt = DateTime.UtcNow 
                        };

                        // AUTO-EXPIRY ZERO STOCK LOGIC for new pending batches
                        if (newBatch.Expiry.HasValue && newBatch.Expiry.Value < today)
                        {
                            if ((newBatch.Stock ?? 0) > 0)
                            {
                                newBatch.ExpiredStock = newBatch.Stock;
                                newBatch.Stock = 0;
                            }
                        }
                        db.ProductBatches.Add(newBatch);
                    }
                    db.SaveChanges();
                    
                    // Final sync for MarketedBy to ensure consistency across all batches
                    var finalMkt = txtMarketedBy.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(finalMkt))
                    {
                        InventoryService.SyncProductMetadata(db, Model.Id, finalMkt);
                    }
                    _pendingBatches.Clear();
                }
            }
            DialogResult = DialogResult.OK; Close();
        }

        private void BuildBatchGridColumns()
        {
            try
            {
                gridBatches.Columns.Clear();
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", Name = "Id", Visible = false });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BatchNumber", HeaderText = "Batch", Name = "BatchNumber", Width = 120 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Expiry", HeaderText = "Expiry", DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/yyyy" }, Width = 90 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Mrp", HeaderText = "MRP", DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }, Width = 80 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SellingPrice", HeaderText = "Price", DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }, Width = 80 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CostPrice", HeaderText = "Cost", DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }, Width = 80 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Margin", HeaderText = "Margin", Name = "Margin", DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }, Width = 80 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "MarginPct", HeaderText = "Margin %", DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }, Width = 80 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CpPct", HeaderText = "CP %", DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }, Width = 70 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Stock", HeaderText = "Stock", DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }, Width = 70 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Pack", HeaderText = "Pack", Width = 90 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "MarketedBy", HeaderText = "Mkt", Width = 140 });
                gridBatches.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Bonus", HeaderText = "Scheme", Width = 90 });
            } catch { }
        }

        private void LoadBatches()
        {
            try
            {
                using var db = new AppDbContext();
                if (Model.Id == 0)
                {
                    var listPending = _pendingBatches.OrderByDescending(b => b.UpdatedAt ?? DateTime.UtcNow).ThenByDescending(b => b.Id)
                        .Select(b => new {
                            Id = b.Id != 0 ? b.Id : -(_pendingBatches.IndexOf(b) + 1),
                            b.BatchNumber, b.Expiry, b.Mrp, b.SellingPrice, b.CostPrice,
                            Margin = (decimal?)((b.SellingPrice ?? 0m) - (b.CostPrice ?? 0m)),
                            MarginPct = (b.SellingPrice != null && b.SellingPrice > 0m) ? (decimal?)(((b.SellingPrice - b.CostPrice) / b.SellingPrice) * 100m) : null,
                            CpPct = (b.SellingPrice != null && b.SellingPrice > 0m) ? (decimal?)(((b.CostPrice ?? 0m) / b.SellingPrice.Value) * 100m) : null,
                            b.Stock, b.Pack, b.MarketedBy, b.Bonus
                        }).ToList();
                    var bsPending = new BindingSource(); bsPending.DataSource = listPending; gridBatches.DataSource = bsPending;
                    btnBatchAdd.Enabled = true; btnBatchEdit.Enabled = _pendingBatches.Count > 0; btnBatchDelete.Enabled = _pendingBatches.Count > 0;
                    return;
                }
                btnBatchAdd.Enabled = true; btnBatchEdit.Enabled = true; btnBatchDelete.Enabled = true;
                var list = db.ProductBatches.Where(b => b.ProductIdRef == Model.Id)
                    .OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id)
                    .Select(b => new {
                        b.Id, b.BatchNumber, b.Expiry, b.Mrp, b.SellingPrice, b.CostPrice,
                        Margin = (decimal?)((b.SellingPrice ?? 0m) - (b.CostPrice ?? 0m)),
                        MarginPct = (b.SellingPrice != null && b.SellingPrice > 0m) ? (decimal?)(((b.SellingPrice - b.CostPrice) / b.SellingPrice) * 100m) : null,
                        CpPct = (b.SellingPrice != null && b.SellingPrice > 0m) ? (decimal?)(((b.CostPrice ?? 0m) / b.SellingPrice.Value) * 100m) : null,
                        b.Stock, b.Pack, b.MarketedBy, b.Bonus
                    }).ToList();
                var bs = new BindingSource(); bs.DataSource = list; gridBatches.DataSource = bs;
            } catch { gridBatches.DataSource = null; }
        }

        private void GridBatches_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            try { if (gridBatches.Columns[e.ColumnIndex].Name == "Margin" && e.Value is decimal dv) e.CellStyle.ForeColor = dv >= 0 ? Color.ForestGreen : Color.Firebrick; } catch { }
        }

        private int? GetSelectedBatchId() { try { if (gridBatches.CurrentRow?.DataBoundItem == null) return null; var bound = gridBatches.CurrentRow.DataBoundItem; return (int?)bound.GetType().GetProperty("Id")?.GetValue(bound); } catch { return null; } }

        private void TryAddBatch()
        {
            try
            {
                using var dlg = new BatchDialog();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (Model.Id == 0)
                    {
                        if (_pendingBatches.Any(x => string.Equals(x.BatchNumber, dlg.BatchNumber, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("Batch already exists."); return; }
                        var newPending = new ProductBatch { 
                            Id = -(_pendingBatches.Count + 1), 
                            ProductIdRef = 0, 
                            BatchNumber = dlg.BatchNumber, 
                            Expiry = DateUtils.ParseExpiryDate(dlg.ExpiryText), 
                            Mrp = dlg.Mrp, 
                            CostPrice = dlg.CostPrice, 
                            SellingPrice = dlg.SellingPrice, 
                            Stock = dlg.Stock, 
                            Pack = dlg.Pack, 
                            Bonus = dlg.Bonus,
                            Rate = dlg.Rate,
                            DiscountPercent = dlg.DiscountPercent,
                            CgstPercent = dlg.CgstPercent,
                            SgstPercent = dlg.SgstPercent,
                            IgstPercent = dlg.IgstPercent,
                            Hsn = Model.Hsn, 
                            MarketedBy = txtMarketedBy.Text.Trim(),
                            UpdatedAt = DateTime.UtcNow 
                        };
                        
                        // AUTO-EXPIRY ZERO STOCK LOGIC
                        if (newPending.Expiry.HasValue && newPending.Expiry.Value < DateTime.Today)
                        {
                            if ((newPending.Stock ?? 0) > 0)
                            {
                                newPending.ExpiredStock = newPending.Stock;
                                newPending.Stock = 0;
                            }
                        }
                        _pendingBatches.Add(newPending);

                        LoadBatches();
                    }
                    else
                    {
                        using var db = new AppDbContext(); var newCode = (dlg.BatchNumber ?? "").Trim();
                        if (db.ProductBatches.Any(b => b.ProductIdRef == Model.Id && b.BatchNumber != null && b.BatchNumber.Trim().ToLower() == newCode.ToLower())) { MessageBox.Show("Batch already exists."); return; }
                        var newB = new ProductBatch { 
                            ProductIdRef = Model.Id, 
                            BatchNumber = dlg.BatchNumber, 
                            Expiry = DateUtils.ParseExpiryDate(dlg.ExpiryText), 
                            Mrp = dlg.Mrp, 
                            CostPrice = dlg.CostPrice, 
                            SellingPrice = dlg.SellingPrice, 
                            Stock = dlg.Stock, 
                            Pack = dlg.Pack, 
                            Bonus = dlg.Bonus,
                            Rate = dlg.Rate,
                            DiscountPercent = dlg.DiscountPercent,
                            CgstPercent = dlg.CgstPercent,
                            SgstPercent = dlg.SgstPercent,
                            IgstPercent = dlg.IgstPercent,
                            Hsn = Model.Hsn, 
                            MarketedBy = txtMarketedBy.Text.Trim(),
                            UpdatedAt = DateTime.UtcNow 
                        };

                        // AUTO-EXPIRY ZERO STOCK LOGIC
                        if (newB.Expiry.HasValue && newB.Expiry.Value < DateTime.Today)
                        {
                            if ((newB.Stock ?? 0) > 0)
                            {
                                newB.ExpiredStock = newB.Stock;
                                newB.Stock = 0;
                            }
                        }
                        db.ProductBatches.Add(newB);

                        db.SaveChanges(); LoadBatches();
                    }
                }
            } catch { }
        }

        private void TryEditBatch()
        {
            try
            {
                var id = GetSelectedBatchId(); if (id == null) { MessageBox.Show("Select a batch."); return; }
                if (Model.Id == 0 || id < 0)
                {
                    var pb = _pendingBatches.FirstOrDefault(x => x.Id == id || -(_pendingBatches.IndexOf(x) + 1) == id); if (pb == null) return;
                    using var dlg = new BatchDialog(new BatchDialog.Model { 
                        BatchNumber = pb.BatchNumber, 
                        ExpiryText = pb.Expiry?.ToString("MM/yyyy"), 
                        Mrp = pb.Mrp, 
                        CostPrice = pb.CostPrice, 
                        SellingPrice = pb.SellingPrice, 
                        Stock = pb.Stock, 
                        Pack = pb.Pack, 
                        Bonus = pb.Bonus,
                        Rate = pb.Rate,
                        DiscountPercent = pb.DiscountPercent,
                        CgstPercent = pb.CgstPercent,
                        SgstPercent = pb.SgstPercent,
                        IgstPercent = pb.IgstPercent
                    });
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (!string.Equals(pb.BatchNumber, dlg.BatchNumber, StringComparison.OrdinalIgnoreCase) && _pendingBatches.Any(x => x != pb && string.Equals(x.BatchNumber, dlg.BatchNumber, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("Duplicate batch."); return; }
                        pb.BatchNumber = dlg.BatchNumber; 
                        pb.Expiry = DateUtils.ParseExpiryDate(dlg.ExpiryText); 
                        pb.Mrp = dlg.Mrp; 
                        pb.CostPrice = dlg.CostPrice; 
                        pb.SellingPrice = dlg.SellingPrice; 
                        pb.Stock = dlg.Stock; 
                        pb.Pack = dlg.Pack; 
                        pb.Bonus = dlg.Bonus; 
                        pb.Rate = dlg.Rate;
                        pb.DiscountPercent = dlg.DiscountPercent;
                        pb.CgstPercent = dlg.CgstPercent;
                        pb.SgstPercent = dlg.SgstPercent;
                        pb.IgstPercent = dlg.IgstPercent;
                        
                        // AUTO-EXPIRY ZERO STOCK LOGIC
                        if (pb.Expiry.HasValue && pb.Expiry.Value < DateTime.Today)
                        {
                            if ((pb.Stock ?? 0) > 0)
                            {
                                pb.ExpiredStock = (pb.ExpiredStock ?? 0) + pb.Stock;
                                pb.Stock = 0;
                            }
                        }

                        pb.UpdatedAt = DateTime.UtcNow; LoadBatches();
                    }
                }
                else
                {
                    using var db = new AppDbContext(); var b = db.ProductBatches.FirstOrDefault(x => x.Id == id.Value); if (b == null) return;
                    using var dlg = new BatchDialog(new BatchDialog.Model { 
                        BatchNumber = b.BatchNumber, 
                        ExpiryText = b.Expiry?.ToString("MM/yyyy"), 
                        Mrp = b.Mrp, 
                        CostPrice = b.CostPrice, 
                        SellingPrice = b.SellingPrice, 
                        Stock = b.Stock, 
                        Pack = b.Pack, 
                        Bonus = b.Bonus,
                        Rate = b.Rate,
                        DiscountPercent = b.DiscountPercent,
                        CgstPercent = b.CgstPercent,
                        SgstPercent = b.SgstPercent,
                        IgstPercent = b.IgstPercent
                    });
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var newCode = (dlg.BatchNumber ?? "").Trim();
                        if (!string.Equals((b.BatchNumber ?? "").Trim(), newCode, StringComparison.OrdinalIgnoreCase) && db.ProductBatches.Any(x => x.ProductIdRef == Model.Id && x.BatchNumber != null && x.BatchNumber.Trim().ToLower() == newCode.ToLower() && x.Id != b.Id)) { MessageBox.Show("Duplicate batch."); return; }
                        b.BatchNumber = newCode; 
                        b.Expiry = DateUtils.ParseExpiryDate(dlg.ExpiryText); 
                        b.Mrp = dlg.Mrp; 
                        b.CostPrice = dlg.CostPrice; 
                        b.SellingPrice = dlg.SellingPrice; 
                        b.Stock = dlg.Stock; 
                        b.Pack = dlg.Pack; 
                        b.Bonus = dlg.Bonus; 
                        b.Rate = dlg.Rate;
                        b.DiscountPercent = dlg.DiscountPercent;
                        b.CgstPercent = dlg.CgstPercent;
                        b.SgstPercent = dlg.SgstPercent;
                        b.IgstPercent = dlg.IgstPercent;

                        // AUTO-EXPIRY ZERO STOCK LOGIC
                        if (b.Expiry.HasValue && b.Expiry.Value < DateTime.Today)
                        {
                            if ((b.Stock ?? 0) > 0)
                            {
                                b.ExpiredStock = (b.ExpiredStock ?? 0) + b.Stock;
                                b.Stock = 0;
                            }
                        }

                        b.UpdatedAt = DateTime.UtcNow; db.SaveChanges(); LoadBatches();
                    }
                }
            } catch { }
        }

        private void TryDeleteBatch()
        {
            try
            {
                var id = GetSelectedBatchId(); if (id == null) { MessageBox.Show("Select a batch."); return; }
                if (MessageBox.Show("Delete selected batch?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                if (Model.Id == 0 || id < 0) { var idx = _pendingBatches.FindIndex(x => x.Id == id || -(_pendingBatches.IndexOf(x) + 1) == id); if (idx >= 0) { _pendingBatches.RemoveAt(idx); LoadBatches(); } }
                else { using var db = new AppDbContext(); var b = db.ProductBatches.FirstOrDefault(x => x.Id == id.Value); if (b != null) { db.ProductBatches.Remove(b); db.SaveChanges(); LoadBatches(); } }
            } catch { }
        }

        private class BatchDialog : Form
        {
            public class Model 
            { 
                public string BatchNumber { get; set; } = string.Empty; 
                public string? ExpiryText { get; set; } 
                public decimal? Mrp { get; set; } 
                public decimal? CostPrice { get; set; } 
                public decimal? SellingPrice { get; set; } 
                public decimal? Stock { get; set; } 
                public string? Pack { get; set; } 
                public string? Bonus { get; set; }
                public decimal? Rate { get; set; }
                public decimal? DiscountPercent { get; set; }
                public decimal? CgstPercent { get; set; }
                public decimal? SgstPercent { get; set; }
                public decimal? IgstPercent { get; set; }
            }
        
            private TextBox txtBatch = new TextBox(); 
            private TextBox txtExpiry = new TextBox(); 
            private TextBox txtMrp = new TextBox(); 
            private TextBox txtCost = new TextBox(); 
            private TextBox txtSell = new TextBox(); 
            private TextBox txtStock = new TextBox(); 
            private TextBox txtPack = new TextBox(); 
            private TextBox txtRate = new TextBox(); 
            private TextBox txtDisc = new TextBox(); 
            private TextBox txtCgst = new TextBox(); 
            private TextBox txtSgst = new TextBox();
            private TextBox txtIgst = new TextBox();
            private TextBox txtBonus = new TextBox();
            private Label lblFeedback = new Label { AutoSize = true, ForeColor = Color.DarkOrange, Font = new Font(DefaultFont.FontFamily, 8.5f, FontStyle.Italic) };
            private Button btnOk = new Button(); private Button btnCancel = new Button();
            private bool _isCalculating = false;
        
            public string BatchNumber => txtBatch.Text.Trim(); 
            public string ExpiryText => txtExpiry.Text ?? ""; 
            public decimal? Mrp => decimal.TryParse(txtMrp.Text, out var v) ? v : (decimal?)null; 
            public decimal? CostPrice => decimal.TryParse(txtCost.Text, out var v) ? v : (decimal?)null; 
            public decimal? SellingPrice => decimal.TryParse(txtSell.Text, out var v) ? v : (decimal?)null; 
            public decimal? Stock => decimal.TryParse(txtStock.Text, out var v) ? v : (decimal?)null; 
            public string? Pack => string.IsNullOrWhiteSpace(txtPack.Text) ? null : txtPack.Text.Trim(); 
            public string? Bonus => string.IsNullOrWhiteSpace(txtBonus.Text) ? null : txtBonus.Text.Trim();
            public decimal? Rate => decimal.TryParse(txtRate.Text, out var v) ? v : (decimal?)null;
            public decimal? DiscountPercent => decimal.TryParse(txtDisc.Text, out var v) ? v : (decimal?)null;
            public decimal? CgstPercent => decimal.TryParse(txtCgst.Text, out var v) ? v : (decimal?)null;
            public decimal? SgstPercent => decimal.TryParse(txtSgst.Text, out var v) ? v : (decimal?)null;
            public decimal? IgstPercent => decimal.TryParse(txtIgst.Text, out var v) ? v : (decimal?)null;
        
            public BatchDialog(Model? model = null)
            {
                this.Text = model == null ? "Add Batch" : "Edit Batch"; 
                this.Size = new Size(580, 500); 
                this.StartPosition = FormStartPosition.CenterParent;
                this.BackColor = Color.White;
                
                TableLayoutPanel tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoScroll = true, Padding = new Padding(15) };
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F)); tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F)); tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                
                txtExpiry.PlaceholderText = "e.g. 12/2025, 10/25";
                txtExpiry.Validating += (s, e) => {
                    if (!string.IsNullOrWhiteSpace(txtExpiry.Text)) {
                        var parsed = DateUtils.ParseExpiryDate(txtExpiry.Text);
                        if (parsed == null) {
                            e.Cancel = true;
                            MessageBox.Show("Please enter a valid expiry date (e.g. MM/YYYY or DD/MM/YYYY).");
                        } else {
                            txtExpiry.Text = parsed.Value.ToString("MM/yyyy");
                        }
                    }
                };
        
                void AddR(int r, string l1, Control c1, string l2, Control c2) 
                { 
                    tlp.Controls.Add(new Label { Text = l1, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r); 
                    c1.Anchor = AnchorStyles.Left | AnchorStyles.Right; tlp.Controls.Add(c1, 1, r); 
                    tlp.Controls.Add(new Label { Text = l2, AutoSize = true, Anchor = AnchorStyles.Left }, 2, r); 
                    c2.Anchor = AnchorStyles.Left | AnchorStyles.Right; tlp.Controls.Add(c2, 3, r); 
                }
                
                AddR(0, "Batch No.", txtBatch, "Expiry", txtExpiry);
                AddR(1, "MRP", txtMrp, "Selling Price", txtSell);
                AddR(2, "Rate", txtRate, "Disc %", txtDisc);
                AddR(3, "CGST %", txtCgst, "SGST %", txtSgst);
                AddR(4, "IGST %", txtIgst, "Scheme", txtBonus);
                AddR(5, "Net Cost", txtCost, "Stock", txtStock);
                AddR(6, "Pack", txtPack, "", new Control { Visible = false });
                
                tlp.Controls.Add(lblFeedback, 1, 7);
                tlp.SetColumnSpan(lblFeedback, 3);
                
                txtRate.Enter += (s, e) => lblFeedback.Text = "* Changing Rate will update Net Cost";
                txtDisc.Enter += (s, e) => lblFeedback.Text = "* Changing Discount will update Net Cost";
                txtCgst.Enter += (s, e) => lblFeedback.Text = "* Changing Taxes will update Net Cost";
                txtSgst.Enter += (s, e) => lblFeedback.Text = "* Changing Taxes will update Net Cost";
                txtIgst.Enter += (s, e) => lblFeedback.Text = "* Changing IGST will update Net Cost";
                txtCost.Enter += (s, e) => lblFeedback.Text = "* Changing Net Cost will update Base Rate";
                
                txtDisc.Text = "0"; txtCgst.Text = "0"; txtSgst.Text = "0"; txtIgst.Text = "0";
        
                if (model != null)
                {
                    txtBatch.Text = model.BatchNumber;
                    txtExpiry.Text = model.ExpiryText;
                    txtMrp.Text = model.Mrp?.ToString();
                    txtCost.Text = model.CostPrice?.ToString();
                    txtSell.Text = model.SellingPrice?.ToString();
                    txtStock.Text = model.Stock?.ToString();
                    txtPack.Text = model.Pack;
                    txtBonus.Text = model.Bonus;
                    txtRate.Text = model.Rate?.ToString();
                    txtDisc.Text = (model.DiscountPercent ?? 0).ToString();
                    txtCgst.Text = (model.CgstPercent ?? 0).ToString();
                    txtSgst.Text = (model.SgstPercent ?? 0).ToString();
                    txtIgst.Text = (model.IgstPercent ?? 0).ToString();
                }
        
                txtRate.TextChanged += (s, e) => RecalcCPFromRate();
                txtDisc.TextChanged += (s, e) => RecalcCPFromRate();
                txtCgst.TextChanged += (s, e) => { if (!_isCalculating && ParseDecimal(txtCgst.Text) > 0) { _isCalculating = true; txtIgst.Text = "0"; _isCalculating = false; } RecalcCPFromRate(); };
                txtSgst.TextChanged += (s, e) => { if (!_isCalculating && ParseDecimal(txtSgst.Text) > 0) { _isCalculating = true; txtIgst.Text = "0"; _isCalculating = false; } RecalcCPFromRate(); };
                txtIgst.TextChanged += (s, e) => { if (!_isCalculating && ParseDecimal(txtIgst.Text) > 0) { _isCalculating = true; txtCgst.Text = "0"; txtSgst.Text = "0"; _isCalculating = false; } RecalcCPFromRate(); };
                txtCost.TextChanged += (s, e) => RecalcRateFromCP();
        
                FlowLayoutPanel flp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0,10,15,0) };
                btnCancel.Text = "Cancel"; btnOk.Text = "Save Batch"; btnOk.DialogResult = DialogResult.OK; btnCancel.DialogResult = DialogResult.Cancel;
                btnOk.BackColor = Color.FromArgb(0, 122, 204); btnOk.ForeColor = Color.White; btnOk.FlatStyle = FlatStyle.Flat; btnCancel.FlatStyle = FlatStyle.Flat;
                flp.Controls.Add(btnCancel); flp.Controls.Add(btnOk);
                
                this.Controls.Add(tlp); this.Controls.Add(flp);
                
                void NumericOnly(object? s, KeyPressEventArgs e) { if (char.IsControl(e.KeyChar)) return; var tb = s as TextBox; if (tb == null) return; if (e.KeyChar == '.' && tb.Text.Contains('.')) { e.Handled = true; return; } if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.') e.Handled = true; }
                foreach (var tb in new[] { txtMrp, txtSell, txtCost, txtStock, txtRate, txtDisc, txtCgst, txtSgst, txtIgst }) tb.KeyPress += NumericOnly;
                AcceptButton = btnOk; CancelButton = btnCancel;
            }
            
            private void RecalcCPFromRate()
            {
                if (_isCalculating) return;
                _isCalculating = true;
                try
                {
                    decimal rate = decimal.TryParse(txtRate.Text, out var r) ? r : 0;
                    decimal disc = decimal.TryParse(txtDisc.Text, out var d) ? d : 0;
                    decimal cgst = decimal.TryParse(txtCgst.Text, out var c) ? c : 0;
                    decimal sgst = decimal.TryParse(txtSgst.Text, out var s) ? s : 0;
                    decimal igst = decimal.TryParse(txtIgst.Text, out var i) ? i : 0;
                    
                    decimal taxable = rate * (1 - disc / 100);
                    decimal cost = taxable * (1 + (cgst + sgst + igst) / 100);
                    
                    txtCost.Text = Math.Round(cost, 2).ToString();
                }
                finally { _isCalculating = false; }
            }
            
            private void RecalcRateFromCP()
            {
                if (_isCalculating) return;
                _isCalculating = true;
                try
                {
                    decimal cost = decimal.TryParse(txtCost.Text, out var cp) ? cp : 0;
                    decimal disc = decimal.TryParse(txtDisc.Text, out var d) ? d : 0;
                    decimal cgst = decimal.TryParse(txtCgst.Text, out var c) ? c : 0;
                    decimal sgst = decimal.TryParse(txtSgst.Text, out var s) ? s : 0;
                    decimal igst = decimal.TryParse(txtIgst.Text, out var i) ? i : 0;
                    
                    if (disc >= 100) disc = 0;
                    
                    decimal taxable = cost / (1 + (cgst + sgst + igst) / 100);
                    decimal rate = taxable / (1 - disc / 100);
                    
                    txtRate.Text = Math.Round(rate, 2).ToString();
                }
                finally { _isCalculating = false; }
            }
            private decimal ParseDecimal(string s) => decimal.TryParse(s, out var v) ? v : 0m;
        }
    }
}
