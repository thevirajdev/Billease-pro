using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;

namespace BillingSuite.App.Forms
{
    public class CustomerQuickDialog : Form
    {
        private readonly TextBox txtName = new TextBox();
        private readonly ListBox lst = new ListBox();
        private readonly Button btnOk = new Button();
        private readonly Button btnAdd = new Button();
        private readonly Button btnCancel = new Button();

        private List<Customer> all = new List<Customer>();
        private List<Customer> filtered = new List<Customer>();

        public string? CustomerName => string.IsNullOrWhiteSpace(txtName.Text) ? null : txtName.Text.Trim();
        public int? SelectedCustomerId { get; private set; }

        public CustomerQuickDialog()
        {
            Text = "Customer";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(420, 380);
            MinimumSize = new Size(400, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.W) { e.Handled = true; DialogResult = DialogResult.Cancel; Close(); } };

            var lbl = new Label { Text = "Type to search or add new:", AutoSize = true, Location = new Point(12, 12) };
            txtName.Location = new Point(15, 36); txtName.Width = 380; txtName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtName.TextChanged += (s, e) => ApplyFilter();
            txtName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    if (lst.Items.Count > 0)
                    {
                        lst.SelectedIndex = 0;
                        TrySelectFromList();
                    }
                }
            };

            lst.Location = new Point(15, 68); lst.Size = new Size(380, 220);
            lst.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            lst.DisplayMember = nameof(Customer.Name);
            lst.ValueMember = nameof(Customer.Id);
            lst.DoubleClick += (s, e) => { TrySelectFromList(); };
            lst.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; TrySelectFromList(); } };

            btnOk.Text = "Select"; btnOk.Location = new Point(200, 300); btnOk.Size = new Size(80, 30); btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Click += (s, e) => { TrySelectFromList(); };
            btnAdd.Text = "Add New"; btnAdd.Location = new Point(290, 300); btnAdd.Size = new Size(90, 30); btnAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAdd.Click += (s, e) =>
            {
                var typed = CustomerName;
                using var addDlg = new CustomerEditForm(new Customer { Name = typed ?? string.Empty });
                if (addDlg.ShowDialog(this) == DialogResult.OK && addDlg.Model != null)
                {
                    // Newly created/updated customer is saved in DB inside CustomerEditForm
                    SelectedCustomerId = addDlg.Model.Id;
                    txtName.Text = addDlg.Model.Name ?? string.Empty;
                    // refresh list so it contains the new customer
                    LoadCustomers();
                    lst.SelectedValue = SelectedCustomerId;
                    this.DialogResult = DialogResult.OK;
                }
            };
            btnCancel.Text = "Cancel"; btnCancel.Location = new Point(110, 300); btnCancel.Size = new Size(80, 30); btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; btnCancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] { lbl, txtName, lst, btnCancel, btnOk, btnAdd });
            AcceptButton = btnOk; CancelButton = btnCancel;
            Shown += (s, e) => { LoadCustomers(); txtName.Focus(); txtName.SelectAll(); };
        }

        private void LoadCustomers()
        {
            try
            {
                using var db = new AppDbContext();
                all = db.Customers.AsQueryable().OrderBy(c => c.Name).ToList();
            }
            catch { all = new List<Customer>(); }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var term = (txtName.Text ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(term)) filtered = all.ToList();
            else filtered = all.Where(c => (c.Name ?? string.Empty).ToLowerInvariant().Contains(term)).ToList();
            lst.DataSource = null;
            lst.DataSource = filtered;
            lst.DisplayMember = nameof(Customer.Name);
            lst.ValueMember = nameof(Customer.Id);
            // Preselect exact match if exists
            var exact = filtered.FirstOrDefault(c => string.Equals(c.Name?.Trim(), txtName.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                lst.SelectedValue = exact.Id;
            }
        }

        private void TrySelectFromList()
        {
            if (lst.SelectedItem is Customer c)
            {
                SelectedCustomerId = c.Id;
                txtName.Text = c.Name ?? string.Empty;
                this.DialogResult = DialogResult.OK;
                return;
            }
            // No selection: do nothing (user must click Add New to create)
        }
    }
}
