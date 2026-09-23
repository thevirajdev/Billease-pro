using System;
using System.Drawing;
using System.Windows.Forms;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    public class AiSettingsForm : Form
    {
        private TextBox txtApiKey;
        private Button btnSave;
        private Button btnCancel;
        private AiAgentService _aiService;

        public AiSettingsForm()
        {
            _aiService = new AiAgentService();
            InitializeComponent();
            txtApiKey.Text = _aiService.GetApiKey() ?? "";
        }

        private void InitializeComponent()
        {
            this.Text = "AI Assistant Settings";
            this.Size = new Size(500, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblKey = new Label { Text = "Gemini API Key:", Location = new Point(20, 30), AutoSize = true };
            txtApiKey = new TextBox { Location = new Point(20, 55), Width = 440, PasswordChar = '*' };
            
            var lblHint = new Label 
            { 
                Text = "Get your key from Google AI Studio (aistudio.google.com).", 
                Location = new Point(20, 85), 
                AutoSize = true, 
                ForeColor = Color.Gray 
            };

            btnSave = new Button { Text = "Save Settings", Location = new Point(250, 120), Width = 100, Height = 30 };
            btnCancel = new Button { Text = "Cancel", Location = new Point(360, 120), Width = 100, Height = 30 };

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { lblKey, txtApiKey, lblHint, btnSave, btnCancel });
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var key = txtApiKey.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Please enter a valid API Key.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _aiService.SaveApiKey(key);
            MessageBox.Show("AI Settings saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
