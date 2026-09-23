using System;
using System.Drawing;
using System.Windows.Forms;

namespace BillingSuite.App.Forms
{
    public class QuantityPrompt : Form
    {
        private readonly NumericUpDown numQty = new NumericUpDown();
        private readonly Button btnOk = new Button();
        private readonly Button btnCancel = new Button();

        public decimal Quantity => numQty.Value;

        public QuantityPrompt(decimal defaultQty = 1m)
        {
            Text = "Quantity";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(260, 140);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            var lbl = new Label { Text = "Enter quantity:", AutoSize = true, Location = new Point(12, 12) };
            numQty.Location = new Point(15, 36); numQty.Width = 100; numQty.DecimalPlaces = 2; numQty.Maximum = 100000; numQty.Minimum = 0.01m; numQty.Value = defaultQty;
            btnOk.Text = "OK"; btnOk.Location = new Point(90, 70); btnOk.DialogResult = DialogResult.OK;
            btnCancel.Text = "Cancel"; btnCancel.Location = new Point(170, 70); btnCancel.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { lbl, numQty, btnOk, btnCancel });
            AcceptButton = btnOk; CancelButton = btnCancel;
            Shown += (s, e) => { numQty.Focus(); numQty.Select(0, numQty.Text.Length); };
        }
    }
}
