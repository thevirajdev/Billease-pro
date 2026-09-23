using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using BillingSuite.App.Models;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    public class AiChatSidePanel : UserControl
    {
        private readonly RichTextBox _chatLog = new RichTextBox();
        private readonly TextBox _txtInput = new TextBox();
        private readonly Button _btnSend = new Button();
        private readonly ProgressBar _progressBar = new ProgressBar();
        private readonly Label _lblStatus = new Label();
        
        private readonly AiAgentService _aiService;
        private readonly AiDocumentProcessor _docProcessor;
        private readonly bool _isFloating;

        public event Action<AiActionResponse>? ActionApproved;

        public AiChatSidePanel(bool isFloating = false)
        {
            _isFloating = isFloating;
            _aiService = new AiAgentService();
            _docProcessor = new AiDocumentProcessor(_aiService);
            
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
            var btnNewChat = new Button { Text = "New Chat", Dock = DockStyle.Left, Width = 80, FlatStyle = FlatStyle.Flat, ForeColor = Color.LightGray };
            btnNewChat.Click += (s, e) => NewChat();
            
            pnlTop.Controls.Add(btnNewChat);

            _chatLog.Dock = DockStyle.Fill;
            _chatLog.BorderStyle = BorderStyle.None;
            _chatLog.BackColor = Color.FromArgb(45, 45, 48);
            _chatLog.ForeColor = Color.FromArgb(220, 220, 220);
            _chatLog.ReadOnly = true;
            _chatLog.Font = new Font("Segoe UI", 10);
            _chatLog.AllowDrop = true;
            _chatLog.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            _chatLog.DragDrop += OnFileDrop;

            var pnlInput = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(10) };
            _txtInput.Dock = DockStyle.Fill;
            _txtInput.Multiline = true;
            _txtInput.BackColor = Color.FromArgb(60, 60, 60);
            _txtInput.ForeColor = Color.White;
            _txtInput.BorderStyle = BorderStyle.FixedSingle;
            _txtInput.PlaceholderText = "Type message or drop PDF/Excel here...";
            _txtInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && !e.Control) { e.SuppressKeyPress = true; SendMessage(); } };

            _btnSend.Dock = DockStyle.Right;
            _btnSend.Width = 60;
            _btnSend.Text = "Send";
            _btnSend.FlatStyle = FlatStyle.Flat;
            _btnSend.Click += (s, e) => SendMessage();

            pnlInput.Controls.Add(_txtInput);
            pnlInput.Controls.Add(_btnSend);

            _progressBar.Dock = DockStyle.Bottom;
            _progressBar.Height = 8;
            _progressBar.Visible = false;

            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 20;
            _lblStatus.Text = "Gemini AI Ready";
            _lblStatus.Font = new Font("Segoe UI", 8);

            Controls.Add(_chatLog);
            Controls.Add(pnlTop);
            Controls.Add(_progressBar);
            Controls.Add(_lblStatus);
            Controls.Add(pnlInput);

            this.Load += (s, e) => LoadHistory();
        }

        private async void LoadHistory()
        {
            try
            {
                using var db = new Data.AppDbContext();
                var messages = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.AiChatMessages);
                if (messages.Count == 0)
                {
                    AppendChat("System", "Welcome to AI Billing Assistant! Use natural language or drop a file to start.", saveToDb: false);
                }
                else
                {
                    foreach (var m in messages)
                    {
                        AppendChatInternal(m.Role, m.Content, m.Timestamp);
                    }
                }
            }
            catch { }
        }

        private void NewChat()
        {
            if (MessageBox.Show("Delete current chat history and start fresh?", "New Chat", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _chatLog.Clear();
                using (var db = new Data.AppDbContext())
                {
                    db.Database.ExecuteSqlRaw("DELETE FROM AiChatMessages");
                }
                AppendChat("System", "Chat cleared. Starting new session.", saveToDb: true);
            }
        }

        private async void OnFileDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                var file = files[0];
                var ext = Path.GetExtension(file).ToLower();
                
                SetStatus("Processing file: " + Path.GetFileName(file), true);
                AppendChat("User", "[File Uploaded: " + Path.GetFileName(file) + "]");

                string text = "";
                if (ext == ".pdf") text = await _docProcessor.ExtractTextFromPdfAsync(file);
                else if (ext == ".xlsx" || ext == ".xls") text = await _docProcessor.ExtractTextFromExcelAsync(file);
                else { SetStatus("Unsupported file type.", false); return; }

                var response = await _docProcessor.ProcessRawTextWithAiAsync(text);
                HandleAiResponse(response);
                SetStatus("Gemini AI Ready", false);
            }
        }

        private async void SendMessage()
        {
            var msg = _txtInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(msg)) return;

            AppendChat("You", msg);
            _txtInput.Clear();
            SetStatus("Thinking...", true);

            // Fetch Live Context for AI Awareness
            var context = AiContextManager.GetCurrentContextData();
            var formName = AiContextManager.GetActiveFormName();

            var response = await _aiService.GetAiActionAsync(msg, context, formName);
            HandleAiResponse(response);
            SetStatus("Gemini AI Ready", false);
        }

        private async void HandleAiResponse(AiActionResponse response)
        {
            AppendChat("AI", response.Message);
            
            if (response.Action == "active_ui_action")
            {
                // Directly manipulate the UI
                bool success = await AiContextManager.PerformActiveActionAsync("ui_update", response.Data);
                if (success) AppendChat("System", "UI updated successfully.", saveToDb: false);
                else AppendChat("System", "Failed to apply UI update.", saveToDb: false);
                return;
            }

            if (response.Action != "no_action")
            {
                // Show review dialog for DB actions
                using (var review = new AiActionReviewForm(response))
                {
                    if (review.ShowDialog(this) == DialogResult.OK)
                    {
                        ActionApproved?.Invoke(review.ResultResponse);
                    }
                }
            }
        }

        private void AppendChat(string sender, string message, bool saveToDb = true)
        {
            AppendChatInternal(sender, message, DateTime.Now);

            if (saveToDb)
            {
                try
                {
                    using var db = new Data.AppDbContext();
                    db.AiChatMessages.Add(new AiChatMessage { Role = sender, Content = message, Timestamp = DateTime.Now });
                    db.SaveChanges();
                }
                catch { }
            }
        }

        private void AppendChatInternal(string sender, string message, DateTime ts)
        {
            if (this.InvokeRequired) { this.Invoke(() => AppendChatInternal(sender, message, ts)); return; }
            _chatLog.SelectionStart = _chatLog.TextLength;
            _chatLog.SelectionLength = 0;
            _chatLog.SelectionColor = sender == "AI" ? Color.SkyBlue : (sender == "System" ? Color.Khaki : Color.LightGray);
            _chatLog.AppendText($"[{sender}] {ts:HH:mm}\n");
            _chatLog.SelectionColor = Color.White;
            _chatLog.AppendText($"{message}\n\n");
            _chatLog.ScrollToCaret();
        }

        private void SetStatus(string msg, bool busy)
        {
            _lblStatus.Text = msg;
            _progressBar.Visible = busy;
            _progressBar.Style = ProgressBarStyle.Marquee;
            _btnSend.Enabled = !busy;
        }
    }
}
