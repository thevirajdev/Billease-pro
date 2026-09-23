using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Data;

namespace BillingSuite.App.Forms
{
    public class SupplierEditForm : Form
    {
        private TextBox txtName = new TextBox();
        private TextBox txtEmail = new TextBox();
        private TextBox txtPhone = new TextBox();
        private TextBox txtCity = new TextBox();
        private TextBox txtState = new TextBox();
        private TextBox txtGst = new TextBox();
        private NumericUpDown numBack = new NumericUpDown();
        private Button btnOk = new Button();
        private Button btnCancel = new Button();

        public Supplier Model { get; private set; }

        public SupplierEditForm(Supplier? supplier = null)
        {
            Model = supplier ?? new Supplier();
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = Model.Id == 0 ? "Add Supplier" : "Edit Supplier";
            this.Size = new Size(450, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            TableLayoutPanel tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(20),
                AutoSize = true
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            void AddRow(string label, Control control, int row)
            {
                tlp.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9F) }, 0, row);
                control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                control.Font = new Font("Segoe UI", 9F);
                tlp.Controls.Add(control, 1, row);
            }

            AddRow("Name *", txtName, 0);
            AddRow("Email", txtEmail, 1);
            AddRow("Phone", txtPhone, 2);
            AddRow("City", txtCity, 3);
            AddRow("State", txtState, 4);
            AddRow("GST", txtGst, 5);
            
            numBack.DecimalPlaces = 2;
            numBack.Maximum = 100000000;
            AddRow("Back Dues", numBack, 6);

            FlowLayoutPanel flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 20, 0)
            };

            btnOk.Text = "Save";
            btnOk.Size = new Size(100, 35);
            btnOk.BackColor = Color.FromArgb(0, 122, 204);
            btnOk.ForeColor = Color.White;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.Click += BtnOk_Click;

            btnCancel.Text = "Cancel";
            btnCancel.Size = new Size(100, 35);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.DialogResult = DialogResult.Cancel;

            flp.Controls.Add(btnCancel);
            flp.Controls.Add(btnOk);

            this.Controls.Add(tlp);
            this.Controls.Add(flp);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void LoadData()
        {
            if (Model.Id != 0)
            {
                txtName.Text = Model.Name;
                txtEmail.Text = Model.Email;
                txtPhone.Text = Model.Phone;
                txtCity.Text = Model.City;
                txtState.Text = Model.State;
                txtGst.Text = Model.GstNumber;
                numBack.Value = Model.BackDues ?? 0m;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Supplier Name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var db = new AppDbContext())
            {
                var nameText = txtName.Text.Trim();
                if (db.Suppliers.Any(s => s.Name.ToLower() == nameText.ToLower() && s.Id != Model.Id))
                {
                    MessageBox.Show("A supplier with this name already exists.", "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            Model.Name = txtName.Text.Trim();
            Model.Email = txtEmail.Text.Trim();
            Model.Phone = txtPhone.Text.Trim();
            Model.City = txtCity.Text.Trim();
            Model.State = txtState.Text.Trim();
            Model.GstNumber = txtGst.Text.Trim();
            Model.BackDues = numBack.Value;
            Model.UpdatedAt = DateTime.UtcNow;
            if (Model.Id == 0) Model.CreatedAt = DateTime.UtcNow;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
