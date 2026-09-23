using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Services;

namespace BillingSuite.App.Forms
{
    /// <summary>
    /// Full-window onboarding form shown after login/signup when business details are not set.
    /// Captures Company Name, Address, GSTIN, Phone, Email, Currency, etc. in a clean, spacious layout.
    /// </summary>
    public class OnboardingSetupForm : Form
    {
        private TextBox txtCompanyName = new();
        private TextBox txtTagline = new();
        private TextBox txtPhone = new();
        private TextBox txtEmail = new();
        private TextBox txtAddress1 = new();
        private TextBox txtAddress2 = new();
        private TextBox txtCity = new();
        private TextBox txtState = new();
        private TextBox txtPostalCode = new();
        private TextBox txtGstNumber = new();
        private TextBox txtCurrency = new();
        private TextBox txtInvoicePrefix = new();
        private Button btnSave = new();
        private Label lblError = new();

        public OnboardingSetupForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Billease Pro - Complete Your Business Profile";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(820, 680);
            MinimumSize = new Size(760, 600);
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Color.FromArgb(248, 250, 252); // Light background

            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "billease.ico");
                if (System.IO.File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }

            // Header Banner
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(15, 23, 42), // Dark slate header
                Padding = new Padding(24, 16, 24, 16)
            };

            var lblTitle = new Label
            {
                Text = "Welcome to Billease Pro! 🎉",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblSubtitle = new Label
            {
                Text = "Set up your business profile below. These details will appear on your invoices and receipts.",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(lblTitle);

            // Scrollable Content
            var pnlScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(24)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(12)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Section 1: Business Identity
            AddSectionHeader(table, "🏢 Business Identity");

            txtCompanyName.Width = 330;
            txtCompanyName.Text = AuthService.CurrentUser?.CompanyName ?? AppSettingsService.CompanyName;
            if (txtCompanyName.Text == "Your Company") txtCompanyName.Text = "";
            AddInput(table, "Shop / Company Name (Required)", txtCompanyName, 0);

            txtTagline.Width = 330;
            txtTagline.Text = AppSettingsService.GetString("company.tagline");
            AddInput(table, "Tagline / Subtitle", txtTagline, 1);

            txtPhone.Width = 330;
            txtPhone.Text = AuthService.CurrentUser?.CompanyPhone ?? AppSettingsService.CompanyPhone;
            AddInput(table, "Phone Number", txtPhone, 0);

            txtEmail.Width = 330;
            txtEmail.Text = AuthService.CurrentUser?.CompanyEmail ?? AuthService.CurrentUser?.Email ?? AppSettingsService.CompanyEmail;
            AddInput(table, "Business Email", txtEmail, 1);

            // Section 2: Address & Location
            AddSectionHeader(table, "📍 Address Details");

            txtAddress1.Width = 330;
            txtAddress1.Text = AuthService.CurrentUser?.CompanyAddress ?? AppSettingsService.CompanyAddress1;
            AddInput(table, "Address Line 1", txtAddress1, 0);

            txtAddress2.Width = 330;
            txtAddress2.Text = AppSettingsService.GetString(AppSettingKeys.CompanyAddress2);
            AddInput(table, "Address Line 2", txtAddress2, 1);

            txtCity.Width = 330;
            txtCity.Text = AppSettingsService.GetString(AppSettingKeys.CompanyCity);
            AddInput(table, "City", txtCity, 0);

            txtState.Width = 330;
            txtState.Text = AppSettingsService.GetString(AppSettingKeys.CompanyState);
            AddInput(table, "State", txtState, 1);

            // Section 3: Tax & Currency
            AddSectionHeader(table, "💼 Billing & Tax Settings");

            txtGstNumber.Width = 330;
            txtGstNumber.Text = AuthService.CurrentUser?.CompanyTaxNumber ?? AppSettingsService.CompanyTaxNumber;
            AddInput(table, "GSTIN / Tax Registration No.", txtGstNumber, 0);

            txtCurrency.Width = 330;
            txtCurrency.Text = AppSettingsService.CurrencySymbol;
            if (string.IsNullOrWhiteSpace(txtCurrency.Text)) txtCurrency.Text = "₹";
            AddInput(table, "Currency Symbol (e.g. ₹ or $)", txtCurrency, 1);

            txtInvoicePrefix.Width = 330;
            txtInvoicePrefix.Text = AppSettingsService.GetString(AppSettingKeys.InvoicePrefix, "INV-");
            if (string.IsNullOrWhiteSpace(txtInvoicePrefix.Text)) txtInvoicePrefix.Text = "INV-";
            AddInput(table, "Invoice Number Prefix", txtInvoicePrefix, 0);

            pnlScroll.Controls.Add(table);

            // Footer / Action Bar
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(24, 12, 24, 12)
            };

            lblError.ForeColor = Color.Firebrick;
            lblError.AutoSize = true;
            lblError.Location = new Point(24, 24);

            btnSave.Text = "🚀 Save Profile & Launch Billease Pro";
            btnSave.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnSave.BackColor = Color.FromArgb(37, 99, 235);
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Size = new Size(320, 42);
            btnSave.Location = new Point(pnlFooter.Width - 340, 14);
            btnSave.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnSave.Click += BtnSave_Click;

            pnlFooter.Controls.Add(lblError);
            pnlFooter.Controls.Add(btnSave);

            Controls.Add(pnlScroll);
            Controls.Add(pnlHeader);
            Controls.Add(pnlFooter);

            AcceptButton = btnSave;
        }

        private void AddSectionHeader(TableLayoutPanel table, string title)
        {
            var lbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Margin = new Padding(0, 18, 0, 8),
                AutoSize = true
            };
            table.Controls.Add(lbl);
            table.SetColumnSpan(lbl, 2);
        }

        private void AddInput(TableLayoutPanel table, string labelText, Control inputControl, int column)
        {
            var pnl = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Margin = new Padding(0, 4, 12, 12)
            };

            var lbl = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            pnl.Controls.Add(lbl);
            pnl.Controls.Add(inputControl);

            table.Controls.Add(pnl, column, table.RowCount - 1);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            lblError.Text = string.Empty;

            var companyName = txtCompanyName.Text.Trim();
            if (string.IsNullOrWhiteSpace(companyName))
            {
                lblError.Text = "Shop / Company Name is required.";
                txtCompanyName.Focus();
                return;
            }

            try
            {
                var settings = new List<KeyValuePair<string, string?>>
                {
                    new(AppSettingKeys.CompanyName, companyName),
                    new("company.tagline", txtTagline.Text.Trim()),
                    new(AppSettingKeys.CompanyPhone, txtPhone.Text.Trim()),
                    new(AppSettingKeys.CompanyEmail, txtEmail.Text.Trim()),
                    new(AppSettingKeys.CompanyAddress1, txtAddress1.Text.Trim()),
                    new(AppSettingKeys.CompanyAddress2, txtAddress2.Text.Trim()),
                    new(AppSettingKeys.CompanyCity, txtCity.Text.Trim()),
                    new(AppSettingKeys.CompanyState, txtState.Text.Trim()),
                    new(AppSettingKeys.CompanyTaxNumber, txtGstNumber.Text.Trim()),
                    new(AppSettingKeys.CurrencySymbol, string.IsNullOrWhiteSpace(txtCurrency.Text) ? "₹" : txtCurrency.Text.Trim()),
                    new(AppSettingKeys.InvoicePrefix, string.IsNullOrWhiteSpace(txtInvoicePrefix.Text) ? "INV-" : txtInvoicePrefix.Text.Trim())
                };

                AppSettingsService.SetMany(settings);
                AppSettingsService.Invalidate();

                // Update current user profile if logged in
                if (AuthService.CurrentUser != null)
                {
                    AuthService.CurrentUser.CompanyName = companyName;
                    AuthService.CurrentUser.CompanyPhone = txtPhone.Text.Trim();
                    AuthService.CurrentUser.CompanyEmail = txtEmail.Text.Trim();
                    AuthService.CurrentUser.CompanyAddress = txtAddress1.Text.Trim();
                    AuthService.CurrentUser.CompanyTaxNumber = txtGstNumber.Text.Trim();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblError.Text = $"Could not save business profile: {ex.Message}";
            }
        }
    }
}
