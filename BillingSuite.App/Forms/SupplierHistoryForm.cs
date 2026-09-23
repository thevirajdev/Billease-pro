using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using System.Collections.Generic;

namespace BillingSuite.App.Forms
{
    public class SupplierHistoryForm : Form
    {
        private readonly int _supplierId;
        private readonly DataGridView gridPurchases = new DataGridView();
        private readonly DataGridView gridItems = new DataGridView();
        private readonly SplitContainer split = new SplitContainer();
        private readonly Label lblHeader = new Label();
        private readonly Label lblLifeTotal = new Label();
        private readonly Label lblLifePaid = new Label();
        private readonly Label lblLifeDue = new Label();
        private readonly TextBox txtSearchProduct = new TextBox();
        private string _lastSearchTerm = string.Empty;

        public SupplierHistoryForm(int supplierId)
        {
            _supplierId = supplierId;
            Text = "Supplier Purchase History";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 680);

            // Header Section - Simplified to a single clean toolbar
            var pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(245, 246, 250), BorderStyle = BorderStyle.FixedSingle };
            var flpLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 12, 0, 0) };
            
            lblHeader.AutoSize = true; lblHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold); lblHeader.Margin = new Padding(0, 0, 20, 0);
            
            lblLifeTotal.AutoSize = true; lblLifeTotal.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold); lblLifeTotal.ForeColor = Color.FromArgb(64, 64, 64);
            lblLifePaid.AutoSize = true; lblLifePaid.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold); lblLifePaid.ForeColor = Color.ForestGreen; lblLifePaid.Margin = new Padding(25, 0, 0, 0);
            lblLifeDue.AutoSize = true; lblLifeDue.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold); lblLifeDue.ForeColor = Color.Firebrick; lblLifeDue.Margin = new Padding(25, 0, 0, 0);
            
            flpLeft.Controls.AddRange(new Control[] { lblHeader, lblLifeTotal, lblLifePaid, lblLifeDue });

            var pnlSearch = new Panel { Dock = DockStyle.Right, Width = 310, Padding = new Padding(0, 10, 10, 0) };
            var lblSearch = new Label { Text = "Search Item:", AutoSize = true, Location = new Point(0, 5), Font = new Font("Segoe UI", 9F) };
            txtSearchProduct.SetBounds(90, 2, 200, 26);
            txtSearchProduct.PlaceholderText = "Type product name...";
            txtSearchProduct.TextChanged += (s, e) => { _lastSearchTerm = txtSearchProduct.Text.Trim(); Reload(); };
            pnlSearch.Controls.AddRange(new Control[] { lblSearch, txtSearchProduct });

            pnlToolbar.Controls.Add(flpLeft);
            pnlToolbar.Controls.Add(pnlSearch);
            Controls.Add(pnlToolbar);

            split.Orientation = Orientation.Horizontal; split.Dock = DockStyle.Fill; split.SplitterDistance = 260; Controls.Add(split);
            split.BringToFront(); 

            ConfigureGrid(gridPurchases); ConfigureGrid(gridItems);
            gridItems.CellFormatting += GridItems_CellFormatting;

            var hostTop = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
            var hostBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
            hostTop.Controls.Add(gridPurchases);
            hostBottom.Controls.Add(gridItems);
            split.Panel1.Controls.Add(hostTop);
            split.Panel2.Controls.Add(hostBottom);

            Load += (s,e) => Reload();
            gridPurchases.SelectionChanged += (s,e) => LoadItemsForSelected();
        }

        private void ConfigureGrid(DataGridView g)
        {
            g.Dock = DockStyle.Fill; g.ReadOnly = true; g.RowHeadersVisible = false; g.AllowUserToAddRows = false; g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.AutoGenerateColumns = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void Reload()
        {
            using var db = new AppDbContext();
            var sup = db.Suppliers.FirstOrDefault(s => s.Id == _supplierId);
            lblHeader.Text = sup?.Name ?? "Supplier";

            var purchasesQuery = db.Purchases.Where(p => p.SupplierId == _supplierId);

            if (!string.IsNullOrWhiteSpace(_lastSearchTerm))
            {
                var term = _lastSearchTerm.ToLower();
                var matchingPurchaseIds = db.PurchaseItems
                    .Where(i => i.ProductName != null && i.ProductName.ToLower().Contains(term))
                    .Select(i => i.PurchaseId)
                    .Distinct()
                    .ToList();
                purchasesQuery = purchasesQuery.Where(p => matchingPurchaseIds.Contains(p.Id));
            }

            var allPurchases = purchasesQuery.OrderByDescending(p => p.PurchaseDate).ToList();
            
            var lifeTotal = allPurchases.Sum(p => p.Total ?? 0m);
            var lifePaid = allPurchases.Sum(p => p.Paid ?? 0m);
            var lifeDue = lifeTotal - lifePaid;

            lblLifeTotal.Text = $"TOTAL BILL: {lifeTotal:N2}";
            lblLifePaid.Text = $"TOTAL PAID: {lifePaid:N2}";
            lblLifeDue.Text = $"TOTAL DUE: {lifeDue:N2}";

            var displayList = allPurchases.Select(p => new {
                p.Id, 
                p.PurchaseDate, 
                p.InvoiceNumber, 
                p.Subtotal, 
                p.Discount, 
                p.Tax, 
                p.Shipping, 
                p.Total, 
                p.Paid, 
                p.Due 
            }).ToList();

            gridPurchases.Columns.Clear();
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Id", Name = "Id", HeaderText = "Id", FillWeight = 8, MinimumWidth = 60 });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "PurchaseDate", HeaderText = "Date", FillWeight = 16, MinimumWidth = 120, DefaultCellStyle = new DataGridViewCellStyle{ Format = "dd-MMM-yyyy" }});
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "InvoiceNumber", HeaderText = "Invoice #", FillWeight = 22, MinimumWidth = 140 });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Subtotal", HeaderText = "Subtotal", FillWeight = 12, MinimumWidth = 100, DefaultCellStyle = Right2()});
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Discount", HeaderText = "Discount", FillWeight = 10, MinimumWidth = 90, DefaultCellStyle = Right2()});
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Tax", HeaderText = "Tax", FillWeight = 10, MinimumWidth = 80, DefaultCellStyle = Right2()});
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Shipping", HeaderText = "Shipping", FillWeight = 10, MinimumWidth = 90, DefaultCellStyle = Right2()});
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Total", HeaderText = "Total", FillWeight = 12, MinimumWidth = 110, DefaultCellStyle = Right2()});
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Paid", HeaderText = "Paid", FillWeight = 10, MinimumWidth = 90, DefaultCellStyle = Right2()});
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Due", HeaderText = "Due", FillWeight = 10, MinimumWidth = 90, DefaultCellStyle = Right2()});
            gridPurchases.DataSource = displayList;

            BeginInvoke(new Action(() => {
                try { 
                    if (gridPurchases.Rows.Count > 0) { 
                        gridPurchases.Focus();
                        gridPurchases.ClearSelection(); 
                        gridPurchases.Rows[0].Selected = true; 
                        gridPurchases.CurrentCell = gridPurchases.Rows[0].Cells[gridPurchases.Columns["PurchaseDate"]?.Index ?? 0]; 
                        gridPurchases.Update();
                        Application.DoEvents(); 
                        gridPurchases.FirstDisplayedScrollingRowIndex = 0; 
                    } 
                } catch { }
            }));
            LoadItemsForSelected();
        }

        private DataGridViewCellStyle Right2() => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" };

        private int? GetSelectedPurchaseId()
        {
            try
            {
                if (gridPurchases.CurrentRow == null) return null;
                var cell = gridPurchases.CurrentRow.Cells["Id"];
                if (cell == null) return null;
                var v = cell.Value;
                if (v == null) return null;
                if (v is int i) return i;
                if (int.TryParse(v.ToString(), out var parsed)) return parsed;
                return null;
            }
            catch { return null; }
        }

        private void LoadItemsForSelected()
        {
            gridItems.Columns.Clear();
            var pid = GetSelectedPurchaseId(); if (pid == null) { gridItems.DataSource = null; return; }
            using var db = new AppDbContext();
            var rows = db.PurchaseItems.Where(i => i.PurchaseId == pid.Value)
                .Select(i => new { i.ProductName, i.BatchNumber, i.Expiry, i.Mrp, i.Rate, i.DiscountPercent, i.CgstPercent, i.SgstPercent, i.IgstPercent, i.CostPrice, i.SellingPrice, i.Quantity, i.Pack, i.Bonus, i.MarketedBy, i.LineTotal })
                .ToList();
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "ProductName", Name = "ProductName", HeaderText = "Product", FillWeight = 28, MinimumWidth = 160 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "BatchNumber", HeaderText = "Batch", FillWeight = 10, MinimumWidth = 80 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Expiry", HeaderText = "Expiry", FillWeight = 10, MinimumWidth = 80, DefaultCellStyle = new DataGridViewCellStyle{ Format = "MM/yy" }});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Rate", HeaderText = "Rate", FillWeight = 10, MinimumWidth = 80, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "DiscountPercent", HeaderText = "Disc%", FillWeight = 8, MinimumWidth = 60, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "CgstPercent", HeaderText = "C%", FillWeight = 6, MinimumWidth = 50, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "SgstPercent", HeaderText = "S%", FillWeight = 6, MinimumWidth = 50, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "IgstPercent", HeaderText = "I%", FillWeight = 6, MinimumWidth = 50, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "CostPrice", HeaderText = "CP", FillWeight = 10, MinimumWidth = 80, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "SellingPrice", HeaderText = "SP", FillWeight = 10, MinimumWidth = 80, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Quantity", HeaderText = "Qty", FillWeight = 8, MinimumWidth = 60, DefaultCellStyle = Right2()});
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Pack", HeaderText = "Pack", FillWeight = 10, MinimumWidth = 90 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "Bonus", HeaderText = "Bonus", FillWeight = 10, MinimumWidth = 90 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "MarketedBy", HeaderText = "Mkt.", FillWeight = 16, MinimumWidth = 120 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn{ DataPropertyName = "LineTotal", HeaderText = "Line Total", FillWeight = 10, MinimumWidth = 100, DefaultCellStyle = Right2()});
            gridItems.DataSource = rows;

            // Auto-scroll to first matching item if searching
            if (!string.IsNullOrWhiteSpace(_lastSearchTerm))
            {
                var term = _lastSearchTerm.ToLower();
                foreach (DataGridViewRow row in gridItems.Rows)
                {
                    var name = row.Cells["ProductName"].Value?.ToString()?.ToLower();
                    if (name != null && name.Contains(term))
                    {
                        var targetRow = row;
                        BeginInvoke(new Action(() => {
                            try {
                                gridItems.Focus();
                                gridItems.ClearSelection();
                                targetRow.Selected = true;
                                if (targetRow.Index >= 0) {
                                    gridItems.Update();
                                    Application.DoEvents();
                                    gridItems.FirstDisplayedScrollingRowIndex = targetRow.Index;
                                }
                            } catch { }
                        }));
                        break;
                    }
                }
            }
        }


        private void GridItems_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastSearchTerm)) return;
            if (e.RowIndex < 0) return;

            var grid = sender as DataGridView;
            if (grid == null) return;

            var row = grid.Rows[e.RowIndex];
            var prodName = row.Cells["ProductName"].Value?.ToString();
            if (prodName != null && prodName.ToLower().Contains(_lastSearchTerm.ToLower()))
            {
                e.CellStyle.BackColor = Color.Yellow;
                e.CellStyle.SelectionBackColor = Color.Gold;
                e.CellStyle.SelectionForeColor = Color.Black;
            }
        }
    }
}
