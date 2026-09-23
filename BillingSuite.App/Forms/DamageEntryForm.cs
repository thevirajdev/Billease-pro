using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BillingSuite.App.Forms
{
    public class DamageEntryForm : Form
    {
        private ComboBox cmbProduct = new ComboBox();
        private ComboBox cmbBatch = new ComboBox();
        private NumericUpDown numQty = new NumericUpDown();
        private TextBox txtReason = new TextBox();
        private Label lblMrp = new Label();
        private Label lblCp = new Label();
        private Label lblExp = new Label();
        private Label lblStock = new Label();
        private Label lblLoss = new Label();
        private Button btnSave = new Button();
        private Button btnCancel = new Button();

        private List<Product> _products;
        private ProductBatch _selectedBatch;

        public DamageEntryForm() : this(null, null) { }
        
        public DamageEntryForm(Product? preselectProduct = null, ProductBatch? preselectBatch = null)
        {
            Text = "Log Damaged Stock";
            Size = new Size(500, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            InitializeUI();
            LoadData();

            if (preselectProduct != null)
            {
                cmbProduct.SelectedValue = preselectProduct.Id;
                if (preselectBatch != null)
                {
                    cmbBatch.SelectedItem = cmbBatch.Items.Cast<ProductBatch>().FirstOrDefault(b => b.Id == preselectBatch.Id);
                }
            }
        }

        private void InitializeUI()
        {
            int y = 20;
            AddRow("Search Product:", cmbProduct, ref y);
            AddRow("Select Batch:", cmbBatch, ref y);
            
            var gb = new GroupBox { Text = "Batch Details", Location = new Point(20, y), Size = new Size(440, 100) };
            gb.Controls.Add(new Label { Text = "MRP:", Location = new Point(15, 25), AutoSize = true });
            lblMrp.SetBounds(60, 25, 100, 20); gb.Controls.Add(lblMrp);
            
            gb.Controls.Add(new Label { Text = "Cost:", Location = new Point(15, 60), AutoSize = true });
            lblCp.SetBounds(60, 60, 100, 20); gb.Controls.Add(lblCp);

            gb.Controls.Add(new Label { Text = "Expiry:", Location = new Point(200, 25), AutoSize = true });
            lblExp.SetBounds(260, 25, 150, 20); gb.Controls.Add(lblExp);

            gb.Controls.Add(new Label { Text = "Stock:", Location = new Point(200, 60), AutoSize = true });
            lblStock.SetBounds(260, 60, 150, 20); gb.Controls.Add(lblStock);
            this.Controls.Add(gb);
            y += 110;

            AddRow("Damage Qty:", numQty, ref y);
            numQty.DecimalPlaces = 2; numQty.Maximum = 999999;
            numQty.ValueChanged += (s, e) => UpdateLoss();

            AddRow("Reason:", txtReason, ref y);
            txtReason.Multiline = true; txtReason.Height = 60; y += 40;

            var pnlLoss = new Panel { BackColor = Color.FromArgb(255, 240, 240), Location = new Point(20, y), Size = new Size(440, 40) };
            pnlLoss.Controls.Add(new Label { Text = "Estimated Loss:", Font = new Font(Font, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true });
            lblLoss.SetBounds(150, 10, 200, 20); lblLoss.Font = new Font(Font, FontStyle.Bold); lblLoss.ForeColor = Color.Red;
            pnlLoss.Controls.Add(lblLoss);
            this.Controls.Add(pnlLoss);
            y += 60;

            btnSave.Text = "Log Damage"; btnSave.SetBounds(240, y, 120, 35);
            btnSave.BackColor = Color.FromArgb(214, 48, 49); btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Click += BtnSave_Click;

            btnCancel.Text = "Cancel"; btnCancel.SetBounds(370, y, 90, 35);
            btnCancel.Click += (s, e) => Close();

            this.Controls.AddRange(new Control[] { btnSave, btnCancel });

            cmbProduct.SelectedIndexChanged += (s, e) => LoadBatches();
            cmbBatch.SelectedIndexChanged += (s, e) => UpdateBatchDetails();
        }

        private void AddRow(string label, Control c, ref int y)
        {
            this.Controls.Add(new Label { Text = label, Location = new Point(20, y + 3), AutoSize = true });
            c.SetBounds(140, y, 320, 25);
            this.Controls.Add(c);
            y += 35;
        }

        private void LoadData()
        {
            using var db = new AppDbContext();
            _products = db.Products.OrderBy(p => p.Name).ToList();
            cmbProduct.DisplayMember = "Name";
            cmbProduct.ValueMember = "Id";
            cmbProduct.DataSource = _products;
            
            cmbProduct.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbProduct.AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        private void LoadBatches()
        {
            if (cmbProduct.SelectedItem is not Product p) return;
            using var db = new AppDbContext();
            var batches = db.ProductBatches.Where(b => b.ProductIdRef == p.Id && (b.Stock ?? 0) > 0).ToList();
            cmbBatch.DisplayMember = "BatchNumber";
            cmbBatch.DataSource = batches;
            if (batches.Count == 0)
            {
                lblMrp.Text = lblCp.Text = lblExp.Text = lblStock.Text = "N/A";
            }
        }

        private void UpdateBatchDetails()
        {
            if (cmbBatch.SelectedItem is not ProductBatch b) return;
            _selectedBatch = b;
            lblMrp.Text = Services.AppSettingsService.Money(b.Mrp ?? 0m);
            lblCp.Text = Services.AppSettingsService.Money(b.CostPrice ?? 0m);
            lblExp.Text = b.Expiry?.ToString("dd/MM/yyyy") ?? "N/A";
            lblStock.Text = b.Stock?.ToString("0.##") ?? "0";
            numQty.Maximum = b.Stock ?? 0;
            UpdateLoss();
        }

        private void UpdateLoss()
        {
            if (_selectedBatch == null) return;
            decimal loss = numQty.Value * (_selectedBatch.CostPrice ?? 0m);
            lblLoss.Text = Services.AppSettingsService.Money(loss);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_selectedBatch == null || numQty.Value <= 0)
            {
                MessageBox.Show("Please select a product/batch and enter quantity.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                // 1. Create DamageRecord
                var damage = new DamagedItem
                {
                    ProductIdRef = _selectedBatch.ProductIdRef,
                    BatchNumber = _selectedBatch.BatchNumber,
                    Quantity = numQty.Value,
                    Reason = txtReason.Text,
                    LossAmount = numQty.Value * (_selectedBatch.CostPrice ?? 0m),
                    Date = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                // 2. Reduce Stock
                var batch = db.ProductBatches.FirstOrDefault(b => b.Id == _selectedBatch.Id);
                if (batch != null)
                {
                    batch.Stock = Math.Max(0, (batch.Stock ?? 0) - numQty.Value);
                }

                db.DamagedItems.Add(damage);
                db.SaveChanges();
                
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to log damage: " + ex.Message);
            }
        }
    }
}
