using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Forms
{
    public class ExpenseForm : Form
    {
        private readonly DataGridView gridExpenses = new DataGridView();
        private readonly DataGridView gridLosses = new DataGridView();
        private readonly TabControl tabs = new TabControl();
        private readonly Button btnAdd = new Button();
        private readonly Button btnDelete = new Button();
        private readonly Label lblTotalExpenses = new Label();
        private readonly Label lblTotalStockLoss = new Label();
        private readonly Label lblGrandTotal = new Label();

        public ExpenseForm()
        {
            Text = "Business Expense & Stock Loss Management";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            Size = new Size(1100, 750);

            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI", 10F);
            var tpManual = new TabPage("Business Expenses (Salary, Transport, etc.)");
            var tpLoss = new TabPage("Stock Losses (Expired/Damaged)");

            SetupManualTab(tpManual);
            SetupLossTab(tpLoss);

            tabs.TabPages.Add(tpManual);
            tabs.TabPages.Add(tpLoss);

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(240, 240, 240), Padding = new Padding(10) };
            lblTotalExpenses.AutoSize = true; lblTotalExpenses.Location = new Point(20, 10); lblTotalExpenses.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lblTotalStockLoss.AutoSize = true; lblTotalStockLoss.Location = new Point(20, 32); lblTotalStockLoss.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            
            lblGrandTotal.AutoSize = true; 
            lblGrandTotal.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            lblGrandTotal.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblGrandTotal.ForeColor = Color.DarkRed;
            lblGrandTotal.TextAlign = ContentAlignment.MiddleRight;
            lblGrandTotal.Location = new Point(pnlBottom.Width - 400, 15); // Initial position, anchor will keep it right
            lblGrandTotal.Width = 380; // Fixed width to prevent jumping
            lblGrandTotal.AutoSize = false;

            pnlBottom.Controls.AddRange(new Control[] { lblTotalExpenses, lblTotalStockLoss, lblGrandTotal });

            Controls.Add(tabs);
            Controls.Add(pnlBottom);

            Load += (s, e) => { ReloadManual(); ReloadLosses(); };
        }

        private void SetupManualTab(TabPage tp)
        {
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(10) };
            btnAdd.Text = "➕ Add Expense"; btnAdd.Size = new Size(130, 35); btnAdd.Location = new Point(10, 10); btnAdd.FlatStyle = FlatStyle.Flat; btnAdd.BackColor = Color.LightBlue; btnAdd.Click += (s, e) => AddExpense();
            btnDelete.Text = "🗑️ Delete Selected"; btnDelete.Size = new Size(130, 35); btnDelete.Location = new Point(150, 10); btnDelete.FlatStyle = FlatStyle.Flat; btnDelete.Click += (s, e) => DeleteExpense();
            pnlTop.Controls.AddRange(new Control[] { btnAdd, btnDelete });

            gridExpenses.Dock = DockStyle.Fill; gridExpenses.ReadOnly = true; gridExpenses.RowHeadersVisible = false;
            gridExpenses.AllowUserToAddRows = false; gridExpenses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridExpenses.AutoGenerateColumns = false;
            gridExpenses.BackgroundColor = Color.White;
            gridExpenses.BorderStyle = BorderStyle.None;
            gridExpenses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "Date", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            gridExpenses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Category", DataPropertyName = "Category", Width = 150 });
            gridExpenses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gridExpenses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Amount", DataPropertyName = "Amount", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            
            tp.Controls.Add(gridExpenses); tp.Controls.Add(pnlTop);
        }

        private void SetupLossTab(TabPage tp)
        {
            gridLosses.Dock = DockStyle.Fill; gridLosses.ReadOnly = true; gridLosses.RowHeadersVisible = false;
            gridLosses.AllowUserToAddRows = false; gridLosses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridLosses.AutoGenerateColumns = false;
            gridLosses.BackgroundColor = Color.White;
            gridLosses.BorderStyle = BorderStyle.None;
            gridLosses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "Date", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            gridLosses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", DataPropertyName = "Type", Width = 120 });
            gridLosses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product/Item Details", DataPropertyName = "Details", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gridLosses.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Valuation/Loss", DataPropertyName = "Amount", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });

            tp.Controls.Add(gridLosses);
        }

        private decimal _manualTotal = 0;
        private decimal _autoTotal = 0;

        private void UpdateSummary()
        {
            lblTotalExpenses.Text = $"Business Expenses Total: {Services.AppSettingsService.Money(_manualTotal)}";
            lblTotalStockLoss.Text = $"Stock Losses Total: {Services.AppSettingsService.Money(_autoTotal)}";
            lblGrandTotal.Text = $"GRAND TOTAL EXPENSE: {Services.AppSettingsService.Money(_manualTotal + _autoTotal)}";
        }

        private void ReloadManual()
        {
            try
            {
                using var db = new AppDbContext();
                var list = db.Expenses.OrderByDescending(x => x.Date).ToList();
                gridExpenses.DataSource = list;
                _manualTotal = list.Sum(x => x.Amount);
                UpdateSummary();
            }
            catch { }
        }

        private void ReloadLosses()
        {
            try
            {
                using var db = new AppDbContext();
                var losses = new List<LossItem>();

                // Losses from Damaged Items
                var damages = db.DamagedItems.Include(d => d.Product).ToList();
                foreach (var d in damages)
                {
                    losses.Add(new LossItem
                    {
                        Date = d.Date,
                        Type = "Damaged",
                        Details = $"{d.Product?.Name} ({d.BatchNumber}) - Qty: {d.Quantity} - Note: {d.Reason}",
                        Amount = d.LossAmount
                    });
                }

                // Losses from Expired Products
                var today = DateTime.Today;
                var expired = db.ProductBatches.Include(b => b.Product)
                    .Where(b => b.Expiry < today || b.ExpiredStock > 0).ToList();
                foreach (var b in expired)
                {
                    decimal stock = (b.ExpiredStock ?? 0) > 0 ? b.ExpiredStock.Value : (b.Stock ?? 0);
                    if (stock <= 0) continue;

                    losses.Add(new LossItem
                    {
                        Date = b.Expiry ?? today,
                        Type = "Expired",
                        Details = $"{b.Product?.Name} ({b.BatchNumber}) - Expired Qty: {stock}",
                        Amount = stock * (b.CostPrice ?? 0m)
                    });
                }

                var list = losses.OrderByDescending(x => x.Date).ToList();
                gridLosses.DataSource = list;
                _autoTotal = list.Sum(x => x.Amount);
                UpdateSummary();
            }
            catch { }
        }

        private void AddExpense()
        {
            using var dlg = new ExpenseEditDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                using var db = new AppDbContext();
                db.Expenses.Add(new Expense
                {
                    Description = dlg.Description,
                    Amount = dlg.Amount,
                    Category = dlg.Category,
                    Date = dlg.Date,
                    Note = dlg.Note
                });
                db.SaveChanges();
                ReloadManual();
            }
        }

        private void DeleteExpense()
        {
            if (gridExpenses.CurrentRow == null) return;
            if (MessageBox.Show("Move this expense to Recycle Bin?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            
            var expense = (Expense)gridExpenses.CurrentRow.DataBoundItem;
            using var db = new AppDbContext();
            var dbExp = db.Expenses.Find(expense.Id);
            if (dbExp != null)
            {
                try
                {
                    db.RecycleBin.Add(new RecycleBinItem
                    {
                        EntityType = "Expense",
                        EntityId = dbExp.Id,
                        JsonData = System.Text.Json.JsonSerializer.Serialize(dbExp),
                        DeletedAt = DateTime.UtcNow
                    });
                }
                catch { }

                db.Expenses.Remove(dbExp);
                db.SaveChanges();
                ReloadManual();
            }
        }

        private class LossItem
        {
            public DateTime Date { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }

        private class ExpenseEditDialog : Form
        {
            private TextBox txtDesc = new TextBox() { Width = 250 };
            private TextBox txtAmount = new TextBox() { Width = 100 };
            private ComboBox cboCategory = new ComboBox() { Width = 150 };
            private DateTimePicker dtp = new DateTimePicker() { Width = 150 };
            private TextBox txtNote = new TextBox() { Width = 250, Multiline = true, Height = 60 };
            private Button btnOk = new Button() { Text = "Save", DialogResult = DialogResult.OK };
            private Button btnCancel = new Button() { Text = "Cancel", DialogResult = DialogResult.Cancel };

            public string Description => txtDesc.Text;
            public decimal Amount => decimal.TryParse(txtAmount.Text, out var v) ? v : 0m;
            public string Category => cboCategory.Text;
            public DateTime Date => dtp.Value;
            public string Note => txtNote.Text;

            public ExpenseEditDialog()
            {
                Text = "Add Expense";
                Size = new Size(400, 350);
                StartPosition = FormStartPosition.CenterParent;
                
                cboCategory.Items.AddRange(new object[] { "Salary", "Transport", "Rent", "Utility", "Other" });
                cboCategory.SelectedIndex = 0;

                var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20) };
                tlp.Controls.Add(new Label { Text = "Category", AutoSize = true }, 0, 0);
                tlp.Controls.Add(cboCategory, 1, 0);
                tlp.Controls.Add(new Label { Text = "Description", AutoSize = true }, 0, 1);
                tlp.Controls.Add(txtDesc, 1, 1);
                tlp.Controls.Add(new Label { Text = "Amount", AutoSize = true }, 0, 2);
                tlp.Controls.Add(txtAmount, 1, 2);
                tlp.Controls.Add(new Label { Text = "Date", AutoSize = true }, 0, 3);
                tlp.Controls.Add(dtp, 1, 3);
                tlp.Controls.Add(new Label { Text = "Note", AutoSize = true }, 0, 4);
                tlp.Controls.Add(txtNote, 1, 4);

                var flp = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50 };
                flp.Controls.AddRange(new Control[] { btnCancel, btnOk });

                Controls.Add(tlp);
                Controls.Add(flp);
            }
        }
    }
}
