using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Forms
{
    public class SuppliersForm : Form
    {
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox txtSearch = new TextBox();
        private readonly Button btnAdd = new Button();
        private readonly Button btnEdit = new Button();
        private readonly Button btnPurchase = new Button();
        private readonly Button btnDelete = new Button();
        public SuppliersForm()
        {
            Text = "Suppliers"; StartPosition = FormStartPosition.CenterParent; Size = new Size(1000, 600);
            var top = new Panel { Dock = DockStyle.Top, Height = 42 };
            txtSearch.PlaceholderText = "Search name/email/phone/city/state"; txtSearch.Width = 320; txtSearch.Location = new Point(8, 9);
            btnAdd.Text = "Add"; btnAdd.Location = new Point(340, 8);
            btnEdit.Text = "Edit"; btnEdit.Location = new Point(400, 8);
            btnPurchase.Text = "New Purchase"; btnPurchase.Location = new Point(460, 8); btnPurchase.Width = 100;
            btnDelete.Text = "Delete"; btnDelete.Location = new Point(570, 8);
            top.Controls.Add(txtSearch);
            top.Controls.Add(btnAdd);
            top.Controls.Add(btnEdit);
            top.Controls.Add(btnPurchase);
            top.Controls.Add(btnDelete);
            Controls.Add(top);

            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            try { typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(grid, true, null); } catch { }
            Controls.Add(grid);

            Load += (s, e) => Reload();
            txtSearch.TextChanged += (s, e) => Reload();
            grid.CellDoubleClick += (s, e) => OpenHistoryForSelected();
            btnPurchase.Click += (s, e) => StartNewPurchaseForSelected();
            btnAdd.Click += (s, e) => AddSupplier();
            btnEdit.Click += (s, e) => EditSupplier();
            btnDelete.Click += (s, e) => DeleteSupplier();
            grid.KeyDown += (s, e) => { try { if (e.Control && e.KeyCode == Keys.N) { e.Handled = true; AddSupplier(); } else if (e.KeyCode == Keys.Enter) { e.Handled = true; EditSupplier(); } else if (e.KeyCode == Keys.Delete) { e.Handled = true; DeleteSupplier(); } } catch { } };
        }

        private void Reload()
        {
            using var db = new AppDbContext();
            var t = (txtSearch.Text ?? string.Empty).Trim().ToLowerInvariant();
            var q = db.Suppliers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(t))
            {
                q = q.Where(s => (s.Name != null && s.Name.ToLower().Contains(t)) ||
                                 (s.Email != null && s.Email.ToLower().Contains(t)) ||
                                 (s.Phone != null && s.Phone.ToLower().Contains(t)) ||
                                 (s.City != null && s.City.ToLower().Contains(t)) ||
                                 (s.State != null && s.State.ToLower().Contains(t)) ||
                                 (s.GstNumber != null && s.GstNumber.ToLower().Contains(t)));
            }
            var list = q
                .Select(s => new {
                    s.Id,
                    s.Name,
                    s.Email,
                    s.Phone,
                    s.City,
                    s.State,
                    GST = s.GstNumber,
                    TotalPurchase = (decimal?)db.Purchases.AsNoTracking().Where(p => p.SupplierId == s.Id).Sum(p => (decimal?)p.Total) ?? 0m,
                    BackDues = s.BackDues ?? ((decimal?)db.Purchases.AsNoTracking().Where(p => p.SupplierId == s.Id).Sum(p => (decimal?)(p.Due))) ?? 0m,
                    LastPurchase = db.Purchases.AsNoTracking().Where(p => p.SupplierId == s.Id).OrderByDescending(p => p.PurchaseDate).Select(p => (DateTime?)p.PurchaseDate).FirstOrDefault()
                })
                .OrderBy(r => r.Name)
                .ToList();

            grid.Columns.Clear();
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", Name = "Id", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name", Width = 220 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Email", HeaderText = "Email", Width = 200 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Phone", HeaderText = "Phone", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "City", HeaderText = "City", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "State", HeaderText = "State", Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "GST", HeaderText = "GST", Width = 140 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalPurchase", HeaderText = "Total Purchase", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BackDues", HeaderText = "Back Dues", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastPurchase", HeaderText = "Last Purchase", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd-MMM-yyyy" } });
            grid.DataSource = list;
        }

        private int? GetSelectedSupplierId()
        {
            try
            {
                if (grid.CurrentRow == null) return null;
                var cell = grid.CurrentRow.Cells["Id"];
                if (cell != null && cell.Value is int i) return i;
                // Fallback to DataBoundItem reflection
                var bound = grid.CurrentRow.DataBoundItem;
                var prop = bound?.GetType().GetProperty("Id");
                if (prop != null)
                {
                    var val = prop.GetValue(bound);
                    if (val is int idv) return idv;
                }
            }
            catch { }
            return null;
        }

        private void OpenHistoryForSelected()
        {
            var id = GetSelectedSupplierId(); if (id == null) return;
            using var dlg = new SupplierHistoryForm(id.Value);
            dlg.ShowDialog(this);
        }

        private void StartNewPurchaseForSelected()
        {
            var id = GetSelectedSupplierId();
            using var dlg = new SavePurchaseForm(id);
            dlg.ShowDialog(this);
            Reload();
        }

        private void AddSupplier()
        {
            try
            {
                using var f = new SupplierEditForm();
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    using var db = new AppDbContext();
                    db.Suppliers.Add(f.Model);
                    db.SaveChanges();
                    Reload();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding supplier: " + ex.Message);
            }
        }

        private void EditSupplier()
        {
            try
            {
                var id = GetSelectedSupplierId(); if (id == null) return;
                using var db = new AppDbContext();
                var s = db.Suppliers.FirstOrDefault(x => x.Id == id.Value); if (s == null) return;
                using var f = new SupplierEditForm(s);
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    db.SaveChanges();
                    Reload();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error editing supplier: " + ex.Message);
            }
        }

        private void DeleteSupplier()
        {
            try
            {
                var id = GetSelectedSupplierId();
                if (id == null)
                {
                    MessageBox.Show("Please select a supplier to delete.");
                    return;
                }

                if (MessageBox.Show("Are you sure you want to move this supplier to the Recycle Bin?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                using var db = new AppDbContext();
                var s = db.Suppliers.FirstOrDefault(x => x.Id == id.Value);
                if (s == null) return;

                // Check if supplier has associated purchases
                bool hasPurchases = db.Purchases.Any(p => p.SupplierId == s.Id);
                if (hasPurchases)
                {
                    var proceed = MessageBox.Show(
                        "This supplier has linked purchase bills. Moving the supplier to Recycle Bin will keep past purchase bills intact, but the supplier will no longer appear in active lists.\n\nProceed?",
                        "Linked Purchases Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (proceed != DialogResult.Yes) return;
                }

                try
                {
                    db.RecycleBin.Add(new RecycleBinItem
                    {
                        EntityType = "Supplier",
                        EntityId = s.Id,
                        JsonData = System.Text.Json.JsonSerializer.Serialize(s),
                        DeletedAt = DateTime.UtcNow
                    });
                }
                catch { }

                db.Suppliers.Remove(s);
                db.SaveChanges();
                Reload();
                MessageBox.Show("Supplier moved to Recycle Bin.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error deleting supplier: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
