using System;
using System.Drawing;
using System.Windows.Forms;
using BillingSuite.App.Models;

namespace BillingSuite.App.Forms
{
    public class PaymentForm : Form
    {
        private readonly DateTimePicker dtDate = new DateTimePicker();
        private readonly ComboBox cmbMethod = new ComboBox();
        private readonly NumericUpDown numAmount = new NumericUpDown();
        private readonly TextBox txtNotes = new TextBox();
        private readonly Button btnOk = new Button();
        private readonly Button btnCancel = new Button();

        public Payment Model { get; private set; } = new Payment();

        public PaymentForm(decimal suggestedAmount)
        {
            Text = "Add Payment";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(420, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;

            var lblDate = new Label { Text = "Date", AutoSize = true, Location = new Point(20, 20) };
            dtDate.Location = new Point(120, 16); dtDate.Width = 250;

            var lblMethod = new Label { Text = "Method", AutoSize = true, Location = new Point(20, 55) };
            cmbMethod.Location = new Point(120, 51); cmbMethod.Width = 250; cmbMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMethod.Items.AddRange(new object[] { "Cash", "Card", "Bank", "UPI" });
            cmbMethod.SelectedIndex = 0;

            var lblAmount = new Label { Text = "Amount", AutoSize = true, Location = new Point(20, 90) };
            numAmount.Location = new Point(120, 86); numAmount.DecimalPlaces = 2; numAmount.Maximum = 100000000; numAmount.Value = suggestedAmount;

            var lblNotes = new Label { Text = "Notes", AutoSize = true, Location = new Point(20, 125) };
            txtNotes.Location = new Point(120, 121); txtNotes.Width = 250; txtNotes.Height = 60; txtNotes.Multiline = true;

            btnOk.Text = "OK"; btnOk.Location = new Point(210, 200); btnOk.Click += (s, e) =>
            {
                if (numAmount.Value <= 0) { MessageBox.Show("Enter amount."); return; }
                Model.Date = dtDate.Value.Date;
                Model.Method = cmbMethod.SelectedItem?.ToString() ?? "Cash";
                Model.Amount = numAmount.Value;
                Model.Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };
            btnCancel.Text = "Cancel"; btnCancel.Location = new Point(295, 200); btnCancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] { lblDate, dtDate, lblMethod, cmbMethod, lblAmount, numAmount, lblNotes, txtNotes, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.W) { e.Handled = true; DialogResult = DialogResult.Cancel; Close(); } };
        }
    }
}
