using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using ClosedXML.Excel;

namespace BillingSuite.App.Forms
{
    public class PurchaseSearchForm : Form
    {
        private readonly TextBox txtProductSearch = new TextBox();
        private readonly Button btnSearch = new Button();
        private readonly Button btnReset = new Button();
        private readonly DataGridView gridPurchases = new DataGridView();
        private readonly DataGridView gridPurchaseItems = new DataGridView();
        private readonly SplitContainer splitContainer = new SplitContainer();
        private readonly Label lblProductInfo = new Label();

        public PurchaseSearchForm()
        {
            Text = "Purchase Product Search";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1200, 700);
            MinimumSize = new Size(1000, 600);

            // Top search panel
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            
            var lblSearch = new Label { Text = "Product Search:", Location = new Point(10, 15), AutoSize = true };
            txtProductSearch.Location = new Point(120, 12);
            txtProductSearch.Size = new Size(300, 23);
            txtProductSearch.PlaceholderText = "Enter product name or barcode...";
            
            btnSearch.Text = "Search";
            btnSearch.Location = new Point(430, 12);
            btnSearch.Size = new Size(80, 23);
            btnSearch.Click += (s, e) => SearchProduct();
            
            btnReset.Text = "Reset";
            btnReset.Location = new Point(520, 12);
            btnReset.Size = new Size(80, 23);
            btnReset.Click += (s, e) => ResetSearch();

            topPanel.Controls.AddRange(new Control[] { lblSearch, txtProductSearch, btnSearch, btnReset });

            // Product info label
            lblProductInfo.Dock = DockStyle.Top;
            lblProductInfo.Height = 30;
            lblProductInfo.BackColor = Color.LightYellow;
            lblProductInfo.TextAlign = ContentAlignment.MiddleLeft;
            lblProductInfo.Padding = new Padding(10, 5, 10, 5);
            lblProductInfo.Font = new Font(Font.FontFamily, 9, FontStyle.Bold);

            // Split container for purchases and items
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.SplitterDistance = 400;
            splitContainer.Orientation = Orientation.Horizontal;

            // Purchases grid (top)
            gridPurchases.Dock = DockStyle.Fill;
            gridPurchases.ReadOnly = true;
            gridPurchases.RowHeadersVisible = false;
            gridPurchases.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridPurchases.AutoGenerateColumns = false;
            gridPurchases.MultiSelect = false;
            
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Purchase ID",
                Name = "colPurchaseId",
                Visible = false
            });
            // Product Name column
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Product Name",
                Name = "colProductName",
                DataPropertyName = "ProductName",
                Width = 200
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Date", 
                Name = "colDate", 
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" }
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn {
                HeaderText = "Bill No",
                Name = "colBillNo",
                Width = 120
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Supplier", 
                Name = "colSupplier", 
                Width = 200 
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Batch", 
                Name = "colBatch", 
                Width = 100 
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Quantity", 
                Name = "colQuantity", 
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.##" 
                }
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Cost Price", 
                Name = "colCostPrice", 
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.00" 
                }
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Selling Price", 
                Name = "colSellingPrice", 
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.00" 
                }
            });
            gridPurchases.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "MRP", 
                Name = "colMrp", 
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.00" 
                }
            });

            // Purchase items grid (bottom)
            gridPurchaseItems.Dock = DockStyle.Fill;
            gridPurchaseItems.ReadOnly = true;
            gridPurchaseItems.RowHeadersVisible = false;
            gridPurchaseItems.AutoGenerateColumns = false;
            
            gridPurchaseItems.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Product Name", 
                Name = "colProductName", 
                Width = 250 
            });
            gridPurchaseItems.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Batch", 
                Name = "colBatch", 
                Width = 100 
            });
            gridPurchaseItems.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Quantity", 
                Name = "colQuantity", 
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.##" 
                }
            });
            gridPurchaseItems.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Cost Price", 
                Name = "colCostPrice", 
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.00" 
                }
            });
            gridPurchaseItems.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Selling Price", 
                Name = "colSellingPrice", 
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.00" 
                }
            });
            gridPurchaseItems.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "MRP", 
                Name = "colMrp", 
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { 
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "0.00" 
                }
            });
            gridPurchaseItems.Columns.Add(new DataGridViewTextBoxColumn { 
                HeaderText = "Expiry", 
                Name = "colExpiry", 
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/yyyy" }
            });

            splitContainer.Panel1.Controls.Add(gridPurchases);
            splitContainer.Panel2.Controls.Add(gridPurchaseItems);

            // Add controls to form
            Controls.Add(topPanel);
            Controls.Add(lblProductInfo);
            Controls.Add(splitContainer);

            // Event handlers
            gridPurchases.SelectionChanged += (s, e) => LoadPurchaseItems();
            txtProductSearch.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter) {
                    e.Handled = true;
                    SearchProduct();
                }
            };

            // Load initial data
            Load += (s, e) => {
                txtProductSearch.Focus();
            };
        }

        private void SearchProduct()
        {
            var searchTerm = txtProductSearch.Text?.Trim();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                MessageBox.Show("Please enter a product name or barcode to search.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                
                // Search for product
                var product = db.Products.FirstOrDefault(p => 
                    p.Name.ToLower().Contains(searchTerm.ToLower()) || 
                    p.Barcode.ToLower().Contains(searchTerm.ToLower()));

                if (product == null)
                {
                    MessageBox.Show("No product found matching the search term.");
                    return;
                }

                // Update product info
                var totalStock = (product.OldStock ?? 0m) + (product.NewStock ?? 0m) + (product.VeryOldStock ?? 0m);
                lblProductInfo.Text = $"Product: {product.Name} | Barcode: {product.Barcode} | Current Stock: {totalStock:0.##}";

                // Find all purchases containing this product
                var purchases = (from pi in db.PurchaseItems
                                join p in db.Purchases on pi.PurchaseId equals p.Id
                                join s in db.Suppliers on p.SupplierId equals s.Id
                                where pi.ProductId == product.Id || pi.ProductName.ToLower().Contains(searchTerm.ToLower())
                                orderby p.PurchaseDate descending
                                select new
                                {
                                    PurchaseId = p.Id,
                                    Date = p.PurchaseDate,
                                    BillNo = p.InvoiceNumber ?? $"P-{p.Id:D6}",
                                    Supplier = s.Name,
                                    Batch = pi.BatchNumber ?? "-",
                                    ProductName = pi.ProductName ?? "-",
                                    Quantity = pi.Quantity,
                                    CostPrice = pi.CostPrice ?? 0m,
                                    SellingPrice = pi.SellingPrice ?? 0m,
                                    Mrp = pi.Mrp ?? 0m
                                }).ToList();

                // Highlight rows and bind data
                gridPurchases.DataSource = purchases;
                
                // Highlight alternating rows for better visibility
                foreach (DataGridViewRow row in gridPurchases.Rows)
                {
                    if (row.Index % 2 == 0)
                    {
                        row.DefaultCellStyle.BackColor = Color.AliceBlue;
                    }
                    // Highlight rows where product name matches search term
                    var prodName = row.Cells["colProductName"].Value?.ToString()?.ToLower() ?? "";
                    if (!string.IsNullOrEmpty(searchTerm) && prodName.Contains(searchTerm.ToLower()))
                    {
                        row.DefaultCellStyle.BackColor = Color.Yellow;
                        row.DefaultCellStyle.SelectionBackColor = Color.Orange;
                        // Scroll first matching row to top
                        if (gridPurchases.FirstDisplayedScrollingRowIndex == -1 || row.Index < gridPurchases.FirstDisplayedScrollingRowIndex)
                        {
                            try { gridPurchases.FirstDisplayedScrollingRowIndex = row.Index; } catch { }
                        }
                    }
                    // Highlight recent purchases (last 30 days) in light green
                    if (row.DataBoundItem != null)
                    {
                        var data = dynamic_cast(row.DataBoundItem);
                        if (data != null)
                        {
                            var purchaseDate = (DateTime)data.Date;
                            if (purchaseDate >= DateTime.Today.AddDays(-30))
                            {
                                row.DefaultCellStyle.BackColor = Color.LightGreen;
                            }
                        }
                    }
                }
                

                if (purchases.Count == 0)
                {
                    MessageBox.Show("No purchase history found for this product.");
                }
                else
                {
                    // Auto-select first row to show items and scroll to top
                    // Use BeginInvoke to ensure this happens after grid finishes binding and layout
                    BeginInvoke(new Action(() => {
                        try {
                            if (gridPurchases.Rows.Count > 0)
                            {
                                gridPurchases.Focus();
                                gridPurchases.ClearSelection();
                                gridPurchases.Rows[0].Selected = true;
                                gridPurchases.CurrentCell = gridPurchases.Rows[0].Cells[0];
                                gridPurchases.Update();
                                Application.DoEvents();
                                gridPurchases.FirstDisplayedScrollingRowIndex = 0;
                            }
                        } catch { }
                    }));
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching product: {ex.Message}");
            }
        }

        private void LoadPurchaseItems()
        {
            if (gridPurchases.CurrentRow == null) 
            {
                gridPurchaseItems.DataSource = null;
                return;
            }

            try
            {
                var purchaseId = (int)gridPurchases.CurrentRow.Cells["colPurchaseId"].Value;
                
                using var db = new AppDbContext();
                var items = db.PurchaseItems
                    .Where(pi => pi.PurchaseId == purchaseId)
                    .Select(pi => new
                    {
                        ProductName = pi.ProductName,
                        Batch = pi.BatchNumber ?? "-",
                        Quantity = pi.Quantity,
                        CostPrice = pi.CostPrice ?? 0m,
                        SellingPrice = pi.SellingPrice ?? 0m,
                        Mrp = pi.Mrp ?? 0m,
                        Expiry = pi.Expiry
                    })
                    .OrderBy(pi => pi.ProductName)
                    .ToList();

                gridPurchaseItems.DataSource = items;

                // Highlight and scroll to matching product
                string searchTerm = txtProductSearch.Text.Trim().ToLower();
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    foreach (DataGridViewRow row in gridPurchaseItems.Rows)
                    {
                        var prodName = row.Cells[0].Value?.ToString()?.ToLower() ?? "";
                        if (prodName.Contains(searchTerm))
                        {
                            row.DefaultCellStyle.BackColor = Color.Yellow;
                            row.DefaultCellStyle.SelectionBackColor = Color.Orange;
                            
                            // Scroll this row to top if it's the first match
                            if (gridPurchaseItems.FirstDisplayedScrollingRowIndex == -1 || row.Index < gridPurchaseItems.FirstDisplayedScrollingRowIndex) {
                                try { gridPurchaseItems.FirstDisplayedScrollingRowIndex = row.Index; } catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading purchase items: {ex.Message}");
            }
        }

        private void ResetSearch()
        {
            txtProductSearch.Clear();
            gridPurchases.DataSource = null;
            gridPurchaseItems.DataSource = null;
            lblProductInfo.Text = "Enter a product name or barcode to search purchase history";
            txtProductSearch.Focus();
        }

        private dynamic dynamic_cast(object obj)
        {
            return obj;
        }
    }
}
