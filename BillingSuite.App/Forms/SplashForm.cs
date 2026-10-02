using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Sleek, borderless GDI+ Splash Screen featuring smooth Opacity Fade-In,
    /// Fade-Up sliding movement, animated progress bar, and status feedback.
    /// </summary>
    public class SplashForm : Form
    {
        private System.Windows.Forms.Timer _animTimer;
        private double _targetOpacity = 0.0;
        private int _startTop;
        private int _targetTop;
        private float _animProgress = 0f;
        private float _loadProgress = 0f;
        private string _statusText = "Initializing Application Engine...";
        private bool _isFadingOut = false;

        public SplashForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Billease Pro";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(650, 380);
            BackColor = Color.FromArgb(15, 23, 42); // #0F172A Dark Slate
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;

            try
            {
                var icon = Services.FormIconHelper.GetAppIcon();
                if (icon != null) Icon = icon;
            }
            catch { }

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
            _animTimer.Tick += AnimTimer_Tick;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _targetTop = Top;
            _startTop = Top + 25; // 25px slide up offset
            Top = _startTop;
            Opacity = 0.0;

            _animTimer.Start();
            _ = RunInitializationSequenceAsync();
        }

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isFadingOut)
            {
                // Smooth Fade-In & Slide-Up
                if (_animProgress < 1.0f)
                {
                    _animProgress += 0.04f;
                    if (_animProgress > 1.0f) _animProgress = 1.0f;

                    Opacity = _animProgress;
                    Top = (int)(_startTop - (_startTop - _targetTop) * EaseOutCubic(_animProgress));
                }

                // Smooth Progress Bar Interpolation
                Invalidate();
            }
            else
            {
                // Smooth Fade-Out
                if (Opacity > 0.05)
                {
                    Opacity -= 0.08;
                }
                else
                {
                    Opacity = 0.0;
                    _animTimer.Stop();
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }

        private float EaseOutCubic(float x)
        {
            return 1f - (float)Math.Pow(1f - x, 3);
        }

        private async Task RunInitializationSequenceAsync()
        {
            await Task.Delay(200);
            SetStatus("Verifying Encrypted Security Vault...", 0.25f);
            await Task.Delay(250);

            SetStatus("Connecting to 24/7 Cloud Engine...", 0.50f);
            await Task.Delay(300);

            SetStatus("Syncing Business Profile & Databases...", 0.75f);
            await Task.Delay(250);

            SetStatus("Ready!", 1.0f);
            await Task.Delay(200);

            _isFadingOut = true;
        }

        private void SetStatus(string status, float progress)
        {
            _statusText = status;
            _loadProgress = progress;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 1. Render Background Gradient
            using (var brush = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(15, 23, 42),
                Color.FromArgb(30, 41, 59),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(brush, ClientRectangle);
            }

            // 2. Render Outer Border
            using (var pen = new Pen(Color.FromArgb(51, 65, 85), 1.5f))
            {
                var r = ClientRectangle;
                r.Width -= 1;
                r.Height -= 1;
                g.DrawRectangle(pen, r);
            }

            // 3. App Title & Branding
            using (var fontTitle = new Font("Segoe UI", 26F, FontStyle.Bold))
            using (var brushTitle = new SolidBrush(Color.White))
            {
                g.DrawString("BILLEASE PRO", fontTitle, brushTitle, new PointF(50, 70));
            }

            using (var fontSub = new Font("Segoe UI", 12F, FontStyle.Regular))
            using (var brushSub = new SolidBrush(Color.FromArgb(56, 189, 248))) // #38BDF8 Light Blue
            {
                g.DrawString("Enterprise Billing Suite & 24/7 Cloud Sync", fontSub, brushSub, new PointF(52, 118));
            }

            // 4. Status Text
            using (var fontStatus = new Font("Segoe UI", 9.5F, FontStyle.Regular))
            using (var brushStatus = new SolidBrush(Color.FromArgb(148, 163, 184))) // #94A3B8
            {
                g.DrawString(_statusText, fontStatus, brushStatus, new PointF(52, 235));
            }

            // 5. Animated Glowing Progress Bar Track
            int barX = 52;
            int barY = 265;
            int barW = Width - 104;
            int barH = 6;

            using (var trackBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            {
                g.FillRectangle(trackBrush, barX, barY, barW, barH);
            }

            // Filled Progress Bar
            int fillW = (int)(barW * _loadProgress);
            if (fillW > 0)
            {
                using var fillBrush = new LinearGradientBrush(
                    new Rectangle(barX, barY, fillW, barH),
                    Color.FromArgb(37, 99, 235), // Blue
                    Color.FromArgb(56, 189, 248), // Light Cyan
                    LinearGradientMode.Horizontal);

                g.FillRectangle(fillBrush, barX, barY, fillW, barH);
            }

            // Version info at bottom right
            using (var fontVer = new Font("Segoe UI", 8.5F, FontStyle.Regular))
            using (var brushVer = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                g.DrawString("v2.5 Pro", fontVer, brushVer, new PointF(Width - 100, Height - 35));
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _animTimer?.Stop();
            _animTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
