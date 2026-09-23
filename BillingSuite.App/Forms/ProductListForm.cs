using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;

namespace BillingSuite.App.Forms
{
    public class ProductListForm : Form
    {
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox txtSearch = new TextBox();
        private readonly Button btnRefresh = new Button();

        public ProductListForm()
        {
            Text = "Products";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 600);
            MinimumSize = new Size(900, 520);

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 40 };
            txtSearch.SetBounds(8, 8, 300, 24);
            txtSearch.TextChanged += (s, e) => LoadData();
            btnRefresh.Text = "Refresh"; btnRefresh.SetBounds(320, 8, 90, 24);
            btnRefresh.Click += (s, e) => LoadData();
            
            var btnEdit = new Button { Text = "Edit Product", Bounds = new Rectangle(420, 8, 100, 24), BackColor = Color.FromArgb(9, 132, 227), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnEdit.Click += (s, e) => EditProductAtSelectedRow();

            var btnDamage = new Button { 
                Text = "Log Damage", 
                Bounds = new Rectangle(530, 8, 120, 24), 
                BackColor = Color.FromArgb(214, 48, 49), 
                ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat 
            };
            btnDamage.Click += (s, e) => {
                using var dlg = new DamageEntryForm();
                if (dlg.ShowDialog(this) == DialogResult.OK) LoadData();
            };

            pnlTop.Controls.AddRange(new Control[] { txtSearch, btnRefresh, btnEdit, btnDamage });

            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditProductAtSelectedRow(); };
            grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; EditProductAtSelectedRow(); } };
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns.Clear();
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HSN", DataPropertyName = "Hsn", FillWeight = 10, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", FillWeight = 22, MinimumWidth = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch No.", DataPropertyName = "BatchNumber", FillWeight = 12, MinimumWidth = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Category", DataPropertyName = "Category", FillWeight = 14, MinimumWidth = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry", DataPropertyName = "Expiry", FillWeight = 10, MinimumWidth = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MRP", DataPropertyName = "Mrp", FillWeight = 8, MinimumWidth = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cost Price", DataPropertyName = "CostPrice", FillWeight = 8, MinimumWidth = 80, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Selling Price", DataPropertyName = "SellingPrice", FillWeight = 8, MinimumWidth = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stock", DataPropertyName = "Stock", FillWeight = 8, MinimumWidth = 70, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bonus", DataPropertyName = "Bonus", FillWeight = 8, MinimumWidth = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pack", DataPropertyName = "Pack", FillWeight = 8, MinimumWidth = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mkt.", DataPropertyName = "MarketedBy", FillWeight = 12, MinimumWidth = 120 });

            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditProductAtSelectedRow(); };
            Controls.Add(grid);
            Controls.Add(pnlTop);

            Load += (s, e) => LoadData();
        }

        private void EditProductAtSelectedRow()
        {
            try
            {
                if (grid.CurrentRow == null) return;
                var rowObj = grid.CurrentRow.DataBoundItem;
                if (rowObj == null) return;

                // Use reflection to get Name and BatchNumber from anonymous object
                var type = rowObj.GetType();
                var name = type.GetProperty("Name")?.GetValue(rowObj)?.ToString();
                var batch = type.GetProperty("BatchNumber")?.GetValue(rowObj)?.ToString();
                if (string.IsNullOrEmpty(name)) return;

                using var db = new AppDbContext();
                var prod = db.Products.FirstOrDefault(p => p.Name.ToLower() == name.ToLower());
                if (prod == null) return;

                using var dlg = new ProductEditForm(prod);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadData(name, batch);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error editing product: " + ex.Message); }
        }

        private void LoadData(string selectedName = null, string selectedBatch = null)
        {
            try
            {
                using var db = new AppDbContext();
                var q = from p in db.Products
                        join b in db.ProductBatches on p.Id equals b.ProductIdRef
                        select new
                        {
                            p.Hsn,
                            p.Name,
                            p.Category,
                            BatchNumber = b.BatchNumber,
                            Expiry = b.Expiry,
                            Mrp = b.Mrp,
                            CostPrice = b.CostPrice,
                            SellingPrice = b.SellingPrice,
                            Stock = b.Stock,
                            Bonus = b.Bonus,
                            Pack = b.Pack,
                            MarketedBy = b.MarketedBy
                        };
                var term = txtSearch.Text?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(term))
                {
                    q = q.Where(x => (x.Name != null && x.Name.ToLower().Contains(term)) ||
                                     (x.Hsn != null && x.Hsn.ToLower().Contains(term)) ||
                                     (x.Category != null && x.Category.ToLower().Contains(term)) ||
                                     (x.BatchNumber != null && x.BatchNumber.ToLower().Contains(term)));
                }
                var data = q.OrderBy(x => x.Name).ThenBy(x => x.BatchNumber).ToList();
                grid.DataSource = null;
                grid.DataSource = data;
                grid.Refresh();

                // Restore selection
                if (!string.IsNullOrEmpty(selectedName))
                {
                    foreach (DataGridViewRow row in grid.Rows)
                    {
                        var r = row.DataBoundItem;
                        if (r == null) continue;
                        var rType = r.GetType();
                        var rName = rType.GetProperty("Name")?.GetValue(r)?.ToString();
                        var rBatch = rType.GetProperty("BatchNumber")?.GetValue(r)?.ToString();

                        if (string.Equals(rName, selectedName, StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrEmpty(selectedBatch) || string.Equals(rBatch, selectedBatch, StringComparison.OrdinalIgnoreCase)))
                        {
                            grid.ClearSelection();
                            row.Selected = true;
                            if (grid.Columns.Count > 0) grid.CurrentCell = row.Cells[0];
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading products: " + ex.Message);
            }
        }
    }
}
