using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using BillingSuite.App.Services;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Forms
{
    public class AlertsForm : Form
    {
        private readonly TabControl tabs = new TabControl();
        private readonly DataGridView gridExpiry = new DataGridView();
        private readonly DataGridView gridStock = new DataGridView();
        private readonly NumericUpDown numDefaultDays = new NumericUpDown();
        private readonly Button btnRefresh = new Button();
        private readonly Button btnSaveAlertDays = new Button();

        public AlertsForm()
        {
            Text = "Alerts";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 580);

            tabs.Dock = DockStyle.Fill;
            var tpExp = new TabPage("Near Expiry");
            var tpStock = new TabPage("Low Stock");

            gridExpiry.Dock = DockStyle.Fill; gridExpiry.RowHeadersVisible = false; gridExpiry.AutoGenerateColumns = false; gridExpiry.AllowUserToAddRows = false;
            gridExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Barcode", DataPropertyName = "Barcode", Width = 120 });
            gridExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
            gridExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch", DataPropertyName = "Batch", Width = 120 });
            gridExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry", DataPropertyName = "Expiry", Width = 120 });
            gridExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Days Left", DataPropertyName = "DaysLeft", Width = 90 });
            gridExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Alert Days", DataPropertyName = "AlertDays", Width = 90 });

            gridStock.Dock = DockStyle.Fill; gridStock.RowHeadersVisible = false; gridStock.AutoGenerateColumns = false; gridStock.AllowUserToAddRows = false;
            gridStock.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Barcode", DataPropertyName = "Barcode", Width = 120 });
            gridStock.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
            gridStock.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch", DataPropertyName = "Batch", Width = 120 });
            gridStock.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stock", DataPropertyName = "Stock", Width = 120 });
            gridStock.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Threshold", DataPropertyName = "Threshold", Width = 120 });

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 40 };
            pnlTop.Controls.Add(new Label { Text = "Default Expiry Alert Days:", AutoSize = true, Location = new Point(10, 12) });

            // Load the global default from Settings (was hardcoded to 30)
            numDefaultDays.Minimum = 1;
            numDefaultDays.Maximum = 365;
            numDefaultDays.Value = AppSettingsService.DefaultExpiryAlertDays;
            numDefaultDays.Location = new Point(170, 10);

            btnRefresh.Text = "Refresh"; btnRefresh.Location = new Point(250, 7); btnRefresh.Click += (s, e) => LoadData();
            btnSaveAlertDays.Text = "Save Alert Days"; btnSaveAlertDays.Location = new Point(330, 7); btnSaveAlertDays.Click += (s, e) => SaveAlertDays();
            pnlTop.Controls.AddRange(new Control[] { numDefaultDays, btnRefresh, btnSaveAlertDays });

            tpExp.Controls.Add(gridExpiry); tpExp.Controls.Add(pnlTop);
            tpStock.Controls.Add(gridStock);

            tabs.TabPages.Add(tpExp);
            tabs.TabPages.Add(tpStock);
            Controls.Add(tabs);

            Load += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var today = DateTime.Today;
                int defaultDays = (int)numDefaultDays.Value;

                // 1. Expiry Alerts (Query Batches)
                var dataExp = db.ProductBatches
                    .Include(b => b.Product)
                    .Where(b => b.Expiry != null && b.Stock > 0)
                    .AsEnumerable()
                    .Select(b => new
                    {
                        Barcode = b.Product?.Barcode ?? "N/A",
                        Name = b.Product?.Name ?? "N/A",
                        Batch = b.BatchNumber,
                        Expiry = b.Expiry!.Value.ToString("yyyy-MM-dd"),
                        DaysLeft = (b.Expiry!.Value.Date - today).TotalDays,
                        AlertDays = b.Product?.ExpiryAlertDays ?? defaultDays
                    })
                    .Where(x => x.DaysLeft <= x.AlertDays)
                    .OrderBy(x => x.DaysLeft)
                    .ToList();
                gridExpiry.DataSource = dataExp;

                // 2. Low Stock Alerts (Aggregated by Product)
                // Falls back to the global DefaultLowStockThreshold from Settings when a product
                // has no per-product threshold configured.
                int globalThreshold = AppSettingsService.DefaultLowStockThreshold;
                var dataStock = db.ProductBatches
                    .Include(b => b.Product)
                    .GroupBy(b => b.ProductIdRef)
                    .Select(g => new
                    {
                        Product = g.First().Product,
                        TotalStock = g.Sum(b => b.Stock ?? 0m)
                    })
                    .ToList() // evaluate in memory so we can use globalThreshold
                    .Where(x => x.Product != null &&
                                x.TotalStock <= (x.Product.LowStockThreshold ?? (decimal)globalThreshold) &&
                                (x.Product.LowStockThreshold ?? (decimal)globalThreshold) > 0)
                    .Select(x => new
                    {
                        Barcode = x.Product?.Barcode ?? "N/A",
                        Name = x.Product?.Name ?? "N/A",
                        Batch = "ALL BATCHES",
                        Stock = x.TotalStock,
                        Threshold = x.Product?.LowStockThreshold ?? (decimal)globalThreshold
                    })
                    .OrderBy(x => x.Stock)
                    .ToList();

                gridStock.DataSource = dataStock;
            }
            catch { }
        }

        private void SaveAlertDays()
        {
            // Persist the global default to settings so it survives a restart
            AppSettingsService.Set(AppSettingKeys.DefaultExpiryAlertDays, ((int)numDefaultDays.Value).ToString());

            if (gridExpiry.DataSource is System.Collections.IEnumerable)
            {
                using var db = new AppDbContext();
                foreach (DataGridViewRow row in gridExpiry.Rows)
                {
                    var barcode = row.Cells["Barcode"].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(barcode)) continue;
                    var p = db.Products.FirstOrDefault(x => x.Barcode == barcode);
                    if (p == null) continue;
                    if (int.TryParse(row.Cells["AlertDays"].Value?.ToString(), out var days))
                    {
                        p.ExpiryAlertDays = days;
                    }
                }
                db.SaveChanges();
                MessageBox.Show("Alert days saved.");
            }
        }
    }
}
