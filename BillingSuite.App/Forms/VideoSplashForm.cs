using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Controls;
using System.Windows.Media;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Sleek borderless video splash screen that plays splash.mp4 on app launch.
    /// Auto-closes upon video completion or keypress / mouse click.
    /// </summary>
    public class VideoSplashForm : Form
    {
        private ElementHost? _elementHost;
        private MediaElement? _mediaElement;
        private System.Windows.Forms.Timer? _timeoutTimer;
        private bool _isClosing = false;

        public VideoSplashForm()
        {
            InitializeComponent();
            InitializeMedia();
        }

        private void InitializeComponent()
        {
            Text = "Billease Pro Loading...";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(800, 450); // 16:9 ratio centered window
            BackColor = System.Drawing.Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;

            // Load app icon if available
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "billease.ico");
                if (File.Exists(iconPath))
                    Icon = new Icon(iconPath);
            }
            catch { }
        }

        private void InitializeMedia()
        {
            try
            {
                var videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "splash.mp4");
                if (!File.Exists(videoPath))
                {
                    videoPath = Path.Combine(Application.StartupPath, "splash.mp4");
                }

                if (!File.Exists(videoPath))
                {
                    // Video missing, close immediately
                    Load += (s, e) => CloseSplash();
                    return;
                }

                _mediaElement = new MediaElement
                {
                    LoadedBehavior = MediaState.Play,
                    UnloadedBehavior = MediaState.Close,
                    Stretch = Stretch.UniformToFill,
                    Volume = 1.0,
                    Source = new Uri(videoPath, UriKind.Absolute)
                };

                _mediaElement.MediaEnded += (s, e) => InvokeClose();
                _mediaElement.MediaFailed += (s, e) => InvokeClose();

                _elementHost = new ElementHost
                {
                    Dock = DockStyle.Fill,
                    Child = _mediaElement,
                    BackColor = System.Drawing.Color.Black
                };

                // Mouse click to skip
                _elementHost.Click += (s, e) => CloseSplash();
                Click += (s, e) => CloseSplash();
                KeyDown += (s, e) => CloseSplash();

                Controls.Add(_elementHost);

                // Safety timeout (8.5 seconds max)
                _timeoutTimer = new System.Windows.Forms.Timer { Interval = 8500 };
                _timeoutTimer.Tick += (s, e) => CloseSplash();
                _timeoutTimer.Start();
            }
            catch
            {
                CloseSplash();
            }
        }

        private void InvokeClose()
        {
            try
            {
                if (InvokeRequired)
                    BeginInvoke(new Action(CloseSplash));
                else
                    CloseSplash();
            }
            catch
            {
                CloseSplash();
            }
        }

        private void CloseSplash()
        {
            if (_isClosing) return;
            _isClosing = true;

            try
            {
                _timeoutTimer?.Stop();
                _timeoutTimer?.Dispose();
                _timeoutTimer = null;

                if (_mediaElement != null)
                {
                    _mediaElement.Stop();
                    _mediaElement.Source = null;
                }
            }
            catch { }

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            CloseSplash();
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
