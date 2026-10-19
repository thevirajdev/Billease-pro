using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Modern Sign-In, Sign-Up, and 6-Digit Email OTP Verification form for Billease Pro.
    /// Features:
    /// - 6-Digit Email OTP Signup Verification with 30s Resend Cooldown Timer.
    /// - 2-Step 6-Digit Email OTP Password Reset Flow with 30s Resend Cooldown Timer.
    /// - Non-blocking asynchronous Supabase Cloud authentication.
    /// - One-click Cloud Restore for new device setup.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly bool _firstRun;
        private string _pendingEmail = string.Empty;
        private string _pendingForgotEmail = string.Empty;

        private readonly System.Windows.Forms.Timer _signupResendTimer = new();
        private int _signupCooldown = 0;

        private readonly System.Windows.Forms.Timer _forgotResendTimer = new();
        private int _forgotCooldown = 0;

        // Rate-limit countdown timer shown during lockout
        private readonly System.Windows.Forms.Timer _lockoutTimer = new() { Interval = 1000 };
        private string _lockoutEmail = string.Empty;

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
        private Panel cardOtp = new();

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

        // Forgot Password Card Controls (2-Step OTP Verification)
        private TextBox txtForgotEmail = new();
        private Button btnForgotSendOtp = new();
        private FlowLayoutPanel pnlForgotStep2 = new();
        private Label lblForgotOtpNotice = new();
        private TextBox txtForgotOtpCode = new();
        private TextBox txtForgotNewPassword = new();
        private TextBox txtForgotConfirm = new();
        private Button btnForgotConfirm = new();
        private LinkLabel lnkResendForgotOtp = new();
        private LinkLabel lnkBackToSignIn = new();
        private Label lblForgotStatus = new();

        // Signup OTP Verification Card Controls
        private Label lblOtpNotice = new();
        private TextBox txtOtpCode = new();
        private Button btnVerifyOtp = new();
        private LinkLabel lnkResendOtp = new();
        private LinkLabel lnkBackToCreate = new();
        private Label lblOtpStatus = new();

        public LoginForm(bool firstRun)
        {
            Services.FormIconHelper.ApplyIcon(this);
            _firstRun = firstRun;
            
            _signupResendTimer.Interval = 1000;
            _signupResendTimer.Tick += SignupResendTimer_Tick;

            _forgotResendTimer.Interval = 1000;
            _forgotResendTimer.Tick += ForgotResendTimer_Tick;

            _lockoutTimer.Tick += LockoutTimer_Tick;

            BuildUi();
        }

        private void BuildUi()
        {
            Text = _firstRun ? "Billease Pro - Account Setup" : "Billease Pro - Sign In";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 580);
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
            pnlHeader.Height = 82;
            pnlHeader.BackColor = Color.FromArgb(15, 23, 42);
            pnlHeader.Padding = new Padding(16, 12, 16, 12);

            var picLogo = new PictureBox
            {
                Size = new Size(50, 50),
                Location = new Point(16, 14),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "billease.ico");
                if (!System.IO.File.Exists(iconPath))
                    iconPath = System.IO.Path.Combine(Application.StartupPath, "billease.ico");

                if (System.IO.File.Exists(iconPath))
                {
                    using var ico = new Icon(iconPath, 64, 64);
                    picLogo.Image = ico.ToBitmap();
                }
            }
            catch { }

            var pnlHeaderText = new Panel
            {
                Location = new Point(74, 12),
                Size = new Size(360, 56),
                BackColor = Color.Transparent
            };

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

            pnlHeaderText.Controls.Add(lblHeaderSub);
            pnlHeaderText.Controls.Add(lblHeaderTitle);

            pnlHeader.Controls.Add(pnlHeaderText);
            pnlHeader.Controls.Add(picLogo);

            // 2. Navigation Switcher
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
            BuildOtpCard();

            pnlCardsHost.Controls.Add(cardSignIn);
            pnlCardsHost.Controls.Add(cardCreate);
            pnlCardsHost.Controls.Add(cardForgot);
            pnlCardsHost.Controls.Add(cardOtp);

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
            cardOtp.Visible = (card == cardOtp);

            pnlNav.Visible = (card == cardSignIn || card == cardCreate);

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
                AcceptButton = pnlForgotStep2.Visible ? btnForgotConfirm : btnForgotSendOtp;
                lblHeaderSub.Text = "Reset your account password with Email OTP";
            }
            else if (card == cardOtp)
            {
                AcceptButton = btnVerifyOtp;
                lblHeaderSub.Text = "Email OTP Verification Required";
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
            lnkForgotPass.Click += (s, e) =>
            {
                pnlForgotStep2.Visible = false;
                btnForgotSendOtp.Visible = true;
                txtForgotEmail.Enabled = true;
                lblForgotStatus.Text = string.Empty;
                ShowCard(cardForgot);
            };

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

            btnCreate.Text = "Send Verification OTP →";
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

            btnForgotSendOtp.Text = "📧 Send Reset OTP to Email";
            btnForgotSendOtp.Size = new Size(390, 38);
            btnForgotSendOtp.BackColor = Color.FromArgb(37, 99, 235);
            btnForgotSendOtp.ForeColor = Color.White;
            btnForgotSendOtp.FlatStyle = FlatStyle.Flat;
            btnForgotSendOtp.FlatAppearance.BorderSize = 0;
            btnForgotSendOtp.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnForgotSendOtp.Margin = new Padding(0, 12, 0, 8);
            btnForgotSendOtp.Click += BtnForgotSendOtp_Click;

            // Step 2 Panel (Shown after OTP sent)
            pnlForgotStep2.Width = 390;
            pnlForgotStep2.AutoSize = true;
            pnlForgotStep2.FlowDirection = FlowDirection.TopDown;
            pnlForgotStep2.WrapContents = false;
            pnlForgotStep2.Visible = false;
            pnlForgotStep2.Margin = new Padding(0, 4, 0, 8);

            lblForgotOtpNotice.Text = "A 6-digit reset code has been sent to your email. Enter the code and your new password:";
            lblForgotOtpNotice.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblForgotOtpNotice.ForeColor = Color.FromArgb(51, 65, 85);
            lblForgotOtpNotice.MaximumSize = new Size(390, 0);
            lblForgotOtpNotice.AutoSize = true;
            lblForgotOtpNotice.Margin = new Padding(0, 4, 0, 8);

            txtForgotOtpCode.Width = 390;
            txtForgotOtpCode.Font = new Font("Consolas", 14F, FontStyle.Bold);
            txtForgotOtpCode.TextAlign = HorizontalAlignment.Center;
            txtForgotOtpCode.MaxLength = 6;

            StyleInput(txtForgotNewPassword);
            StyleInput(txtForgotConfirm);
            txtForgotNewPassword.UseSystemPasswordChar = true;
            txtForgotConfirm.UseSystemPasswordChar = true;

            btnForgotConfirm.Text = "🔐 Confirm Password Reset";
            btnForgotConfirm.Size = new Size(390, 38);
            btnForgotConfirm.BackColor = Color.FromArgb(16, 185, 129); // Green
            btnForgotConfirm.ForeColor = Color.White;
            btnForgotConfirm.FlatStyle = FlatStyle.Flat;
            btnForgotConfirm.FlatAppearance.BorderSize = 0;
            btnForgotConfirm.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnForgotConfirm.Margin = new Padding(0, 12, 0, 6);
            btnForgotConfirm.Click += BtnForgotConfirm_Click;

            lnkResendForgotOtp.Text = "Didn't receive code? Resend OTP";
            lnkResendForgotOtp.AutoSize = true;
            lnkResendForgotOtp.LinkColor = Color.FromArgb(37, 99, 235);
            lnkResendForgotOtp.Margin = new Padding(0, 4, 0, 8);
            lnkResendForgotOtp.Click += LnkResendForgotOtp_Click;

            pnlForgotStep2.Controls.Add(lblForgotOtpNotice);
            pnlForgotStep2.Controls.Add(Label("6-Digit Reset Code"));
            pnlForgotStep2.Controls.Add(txtForgotOtpCode);
            pnlForgotStep2.Controls.Add(Label("New Password (min 6 characters)"));
            pnlForgotStep2.Controls.Add(txtForgotNewPassword);
            pnlForgotStep2.Controls.Add(Label("Confirm New Password"));
            pnlForgotStep2.Controls.Add(txtForgotConfirm);
            pnlForgotStep2.Controls.Add(btnForgotConfirm);
            pnlForgotStep2.Controls.Add(lnkResendForgotOtp);

            lnkBackToSignIn.Text = "← Back to Sign In";
            lnkBackToSignIn.AutoSize = true;
            lnkBackToSignIn.LinkColor = Color.FromArgb(100, 116, 139);
            lnkBackToSignIn.Margin = new Padding(0, 8, 0, 12);
            lnkBackToSignIn.Click += (s, e) => ShowCard(cardSignIn);

            lblForgotStatus.AutoSize = true;
            lblForgotStatus.MaximumSize = new Size(390, 0);
            lblForgotStatus.ForeColor = Color.Firebrick;

            layout.Controls.Add(Label("Registered Account Email"));
            layout.Controls.Add(txtForgotEmail);
            layout.Controls.Add(btnForgotSendOtp);
            layout.Controls.Add(pnlForgotStep2);
            layout.Controls.Add(lnkBackToSignIn);
            layout.Controls.Add(lblForgotStatus);

            cardForgot.Controls.Add(layout);
        }

        private void BuildOtpCard()
        {
            cardOtp.Dock = DockStyle.Fill;
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false
            };

            lblOtpNotice.Text = "We have sent a 6-digit verification code to your email.\nPlease check your inbox/spam folder and enter the code below:";
            lblOtpNotice.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            lblOtpNotice.ForeColor = Color.FromArgb(30, 41, 59);
            lblOtpNotice.MaximumSize = new Size(390, 0);
            lblOtpNotice.AutoSize = true;
            lblOtpNotice.Margin = new Padding(0, 8, 0, 12);

            txtOtpCode.Width = 390;
            txtOtpCode.Font = new Font("Consolas", 16F, FontStyle.Bold);
            txtOtpCode.TextAlign = HorizontalAlignment.Center;
            txtOtpCode.MaxLength = 6;

            btnVerifyOtp.Text = "Verify OTP & Complete Registration";
            btnVerifyOtp.Size = new Size(390, 40);
            btnVerifyOtp.BackColor = Color.FromArgb(37, 99, 235);
            btnVerifyOtp.ForeColor = Color.White;
            btnVerifyOtp.FlatStyle = FlatStyle.Flat;
            btnVerifyOtp.FlatAppearance.BorderSize = 0;
            btnVerifyOtp.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnVerifyOtp.Margin = new Padding(0, 14, 0, 8);
            btnVerifyOtp.Click += BtnVerifyOtp_Click;

            lnkResendOtp.Text = "Didn't receive code? Resend OTP";
            lnkResendOtp.AutoSize = true;
            lnkResendOtp.LinkColor = Color.FromArgb(37, 99, 235);
            lnkResendOtp.Margin = new Padding(0, 4, 0, 8);
            lnkResendOtp.Click += LnkResendOtp_Click;

            lnkBackToCreate.Text = "← Edit Email Address / Change Details";
            lnkBackToCreate.AutoSize = true;
            lnkBackToCreate.LinkColor = Color.FromArgb(100, 116, 139);
            lnkBackToCreate.Margin = new Padding(0, 0, 0, 12);
            lnkBackToCreate.Click += (s, e) => ShowCard(cardCreate);

            lblOtpStatus.AutoSize = true;
            lblOtpStatus.MaximumSize = new Size(390, 0);
            lblOtpStatus.ForeColor = Color.Firebrick;

            layout.Controls.Add(Label("Enter 6-Digit Verification Code"));
            layout.Controls.Add(lblOtpNotice);
            layout.Controls.Add(txtOtpCode);
            layout.Controls.Add(btnVerifyOtp);
            layout.Controls.Add(lnkResendOtp);
            layout.Controls.Add(lnkBackToCreate);
            layout.Controls.Add(lblOtpStatus);

            cardOtp.Controls.Add(layout);
        }

        // =========================================================================
        // RESEND COOLDOWN TIMER TICKS
        // =========================================================================

        private void SignupResendTimer_Tick(object? sender, EventArgs e)
        {
            _signupCooldown--;
            if (_signupCooldown <= 0)
            {
                _signupResendTimer.Stop();
                lnkResendOtp.Enabled = true;
                lnkResendOtp.Text = "Didn't receive code? Resend OTP";
            }
            else
            {
                lnkResendOtp.Text = $"Resend code in {_signupCooldown}s";
            }
        }

        private void StartSignupCooldown(int seconds = 30)
        {
            _signupCooldown = seconds;
            lnkResendOtp.Enabled = false;
            lnkResendOtp.Text = $"Resend code in {_signupCooldown}s";
            _signupResendTimer.Start();
        }

        private void ForgotResendTimer_Tick(object? sender, EventArgs e)
        {
            _forgotCooldown--;
            if (_forgotCooldown <= 0)
            {
                _forgotResendTimer.Stop();
                lnkResendForgotOtp.Enabled = true;
                lnkResendForgotOtp.Text = "Didn't receive code? Resend OTP";
            }
            else
            {
                lnkResendForgotOtp.Text = $"Resend code in {_forgotCooldown}s";
            }
        }

        private void StartForgotCooldown(int seconds = 30)
        {
            _forgotCooldown = seconds;
            lnkResendForgotOtp.Enabled = false;
            lnkResendForgotOtp.Text = $"Resend code in {_forgotCooldown}s";
            _forgotResendTimer.Start();
        }

        private void LockoutTimer_Tick(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lockoutEmail))
            {
                _lockoutTimer.Stop();
                btnSignIn.Enabled = true;
                return;
            }

            var (allowed, waitSeconds, _) = LoginRateLimiter.Check(_lockoutEmail);
            if (allowed)
            {
                _lockoutTimer.Stop();
                btnSignIn.Enabled = true;
                lblSignInStatus.ForeColor = Color.DarkSlateBlue;
                lblSignInStatus.Text = "Lockout expired. You can now sign in.";
            }
            else
            {
                btnSignIn.Enabled = false;
                lblSignInStatus.ForeColor = Color.Firebrick;
                if (waitSeconds >= 60)
                {
                    int mins = waitSeconds / 60;
                    int secs = waitSeconds % 60;
                    lblSignInStatus.Text = $"Too many failed attempts. Try again in {mins}m {secs}s.";
                }
                else
                {
                    lblSignInStatus.Text = $"Too many failed attempts. Try again in {waitSeconds}s.";
                }
            }
        }

        // =========================================================================
        // ASYNCHRONOUS EVENT HANDLERS
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

            // Check rate limiting before calling authentication
            var (allowed, waitSeconds, attemptsLeft) = LoginRateLimiter.Check(email);
            if (!allowed)
            {
                _lockoutEmail = email;
                btnSignIn.Enabled = false;
                _lockoutTimer.Start();
                LockoutTimer_Tick(null, EventArgs.Empty);
                try { AuditLogService.Log(AuditLogService.Events.LoginLockout, email, "Blocked by rate limiter"); } catch { }
                return;
            }

            bool isLockedOut = false;
            btnSignIn.Enabled = false;
            btnSignIn.Text = "Signing in...";
            lblSignInStatus.ForeColor = Color.DarkSlateBlue;
            lblSignInStatus.Text = "Authenticating account...";

            try
            {
                var (ok, error) = await AuthService.SignInAsync(email, pass);
                if (!ok)
                {
                    LoginRateLimiter.RecordFailure(email);
                    var postCheck = LoginRateLimiter.Check(email);

                    if (!postCheck.allowed)
                    {
                        isLockedOut = true;
                        _lockoutEmail = email;
                        btnSignIn.Enabled = false;
                        _lockoutTimer.Start();
                        LockoutTimer_Tick(null, EventArgs.Empty);
                        return;
                    }

                    lblSignInStatus.ForeColor = Color.Firebrick;
                    if (postCheck.attemptsLeft <= 2)
                    {
                        lblSignInStatus.Text = $"{error} ({postCheck.attemptsLeft} attempt(s) remaining before temporary lockout)";
                    }
                    else
                    {
                        lblSignInStatus.Text = error;
                    }

                    txtSignInPassword.SelectAll();
                    txtSignInPassword.Focus();
                    return;
                }

                LoginRateLimiter.RecordSuccess(email);
                _lockoutTimer.Stop();

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                if (!isLockedOut)
                {
                    btnSignIn.Enabled = true;
                }
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
            btnCreate.Text = "Sending OTP...";
            lblCreateStatus.ForeColor = Color.DarkSlateBlue;
            lblCreateStatus.Text = "Generating & sending email verification code...";

            try
            {
                var (ok, error) = await AuthService.InitiateRegistrationAsync(email, pass, name);

                if (!ok)
                {
                    lblCreateStatus.ForeColor = Color.Firebrick;
                    lblCreateStatus.Text = error;
                    return;
                }

                _pendingEmail = email;
                lblOtpNotice.Text = $"We have sent a 6-digit verification code to:\n{email}\n\nPlease check your email inbox and enter the 6-digit code below:";
                txtOtpCode.Clear();
                lblOtpStatus.Text = string.Empty;

                StartSignupCooldown(30);
                ShowCard(cardOtp);
                txtOtpCode.Focus();
            }
            finally
            {
                btnCreate.Enabled = true;
                btnCreate.Text = "Send Verification OTP →";
            }
        }

        private async void BtnVerifyOtp_Click(object? sender, EventArgs e)
        {
            lblOtpStatus.ForeColor = Color.Firebrick;
            lblOtpStatus.Text = string.Empty;

            var otp = txtOtpCode.Text.Trim();
            if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6)
            {
                lblOtpStatus.Text = "Please enter the complete 6-digit verification code.";
                return;
            }

            btnVerifyOtp.Enabled = false;
            btnVerifyOtp.Text = "Verifying code...";
            lblOtpStatus.ForeColor = Color.DarkSlateBlue;
            lblOtpStatus.Text = "Verifying OTP code and activating account...";

            try
            {
                var (ok, error) = await AuthService.ConfirmOtpRegistrationAsync(_pendingEmail, otp);

                if (!ok)
                {
                    lblOtpStatus.ForeColor = Color.Firebrick;
                    lblOtpStatus.Text = error;
                    txtOtpCode.SelectAll();
                    txtOtpCode.Focus();
                    return;
                }

                lblOtpStatus.ForeColor = Color.DarkGreen;
                lblOtpStatus.Text = "Email verified successfully! Opening setup...";
                await Task.Delay(400);

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                btnVerifyOtp.Enabled = true;
                btnVerifyOtp.Text = "Verify OTP & Complete Registration";
            }
        }

        private async void LnkResendOtp_Click(object? sender, EventArgs e)
        {
            lblOtpStatus.ForeColor = Color.Firebrick;
            lblOtpStatus.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(_pendingEmail))
            {
                ShowCard(cardCreate);
                return;
            }

            lblOtpStatus.ForeColor = Color.DarkSlateBlue;
            lblOtpStatus.Text = "Resending 6-digit verification code to your email...";

            try
            {
                var (ok, msg) = await AuthService.ResendOtpAsync(_pendingEmail);
                if (ok)
                {
                    lblOtpStatus.ForeColor = Color.DarkGreen;
                    lblOtpStatus.Text = msg;
                    StartSignupCooldown(30);
                }
                else
                {
                    lblOtpStatus.ForeColor = Color.Firebrick;
                    lblOtpStatus.Text = msg;
                }
            }
            catch (Exception ex)
            {
                lblOtpStatus.ForeColor = Color.Firebrick;
                lblOtpStatus.Text = $"Error: {ex.Message}";
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
                var (authOk, authError) = await AuthService.SignInAsync(email, pass, triggerBackgroundPull: false);
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

        private async void BtnForgotSendOtp_Click(object? sender, EventArgs e)
        {
            lblForgotStatus.ForeColor = Color.Firebrick;
            lblForgotStatus.Text = string.Empty;

            var email = txtForgotEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                lblForgotStatus.Text = "Please enter a valid registered email address.";
                return;
            }

            btnForgotSendOtp.Enabled = false;
            btnForgotSendOtp.Text = "Sending Reset OTP...";
            lblForgotStatus.ForeColor = Color.DarkSlateBlue;
            lblForgotStatus.Text = "Verifying account and sending 6-digit reset code...";

            try
            {
                var (ok, msg) = await AuthService.InitiatePasswordResetAsync(email);
                if (!ok)
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = msg;
                    return;
                }

                _pendingForgotEmail = email;
                txtForgotEmail.Enabled = false;
                btnForgotSendOtp.Visible = false;
                pnlForgotStep2.Visible = true;
                txtForgotOtpCode.Clear();
                txtForgotNewPassword.Clear();
                txtForgotConfirm.Clear();

                lblForgotStatus.ForeColor = Color.DarkGreen;
                lblForgotStatus.Text = msg;

                StartForgotCooldown(30);
                txtForgotOtpCode.Focus();
                AcceptButton = btnForgotConfirm;
            }
            finally
            {
                btnForgotSendOtp.Enabled = true;
                btnForgotSendOtp.Text = "📧 Send Reset OTP to Email";
            }
        }

        private async void LnkResendForgotOtp_Click(object? sender, EventArgs e)
        {
            lblForgotStatus.ForeColor = Color.Firebrick;
            lblForgotStatus.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(_pendingForgotEmail))
                return;

            lblForgotStatus.ForeColor = Color.DarkSlateBlue;
            lblForgotStatus.Text = "Resending 6-digit reset code to your email...";

            try
            {
                var (ok, msg) = await AuthService.ResendPasswordResetOtpAsync(_pendingForgotEmail);
                if (ok)
                {
                    lblForgotStatus.ForeColor = Color.DarkGreen;
                    lblForgotStatus.Text = msg;
                    StartForgotCooldown(30);
                }
                else
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = msg;
                }
            }
            catch (Exception ex)
            {
                lblForgotStatus.ForeColor = Color.Firebrick;
                lblForgotStatus.Text = $"Error: {ex.Message}";
            }
        }

        private async void BtnForgotConfirm_Click(object? sender, EventArgs e)
        {
            lblForgotStatus.ForeColor = Color.Firebrick;
            lblForgotStatus.Text = string.Empty;

            var otp = txtForgotOtpCode.Text.Trim();
            var newPass = txtForgotNewPassword.Text;
            var confirm = txtForgotConfirm.Text;

            if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6)
            {
                lblForgotStatus.Text = "Please enter the 6-digit reset code.";
                return;
            }
            if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < 6)
            {
                lblForgotStatus.Text = "New password must be at least 6 characters.";
                return;
            }
            if (newPass != confirm)
            {
                lblForgotStatus.Text = "The passwords do not match.";
                return;
            }

            btnForgotConfirm.Enabled = false;
            btnForgotConfirm.Text = "Resetting password...";
            lblForgotStatus.ForeColor = Color.DarkSlateBlue;
            lblForgotStatus.Text = "Updating password in database...";

            try
            {
                var (ok, msg) = await AuthService.ConfirmPasswordResetAsync(_pendingForgotEmail, otp, newPass);
                if (!ok)
                {
                    lblForgotStatus.ForeColor = Color.Firebrick;
                    lblForgotStatus.Text = msg;
                    return;
                }

                MessageBox.Show("Password reset successfully!\n\nYou can now sign in with your new password.",
                    "Password Reset Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                ShowCard(cardSignIn);
                txtSignInEmail.Text = _pendingForgotEmail;
                txtSignInPassword.Text = newPass;
                txtSignInPassword.Focus();
            }
            finally
            {
                btnForgotConfirm.Enabled = true;
                btnForgotConfirm.Text = "🔐 Confirm Password Reset";
            }
        }
    }
}
