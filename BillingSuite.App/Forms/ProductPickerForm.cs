using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;

namespace BillingSuite.App.Forms
{
    public class ProductPickerForm : Form
    {
        private readonly TextBox txtSearch = new TextBox();
        private readonly Button btnSearch = new Button();
        private readonly DataGridView grid = new DataGridView();
        private readonly Button btnAdd = new Button();
        private readonly Button btnEdit = new Button();
        private readonly Button btnExit = new Button();
        private readonly Panel pnlSearch = new Panel();
        private readonly Panel pnlBottom = new Panel();
        // removed top toolbar per request
        public Product? SelectedProduct { get; private set; }

        public ProductPickerForm()
        {
            Text = "Select Product";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(800, 520);
            MinimumSize = new Size(700, 450);

            // Top toolbar removed; using bottom panel buttons only

            // Search panel (Top)
            pnlSearch.Dock = DockStyle.Top; pnlSearch.Height = 40;
            txtSearch.Location = new Point(10, 8); txtSearch.Width = 540;
            txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSearch.TextChanged += (s, e) => LoadData();
            btnSearch.Text = "Search"; btnSearch.Location = new Point(560, 6); btnSearch.Size = new Size(80, 27);
            btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSearch.Click += (s, e) => LoadData();
            pnlSearch.Controls.AddRange(new Control[] { txtSearch, btnSearch });

            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.ReadOnly = true;
            grid.AutoGenerateColumns = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Columns.Clear();
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Barcode", HeaderText = "Barcode", DataPropertyName = "Barcode", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 240 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Scheme", DataPropertyName = "Bonus", Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch", DataPropertyName = "BatchNumber", Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry", DataPropertyName = "Expiry", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MRP", DataPropertyName = "Mrp", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Selling", DataPropertyName = "SellingPrice", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last Sale", DataPropertyName = "LastSale", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.CellDoubleClick += (s, e) => SelectCurrent();
            grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; SelectCurrent(); } };

            // Bottom panel (Bottom)
            pnlBottom.Dock = DockStyle.Bottom; pnlBottom.Height = 44;
            btnAdd.Text = "Add Product"; btnAdd.Location = new Point(10, 7); btnAdd.Size = new Size(110, 30);
            btnAdd.Click += (s, e) => AddProduct();
            btnEdit.Text = "Edit Product"; btnEdit.Location = new Point(130, 7); btnEdit.Size = new Size(110, 30);
            btnEdit.Click += (s, e) => EditSelectedProduct();
            btnExit.Text = "Exit"; btnExit.Size = new Size(90, 30); btnExit.Anchor = AnchorStyles.Top | AnchorStyles.Right; btnExit.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            pnlBottom.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnExit });
            pnlBottom.Resize += (s, e) => { btnExit.Left = pnlBottom.Width - btnExit.Width - 10; btnExit.Top = 7; };
            pnlBottom.Visible = true;

            // Docking order matters: add Fill first, then Bottom, then Top
            Controls.Add(grid);
            Controls.Add(pnlBottom);
            Controls.Add(pnlSearch);
            Load += (s, e) => LoadData();
        }

        private void LoadData()
        {
            using var db = new AppDbContext();
            var term = txtSearch.Text?.Trim().ToLowerInvariant();
            System.Collections.Generic.List<dynamic> rows;
            try
            {
                var baseQuery = from p in db.Products
                                join b in db.ProductBatches on p.Id equals b.ProductIdRef into pb
                                from b in pb.DefaultIfEmpty()
                                select new { p.Barcode, p.Name, p.Category, Bonus = b != null ? b.Bonus : null, BatchNumber = b != null ? b.BatchNumber : null, Expiry = b != null ? b.Expiry : null, Mrp = b != null ? b.Mrp : null, SellingPrice = b != null ? b.SellingPrice : null };
                if (!string.IsNullOrWhiteSpace(term))
                {
                    baseQuery = baseQuery.Where(x => (x.Name != null && x.Name.ToLower().Contains(term)) || (x.Barcode != null && x.Barcode.ToLower().Contains(term)) || (x.Bonus != null && x.Bonus.ToLower().Contains(term)) || (x.BatchNumber != null && x.BatchNumber.ToLower().Contains(term)));
                }
                var list = baseQuery.OrderBy(x => x.Name).ToList();
                rows = new System.Collections.Generic.List<dynamic>(list);
            }
            catch
            {
                // Fallback to legacy fields if ProductBatches is missing
                var pquery = db.Products.AsQueryable();
                if (!string.IsNullOrWhiteSpace(term))
                {
                    pquery = pquery.Where(p => (p.Name != null && p.Name.ToLower().Contains(term)) || (p.Barcode != null && p.Barcode.ToLower().Contains(term)) || (p.Category != null && p.Category.ToLower().Contains(term)) || (p.OldBatch != null && p.OldBatch.ToLower().Contains(term)) || (p.NewBatch != null && p.NewBatch.ToLower().Contains(term)));
                }
                rows = new System.Collections.Generic.List<dynamic>();
                foreach (var p in pquery.OrderBy(p => p.Name))
                {
                    if (!string.IsNullOrWhiteSpace(p.OldBatch)) rows.Add(new { p.Barcode, p.Name, p.Category, Bonus = (string?)null, BatchNumber = p.OldBatch, Expiry = p.OldExpiry, Mrp = p.OldMrp, SellingPrice = p.OldSellingPrice });
                    if (!string.IsNullOrWhiteSpace(p.NewBatch)) rows.Add(new { p.Barcode, p.Name, p.Category, Bonus = (string?)null, BatchNumber = p.NewBatch, Expiry = p.NewExpiry, Mrp = p.NewMrp, SellingPrice = p.NewSellingPrice });
                }
            }
            grid.DataSource = rows;
        }

        private void SelectCurrent()
        {
            if (grid.CurrentRow?.DataBoundItem != null)
            {
                // Keep original behavior: return product regardless of batch row
                var barcode = grid.CurrentRow.Cells["Barcode"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    using var db = new AppDbContext();
                    var p = db.Products.FirstOrDefault(x => x.Barcode == barcode);
                    if (p == null) return;
                    SelectedProduct = p;
                }
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void AddProduct()
        {
            using var dlg = new ProductEditForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                using var db = new AppDbContext();
                db.Products.Add(dlg.Model);
                db.SaveChanges();
                LoadData();
                // select newly added
                foreach (DataGridViewRow r in grid.Rows)
                {
                    if (r.Cells["Barcode"].Value?.ToString() == dlg.Model.Barcode)
                    { grid.ClearSelection(); r.Selected = true; grid.CurrentCell = r.Cells[0]; break; }
                }
            }
        }

        private void EditSelectedProduct()
        {
            if (grid.CurrentRow?.DataBoundItem is not Product p) { MessageBox.Show("Select a product to edit."); return; }
            using var dlg = new ProductEditForm(new Product
            {
                Id = p.Id,
                Barcode = p.Barcode,
                Name = p.Name,
                Description = p.Description,
                Category = p.Category,
                OldBatch = p.OldBatch
            });
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                using var db = new AppDbContext();
                var entity = db.Products.FirstOrDefault(x => x.Id == p.Id);
                if (entity != null)
                {
                    entity.Barcode = dlg.Model.Barcode;
                    entity.Name = dlg.Model.Name;
                    entity.Description = dlg.Model.Description;
                    entity.Category = dlg.Model.Category;
                    entity.OldBatch = dlg.Model.OldBatch;
                    entity.OldMrp = dlg.Model.OldMrp;
                    entity.OldExpiry = dlg.Model.OldExpiry;
                    entity.OldCostPrice = dlg.Model.OldCostPrice;
                    entity.OldSellingPrice = dlg.Model.OldSellingPrice;
                    entity.OldStock = dlg.Model.OldStock;
                    entity.NewBatch = dlg.Model.NewBatch;
                    entity.NewMrp = dlg.Model.NewMrp;
                    entity.NewExpiry = dlg.Model.NewExpiry;
                    entity.NewCostPrice = dlg.Model.NewCostPrice;
                    entity.NewSellingPrice = dlg.Model.NewSellingPrice;
                    entity.NewStock = dlg.Model.NewStock;
                    db.SaveChanges();
                }
                LoadData();
            }
        }
    }
}
