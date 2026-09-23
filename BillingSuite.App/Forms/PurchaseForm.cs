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
    public class PurchaseForm : Form
    {
        private readonly DataGridView grid = new DataGridView();
        private readonly Button btnImport = new Button();
        private readonly Button btnExport = new Button();
        private readonly Button btnExportTemplate = new Button();
        private readonly Button btnSave = new Button();
        private readonly TextBox txtNote = new TextBox();

        public PurchaseForm()
        {
            Text = "Purchases";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 620);

            grid.Dock = DockStyle.Top;
            grid.Height = 480;
            grid.AllowUserToAddRows = true;
            grid.AllowUserToDeleteRows = true;
            grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Barcode", Name = "colBarcode", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product Name", Name = "colName", Width = 240, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch", Name = "colBatch", Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MRP", Name = "colMrp", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry (yyyy-MM-dd)", Name = "colExpiry", Width = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cost", Name = "colCost", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Selling", Name = "colSell", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty", Name = "colQty", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.##" } });
            grid.CellEndEdit += Grid_CellEndEdit;

            var panel = new Panel { Dock = DockStyle.Bottom, Height = 80 };
            btnImport.Text = "Import Excel"; btnImport.Location = new Point(10, 10); btnImport.Click += (s, e) => ImportExcel();
            btnExportTemplate.Text = "Export Template"; btnExportTemplate.Location = new Point(120, 10); btnExportTemplate.Click += (s, e) => ExportTemplate();
            btnExport.Text = "Export Excel"; btnExport.Location = new Point(240, 10); btnExport.Click += (s, e) => ExportExcel();
            btnSave.Text = "Save Purchase"; btnSave.Location = new Point(350, 10); btnSave.Click += (s, e) => SavePurchase();
            txtNote.PlaceholderText = "Note / Supplier / Bill no (optional)"; txtNote.Location = new Point(10, 45); txtNote.Width = 600;
            panel.Controls.AddRange(new Control[] { btnImport, btnExportTemplate, btnExport, btnSave, txtNote });

            Controls.Add(grid);
            Controls.Add(panel);
        }

        private bool ValidateRows()
        {
            bool ok = true;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                // reset styles
                foreach (DataGridViewCell c in row.Cells) c.Style.BackColor = Color.White;

                void Mark(string col)
                {
                    var cell = row.Cells[col]; cell.Style.BackColor = Color.MistyRose; ok = false;
                }
                string barcode = row.Cells["colBarcode"].Value?.ToString()?.Trim() ?? string.Empty;
                string batch = row.Cells["colBatch"].Value?.ToString()?.Trim() ?? string.Empty;
                decimal? mrp = ParseDecimal(row.Cells["colMrp"].Value);
                decimal? cost = ParseDecimal(row.Cells["colCost"].Value);
                decimal? sell = ParseDecimal(row.Cells["colSell"].Value);
                decimal? qty = ParseDecimal(row.Cells["colQty"].Value);
                var expRaw = row.Cells["colExpiry"].Value?.ToString();
                DateTime? exp = ParseDate(expRaw);

                if (string.IsNullOrWhiteSpace(barcode)) Mark("colBarcode");
                if (string.IsNullOrWhiteSpace(batch)) Mark("colBatch");
                if (!mrp.HasValue || mrp.Value < 0) Mark("colMrp");
                if (!cost.HasValue || cost.Value < 0) Mark("colCost");
                if (!sell.HasValue || sell.Value < 0) Mark("colSell");
                if (!qty.HasValue || qty.Value <= 0) Mark("colQty");
                if (!string.IsNullOrWhiteSpace(expRaw) && !exp.HasValue) Mark("colExpiry");
            }
            return ok;
        }

        private void ExportTemplate()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = "Purchase_Template.xlsx" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Template");
            ws.Cell(1,1).Value = "Barcode";
            ws.Cell(1,2).Value = "Product Name";
            ws.Cell(1,3).Value = "Batch";
            ws.Cell(1,4).Value = "MRP";
            ws.Cell(1,5).Value = "Expiry (yyyy-MM-dd)";
            ws.Cell(1,6).Value = "Cost";
            ws.Cell(1,7).Value = "Selling";
            ws.Cell(1,8).Value = "Qty";
            // sample row
            ws.Cell(2,1).Value = "SKU123";
            ws.Cell(2,2).Value = "Sample Product";
            ws.Cell(2,3).Value = "BATCH001";
            ws.Cell(2,4).Value = 100.00;
            ws.Cell(2,5).Value = DateTime.Today.AddMonths(12).ToString("yyyy-MM-dd");
            ws.Cell(2,6).Value = 80.00;
            ws.Cell(2,7).Value = 95.00;
            ws.Cell(2,8).Value = 10;
            ws.Columns().AdjustToContents();
            wb.SaveAs(sfd.FileName);
            MessageBox.Show("Template exported.");
        }

        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var colName = grid.Columns[e.ColumnIndex].Name;
            if (colName == "colBarcode")
            {
                var code = grid.Rows[e.RowIndex].Cells["colBarcode"].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(code)) return;
                using var db = new AppDbContext();
                var p = db.Products.FirstOrDefault(x => x.Barcode == code);
                grid.Rows[e.RowIndex].Cells["colName"].Value = p?.Name ?? "";
                if (p != null)
                {
                    // Prefill from New batch if row fields are empty
                    var mrpCell = grid.Rows[e.RowIndex].Cells["colMrp"];
                    var costCell = grid.Rows[e.RowIndex].Cells["colCost"];
                    var sellCell = grid.Rows[e.RowIndex].Cells["colSell"];
                    var expCell = grid.Rows[e.RowIndex].Cells["colExpiry"];
                    if (mrpCell.Value == null && p.NewMrp.HasValue) mrpCell.Value = p.NewMrp.Value;
                    if (costCell.Value == null && p.NewCostPrice.HasValue) costCell.Value = p.NewCostPrice.Value;
                    if (sellCell.Value == null && p.NewSellingPrice.HasValue) sellCell.Value = p.NewSellingPrice.Value;
                    if (string.IsNullOrWhiteSpace(expCell.Value?.ToString()) && p.NewExpiry.HasValue) expCell.Value = p.NewExpiry.Value.ToString("yyyy-MM-dd");
                }
            }
            else if (colName == "colBatch")
            {
                var code = grid.Rows[e.RowIndex].Cells["colBarcode"].Value?.ToString()?.Trim();
                var batch = grid.Rows[e.RowIndex].Cells["colBatch"].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(batch)) return;
                using var db = new AppDbContext();
                var p = db.Products.FirstOrDefault(x => x.Barcode == code);
                if (p == null) return;
                void FillFrom(string? b, decimal? mrp, DateTime? exp, decimal? cost, decimal? sell)
                {
                    if (string.Equals(batch, b, StringComparison.OrdinalIgnoreCase))
                    {
                        if (mrp.HasValue) grid.Rows[e.RowIndex].Cells["colMrp"].Value = mrp.Value;
                        if (exp.HasValue) grid.Rows[e.RowIndex].Cells["colExpiry"].Value = exp.Value.ToString("yyyy-MM-dd");
                        if (cost.HasValue) grid.Rows[e.RowIndex].Cells["colCost"].Value = cost.Value;
                        if (sell.HasValue) grid.Rows[e.RowIndex].Cells["colSell"].Value = sell.Value;
                    }
                }
                FillFrom(p.NewBatch, p.NewMrp, p.NewExpiry, p.NewCostPrice, p.NewSellingPrice);
                FillFrom(p.OldBatch, p.OldMrp, p.OldExpiry, p.OldCostPrice, p.OldSellingPrice);
                FillFrom(p.VeryOldBatch, p.VeryOldMrp, p.VeryOldExpiry, p.VeryOldCostPrice, p.VeryOldSellingPrice);
            }
        }

        private void ImportExcel()
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            using var wb = new XLWorkbook(ofd.FileName);
            var ws = wb.Worksheets.First();
            grid.Rows.Clear();
            bool first = true;
            foreach (var row in ws.RowsUsed())
            {
                if (first) { first = false; continue; }
                var r = new DataGridViewRow(); r.CreateCells(grid);
                r.Cells[grid.Columns["colBarcode"].Index].Value = row.Cell(1).GetString();
                r.Cells[grid.Columns["colName"].Index].Value = row.Cell(2).GetString();
                r.Cells[grid.Columns["colBatch"].Index].Value = row.Cell(3).GetString();
                r.Cells[grid.Columns["colMrp"].Index].Value = row.Cell(4).GetDouble();
                r.Cells[grid.Columns["colExpiry"].Index].Value = row.Cell(5).GetString();
                r.Cells[grid.Columns["colCost"].Index].Value = row.Cell(6).GetDouble();
                r.Cells[grid.Columns["colSell"].Index].Value = row.Cell(7).GetDouble();
                r.Cells[grid.Columns["colQty"].Index].Value = row.Cell(8).GetDouble();
                grid.Rows.Add(r);
            }
        }

        private void ExportExcel()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = $"Purchase_{DateTime.Now:yyyyMMddHHmm}.xlsx" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Purchase");
            ws.Cell(1,1).Value = "Barcode";
            ws.Cell(1,2).Value = "Product Name";
            ws.Cell(1,3).Value = "Batch";
            ws.Cell(1,4).Value = "MRP";
            ws.Cell(1,5).Value = "Expiry";
            ws.Cell(1,6).Value = "Cost";
            ws.Cell(1,7).Value = "Selling";
            ws.Cell(1,8).Value = "Qty";
            int r = 2;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                ws.Cell(r,1).Value = row.Cells["colBarcode"].Value?.ToString();
                ws.Cell(r,2).Value = row.Cells["colName"].Value?.ToString();
                ws.Cell(r,3).Value = row.Cells["colBatch"].Value?.ToString();
                ws.Cell(r,4).Value = ParseDecimal(row.Cells["colMrp"].Value);
                ws.Cell(r,5).Value = row.Cells["colExpiry"].Value?.ToString();
                ws.Cell(r,6).Value = ParseDecimal(row.Cells["colCost"].Value);
                ws.Cell(r,7).Value = ParseDecimal(row.Cells["colSell"].Value);
                ws.Cell(r,8).Value = ParseDecimal(row.Cells["colQty"].Value);
                r++;
            }
            wb.SaveAs(sfd.FileName);
            MessageBox.Show("Exported.");
        }

        private void SavePurchase()
        {
            if (!ValidateRows()) { MessageBox.Show("Please correct highlighted rows before saving."); return; }
            var items = new List<PurchaseRow>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var br = row.Cells["colBarcode"].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(br)) continue;
                items.Add(new PurchaseRow
                {
                    Barcode = br,
                    Batch = row.Cells["colBatch"].Value?.ToString()?.Trim() ?? string.Empty,
                    Mrp = ParseDecimal(row.Cells["colMrp"].Value) ?? 0m,
                    Cost = ParseDecimal(row.Cells["colCost"].Value) ?? 0m,
                    Sell = ParseDecimal(row.Cells["colSell"].Value) ?? 0m,
                    Qty = ParseDecimal(row.Cells["colQty"].Value) ?? 0m,
                    Expiry = ParseDate(row.Cells["colExpiry"].Value)
                });
            }
            if (items.Count == 0) { MessageBox.Show("No rows to save."); return; }

            using var db = new AppDbContext();
            foreach (var it in items)
            {
                var prod = db.Products.FirstOrDefault(p => p.Barcode == it.Barcode);
                if (prod == null)
                {
                    prod = new Product { Barcode = it.Barcode, Name = it.Barcode };
                    db.Products.Add(prod);
                }
                ApplyPurchase(prod, it);
            }
            db.SaveChanges();
            MessageBox.Show("Purchase saved. Products updated.");
            Close();
        }

        private void ApplyPurchase(Product prod, PurchaseRow it)
        {
            bool SameAsNew = (prod.NewMrp ?? -1m) == it.Mrp
                             && NullableDateEq(prod.NewExpiry, it.Expiry)
                             && (prod.NewCostPrice ?? -1m) == it.Cost;
            if (SameAsNew)
            {
                // Merge into New; update batch code to latest and accumulate stock
                prod.NewBatch = it.Batch;
                prod.NewSellingPrice = it.Sell;
                prod.NewStock = (prod.NewStock ?? 0m) + it.Qty;
                return;
            }

            bool newIsEmpty = string.IsNullOrWhiteSpace(prod.NewBatch) && prod.NewMrp == null && prod.NewExpiry == null && prod.NewCostPrice == null && (prod.NewStock ?? 0m) == 0m;
            if (newIsEmpty)
            {
                SetNew(prod, it);
                return;
            }

            bool oldZeroStock = (prod.OldStock ?? 0m) == 0m;
            if (oldZeroStock)
            {
                MoveNewToOld(prod);
                SetNew(prod, it);
                return;
            }

            // Old already has data: move Old to VeryOld (or merge), then New to Old, then set New
            if ((prod.VeryOldStock ?? 0m) == 0m && string.IsNullOrWhiteSpace(prod.VeryOldBatch))
            {
                MoveOldToVeryOld(prod);
            }
            else
            {
                // Already very old present: merge if identical spec else accumulate stock only
                if (string.Equals(prod.VeryOldBatch ?? string.Empty, prod.OldBatch ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && (prod.VeryOldMrp ?? -1m) == (prod.OldMrp ?? -2m)
                    && NullableDateEq(prod.VeryOldExpiry, prod.OldExpiry)
                    && (prod.VeryOldCostPrice ?? -1m) == (prod.OldCostPrice ?? -2m))
                {
                    prod.VeryOldStock = (prod.VeryOldStock ?? 0m) + (prod.OldStock ?? 0m);
                }
                else
                {
                    // Create/accumulate separate very old by overwriting if zero
                    if ((prod.VeryOldStock ?? 0m) == 0m)
                    {
                        MoveOldToVeryOld(prod);
                    }
                    else
                    {
                        // Fallback: accumulate stock only
                        prod.VeryOldStock = (prod.VeryOldStock ?? 0m) + (prod.OldStock ?? 0m);
                    }
                }
            }
            MoveNewToOld(prod);
            SetNew(prod, it);
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
        private void SetNew(Product p, PurchaseRow it)
        {
            p.NewBatch = it.Batch; p.NewMrp = it.Mrp; p.NewExpiry = it.Expiry; p.NewCostPrice = it.Cost; p.NewSellingPrice = it.Sell; p.NewStock = (p.NewStock ?? 0m) + it.Qty;
        }

        private static decimal? ParseDecimal(object? v)
        {
            if (v == null) return null;
            if (decimal.TryParse(v.ToString(), out var d)) return d;
            return null;
        }
        private static DateTime? ParseDate(object? v)
        {
            if (v == null) return null;
            if (DateTime.TryParse(v.ToString(), out var d)) return d.Date;
            return null;
        }
        private static bool NullableDateEq(DateTime? a, DateTime? b)
        {
            if (!a.HasValue && !b.HasValue) return true;
            if (a.HasValue != b.HasValue) return false;
            return a!.Value.Date == b!.Value.Date;
        }

        private class PurchaseRow
        {
            public string Barcode { get; set; } = string.Empty;
            public string Batch { get; set; } = string.Empty;
            public decimal Mrp { get; set; }
            public DateTime? Expiry { get; set; }
            public decimal Cost { get; set; }
            public decimal Sell { get; set; }
            public decimal Qty { get; set; }
        }
    }
}
