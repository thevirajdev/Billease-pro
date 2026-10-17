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
            Services.FormIconHelper.ApplyIcon(this);
            Text = "Customer";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(420, 380);
            MinimumSize = new Size(400, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.W) { e.Handled = true; DialogResult = DialogResult.Cancel; Close(); } };

            var lbl = new Label { Text = "Type to search or add new:", AutoSize = true, Location = new Point(12, 12) };
            txtName.TabIndex = 0;
            txtName.Location = new Point(15, 36); txtName.Width = 380; txtName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtName.TextChanged += (s, e) => ApplyFilter();
            txtName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    if (lst.Items.Count > 0)
                    {
                        lst.Focus();
                        if (lst.SelectedIndex < lst.Items.Count - 1)
                            lst.SelectedIndex++;
                        else if (lst.SelectedIndex < 0)
                            lst.SelectedIndex = 0;
                    }
                }
                else if (e.KeyCode == Keys.Up)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    if (lst.Items.Count > 0 && lst.SelectedIndex > 0)
                    {
                        lst.Focus();
                        lst.SelectedIndex--;
                    }
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    ConfirmSelectionOrAdd();
                }
            };

            lst.TabIndex = 1;
            lst.Location = new Point(15, 68); lst.Size = new Size(380, 220);
            lst.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            lst.DisplayMember = nameof(Customer.Name);
            lst.ValueMember = nameof(Customer.Id);
            lst.DoubleClick += (s, e) => { ConfirmSelectionOrAdd(); };
            lst.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    ConfirmSelectionOrAdd();
                }
                else if (e.KeyCode == Keys.Up && lst.SelectedIndex <= 0)
                {
                    e.Handled = true;
                    txtName.Focus();
                    txtName.SelectionStart = txtName.Text.Length;
                }
            };
            lst.Enter += (s, e) =>
            {
                if (lst.Items.Count > 0 && lst.SelectedIndex < 0)
                {
                    lst.SelectedIndex = 0;
                }
            };

            btnOk.TabIndex = 2;
            btnOk.Text = "Select"; btnOk.Location = new Point(200, 300); btnOk.Size = new Size(80, 30); btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Click += (s, e) => { ConfirmSelectionOrAdd(); };
            btnAdd.TabIndex = 3;
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
            btnCancel.TabIndex = 4;
            btnCancel.Text = "Cancel"; btnCancel.Location = new Point(110, 300); btnCancel.Size = new Size(80, 30); btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; btnCancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] { lbl, txtName, lst, btnCancel, btnOk, btnAdd });
            CancelButton = btnCancel;
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

            if (filtered.Count > 0)
            {
                var exact = filtered.FirstOrDefault(c => string.Equals(c.Name?.Trim(), txtName.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    lst.SelectedValue = exact.Id;
                }
                else
                {
                    lst.SelectedIndex = -1; // Keep unselected while typing; user can use arrow keys or click to select
                }
            }
            else
            {
                lst.SelectedIndex = -1;
            }
        }

        private void ConfirmSelectionOrAdd()
        {
            var typed = (txtName.Text ?? string.Empty).Trim();

            // Priority 1: User explicitly selected a customer from the list (via click or arrow keys)
            if (lst.SelectedIndex >= 0 && lst.SelectedItem is Customer selC)
            {
                SelectedCustomerId = selC.Id;
                txtName.Text = selC.Name ?? string.Empty;
                this.DialogResult = DialogResult.OK;
                return;
            }

            // Priority 2: Typed text is an exact match for an existing customer
            if (!string.IsNullOrEmpty(typed))
            {
                var exact = all.FirstOrDefault(c => string.Equals(c.Name?.Trim(), typed, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    SelectedCustomerId = exact.Id;
                    txtName.Text = exact.Name ?? string.Empty;
                    this.DialogResult = DialogResult.OK;
                    return;
                }
            }

            // Do not auto-select random filtered customer and do not auto-create on arbitrary typing!
            MessageBox.Show(this,
                string.IsNullOrEmpty(typed)
                    ? "Please select a customer from the list or click 'Add New' to register a new customer."
                    : $"Customer '{typed}' is not in your customer list.\n\nPlease select an existing customer from the list or click 'Add New' below to register them.",
                "Customer Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtName.Focus();
        }
    }
}
