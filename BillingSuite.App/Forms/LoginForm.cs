using System;
using System.Drawing;
using System.Windows.Forms;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Sign-in / account creation shown before the main window opens.
    ///
    /// First run (no accounts on this device) opens on the Create Account tab and also
    /// captures the company profile, so the user is never dropped into an app with
    /// company details still reading "Your Company".
    ///
    /// Later runs open on Sign In. Passwords are verified by AuthService; this form
    /// never sees or stores a plaintext password beyond the textbox.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly bool _firstRun;

        private TabControl tabs = new();
        private TabPage tabSignIn = new("Sign In");
        private TabPage tabCreate = new("Create Account");
        private TabPage tabForgot = new("Forgot Password");

        // Sign-in
        private TextBox txtSignInEmail = new();
        private TextBox txtSignInPassword = new();
        private Button btnSignIn = new();
        private Label lblSignInError = new();

        // Forgot Password
        private TextBox txtForgotEmail = new();
        private TextBox txtForgotNewPassword = new();
        private TextBox txtForgotConfirm = new();
        private Button btnResetPassword = new();
        private Label lblForgotStatus = new();

        // Create account
        private TextBox txtEmail = new();
        private TextBox txtPassword = new();
        private TextBox txtConfirm = new();
        private TextBox txtFullName = new();
        private TextBox txtPhone = new();
        private TextBox txtCompany = new();
        private TextBox txtCompanyAddress = new();
        private TextBox txtCompanyPhone = new();
        private TextBox txtCompanyEmail = new();
        private TextBox txtCompanyTax = new();
        private Button btnCreate = new();
        private Label lblCreateError = new();

        public LoginForm(bool firstRun)
        {
            _firstRun = firstRun;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = _firstRun ? "Billing Suite - First Time Setup" : "Billing Suite - Sign In";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 620);
            Font = new Font("Segoe UI", 9F);

            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "billease.ico");
                if (System.IO.File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }

            var header = new Label
            {
                Text = _firstRun ? "Welcome - set up your account" : "Sign in to continue",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 44,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };

            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 6);
            BuildSignInTab();
            BuildCreateTab();
            BuildForgotTab();
            tabs.TabPages.Add(tabSignIn);
            tabs.TabPages.Add(tabCreate);
            tabs.TabPages.Add(tabForgot);
            tabs.SelectedIndex = _firstRun ? 1 : 0;

            Controls.Add(tabs);
            Controls.Add(header);

            AcceptButton = _firstRun ? btnCreate : btnSignIn;
        }

        private void BuildForgotTab()
        {
            tabForgot.Padding = new Padding(14);
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 9,
                AutoScroll = true
            };
            for (int i = 0; i < 9; i++) host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtForgotEmail.Width = 470;
            txtForgotNewPassword.Width = 470;
            txtForgotNewPassword.UseSystemPasswordChar = true;
            txtForgotConfirm.Width = 470;
            txtForgotConfirm.UseSystemPasswordChar = true;

            lblForgotStatus.ForeColor = Color.Firebrick;
            lblForgotStatus.AutoSize = true;

            btnResetPassword.Text = "🔐 Reset Password (Supabase Cloud)";
            btnResetPassword.Width = 280;
            btnResetPassword.Height = 34;
            btnResetPassword.Click += async (s, e) =>
            {
                lblForgotStatus.Text = string.Empty;
                var email = txtForgotEmail.Text.Trim();
                var pass = txtForgotNewPassword.Text;
                var confirm = txtForgotConfirm.Text;

                if (string.IsNullOrWhiteSpace(email))
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = "Please enter your registered email address.";
                    return;
                }
                if (string.IsNullOrWhiteSpace(pass) || pass.Length < 6)
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = "New password must be at least 6 characters.";
                    return;
                }
                if (pass != confirm)
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = "The two passwords do not match.";
                    return;
                }

                btnResetPassword.Enabled = false;
                lblForgotStatus.ForeColor = Color.DarkSlateBlue;
                lblForgotStatus.Text = "Connecting to Supabase Cloud and resetting password...";

                var connStr = CloudDbConfig.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = "Cloud database connection is not configured.";
                    btnResetPassword.Enabled = true;
                    return;
                }

                var (ok, msg) = await OnlineDatabaseService.CloudResetPasswordAsync(connStr, email, pass);
                btnResetPassword.Enabled = true;

                if (ok)
                {
                    lblForgotStatus.ForeColor = Color.SeaGreen;
                    lblForgotStatus.Text = "Password reset successfully! You can now sign in with your new password.";
                    MessageBox.Show("Password reset successfully! Please sign in with your new password.", "Password Reset Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    tabs.SelectedTab = tabSignIn;
                    txtSignInEmail.Text = email;
                    txtSignInPassword.Text = pass;
                }
                else
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = msg;
                }
            };

            host.Controls.Add(FieldLabel("Registered Account Email"));
            host.Controls.Add(txtForgotEmail);
            host.Controls.Add(FieldLabel("New Password (min 6 characters)"));
            host.Controls.Add(txtForgotNewPassword);
            host.Controls.Add(FieldLabel("Confirm New Password"));
            host.Controls.Add(txtForgotConfirm);
            host.Controls.Add(btnResetPassword);
            host.Controls.Add(lblForgotStatus);
            tabForgot.Controls.Add(host);
        }

        private static Label FieldLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Location = new Point(16, 0)
        };

        private void BuildSignInTab()
        {
            tabSignIn.Padding = new Padding(14);
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                AutoScroll = true
            };
            for (int i = 0; i < 7; i++) host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtSignInEmail.Width = 470;
            txtSignInPassword.Width = 470;
            txtSignInPassword.UseSystemPasswordChar = true;

            lblSignInError.ForeColor = Color.Firebrick;
            lblSignInError.AutoSize = true;

            btnSignIn.Text = "Sign In";
            btnSignIn.Width = 120;
            btnSignIn.Height = 32;
            btnSignIn.Click += BtnSignIn_Click;

            var btnCloudRestore = new Button
            {
                Text = "⬇ Restore Cloud Data for this Account (New Device)",
                Width = 320,
                Height = 32,
                Margin = new Padding(0, 10, 0, 0),
                FlatStyle = FlatStyle.Flat
            };
            btnCloudRestore.Click += async (s, e) =>
            {
                lblSignInError.Text = string.Empty;
                var (ok, error) = AuthService.SignIn(txtSignInEmail.Text, txtSignInPassword.Text);
                if (!ok)
                {
                    lblSignInError.Text = error;
                    txtSignInPassword.SelectAll();
                    txtSignInPassword.Focus();
                    return;
                }

                btnCloudRestore.Enabled = false;
                lblSignInError.ForeColor = Color.DarkSlateBlue;
                lblSignInError.Text = "Connecting to cloud backend and pulling account data...";

                var connStr = CloudDbConfig.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                    connStr = AppSettingsService.SyncConnectionString;

                if (string.IsNullOrWhiteSpace(connStr))
                {
                    lblSignInError.ForeColor = Color.Firebrick;
                    lblSignInError.Text = "Cloud backend is not configured by admin yet.";
                    btnCloudRestore.Enabled = true;
                    return;
                }

                var (pullOk, pullMsg) = await OnlineDatabaseService.PullFromCloudAsync(connStr, AuthService.CurrentUser?.Id);
                btnCloudRestore.Enabled = true;

                if (pullOk)
                {
                    MessageBox.Show($"Cloud Restore Successful!\n\nAll business records for {AuthService.CurrentUser?.Email} have been restored to this device.",
                        "Cloud Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    lblSignInError.ForeColor = Color.Firebrick;
                    lblSignInError.Text = pullMsg;
                }
            };

            var flowAction = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            flowAction.Controls.Add(btnSignIn);
            flowAction.Controls.Add(btnCloudRestore);

            host.Controls.Add(FieldLabel("Email"));
            host.Controls.Add(txtSignInEmail);
            host.Controls.Add(FieldLabel("Password"));
            host.Controls.Add(txtSignInPassword);
            host.Controls.Add(flowAction);
            host.Controls.Add(lblSignInError);
            tabSignIn.Controls.Add(host);
        }

        private void BuildCreateTab()
        {
            tabCreate.Padding = new Padding(14);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            for (int i = 0; i < 24; i++) host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            void Wide(TextBox t) { t.Width = 470; }

            Wide(txtEmail); Wide(txtPassword); Wide(txtConfirm);
            Wide(txtFullName); Wide(txtPhone); Wide(txtCompany);
            Wide(txtCompanyAddress); Wide(txtCompanyPhone); Wide(txtCompanyEmail); Wide(txtCompanyTax);
            txtPassword.UseSystemPasswordChar = true;
            txtConfirm.UseSystemPasswordChar = true;
            txtCompanyAddress.Multiline = true;
            txtCompanyAddress.Height = 54;

            lblCreateError.ForeColor = Color.Firebrick;
            lblCreateError.AutoSize = true;

            btnCreate.Text = _firstRun ? "Create Account and Continue" : "Create Account";
            btnCreate.Width = 220;
            btnCreate.Height = 32;
            btnCreate.Click += BtnCreate_Click;

            host.Controls.Add(FieldLabel("Email (used to sign in)"));
            host.Controls.Add(txtEmail);
            host.Controls.Add(FieldLabel("Password (min 6 characters)"));
            host.Controls.Add(txtPassword);
            host.Controls.Add(FieldLabel("Confirm password"));
            host.Controls.Add(txtConfirm);
            host.Controls.Add(FieldLabel("Your full name"));
            host.Controls.Add(txtFullName);
            host.Controls.Add(FieldLabel("Your phone (optional)"));
            host.Controls.Add(txtPhone);
            host.Controls.Add(FieldLabel("Company name"));
            host.Controls.Add(txtCompany);
            host.Controls.Add(FieldLabel("Company address"));
            host.Controls.Add(txtCompanyAddress);
            host.Controls.Add(FieldLabel("Company phone"));
            host.Controls.Add(txtCompanyPhone);
            host.Controls.Add(FieldLabel("Company email"));
            host.Controls.Add(txtCompanyEmail);
            host.Controls.Add(FieldLabel("Company tax / GST number"));
            host.Controls.Add(txtCompanyTax);
            host.Controls.Add(btnCreate);
            host.Controls.Add(lblCreateError);

            scroll.Controls.Add(host);
            tabCreate.Controls.Add(scroll);
        }

        private void BtnSignIn_Click(object? sender, EventArgs e)
        {
            lblSignInError.Text = string.Empty;
            var (ok, error) = AuthService.SignIn(txtSignInEmail.Text, txtSignInPassword.Text);
            if (!ok)
            {
                lblSignInError.Text = error;
                txtSignInPassword.SelectAll();
                txtSignInPassword.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            lblCreateError.Text = string.Empty;

            if (txtPassword.Text != txtConfirm.Text)
            {
                lblCreateError.Text = "The two passwords do not match.";
                return;
            }

            var (ok, error) = AuthService.Register(
                txtEmail.Text, txtPassword.Text, txtFullName.Text, txtPhone.Text,
                txtCompany.Text, txtCompanyAddress.Text, txtCompanyPhone.Text,
                txtCompanyEmail.Text, txtCompanyTax.Text);

            if (!ok)
            {
                lblCreateError.Text = error;
                return;
            }

            // Seed the company settings from the profile just captured, so invoices stop
            // reading "Your Company" immediately rather than waiting for a Settings visit.
            ApplyProfileToSettings();

            DialogResult = DialogResult.OK;
            Close();
        }

        private static void ApplyProfileToSettings()
        {
            var u = AuthService.CurrentUser;
            if (u == null) return;

            var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string?>>();
            void Add(string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value)) pairs.Add(new(key, value.Trim()));
            }

            Add(Models.AppSettingKeys.CompanyName, u.CompanyName);
            Add(Models.AppSettingKeys.CompanyAddress1, u.CompanyAddress);
            Add(Models.AppSettingKeys.CompanyPhone, u.CompanyPhone);
            Add(Models.AppSettingKeys.CompanyEmail, u.CompanyEmail);
            Add(Models.AppSettingKeys.CompanyTaxNumber, u.CompanyTaxNumber);

            if (pairs.Count > 0) AppSettingsService.SetMany(pairs);
            AppSettingsService.Invalidate();
        }
    }
}
