using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;

namespace BillingSuite.App.Forms
{
    public class GeminiGemBrowserForm : Form
    {
        private WebView2 webView = new WebView2();
        private Button btnImportJson = new Button();
        private Button btnGoToGem = new Button();
        private Label lblStatus = new Label();

        public string ExtractedJson { get; private set; } = string.Empty;

        public const string GemUrl = "https://gemini.google.com/gem/1Y6Osbkmsem6vBhJI4p0nAIrEG5JfOQyT?usp=sharing";

        public GeminiGemBrowserForm()
        {
            Text = "Gemini Gem Medical Invoice Assistant (Web Sync)";
            Size = new Size(1100, 750);
            StartPosition = FormStartPosition.CenterParent;
            Services.FormIconHelper.ApplyIcon(this);

            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(240, 244, 248),
                Padding = new Padding(10, 8, 10, 8)
            };

            btnImportJson.Text = "📥 Auto-Import Extracted JSON into Invoice";
            btnImportJson.Dock = DockStyle.Right;
            btnImportJson.Width = 280;
            btnImportJson.BackColor = Color.FromArgb(16, 124, 65);
            btnImportJson.ForeColor = Color.White;
            btnImportJson.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnImportJson.FlatStyle = FlatStyle.Flat;
            btnImportJson.FlatAppearance.BorderSize = 0;
            btnImportJson.Cursor = Cursors.Hand;
            btnImportJson.Click += async (s, e) => await TryExtractAndImportAsync();

            btnGoToGem.Text = "🌐 Reload Gem";
            btnGoToGem.Dock = DockStyle.Left;
            btnGoToGem.Width = 130;
            btnGoToGem.FlatStyle = FlatStyle.Flat;
            btnGoToGem.Click += (s, e) =>
            {
                if (webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Navigate(GemUrl);
                }
            };

            lblStatus.Text = "Log in once with your Google Account. Upload PDF to Gem, then click 'Auto-Import Extracted JSON'.";
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(150, 16);
            lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblStatus.ForeColor = Color.FromArgb(50, 50, 50);

            pnlTop.Controls.Add(lblStatus);
            pnlTop.Controls.Add(btnGoToGem);
            pnlTop.Controls.Add(btnImportJson);

            webView.Dock = DockStyle.Fill;

            Controls.Add(webView);
            Controls.Add(pnlTop);

            this.Load += async (s, e) => await InitializeBrowserAsync();
        }

        private async Task InitializeBrowserAsync()
        {
            try
            {
                lblStatus.Text = "Initializing secure browser & restoring login session...";
                var profilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BilleasePro",
                    "GeminiGemBrowserProfile"
                );
                Directory.CreateDirectory(profilePath);

                var env = await CoreWebView2Environment.CreateAsync(null, profilePath);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0";
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Handle Google Auth & popups smoothly inside the window
                webView.CoreWebView2.NewWindowRequested += (sender, args) =>
                {
                    args.Handled = true;
                    if (!string.IsNullOrEmpty(args.Uri))
                    {
                        webView.CoreWebView2.Navigate(args.Uri);
                    }
                };

                webView.Source = new Uri(GemUrl);
                lblStatus.Text = "Gemini Gem ready. Log in to your Google Account if needed, upload PDF, then click 'Auto-Import Extracted JSON'.";
                lblStatus.ForeColor = Color.DarkGreen;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Browser Error: " + ex.Message;
                lblStatus.ForeColor = Color.Firebrick;
            }
        }

        private async Task TryExtractAndImportAsync()
        {
            try
            {
                // 1. Check Clipboard first
                if (Clipboard.ContainsText())
                {
                    var clipText = Clipboard.GetText();
                    var match = FindJsonInText(clipText);
                    if (!string.IsNullOrEmpty(match))
                    {
                        ExtractedJson = match;
                        DialogResult = DialogResult.OK;
                        Close();
                        return;
                    }
                }

                // 2. Query WebView2 page DOM content for JSON block
                if (webView.CoreWebView2 != null)
                {
                    string innerText = await webView.CoreWebView2.ExecuteScriptAsync("document.body.innerText");
                    var match = FindJsonInText(innerText);
                    if (!string.IsNullOrEmpty(match))
                    {
                        ExtractedJson = match;
                        DialogResult = DialogResult.OK;
                        Close();
                        return;
                    }
                }

                MessageBox.Show(
                    "Could not find valid invoice JSON. Please copy the generated JSON from Gemini Gem or click 'Copy' in the chat, then click Auto-Import.",
                    "JSON Import Hint",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading JSON from browser: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FindJsonInText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length > 2)
            {
                text = Regex.Unescape(text.Substring(1, text.Length - 2));
            }

            var jsonMatch = Regex.Match(text, @"(?:```json|```)?\s*(\{[\s\S]*?""products""[\s\S]*?\})\s*(?:```)?", RegexOptions.IgnoreCase);
            if (jsonMatch.Success)
            {
                var candidate = jsonMatch.Groups[1].Value.Trim();
                try
                {
                    JObject.Parse(candidate);
                    return candidate;
                }
                catch { }
            }

            var objMatch = Regex.Match(text, @"\{[^{}]*?""SUPPLIER""[\s\S]*?\}", RegexOptions.IgnoreCase);
            if (objMatch.Success)
            {
                try
                {
                    JObject.Parse(objMatch.Value);
                    return objMatch.Value;
                }
                catch { }
            }

            return string.Empty;
        }
    }
}
