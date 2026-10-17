using System;
using System.Drawing;
using System.Windows.Forms;

namespace BillingSuite.App.Forms
{
    public class PurchaseClosingPromptForm : Form
    {
        public PurchaseClosingPromptForm()
        {
            Services.FormIconHelper.ApplyIcon(this);
            Text = "Save Purchase Bill";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Size = new Size(460, 300);

            var lblHeader = new Label
            {
                Text = "What would you like to do with this purchase bill?",
                Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };

            var lblSub = new Label
            {
                Text = "Please choose an option before closing:",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Regular),
                ForeColor = SystemColors.GrayText,
                Location = new Point(20, 40),
                AutoSize = true
            };

            var btnSave = new Button
            {
                Text = "✔ Save (Update Stock)",
                Location = new Point(25, 70),
                Size = new Size(395, 38),
                BackColor = Color.FromArgb(24, 134, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Yes
            };
            btnSave.FlatAppearance.BorderSize = 0;

            var btnDraft = new Button
            {
                Text = "📝 Save as Draft (Do Not Update Stock)",
                Location = new Point(25, 116),
                Size = new Size(395, 38),
                BackColor = Color.FromArgb(210, 120, 20),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.No
            };
            btnDraft.FlatAppearance.BorderSize = 0;

            var btnDiscard = new Button
            {
                Text = "🗑 Discard & Exit (Do Not Save)",
                Location = new Point(25, 162),
                Size = new Size(395, 35),
                BackColor = Color.FromArgb(210, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Abort
            };
            btnDiscard.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Keep Editing (Cancel)",
                Location = new Point(25, 205),
                Size = new Size(395, 30),
                BackColor = SystemColors.ControlLight,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 9f),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            Controls.AddRange(new Control[] { lblHeader, lblSub, btnSave, btnDraft, btnDiscard, btnCancel });
            CancelButton = btnCancel;
        }
    }
}
