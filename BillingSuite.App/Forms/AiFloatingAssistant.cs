using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Floating AI Copilot window. Stays above the main form, can be expanded/collapsed,
    /// dragged anywhere on screen, and resized in expanded mode.
    /// </summary>
    public class AiFloatingAssistant : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        // Win32 hit-test constants for native resize/drag
        private const int WM_NCHITTEST    = 0x84;
        private const int HTCAPTION       = 2;
        private const int HTLEFT          = 10;
        private const int HTRIGHT         = 11;
        private const int HTTOP           = 12;
        private const int HTTOPLEFT       = 13;
        private const int HTTOPRIGHT      = 14;
        private const int HTBOTTOM        = 15;
        private const int HTBOTTOMLEFT    = 16;
        private const int HTBOTTOMRIGHT   = 17;

        // State
        private bool _isExpanded = false;
        private readonly Panel _pnlBubble  = new Panel();
        private readonly Panel _pnlContent = new Panel();
        private AiChatSidePanel? _chatPanel;

        // Bubble manual drag (avoids HTCAPTION which swallows click events)
        private bool   _bubbleDragging  = false;
        private bool   _bubbleMoved     = false;
        private Point  _bubbleMouseDown;
        private Point  _bubbleFormStart;

        // Event exposed to Form1 so approved actions can be executed
        public event Action<AiActionResponse>? ActionApproved;

        public AiFloatingAssistant()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;
            BackColor       = Color.FromArgb(30, 30, 30);
            Size            = new Size(60, 60);
            StartPosition   = FormStartPosition.Manual;
            // Default: bottom-right corner
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
            Location = new Point(wa.Right - 80, wa.Bottom - 80);

            InitComponents();
            ApplyBubbleRegion();
        }

        // ─────────────────────────────────────── WndProc (resize in expanded mode) ───
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && _isExpanded)
            {
                var pos = PointToClient(new Point(m.LParam.ToInt32()));
                const int B = 8; // resize border px
                bool l = pos.X <= B, r = pos.X >= Width  - B;
                bool t = pos.Y <= B, b = pos.Y >= Height - B;

                if (l && t) { m.Result = (IntPtr)HTTOPLEFT;     return; }
                if (r && t) { m.Result = (IntPtr)HTTOPRIGHT;    return; }
                if (l && b) { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
                if (r && b) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                if (l)      { m.Result = (IntPtr)HTLEFT;        return; }
                if (r)      { m.Result = (IntPtr)HTRIGHT;       return; }
                if (t)      { m.Result = (IntPtr)HTTOP;         return; }
                if (b)      { m.Result = (IntPtr)HTBOTTOM;      return; }
                // Header area (excluding the right 70 px where buttons live)
                if (pos.Y <= 36 && pos.X < Width - 70)
                { m.Result = (IntPtr)HTCAPTION; return; }
            }
            base.WndProc(ref m);
        }

        // ─────────────────────────────────────── Build UI ───────────────────────────
        private void InitComponents()
        {
            // ── Bubble (collapsed state) ──────────────────────────────────────────
            _pnlBubble.Dock      = DockStyle.Fill;
            _pnlBubble.BackColor = Color.FromArgb(0, 120, 215);
            _pnlBubble.Cursor    = Cursors.SizeAll;

            var lblG = new Label
            {
                Text      = "AI",
                ForeColor = Color.White,
                Font      = new Font("Segoe UI Semibold", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill,
                Cursor    = Cursors.SizeAll
            };

            // Use manual mouse tracking so click AND drag both work
            lblG.MouseDown  += Bubble_MouseDown;
            lblG.MouseMove  += Bubble_MouseMove;
            lblG.MouseUp    += Bubble_MouseUp;
            _pnlBubble.MouseDown += Bubble_MouseDown;
            _pnlBubble.MouseMove += Bubble_MouseMove;
            _pnlBubble.MouseUp   += Bubble_MouseUp;

            _pnlBubble.Controls.Add(lblG);
            Controls.Add(_pnlBubble);

            // ── Expanded content panel ────────────────────────────────────────────
            _pnlContent.Dock      = DockStyle.Fill;
            _pnlContent.BackColor = Color.FromArgb(30, 30, 30);
            _pnlContent.Visible   = false;

            // Header bar
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 36,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            var lblTitle = new Label
            {
                Text      = "✦ Gemini AI Copilot",
                ForeColor = Color.White,
                Font      = new Font("Segoe UI Semibold", 10),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0),
                Dock      = DockStyle.Fill
            };

            var btnClose = new Button
            {
                Text      = "✕",
                Size      = new Size(36, 36),
                Dock      = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 11),
                Cursor    = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => Collapse();
            header.Controls.Add(lblTitle);
            header.Controls.Add(btnClose);

            _pnlContent.Controls.Add(header);
            Controls.Add(_pnlContent);
        }

        // ─────────────────────────────────────── Bubble drag logic ──────────────────
        private void Bubble_MouseDown(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _bubbleDragging  = true;
            _bubbleMoved     = false;
            _bubbleMouseDown = Cursor.Position;
            _bubbleFormStart = Location;
        }

        private void Bubble_MouseMove(object? s, MouseEventArgs e)
        {
            if (!_bubbleDragging) return;
            int dx = Cursor.Position.X - _bubbleMouseDown.X;
            int dy = Cursor.Position.Y - _bubbleMouseDown.Y;
            if (Math.Abs(dx) > 4 || Math.Abs(dy) > 4)
                _bubbleMoved = true;
            if (_bubbleMoved)
                Location = new Point(_bubbleFormStart.X + dx, _bubbleFormStart.Y + dy);
        }

        private void Bubble_MouseUp(object? s, MouseEventArgs e)
        {
            if (!_bubbleDragging) return;
            _bubbleDragging = false;
            // If mouse didn't move significantly → treat as a click → toggle
            if (!_bubbleMoved)
                ToggleState();
        }

        // ─────────────────────────────────────── Expand / Collapse ──────────────────
        private void ToggleState()
        {
            if (_isExpanded) Collapse(); else Expand();
        }

        private void Expand()
        {
            _isExpanded = true;
            _pnlBubble.Visible  = false;
            _pnlContent.Visible = true;
            Region = null; // Remove circle; allow native resize on edges

            // Re-position so the panel opens upward-left of the bubble
            int newW = 400, newH = 600;
            var wa = Screen.FromControl(this).WorkingArea;
            int newX = Math.Max(wa.Left, Location.X - (newW - 60));
            int newY = Math.Max(wa.Top,  Location.Y - (newH - 60));
            Location = new Point(newX, newY);
            Size     = new Size(newW, newH);

            if (_chatPanel == null)
            {
                _chatPanel = new AiChatSidePanel(isFloating: true);
                _chatPanel.Dock = DockStyle.Fill;
                _chatPanel.ActionApproved += r => ActionApproved?.Invoke(r);
                _pnlContent.Controls.Add(_chatPanel);
                _chatPanel.BringToFront();
            }
        }

        private void Collapse()
        {
            _isExpanded = false;
            _pnlContent.Visible = false;
            _pnlBubble.Visible  = true;

            // Return to bubble size at current top-left position (snap to bottom-right)
            var wa  = Screen.FromControl(this).WorkingArea;
            Location = new Point(wa.Right - 80, wa.Bottom - 80);
            Size     = new Size(60, 60);
            ApplyBubbleRegion();
        }

        private void ApplyBubbleRegion()
        {
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, Width, Height));
        }
    }
}
