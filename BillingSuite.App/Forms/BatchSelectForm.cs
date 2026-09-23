using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Forms
{
    public class BatchSelectForm : Form
    {
        private readonly RadioButton rbOld = new RadioButton();
        private readonly RadioButton rbNew = new RadioButton();
        private readonly Label lblOld = new Label();
        private readonly Label lblNew = new Label();
        private readonly Label lblOldLastSale = new Label();
        private readonly Label lblNewLastSale = new Label();
        private readonly Button btnOk = new Button();
        private readonly Button btnCancel = new Button();

        private readonly Product product;
        private readonly int? customerId;
        private decimal? oldBatchLastSale;
        private decimal? newBatchLastSale;
        private string? lastSoldBatchCode;

        public string? SelectedBatchCode { get; private set; }
        public decimal? SelectedMrp { get; private set; }
        public decimal? SelectedSellingPrice { get; private set; }
        public DateTime? SelectedExpiry { get; private set; }

        public BatchSelectForm(Product p, int? customerId = null)
        {
            product = p;
            this.customerId = customerId;
            Text = "Select Batch";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(500, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);

            // Batch selection radio buttons
            rbOld.Text = "Old Batch"; 
            rbOld.Location = new Point(20, 20); 
            rbOld.AutoSize = true;
            rbOld.Width = 120;
            
            rbNew.Text = "New Batch"; 
            rbNew.Location = new Point(20, 120); 
            rbNew.AutoSize = true;
            rbNew.Width = 120;

            // Batch info labels
            lblOld.Location = new Point(40, 45); 
            lblOld.AutoSize = false;
            lblOld.Width = 440;
            lblOld.Height = 30;
            
            lblNew.Location = new Point(40, 145); 
            lblNew.AutoSize = false;
            lblNew.Width = 440;
            lblNew.Height = 30;

            // Last sale labels
            lblOldLastSale.Location = new Point(40, 75);
            lblOldLastSale.AutoSize = false;
            lblOldLastSale.Width = 440;
            lblOldLastSale.Height = 20;
            lblOldLastSale.ForeColor = Color.DarkGreen;
            
            lblNewLastSale.Location = new Point(40, 175);
            lblNewLastSale.AutoSize = false;
            lblNewLastSale.Width = 440;
            lblNewLastSale.Height = 20;
            lblNewLastSale.ForeColor = Color.DarkGreen;

            // Buttons
            btnOk.Text = "OK"; 
            btnOk.Location = new Point(310, 210); 
            btnOk.Click += BtnOk_Click; 
            btnOk.TabIndex = 10;
            btnOk.Width = 80;
            
            btnCancel.Text = "Cancel"; 
            btnCancel.Location = new Point(400, 210); 
            btnCancel.DialogResult = DialogResult.Cancel; 
            btnCancel.TabIndex = 11;
            btnCancel.Width = 80;

            Controls.AddRange(new Control[] { 
                rbOld, lblOld, lblOldLastSale, 
                rbNew, lblNew, lblNewLastSale, 
                btnOk, btnCancel 
            });

            // Make Enter confirm and Escape cancel
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            Load += async (s, e) =>
            {
                // Load last sale data asynchronously
                await LoadLastSaleDataAsync();
                
                // Populate the form with product data
                Populate();
                
                // Ensure some option is pre-selected
                if (!rbOld.Enabled && rbNew.Enabled) rbNew.Checked = true;
                if (rbOld.Enabled && !rbNew.Enabled) rbOld.Checked = true;
                if (!rbOld.Enabled && !rbNew.Enabled)
                {
                    // No batches; disable OK
                    btnOk.Enabled = false;
                }
                
                // Set focus to the first enabled radio button
                if (rbOld.Enabled) rbOld.Focus();
                else if (rbNew.Enabled) rbNew.Focus();
            };
        }

        private async System.Threading.Tasks.Task LoadLastSaleDataAsync()
        {
            if (customerId.HasValue && customerId > 0)
            {
                try
                {
                    using var db = new AppDbContext();
                    // Per-batch last sale
                    if (!string.IsNullOrEmpty(product.OldBatch))
                    {
                        var oldSale = await db.InvoiceItems
                            .Include(i => i.Invoice)
                            .Where(i => i.Invoice.CustomerId == customerId &&
                                       i.ProductIdRef == product.Id &&
                                       i.Description != null &&
                                       i.Description.Contains($"BATCH:{product.OldBatch}"))
                            .OrderByDescending(i => i.Invoice.InvoiceDate)
                            .ThenByDescending(i => i.Id)
                            .Select(i => i.Price)
                            .FirstOrDefaultAsync();
                        oldBatchLastSale = oldSale > 0 ? oldSale : (decimal?)null;
                    }
                    if (!string.IsNullOrEmpty(product.NewBatch))
                    {
                        var newSale = await db.InvoiceItems
                            .Include(i => i.Invoice)
                            .Where(i => i.Invoice.CustomerId == customerId &&
                                       i.ProductIdRef == product.Id &&
                                       i.Description != null &&
                                       i.Description.Contains($"BATCH:{product.NewBatch}"))
                            .OrderByDescending(i => i.Invoice.InvoiceDate)
                            .ThenByDescending(i => i.Id)
                            .Select(i => i.Price)
                            .FirstOrDefaultAsync();
                        newBatchLastSale = newSale > 0 ? newSale : (decimal?)null;
                    }

                    // Determine which batch was last sold overall for this product+customer
                    var lastItem = await db.InvoiceItems
                        .Include(i => i.Invoice)
                        .Where(i => i.Invoice.CustomerId == customerId && i.ProductIdRef == product.Id)
                        .OrderByDescending(i => i.Invoice.InvoiceDate)
                        .ThenByDescending(i => i.Id)
                        .Select(i => new { i.Description })
                        .FirstOrDefaultAsync();
                    if (lastItem != null && !string.IsNullOrEmpty(lastItem.Description))
                    {
                        var idx = lastItem.Description.IndexOf("BATCH:", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var s = lastItem.Description.Substring(idx + 6);
                            int end = s.IndexOfAny(new[] { ' ', ',', ';', '|', ')', ']' });
                            lastSoldBatchCode = end > 0 ? s.Substring(0, end) : s;
                            lastSoldBatchCode = lastSoldBatchCode?.Trim();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Silently handle any errors
                    Console.WriteLine($"Error loading last sale data: {ex.Message}");
                }
            }
        }

        private void Populate()
        {
            // Format the last sale text with color coding
            string FormatLastSaleText(decimal? lastSale, decimal? currentPrice)
            {
                if (!lastSale.HasValue) return string.Empty;
                
                string text = $"Last Sale: {lastSale.Value:0.00}";
                
                if (currentPrice.HasValue)
                {
                    if (lastSale > currentPrice)
                        return $"{text} (▲ {lastSale - currentPrice:0.00})";
                    else if (lastSale < currentPrice)
                        return $"{text} (▼ {currentPrice - lastSale:0.00})";
                    else
                        return $"{text} (→ No Change)";
                }
                
                return text;
            }

            // Old batch section
            if (!string.IsNullOrWhiteSpace(product.OldBatch))
            {
                rbOld.Enabled = true;
                rbOld.Checked = string.Equals(lastSoldBatchCode, product.OldBatch, StringComparison.OrdinalIgnoreCase);
                lblOld.Text = $"Batch: {product.OldBatch,-10} | " +
                             $"MRP: {product.OldMrp:0.00} | " +
                             $"Sell: {product.OldSellingPrice:0.00} | " +
                             $"Exp: {(product.OldExpiry?.ToString("dd-MMM-yy") ?? "-"),-8} | " +
                             $"Stock: {product.OldStock}";
                lblOldLastSale.Text = FormatLastSaleText(oldBatchLastSale, product.OldSellingPrice);
                lblOldLastSale.Visible = oldBatchLastSale.HasValue;
            }
            else
            {
                rbOld.Enabled = false; 
                lblOld.Text = "No old batch";
                lblOldLastSale.Visible = false;
            }

            // New batch section
            if (!string.IsNullOrWhiteSpace(product.NewBatch))
            {
                rbNew.Enabled = true;
                if (!rbOld.Enabled) rbNew.Checked = true;
                else if (string.Equals(lastSoldBatchCode, product.NewBatch, StringComparison.OrdinalIgnoreCase)) rbNew.Checked = true;
                
                lblNew.Text = $"Batch: {product.NewBatch,-10} | " +
                             $"MRP: {product.NewMrp:0.00} | " +
                             $"Sell: {product.NewSellingPrice:0.00} | " +
                             $"Exp: {(product.NewExpiry?.ToString("dd-MMM-yy") ?? "-"),-8} | " +
                             $"Stock: {product.NewStock}";
                lblNewLastSale.Text = FormatLastSaleText(newBatchLastSale, product.NewSellingPrice);
                lblNewLastSale.Visible = newBatchLastSale.HasValue;
            }
            else
            {
                rbNew.Enabled = false; 
                lblNew.Text = "No new batch";
                lblNewLastSale.Visible = false;
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (rbOld.Checked && rbOld.Enabled)
            {
                SelectedBatchCode = product.OldBatch;
                SelectedMrp = product.OldMrp;
                SelectedSellingPrice = product.OldSellingPrice;
                SelectedExpiry = product.OldExpiry;
            }
            else if (rbNew.Checked && rbNew.Enabled)
            {
                SelectedBatchCode = product.NewBatch;
                SelectedMrp = product.NewMrp;
                SelectedSellingPrice = product.NewSellingPrice;
                SelectedExpiry = product.NewExpiry;
            }
            else
            {
                MessageBox.Show("Please select a batch.");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
