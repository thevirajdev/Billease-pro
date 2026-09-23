using System;
using System.Drawing;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Data;
using System.Linq;

namespace BillingSuite.App.Forms
{
    public class CustomerEditForm : Form
    {
        private readonly TextBox txtName = new TextBox();
        private readonly TextBox txtEmail = new TextBox();
        private readonly TextBox txtPhone = new TextBox();
        private readonly TextBox txtAddress1 = new TextBox();
        private readonly TextBox txtAddress2 = new TextBox();
        private readonly TextBox txtCity = new TextBox();
        private readonly TextBox txtState = new TextBox();
        private readonly TextBox txtPostal = new TextBox();
        private readonly TextBox txtCountry = new TextBox();
        private readonly TextBox txtGst = new TextBox();
        private readonly Button btnOk = new Button();
        private readonly Button btnCancel = new Button();

        public Customer Model { get; private set; }

        public CustomerEditForm(Customer? model = null)
        {
            Text = model == null ? "Add Customer" : "Edit Customer";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Model = model ?? new Customer();

            int y = 20;
            Controls.Add(MakeLabel("Name", 20, y)); txtName.SetBounds(140, y-2, 440, 23); txtName.Text = Model.Name; Controls.Add(txtName); y+=32;
            Controls.Add(MakeLabel("Email", 20, y)); txtEmail.SetBounds(140, y-2, 440, 23); txtEmail.Text = Model.Email; Controls.Add(txtEmail); y+=32;
            Controls.Add(MakeLabel("Phone", 20, y)); txtPhone.SetBounds(140, y-2, 440, 23); txtPhone.Text = Model.Phone; Controls.Add(txtPhone); y+=32;
            Controls.Add(MakeLabel("Address 1", 20, y)); txtAddress1.SetBounds(140, y-2, 440, 23); txtAddress1.Text = Model.Address1; Controls.Add(txtAddress1); y+=32;
            Controls.Add(MakeLabel("Address 2", 20, y)); txtAddress2.SetBounds(140, y-2, 440, 23); txtAddress2.Text = Model.Address2; Controls.Add(txtAddress2); y+=32;
            Controls.Add(MakeLabel("City", 20, y)); txtCity.SetBounds(140, y-2, 200, 23); txtCity.Text = Model.City; Controls.Add(txtCity);
            Controls.Add(MakeLabel("State", 360, y)); txtState.SetBounds(420, y-2, 160, 23); txtState.Text = Model.State; Controls.Add(txtState); y+=32;
            Controls.Add(MakeLabel("Postal", 20, y)); txtPostal.SetBounds(140, y-2, 200, 23); txtPostal.Text = Model.PostalCode; Controls.Add(txtPostal);
            Controls.Add(MakeLabel("Country", 360, y)); txtCountry.SetBounds(420, y-2, 160, 23); txtCountry.Text = Model.Country; Controls.Add(txtCountry); y+=32;
            Controls.Add(MakeLabel("GST/VAT", 20, y)); txtGst.SetBounds(140, y-2, 200, 23); txtGst.Text = Model.GstVatNumber; Controls.Add(txtGst); y+=40;

            btnOk.Text = "OK"; btnOk.Location = new Point(400, 430); btnOk.Click += BtnOk_Click;
            btnCancel.Text = "Cancel"; btnCancel.Location = new Point(495, 430); btnCancel.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { btnOk, btnCancel });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private static Label MakeLabel(string text, int x, int y) => new Label { Text = text, Location = new Point(x, y), AutoSize = true };

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Name is required.");
                return;
            }

            Model.Name = txtName.Text.Trim();
            Model.Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim();
            Model.Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text.Trim();
            Model.Address1 = string.IsNullOrWhiteSpace(txtAddress1.Text) ? null : txtAddress1.Text.Trim();
            Model.Address2 = string.IsNullOrWhiteSpace(txtAddress2.Text) ? null : txtAddress2.Text.Trim();
            Model.City = string.IsNullOrWhiteSpace(txtCity.Text) ? null : txtCity.Text.Trim();
            Model.State = string.IsNullOrWhiteSpace(txtState.Text) ? null : txtState.Text.Trim();
            Model.PostalCode = string.IsNullOrWhiteSpace(txtPostal.Text) ? null : txtPostal.Text.Trim();
            Model.Country = string.IsNullOrWhiteSpace(txtCountry.Text) ? null : txtCountry.Text.Trim();
            Model.GstVatNumber = string.IsNullOrWhiteSpace(txtGst.Text) ? null : txtGst.Text.Trim();

            // Persist to database (add or update)
            using (var db = new AppDbContext())
            {
                if (Model.Id == 0)
                {
                    db.Customers.Add(Model);
                }
                else
                {
                    var existing = db.Customers.FirstOrDefault(c => c.Id == Model.Id);
                    if (existing != null)
                    {
                        existing.Name = Model.Name;
                        existing.Email = Model.Email;
                        existing.Phone = Model.Phone;
                        existing.Address1 = Model.Address1;
                        existing.Address2 = Model.Address2;
                        existing.City = Model.City;
                        existing.State = Model.State;
                        existing.PostalCode = Model.PostalCode;
                        existing.Country = Model.Country;
                        existing.GstVatNumber = Model.GstVatNumber;
                    }
                    else
                    {
                        db.Customers.Add(Model);
                    }
                }
                db.SaveChanges();
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
