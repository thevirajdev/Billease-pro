using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;

namespace BillingSuite.App.Forms
{
    public class CustomerHistoryForm : Form
    {
        private readonly int _customerId;
        private readonly DataGridView gridInvoices = new DataGridView();
        private readonly DataGridView gridItems = new DataGridView();
        private readonly SplitContainer split = new SplitContainer();
        private readonly Label lblHeader = new Label();
        private readonly ToolStrip tool = new ToolStrip();

        public CustomerHistoryForm(int customerId)
        {
            _customerId = customerId;
            Text = "Customer Invoice History";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 700);

            lblHeader.Dock = DockStyle.Top; lblHeader.Height = 34; lblHeader.TextAlign = ContentAlignment.MiddleLeft; lblHeader.Padding = new Padding(10, 0, 0, 0); lblHeader.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            Controls.Add(lblHeader);

            var tsSearchBill = new ToolStripButton { Text = "Search Bill" };
            tsSearchBill.Click += (s, e) => {
                var term = Microsoft.VisualBasic.Interaction.InputBox("Enter Bill Number:", "Search Bill", "");
                if (!string.IsNullOrWhiteSpace(term)) ApplyFilter(term, null);
            };
            var tsSearchProduct = new ToolStripButton { Text = "Search Product" };
            tsSearchProduct.Click += (s, e) => {
                var term = Microsoft.VisualBasic.Interaction.InputBox("Enter Product Name/Barcode:", "Search Product", "");
                if (!string.IsNullOrWhiteSpace(term)) ApplyFilter(null, term);
            };
            var tsProductHistory = new ToolStripButton { Text = "Product History" };
            tsProductHistory.Click += (s, e) => {
                var term = Microsoft.VisualBasic.Interaction.InputBox("Enter Product Name/Barcode:", "Product History", "");
                if (!string.IsNullOrWhiteSpace(term)) ShowProductHistory(term);
            };
            var tsReset = new ToolStripButton { Text = "Reset" };
            tsReset.Click += (s, e) => Reload();

            tool.Items.AddRange(new ToolStripItem[] { tsSearchBill, new ToolStripSeparator(), tsSearchProduct, new ToolStripSeparator(), tsProductHistory, new ToolStripSeparator(), tsReset });
            Controls.Add(tool);

            split.Orientation = Orientation.Horizontal; split.Dock = DockStyle.Fill; split.SplitterDistance = 300; 
            Controls.Add(split);
            split.BringToFront(); // ensure tool is above it if docked top

            ConfigureGrid(gridInvoices); ConfigureGrid(gridItems);
            split.Panel1.Controls.Add(gridInvoices);
            split.Panel2.Controls.Add(gridItems);

            Load += (s, e) => Reload();
            gridInvoices.SelectionChanged += (s, e) => LoadItemsForSelected();
        }

        private void ConfigureGrid(DataGridView g)
        {
            g.Dock = DockStyle.Fill; g.ReadOnly = true; g.RowHeadersVisible = false; g.AllowUserToAddRows = false; g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.AutoGenerateColumns = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void Reload()
        {
            ApplyFilter(null, null);
        }

        private void ApplyFilter(string? billNo, string? productName)
        {
            using var db = new AppDbContext();
            var cust = db.Customers.FirstOrDefault(c => c.Id == _customerId);
            if (cust == null)
            {
                lblHeader.Text = $"Customer History (ID: {_customerId} - Not Found)";
                gridInvoices.DataSource = new List<object>();
                gridItems.DataSource = new List<object>();
                return;
            }
            lblHeader.Text = $"Customer: {cust.Name} | Phone: {cust.Phone ?? "-"}";

            var q = db.Invoices.Where(i => i.CustomerId == _customerId && i.Status != InvoiceStatus.Void);

            if (!string.IsNullOrWhiteSpace(billNo))
            {
                q = q.Where(i => i.InvoiceNumber.Contains(billNo));
            }

            if (!string.IsNullOrWhiteSpace(productName))
            {
                q = q.Where(i => i.Items.Any(ii => ii.Description.Contains(productName)));
            }

            var list = q.OrderByDescending(i => i.InvoiceDate)
                .Select(i => new { i.Id, i.InvoiceDate, i.InvoiceNumber, i.Subtotal, i.DiscountAmount, i.TaxAmount, BillAmount = i.Total - i.BackDues, i.BackDues, i.Total, i.TotalPaid, Balance = i.Total - i.TotalPaid })
                .ToList();

            gridInvoices.Columns.Clear();
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", Name = "Id", Visible = false });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceDate", HeaderText = "Date", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InvoiceNumber", HeaderText = "Invoice #", FillWeight = 20 });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Subtotal", HeaderText = "Subtotal", FillWeight = 12, DefaultCellStyle = Right2() });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DiscountAmount", HeaderText = "Discount", FillWeight = 10, DefaultCellStyle = Right2() });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TaxAmount", HeaderText = "Tax", FillWeight = 10, DefaultCellStyle = Right2() });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BillAmount", HeaderText = "Bill Amt", FillWeight = 12, DefaultCellStyle = Right2() });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BackDues", HeaderText = "B.Dues", FillWeight = 10, DefaultCellStyle = Right2() });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Total", HeaderText = "Total", FillWeight = 12, DefaultCellStyle = Right2() });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalPaid", HeaderText = "Paid", FillWeight = 10, DefaultCellStyle = Right2() });
            gridInvoices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Balance", HeaderText = "Balance", FillWeight = 10, DefaultCellStyle = Right2() });
            gridInvoices.DataSource = list;

            if (list.Count == 0 && productName != null) MessageBox.Show("No invoices found containing that product.");
        }

        private DataGridViewCellStyle Right2() => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" };

        private void LoadItemsForSelected()
        {
            gridItems.Columns.Clear();
            if (gridInvoices.CurrentRow == null) { gridItems.DataSource = null; return; }
            var invId = (int)gridInvoices.CurrentRow.Cells["Id"].Value;

            using var db = new AppDbContext();
            var items = db.InvoiceItems.Where(ii => ii.InvoiceId == invId)
                .Select(ii => new { Product = ii.Description, SKU = ii.ProductId, ii.Quantity, ii.Price, Amount = ii.Quantity * ii.Price })
                .ToList();

            gridItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Product", HeaderText = "Product", FillWeight = 35 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SKU", HeaderText = "SKU/Barcode", FillWeight = 15 });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Quantity", HeaderText = "Qty", FillWeight = 10, DefaultCellStyle = Right2() });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Price", HeaderText = "Price", FillWeight = 15, DefaultCellStyle = Right2() });
            gridItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "Amount", FillWeight = 20, DefaultCellStyle = Right2() });
            gridItems.DataSource = items;
        }

        private void ShowProductHistory(string productName)
        {
            try
            {
                using var db = new AppDbContext();
                var cust = db.Customers.FirstOrDefault(c => c.Id == _customerId);
                if (cust == null) return;

                // Find all invoices containing this product for this customer
                var productInvoices = db.InvoiceItems
                    .Where(ii => ii.Invoice.CustomerId == _customerId && ii.Invoice.Status != InvoiceStatus.Void)
                    .Where(ii => ii.Description.ToLower().Contains(productName.ToLower()) || ii.ProductId.ToLower().Contains(productName.ToLower()))
                    .Select(ii => new {
                        ii.Invoice.InvoiceDate,
                        ii.Invoice.InvoiceNumber,
                        ii.Description,
                        ii.ProductId,
                        ii.Quantity,
                        ii.Price,
                        Amount = ii.Quantity * ii.Price,
                        Batch = ExtractToken(ii.Description, "BATCH"),
                        Expiry = ExtractToken(ii.Description, "EXP"),
                        MRP = ExtractToken(ii.Description, "MRP")
                    })
                    .OrderByDescending(x => x.InvoiceDate)
                    .ToList();

                if (productInvoices.Count == 0)
                {
                    MessageBox.Show($"No purchase history found for product: {productName}");
                    return;
                }

                // Create history dialog
                var historyForm = new Form {
                    Text = $"Product History - {productName}",
                    StartPosition = FormStartPosition.CenterParent,
                    Size = new Size(800, 500),
                    MinimizeBox = false,
                    MaximizeBox = false,
                    FormBorderStyle = FormBorderStyle.Sizable
                };

                var grid = new DataGridView {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    RowHeadersVisible = false,
                    AutoGenerateColumns = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false
                };

                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Date", 
                    DataPropertyName = "InvoiceDate", 
                    Width = 100,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" }
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Bill No", 
                    DataPropertyName = "InvoiceNumber", 
                    Width = 120 
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Product", 
                    DataPropertyName = "Description", 
                    Width = 200 
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Batch", 
                    DataPropertyName = "Batch", 
                    Width = 80 
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Expiry", 
                    DataPropertyName = "Expiry", 
                    Width = 80 
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "MRP", 
                    DataPropertyName = "MRP", 
                    Width = 80,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Qty", 
                    DataPropertyName = "Quantity", 
                    Width = 60,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight }
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Price", 
                    DataPropertyName = "Price", 
                    Width = 80,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn { 
                    HeaderText = "Amount", 
                    DataPropertyName = "Amount", 
                    Width = 90,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }
                });

                grid.DataSource = productInvoices;

                // Summary panel at top
                var summaryPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
                var totalQty = productInvoices.Sum(x => x.Quantity);
                var totalAmount = productInvoices.Sum(x => x.Amount);
                var avgPrice = totalQty > 0 ? totalAmount / totalQty : 0m;
                var firstPurchase = productInvoices.LastOrDefault()?.InvoiceDate;
                var lastPurchase = productInvoices.FirstOrDefault()?.InvoiceDate;

                var summaryLabel = new Label {
                    Text = $"Customer: {cust.Name}\n" +
                           $"Total Purchased: {totalQty:0.##} units | Total Amount: {totalAmount:0.00}\n" +
                           $"Average Price: {avgPrice:0.00} | " +
                           $"From: {firstPurchase:dd/MM/yyyy} To: {lastPurchase:dd/MM/yyyy}",
                    Dock = DockStyle.Fill,
                    Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
                };
                summaryPanel.Controls.Add(summaryLabel);

                historyForm.Controls.Add(grid);
                historyForm.Controls.Add(summaryPanel);
                summaryPanel.BringToFront();

                historyForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing product history: {ex.Message}");
            }
        }

        private static string? ExtractToken(string? desc, string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(desc) || string.IsNullOrWhiteSpace(key)) return null;
                foreach (var part in desc.Split('|'))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        return kv[1].Trim();
                }
                return null;
            }
            catch { return null; }
        }
    }
}
