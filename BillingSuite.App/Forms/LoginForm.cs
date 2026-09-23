using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Modern Sign-In and Sign-Up form for Billease Pro.
    /// Clean card layout, non-blocking asynchronous cloud authentication,
    /// and instant forgot-password recovery flow.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly bool _firstRun;

        private Panel pnlHeader = new();
        private Label lblHeaderTitle = new();
        private Label lblHeaderSub = new();

        private Panel pnlNav = new();
        private Button btnNavSignIn = new();
        private Button btnNavCreate = new();

        private Panel pnlCardsHost = new();
        private Panel cardSignIn = new();
        private Panel cardCreate = new();
        private Panel cardForgot = new();

        // Sign In Card Controls
        private TextBox txtSignInEmail = new();
        private TextBox txtSignInPassword = new();
        private Button btnSignIn = new();
        private LinkLabel lnkForgotPass = new();
        private Button btnCloudRestore = new();
        private Label lblSignInStatus = new();

        // Create Account Card Controls
        private TextBox txtCreateEmail = new();
        private TextBox txtCreateFullName = new();
        private TextBox txtCreatePassword = new();
        private TextBox txtCreateConfirm = new();
        private Button btnCreate = new();
        private Label lblCreateStatus = new();

        // Forgot Password Card Controls
        private TextBox txtForgotEmail = new();
        private TextBox txtForgotNewPassword = new();
        private TextBox txtForgotConfirm = new();
        private Button btnForgotReset = new();
        private LinkLabel lnkBackToSignIn = new();
        private Label lblForgotStatus = new();

        public LoginForm(bool firstRun)
        {
            _firstRun = firstRun;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = _firstRun ? "Billease Pro - Account Setup" : "Billease Pro - Sign In";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 560);
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Color.FromArgb(248, 250, 252);

            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "billease.ico");
                if (System.IO.File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }

            // 1. Header Banner
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 75;
            pnlHeader.BackColor = Color.FromArgb(15, 23, 42); // Dark Slate
            pnlHeader.Padding = new Padding(20, 14, 20, 10);

            lblHeaderTitle.Text = "Billease Pro";
            lblHeaderTitle.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Color.White;
            lblHeaderTitle.Dock = DockStyle.Top;
            lblHeaderTitle.AutoSize = true;

            lblHeaderSub.Text = _firstRun ? "Create your account to start billing" : "Sign in to access your business account";
            lblHeaderSub.Font = new Font("Segoe UI", 9F);
            lblHeaderSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblHeaderSub.Dock = DockStyle.Top;
            lblHeaderSub.AutoSize = true;

            pnlHeader.Controls.Add(lblHeaderSub);
            pnlHeader.Controls.Add(lblHeaderTitle);

            // 2. Navigation Switcher (Sign In vs Create Account)
            pnlNav.Dock = DockStyle.Top;
            pnlNav.Height = 44;
            pnlNav.BackColor = Color.White;
            pnlNav.Padding = new Padding(12, 6, 12, 6);

            btnNavSignIn.Text = "Sign In";
            btnNavSignIn.Size = new Size(210, 32);
            btnNavSignIn.Location = new Point(12, 6);
            btnNavSignIn.FlatStyle = FlatStyle.Flat;
            btnNavSignIn.FlatAppearance.BorderSize = 0;
            btnNavSignIn.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnNavSignIn.Click += (s, e) => ShowCard(cardSignIn);

            btnNavCreate.Text = "Create Account";
            btnNavCreate.Size = new Size(210, 32);
            btnNavCreate.Location = new Point(228, 6);
            btnNavCreate.FlatStyle = FlatStyle.Flat;
            btnNavCreate.FlatAppearance.BorderSize = 0;
            btnNavCreate.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnNavCreate.Click += (s, e) => ShowCard(cardCreate);

            pnlNav.Controls.Add(btnNavSignIn);
            pnlNav.Controls.Add(btnNavCreate);

            // 3. Cards Host Container
            pnlCardsHost.Dock = DockStyle.Fill;
            pnlCardsHost.Padding = new Padding(24, 16, 24, 16);

            BuildSignInCard();
            BuildCreateCard();
            BuildForgotCard();

            pnlCardsHost.Controls.Add(cardSignIn);
            pnlCardsHost.Controls.Add(cardCreate);
            pnlCardsHost.Controls.Add(cardForgot);

            Controls.Add(pnlCardsHost);
            Controls.Add(pnlNav);
            Controls.Add(pnlHeader);

            ShowCard(_firstRun ? cardCreate : cardSignIn);
        }

        private void ShowCard(Panel card)
        {
            cardSignIn.Visible = (card == cardSignIn);
            cardCreate.Visible = (card == cardCreate);
            cardForgot.Visible = (card == cardForgot);

            pnlNav.Visible = (card != cardForgot);

            if (card == cardSignIn)
            {
                btnNavSignIn.BackColor = Color.FromArgb(37, 99, 235);
                btnNavSignIn.ForeColor = Color.White;
                btnNavCreate.BackColor = Color.FromArgb(241, 245, 249);
                btnNavCreate.ForeColor = Color.FromArgb(71, 85, 105);
                AcceptButton = btnSignIn;
                lblHeaderSub.Text = "Sign in to access your business account";
            }
            else if (card == cardCreate)
            {
                btnNavCreate.BackColor = Color.FromArgb(37, 99, 235);
                btnNavCreate.ForeColor = Color.White;
                btnNavSignIn.BackColor = Color.FromArgb(241, 245, 249);
                btnNavSignIn.ForeColor = Color.FromArgb(71, 85, 105);
                AcceptButton = btnCreate;
                lblHeaderSub.Text = "Create your account to start billing";
            }
            else if (card == cardForgot)
            {
                AcceptButton = btnForgotReset;
                lblHeaderSub.Text = "Reset your cloud account password";
            }
        }

        private static Label Label(string text) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 4)
        };

        private static void StyleInput(TextBox txt)
        {
            txt.Width = 390;
            txt.Font = new Font("Segoe UI", 10F);
        }

        private void BuildSignInCard()
        {
            cardSignIn.Dock = DockStyle.Fill;
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false
            };

            StyleInput(txtSignInEmail);
            StyleInput(txtSignInPassword);
            txtSignInPassword.UseSystemPasswordChar = true;

            lnkForgotPass.Text = "Forgot password?";
            lnkForgotPass.AutoSize = true;
            lnkForgotPass.LinkColor = Color.FromArgb(37, 99, 235);
            lnkForgotPass.Margin = new Padding(0, 4, 0, 12);
            lnkForgotPass.Click += (s, e) => ShowCard(cardForgot);

            btnSignIn.Text = "Sign In";
            btnSignIn.Size = new Size(390, 38);
            btnSignIn.BackColor = Color.FromArgb(37, 99, 235);
            btnSignIn.ForeColor = Color.White;
            btnSignIn.FlatStyle = FlatStyle.Flat;
            btnSignIn.FlatAppearance.BorderSize = 0;
            btnSignIn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSignIn.Margin = new Padding(0, 8, 0, 8);
            btnSignIn.Click += BtnSignIn_Click;

            btnCloudRestore.Text = "⬇ Restore Account Data (New Device)";
            btnCloudRestore.Size = new Size(390, 34);
            btnCloudRestore.BackColor = Color.FromArgb(241, 245, 249);
            btnCloudRestore.ForeColor = Color.FromArgb(30, 41, 59);
            btnCloudRestore.FlatStyle = FlatStyle.Flat;
            btnCloudRestore.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnCloudRestore.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCloudRestore.Margin = new Padding(0, 0, 0, 8);
            btnCloudRestore.Click += BtnCloudRestore_Click;

            lblSignInStatus.AutoSize = true;
            lblSignInStatus.MaximumSize = new Size(390, 0);
            lblSignInStatus.ForeColor = Color.Firebrick;

            layout.Controls.Add(Label("Email Address"));
            layout.Controls.Add(txtSignInEmail);
            layout.Controls.Add(Label("Password"));
            layout.Controls.Add(txtSignInPassword);
            layout.Controls.Add(lnkForgotPass);
            layout.Controls.Add(btnSignIn);
            layout.Controls.Add(btnCloudRestore);
            layout.Controls.Add(lblSignInStatus);

            cardSignIn.Controls.Add(layout);
        }

        private void BuildCreateCard()
        {
            cardCreate.Dock = DockStyle.Fill;
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false
            };

            StyleInput(txtCreateEmail);
            StyleInput(txtCreateFullName);
            StyleInput(txtCreatePassword);
            StyleInput(txtCreateConfirm);
            txtCreatePassword.UseSystemPasswordChar = true;
            txtCreateConfirm.UseSystemPasswordChar = true;

            btnCreate.Text = "Create Account & Continue";
            btnCreate.Size = new Size(390, 38);
            btnCreate.BackColor = Color.FromArgb(37, 99, 235);
            btnCreate.ForeColor = Color.White;
            btnCreate.FlatStyle = FlatStyle.Flat;
            btnCreate.FlatAppearance.BorderSize = 0;
            btnCreate.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnCreate.Margin = new Padding(0, 12, 0, 8);
            btnCreate.Click += BtnCreate_Click;

            lblCreateStatus.AutoSize = true;
            lblCreateStatus.MaximumSize = new Size(390, 0);
            lblCreateStatus.ForeColor = Color.Firebrick;

            layout.Controls.Add(Label("Email Address (Used to sign in)"));
            layout.Controls.Add(txtCreateEmail);
            layout.Controls.Add(Label("Your Full Name"));
            layout.Controls.Add(txtCreateFullName);
            layout.Controls.Add(Label("Password (min 6 characters)"));
            layout.Controls.Add(txtCreatePassword);
            layout.Controls.Add(Label("Confirm Password"));
            layout.Controls.Add(txtCreateConfirm);
            layout.Controls.Add(btnCreate);
            layout.Controls.Add(lblCreateStatus);

            cardCreate.Controls.Add(layout);
        }

        private void BuildForgotCard()
        {
            cardForgot.Dock = DockStyle.Fill;
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false
            };

            StyleInput(txtForgotEmail);
            StyleInput(txtForgotNewPassword);
            StyleInput(txtForgotConfirm);
            txtForgotNewPassword.UseSystemPasswordChar = true;
            txtForgotConfirm.UseSystemPasswordChar = true;

            btnForgotReset.Text = "🔐 Reset Password (Supabase Cloud)";
            btnForgotReset.Size = new Size(390, 38);
            btnForgotReset.BackColor = Color.FromArgb(37, 99, 235);
            btnForgotReset.ForeColor = Color.White;
            btnForgotReset.FlatStyle = FlatStyle.Flat;
            btnForgotReset.FlatAppearance.BorderSize = 0;
            btnForgotReset.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnForgotReset.Margin = new Padding(0, 12, 0, 8);
            btnForgotReset.Click += BtnForgotReset_Click;

            lnkBackToSignIn.Text = "← Back to Sign In";
            lnkBackToSignIn.AutoSize = true;
            lnkBackToSignIn.LinkColor = Color.FromArgb(37, 99, 235);
            lnkBackToSignIn.Margin = new Padding(0, 4, 0, 12);
            lnkBackToSignIn.Click += (s, e) => ShowCard(cardSignIn);

            lblForgotStatus.AutoSize = true;
            lblForgotStatus.MaximumSize = new Size(390, 0);
            lblForgotStatus.ForeColor = Color.Firebrick;

            layout.Controls.Add(Label("Registered Account Email"));
            layout.Controls.Add(txtForgotEmail);
            layout.Controls.Add(Label("New Password (min 6 characters)"));
            layout.Controls.Add(txtForgotNewPassword);
            layout.Controls.Add(Label("Confirm New Password"));
            layout.Controls.Add(txtForgotConfirm);
            layout.Controls.Add(btnForgotReset);
            layout.Controls.Add(lnkBackToSignIn);
            layout.Controls.Add(lblForgotStatus);

            cardForgot.Controls.Add(layout);
        }

        // =========================================================================
        // ASYNCHRONOUS EVENT HANDLERS (NO UI DEADLOCKS)
        // =========================================================================

        private async void BtnSignIn_Click(object? sender, EventArgs e)
        {
            lblSignInStatus.ForeColor = Color.Firebrick;
            lblSignInStatus.Text = string.Empty;

            var email = txtSignInEmail.Text.Trim();
            var pass = txtSignInPassword.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                lblSignInStatus.Text = "Please enter your email and password.";
                return;
            }

            btnSignIn.Enabled = false;
            btnSignIn.Text = "Signing in...";
            lblSignInStatus.ForeColor = Color.DarkSlateBlue;
            lblSignInStatus.Text = "Authenticating account...";

            try
            {
                var (ok, error) = await AuthService.SignInAsync(email, pass);
                if (!ok)
                {
                    lblSignInStatus.ForeColor = Color.Firebrick;
                    lblSignInStatus.Text = error;
                    txtSignInPassword.SelectAll();
                    txtSignInPassword.Focus();
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                btnSignIn.Enabled = true;
                btnSignIn.Text = "Sign In";
            }
        }

        private async void BtnCreate_Click(object? sender, EventArgs e)
        {
            lblCreateStatus.ForeColor = Color.Firebrick;
            lblCreateStatus.Text = string.Empty;

            var email = txtCreateEmail.Text.Trim();
            var name = txtCreateFullName.Text.Trim();
            var pass = txtCreatePassword.Text;
            var confirm = txtCreateConfirm.Text;

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                lblCreateStatus.Text = "Please enter a valid email address.";
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                lblCreateStatus.Text = "Please enter your full name.";
                return;
            }
            if (string.IsNullOrWhiteSpace(pass) || pass.Length < 6)
            {
                lblCreateStatus.Text = "Password must be at least 6 characters.";
                return;
            }
            if (pass != confirm)
            {
                lblCreateStatus.Text = "The two passwords do not match.";
                return;
            }

            btnCreate.Enabled = false;
            btnCreate.Text = "Creating account...";
            lblCreateStatus.ForeColor = Color.DarkSlateBlue;
            lblCreateStatus.Text = "Registering user account...";

            try
            {
                var (ok, error) = await AuthService.RegisterAsync(email, pass, name);

                if (!ok)
                {
                    lblCreateStatus.ForeColor = Color.Firebrick;
                    lblCreateStatus.Text = error;
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                btnCreate.Enabled = true;
                btnCreate.Text = "Create Account & Continue";
            }
        }

        private async void BtnCloudRestore_Click(object? sender, EventArgs e)
        {
            lblSignInStatus.ForeColor = Color.Firebrick;
            lblSignInStatus.Text = string.Empty;

            var email = txtSignInEmail.Text.Trim();
            var pass = txtSignInPassword.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                lblSignInStatus.Text = "Enter your account email and password to restore data.";
                return;
            }

            btnCloudRestore.Enabled = false;
            lblSignInStatus.ForeColor = Color.DarkSlateBlue;
            lblSignInStatus.Text = "Authenticating against cloud database...";

            try
            {
                var (authOk, authError) = await AuthService.SignInAsync(email, pass);
                if (!authOk)
                {
                    lblSignInStatus.ForeColor = Color.Firebrick;
                    lblSignInStatus.Text = authError;
                    return;
                }

                lblSignInStatus.Text = "Downloading your business data from Supabase Cloud...";
                var connStr = CloudDbConfig.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                    connStr = AppSettingsService.SyncConnectionString;

                if (string.IsNullOrWhiteSpace(connStr))
                {
                    lblSignInStatus.ForeColor = Color.Firebrick;
                    lblSignInStatus.Text = "Cloud backend connection is not configured.";
                    return;
                }

                var (pullOk, pullMsg) = await OnlineDatabaseService.PullFromCloudAsync(connStr, AuthService.CurrentUser?.Id);

                if (pullOk)
                {
                    MessageBox.Show($"Cloud Restore Successful!\n\nAll business records for {AuthService.CurrentUser?.Email} have been restored to this device.",
                        "Cloud Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    lblSignInStatus.ForeColor = Color.Firebrick;
                    lblSignInStatus.Text = pullMsg;
                }
            }
            finally
            {
                btnCloudRestore.Enabled = true;
            }
        }

        private async void BtnForgotReset_Click(object? sender, EventArgs e)
        {
            lblForgotStatus.ForeColor = Color.Firebrick;
            lblForgotStatus.Text = string.Empty;

            var email = txtForgotEmail.Text.Trim();
            var pass = txtForgotNewPassword.Text;
            var confirm = txtForgotConfirm.Text;

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                lblForgotStatus.Text = "Please enter a valid registered email address.";
                return;
            }
            if (string.IsNullOrWhiteSpace(pass) || pass.Length < 6)
            {
                lblForgotStatus.Text = "New password must be at least 6 characters.";
                return;
            }
            if (pass != confirm)
            {
                lblForgotStatus.Text = "The two passwords do not match.";
                return;
            }

            btnForgotReset.Enabled = false;
            btnForgotReset.Text = "Resetting password...";
            lblForgotStatus.ForeColor = Color.DarkSlateBlue;
            lblForgotStatus.Text = "Connecting to Supabase Cloud...";

            try
            {
                var connStr = CloudDbConfig.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = "Cloud database connection is not configured.";
                    return;
                }

                var (ok, msg) = await OnlineDatabaseService.CloudResetPasswordAsync(connStr, email, pass);

                if (ok)
                {
                    MessageBox.Show("Password reset successfully! Please sign in with your new password.", "Password Reset Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ShowCard(cardSignIn);
                    txtSignInEmail.Text = email;
                    txtSignInPassword.Text = pass;
                }
                else
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = msg;
                }
            }
            finally
            {
                btnForgotReset.Enabled = true;
                btnForgotReset.Text = "🔐 Reset Password (Supabase Cloud)";
            }
        }
    }
}
