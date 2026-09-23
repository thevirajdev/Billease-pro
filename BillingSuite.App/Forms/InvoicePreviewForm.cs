using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;

namespace BillingSuite.App.Forms
{
    public class InvoicePreviewForm : Form
    {
        private readonly WebBrowser webBrowser;
        private readonly Button btnPrint;
        private readonly Button btnSavePdf;
        private readonly Invoice invoice;
        private readonly string htmlContent;

        public InvoicePreviewForm(Invoice invoice, string htmlContent)
        {
            this.invoice = invoice;
            this.htmlContent = htmlContent;
            
            Text = $"Invoice Preview - {invoice.InvoiceNumber}";
            this.Size = new System.Drawing.Size(900, 800);
            StartPosition = FormStartPosition.CenterParent;
            
            // Create web browser control
            webBrowser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                WebBrowserShortcutsEnabled = false,
                AllowWebBrowserDrop = false,
                IsWebBrowserContextMenuEnabled = false,
                ScriptErrorsSuppressed = true
            };

            // Create buttons panel
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            // Add Print button
            btnPrint = new Button
            {
                Text = "Print",
                Size = new System.Drawing.Size(100, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(680, 10)
            };
            btnPrint.Click += (s, e) => webBrowser.ShowPrintDialog();

            // Add Save as PDF button
            btnSavePdf = new Button
            {
                Text = "Save as PDF",
                Size = new System.Drawing.Size(100, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(790, 10)
            };
            btnSavePdf.Click += BtnSavePdf_Click;

            // Add controls to form
            panel.Controls.Add(btnPrint);
            panel.Controls.Add(btnSavePdf);
            
            Controls.Add(webBrowser);
            Controls.Add(panel);

            // Load the HTML content
            webBrowser.DocumentText = htmlContent;
        }

        /// <summary>
        /// Applies the paper size, orientation and margin chosen in Settings. Every
        /// QuestPDF render path calls this so they cannot drift apart.
        /// </summary>
        internal static void ApplyPageSetup(QuestPDF.Fluent.PageDescriptor page)
        {
            var size = Services.AppSettingsService.InvoicePageSize;
            var landscape = Services.AppSettingsService.InvoiceLandscape;

            switch (size?.Trim().ToUpperInvariant())
            {
                case "LETTER": page.Size(landscape ? PageSizes.Letter.Landscape() : PageSizes.Letter); break;
                case "LEGAL": page.Size(landscape ? PageSizes.Legal.Landscape() : PageSizes.Legal); break;
                case "A5": page.Size(landscape ? PageSizes.A5.Landscape() : PageSizes.A5); break;
                case "THERMAL80": page.Size(new QuestPDF.Helpers.PageSize(80, 297, QuestPDF.Infrastructure.Unit.Millimetre)); break;
                case "THERMAL58": page.Size(new QuestPDF.Helpers.PageSize(58, 297, QuestPDF.Infrastructure.Unit.Millimetre)); break;
                default: page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4); break;
            }

            page.Margin((float)Services.AppSettingsService.InvoiceMarginMm);
        }

        public static string BuildHtmlFor(Invoice inv)
        {
            // Compact HTML with items, totals (including Extra/Discount), and payments summary
            var css = @"body { font-family: Arial, sans-serif; margin: 0; padding: 12px; color: #333; }
.viewport { width: 100%; }
.header { margin-bottom: 8px; }
.company-info h2 { margin: 0; font-size: 18px; }
.invoice-info { text-align: right; }
.invoice-info h2 { margin: 0; font-size: 16px; }
.table-wrap { overflow: visible; }
table { width: 100%; border-collapse: collapse; margin-bottom: 12px; table-layout: auto; }
th, td { border: 1px solid #ddd; padding: 6px; text-align: left; white-space: normal; overflow: visible; text-overflow: clip; width: auto !important; }
th { background-color: #f2f2f2; }
.text-right { text-align: right; }
.text-center { text-align: center; }
.total-row { font-weight: bold; }
.section-title { font-weight:bold; margin: 10px 0 6px; }
thead { display: table-header-group; }
tfoot { display: table-footer-group; }
@page { size: {PAGESIZE}; margin: {MARGIN}mm; }
@media print {
  body { margin: 0; padding: 0; }
  .viewport { width: auto; }
  .table-wrap { overflow: visible; }
  table, tr { page-break-inside: auto; }
  tr { page-break-inside: avoid; page-break-after: auto; }
  thead { display: table-header-group; }
  tfoot { display: table-footer-group; }
}";
            // The CSS above is a verbatim string, so the page setup is substituted
            // here rather than interpolated inside it (CSS braces would not survive).
            css = css
                .Replace("{PAGESIZE}", Services.AppSettingsService.InvoicePageSize)
                .Replace("{MARGIN}", Services.AppSettingsService.InvoiceMarginMm.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Phase 6: honour the invoice content toggles from Settings. Each guarded block
            // omits its element when the user turned it off in Settings > Invoice Content.
            var showCompanyAddress = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyAddress);
            var showCompanyPhone = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyPhone);
            var showCompanyEmail = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyEmail);
            var showCompanyTax = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyTaxNumber);
            var showBillTo = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowBillTo);
            var showCustomerAddress = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCustomerAddress);
            var showCustomerPhone = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCustomerPhone);
            var showCustomerTax = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCustomerTaxNumber);
            var showDueDate = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowDueDate);
            var showScheme = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowScheme);
            var showPack = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowPack);
            var showBatch = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowBatch);
            var showExpiry = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowExpiry);
            var showMrp = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowMrp);
            var showTaxAmount = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowTaxAmount);
            var showDiscount = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowDiscount);
            var showBackDues = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowBackDues);
            var showRoundOff = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowRoundOff);

            var companyBlock = new System.Text.StringBuilder();
            companyBlock.Append("<h2>" + System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyName) + "</h2>");
            if (showCompanyAddress)
                companyBlock.Append("<div>" + System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyAddressBlock()).Replace("\r\n", "<br/>").Replace("\n", "<br/>") + "</div>");
            if (showCompanyPhone && !string.IsNullOrWhiteSpace(Services.AppSettingsService.CompanyPhone))
                companyBlock.Append("<div>" + System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyPhone) + "</div>");
            if (showCompanyEmail && !string.IsNullOrWhiteSpace(Services.AppSettingsService.CompanyEmail))
                companyBlock.Append("<div>" + System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyEmail) + "</div>");
            if (showCompanyTax && !string.IsNullOrWhiteSpace(Services.AppSettingsService.CompanyTaxNumber))
                companyBlock.Append("<div>" + System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyTaxLabel) + " " + System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyTaxNumber) + "</div>");

            var customerBlock = new System.Text.StringBuilder();
            if (showBillTo)
            {
                customerBlock.Append("<div style='margin-top:6px;'><strong>Bill To:</strong> " + System.Net.WebUtility.HtmlEncode(inv.Customer?.Name ?? "") + "</div>");
                if (showCustomerAddress && inv.Customer != null)
                {
                    var ca = GetCustomerAddressStatic(inv.Customer);
                    if (!string.IsNullOrWhiteSpace(ca))
                        customerBlock.Append("<div>" + System.Net.WebUtility.HtmlEncode(ca).Replace("\r\n", "<br/>").Replace("\n", "<br/>") + "</div>");
                }
                if (showCustomerPhone && !string.IsNullOrWhiteSpace(inv.Customer?.Phone))
                    customerBlock.Append("<div>" + System.Net.WebUtility.HtmlEncode(inv.Customer.Phone) + "</div>");
                if (showCustomerTax && inv.Customer != null && !string.IsNullOrWhiteSpace(inv.Customer.GstVatNumber))
                    customerBlock.Append("<div>" + System.Net.WebUtility.HtmlEncode(inv.Customer.GstVatNumber) + "</div>");
            }

            var invoiceMeta = new System.Text.StringBuilder();
            invoiceMeta.Append("<div><strong>" + System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.InvoiceTitle) + " #:</strong> " + System.Net.WebUtility.HtmlEncode(inv.InvoiceNumber) + "</div>");
            invoiceMeta.Append("<div><strong>Date:</strong> " + inv.InvoiceDate.ToString("dd/MM/yyyy") + "</div>");
            if (showDueDate && inv.DueDate.HasValue)
                invoiceMeta.Append("<div><strong>Due:</strong> " + inv.DueDate.Value.ToString("dd/MM/yyyy") + "</div>");

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8' /><title>Invoice {inv.InvoiceNumber}</title>
<style>{css}</style></head><body><div class='viewport'>
<div class='header'><div style='display:flex; justify-content:space-between; align-items:flex-start;'><div class='company-info'>{companyBlock}</div><div class='invoice-info'>{invoiceMeta}</div></div>{customerBlock}</div>
<div class='table-wrap'><table><thead><tr>
<th style='width:40px;'>SL</th>
{(showScheme ? "<th style='width:90px;'>Scheme</th>" : "")}
<th>Name</th>
{(showPack ? "<th style='width:70px;'>Pack</th>" : "")}
<th style='width:90px;'>Mkt.</th>
{(showBatch ? "<th style='width:100px;'>Batch No.</th>" : "")}
{(showExpiry ? "<th style='width:90px;'>Expiry</th>" : "")}
{(showMrp ? "<th style='width:80px;' class='text-right'>MRP</th>" : "")}
<th style='width:90px;' class='text-right'>Price</th>
<th style='width:80px;' class='text-right'>Qty</th>
<th style='width:110px;' class='text-right'>Amount</th>
</tr></thead><tbody>";
            int idx = 1;
            foreach (var it in inv.Items)
            {
                string name;
                try
                {
                    using var db = new AppDbContext();
                    name = db.Products.FirstOrDefault(p => (p.Barcode == it.ProductId) || (it.ProductIdRef.HasValue && p.Id == it.ProductIdRef.Value))?.Name ?? (it.ProductId ?? "");
                }
                catch { name = it.ProductId ?? string.Empty; }
                string exp = string.Empty, scheme = string.Empty, pack = string.Empty, mkt = string.Empty, batch = string.Empty; decimal? mrp = null;
                if (!string.IsNullOrWhiteSpace(it.Description))
                {
                    foreach (var part in it.Description.Split('|'))
                    {
                        var kv = part.Split(':'); if (kv.Length == 2)
                        {
                            var k = kv[0].Trim().ToUpperInvariant(); var v = kv[1].Trim();
                            if (k == "EXP") { if (DateTime.TryParse(v, out var dt)) exp = dt.ToString("dd/MM/yyyy"); else exp = v; }
                            else if (k == "MRP") { if (decimal.TryParse(v, out var d)) mrp = d; }
                            else if (k == "SCHEME") scheme = v;
                            else if (k == "PACK") pack = v;
                            else if (k == "MKT") mkt = v;
                            else if (k == "BATCH") batch = v;
                            else if (k == "NAME") { if (!string.IsNullOrWhiteSpace(v)) name = v; }
                        }
                    }
                }
                var row = new System.Text.StringBuilder();
                row.Append($"<tr><td class='text-center'>{idx++}</td>");
                if (showScheme) row.Append($"<td>{System.Net.WebUtility.HtmlEncode(scheme)}</td>");
                row.Append($"<td>{System.Net.WebUtility.HtmlEncode(name)}</td>");
                if (showPack) row.Append($"<td>{System.Net.WebUtility.HtmlEncode(pack)}</td>");
                row.Append($"<td>{System.Net.WebUtility.HtmlEncode(mkt)}</td>");
                if (showBatch) row.Append($"<td>{System.Net.WebUtility.HtmlEncode(batch)}</td>");
                if (showExpiry) row.Append($"<td>{exp}</td>");
                if (showMrp) row.Append($"<td class='text-right'>{(mrp.HasValue ? mrp.Value.ToString("0.00") : "")}</td>");
                row.Append($"<td class='text-right'>{it.Price:0.00}</td><td class='text-right'>{it.Quantity:0.##}</td><td class='text-right'>{(it.Price * it.Quantity):0.00}</td></tr>");
                html += row.ToString();
            }
            // Totals footer with extra/discount, back dues and round-off
            var extraAbs = inv.ExtraCostIsPercent ? Math.Round(inv.Subtotal * (inv.ExtraCostAmount / 100m), 2) : inv.ExtraCostAmount;
            var discountAbs = inv.DiscountIsPercent ? Math.Round(inv.Subtotal * (inv.DiscountAmount / 100m), 2) : inv.DiscountAmount;
            var extraLabel = string.IsNullOrWhiteSpace(inv.ExtraCostName) ? "Extra Cost" : inv.ExtraCostName;
            var taxableBase = Math.Max(0m, inv.Subtotal + extraAbs - discountAbs);
            var tax = inv.TaxAmount;
            if (tax == 0m) tax = Math.Round(taxableBase * 0m, 2);
            // Parse back dues from PrivateNotes (Adj:...BackAmt=...;BackInc=1;)
            decimal backAmt = 0m; bool backInc = false;
            try
            {
                var notes = inv.PrivateNotes ?? string.Empty;
                foreach (var seg in notes.Split(';'))
                {
                    var kv = seg.Split('='); if (kv.Length == 2)
                    {
                        var k = kv[0].Trim(); var v = kv[1].Trim();
                        if (k.EndsWith("BackAmt", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(v, out var bd)) backAmt = bd;
                        else if (k.EndsWith("BackInc", StringComparison.OrdinalIgnoreCase)) backInc = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
            var gross = taxableBase + tax + (backInc ? backAmt : 0m);
            var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);
            var roundOff = rounded - gross;
            // The footer spans every column except the final Amount cell, so the span must
            // track the toggles above (it was hardcoded to 10 and would misalign when hidden).
            int colCount = 4
                + (showScheme ? 1 : 0) + (showPack ? 1 : 0) + (showBatch ? 1 : 0)
                + (showExpiry ? 1 : 0) + (showMrp ? 1 : 0);
            int span = Math.Max(1, colCount - 1);
            html += $"</tbody><tfoot><tr class='total-row'><td colspan='{span}' class='text-right'>Subtotal:</td><td class='text-right'>{inv.Subtotal:0.00}</td></tr>";
            if (extraAbs > 0) html += $"<tr><td colspan='{span}' class='text-right'>{System.Net.WebUtility.HtmlEncode(extraLabel)}:</td><td class='text-right'>{extraAbs:0.00}</td></tr>";
            if (discountAbs > 0 && showDiscount) html += $"<tr><td colspan='{span}' class='text-right'>Discount:</td><td class='text-right'>-{discountAbs:0.00}</td></tr>";
            if (tax > 0 && showTaxAmount) html += $"<tr><td colspan='{span}' class='text-right'>Tax:</td><td class='text-right'>{tax:0.00}</td></tr>";
            if (backInc && backAmt > 0 && showBackDues) html += $"<tr><td colspan='{span}' class='text-right'>Back Dues:</td><td class='text-right'>{backAmt:0.00}</td></tr>";
            if (Math.Abs(roundOff) > 0.0001m && showRoundOff) html += $"<tr><td colspan='{span}' class='text-right'>Round Off:</td><td class='text-right'>{roundOff:0.00}</td></tr>";
            html += $"<tr class='total-row'><td colspan='{span}' class='text-right'>Total:</td><td class='text-right'>{rounded:0.00}</td></tr></tfoot></table></div>";

            // Payments summary section
            if (inv.Payments != null && inv.Payments.Count > 0)
            {
                html += "<div class='section-title'>Payments</div>";
                html += "<div class='table-wrap'><table><thead><tr><th style='width:120px;'>Date</th><th>Method</th><th>Notes</th><th style='width:140px;' class='text-right'>Amount</th></tr></thead><tbody>";
                foreach (var p in inv.Payments.OrderBy(p=>p.Date).ThenBy(p=>p.Id))
                {
                    var notes = System.Net.WebUtility.HtmlEncode(p.Notes ?? "");
                    html += $"<tr><td>{p.Date:dd/MM/yyyy}</td><td>{System.Net.WebUtility.HtmlEncode(p.Method ?? "")}</td><td>{notes}</td><td class='text-right'>{p.Amount:0.00}</td></tr>";
                }
                var totalPaid = inv.Payments.Sum(x => x.Amount);
                var balance = inv.Total - totalPaid;
                html += $"</tbody><tfoot><tr class='total-row'><td colspan='3' class='text-right'>Total Paid:</td><td class='text-right'>{totalPaid:0.00}</td></tr><tr class='total-row'><td colspan='3' class='text-right'>Balance:</td><td class='text-right'>{balance:0.00}</td></tr></tfoot></table></div>";
            }

            html += "</div>";
            if (Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowSignature))
                html += "<div style='margin-top:24px;'><div style='display:inline-block;border-top:1px solid #333;padding-top:4px;min-width:180px;'>Authorised Signature</div></div>";
            var footerText = Services.AppSettingsService.InvoiceFooterText;
            if (!string.IsNullOrWhiteSpace(footerText))
                html += "<div style='margin-top:12px;font-size:11px;color:#666;'>" + System.Net.WebUtility.HtmlEncode(footerText) + "</div>";
            html += "</body></html>";
            return html;
        }

        private static string GetCustomerAddressStatic(Customer customer)
        {
            if (customer == null) return string.Empty;
            var addressParts = new List<string>();
            if (!string.IsNullOrEmpty(customer.Address1)) addressParts.Add(customer.Address1);
            if (!string.IsNullOrEmpty(customer.Address2)) addressParts.Add(customer.Address2);
            if (!string.IsNullOrEmpty(customer.City)) addressParts.Add(customer.City);
            if (!string.IsNullOrEmpty(customer.State)) addressParts.Add(customer.State);
            if (!string.IsNullOrEmpty(customer.PostalCode)) addressParts.Add(customer.PostalCode);
            if (!string.IsNullOrEmpty(customer.Country)) addressParts.Add(customer.Country);
            return string.Join(", ", addressParts);
        }
        private static string ResolveProductNameStatic(InvoiceItem it)
        {
            try
            {
                using var db = new AppDbContext();
                if (it.ProductIdRef.HasValue)
                {
                    var p = db.Products.FirstOrDefault(x => x.Id == it.ProductIdRef.Value);
                    if (p != null && !string.IsNullOrWhiteSpace(p.Name)) return p.Name;
                }
                if (!string.IsNullOrWhiteSpace(it.ProductId))
                {
                    var p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                    if (p != null && !string.IsNullOrWhiteSpace(p.Name)) return p.Name;
                }
            }
            catch { }
            return it.ProductId ?? string.Empty;
        }

        private string ResolveProductName(InvoiceItem it)
        {
            try
            {
                using var db = new AppDbContext();
                if (it.ProductIdRef.HasValue)
                {
                    var p = db.Products.FirstOrDefault(x => x.Id == it.ProductIdRef.Value);
                    if (p != null && !string.IsNullOrWhiteSpace(p.Name)) return p.Name;
                }
                if (!string.IsNullOrWhiteSpace(it.ProductId))
                {
                    var p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                    if (p != null && !string.IsNullOrWhiteSpace(p.Name)) return p.Name;
                }
            }
            catch { }
            return it.ProductId ?? string.Empty;
        }

        private void BtnSavePdf_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "PDF Files (*.pdf)|*.pdf";
                sfd.FileName = $"Invoice_{invoice.InvoiceNumber}_{DateTime.Now:yyyyMMdd}.pdf";
                
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Generate PDF and save to selected location
                        var pdfBytes = GeneratePdfFor(invoice, htmlContent);
                        System.IO.File.WriteAllBytes(sfd.FileName, pdfBytes);
                        MessageBox.Show("Invoice saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving PDF: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private string GetCustomerAddress(Customer customer)
        {
            if (customer == null) return string.Empty;
            
            var addressParts = new List<string>();
            if (!string.IsNullOrEmpty(customer.Address1)) addressParts.Add(customer.Address1);
            if (!string.IsNullOrEmpty(customer.Address2)) addressParts.Add(customer.Address2);
            if (!string.IsNullOrEmpty(customer.City)) addressParts.Add(customer.City);
            if (!string.IsNullOrEmpty(customer.State)) addressParts.Add(customer.State);
            if (!string.IsNullOrEmpty(customer.PostalCode)) addressParts.Add(customer.PostalCode);
            if (!string.IsNullOrEmpty(customer.Country)) addressParts.Add(customer.Country);
            
            return string.Join(", ", addressParts);
        }
        
        private string GetCustomerShippingAddress(Customer customer)
        {
            // For now, just return the billing address since there's no separate shipping address
            // In a real app, you might have a separate shipping address in the Customer model
            return GetCustomerAddress(customer);
        }

        private decimal? TryParseMrp(string? desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            if (decimal.TryParse(desc, out var d)) return d;
            foreach (var part in (desc ?? string.Empty).Split('|'))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && kv[0].Trim().Equals("MRP", StringComparison.OrdinalIgnoreCase))
                {
                    if (decimal.TryParse(kv[1], out var val)) return val;
                }
            }
            return null;
        }

        private DateTime? TryParseExpiry(string? desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            foreach (var part in (desc ?? string.Empty).Split('|'))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && kv[0].Trim().Equals("EXP", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(kv[1], out var dt)) return dt;
                }
            }
            return null;
        }

        private static decimal? TryParseMrpStatic(string? desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            if (decimal.TryParse(desc, out var d)) return d;
            foreach (var part in (desc ?? string.Empty).Split('|'))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && kv[0].Trim().Equals("MRP", StringComparison.OrdinalIgnoreCase))
                {
                    if (decimal.TryParse(kv[1], out var val)) return val;
                }
            }
            return null;
        }

        private static DateTime? TryParseExpiryStatic(string? desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            foreach (var part in (desc ?? string.Empty).Split('|'))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && kv[0].Trim().Equals("EXP", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(kv[1], out var dt)) return dt;
                }
            }
            return null;
        }

        public static byte[] GeneratePdfFor(Invoice invoice, string html)
        {
            // Use QuestPDF to generate PDF from HTML
            using var stream = new System.IO.MemoryStream();
            
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    ApplyPageSetup(page);
                    page.Header().Column(header =>
                    {
                        // Phase 6: honour the same content toggles the HTML path uses, so the
                        // PDF and the on-screen/print preview cannot diverge.
                        var showAddr = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyAddress);
                        var showPhone = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyPhone);
                        var showEmail = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyEmail);
                        var showTaxNo = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCompanyTaxNumber);
                        var showBill = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowBillTo);
                        var showCustAddr = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCustomerAddress);
                        var showCustPhone = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCustomerPhone);
                        var showCustTax = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowCustomerTaxNumber);
                        var showDue = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowDueDate);
                        header.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text((Services.AppSettingsService.CompanyName)).FontSize(11).Bold();
                                if (showAddr)
                                    col.Item().Text(Services.AppSettingsService.CompanyAddressBlock().Replace("\r\n", "\n")).FontSize(8);
                                if (showPhone && !string.IsNullOrWhiteSpace(Services.AppSettingsService.CompanyPhone))
                                    col.Item().Text(Services.AppSettingsService.CompanyPhone).FontSize(8);
                                if (showEmail && !string.IsNullOrWhiteSpace(Services.AppSettingsService.CompanyEmail))
                                    col.Item().Text(Services.AppSettingsService.CompanyEmail).FontSize(8);
                                if (showTaxNo && !string.IsNullOrWhiteSpace(Services.AppSettingsService.CompanyTaxNumber))
                                    col.Item().Text($"{Services.AppSettingsService.CompanyTaxLabel} {Services.AppSettingsService.CompanyTaxNumber}").FontSize(8);
                            });
                            row.RelativeItem().AlignRight().Column(col =>
                            {
                                col.Item().Text($"{Services.AppSettingsService.InvoiceTitle} #: {invoice.InvoiceNumber}").FontSize(9);
                                col.Item().Text($"Date: {invoice.InvoiceDate:dd/MM/yyyy}").FontSize(9);
                                if (showDue && invoice.DueDate.HasValue)
                                    col.Item().Text($"Due: {invoice.DueDate:dd/MM/yyyy}").FontSize(9);
                            });
                        });
                        if (showBill)
                        {
                            header.Item().Text($"Bill To: {invoice.Customer?.Name ?? string.Empty}").FontSize(9);
                            if (showCustAddr && invoice.Customer != null)
                            {
                                var ca = GetCustomerAddressStatic(invoice.Customer);
                                if (!string.IsNullOrWhiteSpace(ca))
                                    header.Item().Text(ca).FontSize(8);
                            }
                            if (showCustPhone && !string.IsNullOrWhiteSpace(invoice.Customer?.Phone))
                                header.Item().Text(invoice.Customer!.Phone).FontSize(8);
                            if (showCustTax && !string.IsNullOrWhiteSpace(invoice.Customer?.GstVatNumber))
                                header.Item().Text(invoice.Customer!.GstVatNumber).FontSize(8);
                        }
                    });
                    
                    page.Content().Element(container =>
                    {
                        // Add invoice content
                        container.PaddingVertical(8).Column(column =>
                        {
                            // Compact layout: header already contains invoice/customer info
                            
                            // Add items table matching HTML preview (SL, Scheme, Name, Pack, Mkt, Batch, Expiry, MRP, Price, Qty, Amount)
                            column.Item().PaddingTop(6).Element(container =>
                            {
                                container.Table(table =>
                                {
                                    var tScheme = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowScheme);
                                    var tPack = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowPack);
                                    var tBatch = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowBatch);
                                    var tExpiry = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowExpiry);
                                    var tMrp = Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowMrp);
                                    table.ColumnsDefinition(columns =>
                                    {
                                        // Use weighted relative columns to prevent fixed-width overflows.
                                        // Optional columns are only declared when their toggle is on,
                                        // matching the HTML table exactly.
                                        columns.ConstantColumn(20);   // SL (small, fixed)
                                        if (tScheme) columns.RelativeColumn(2);    // Scheme
                                        columns.RelativeColumn(5);    // Name
                                        if (tPack) columns.RelativeColumn(2);    // Pack
                                        columns.RelativeColumn(3);    // Mkt
                                        if (tBatch) columns.RelativeColumn(3);    // Batch
                                        if (tExpiry) columns.RelativeColumn(2);    // Expiry
                                        if (tMrp) columns.RelativeColumn(2);    // MRP
                                        columns.RelativeColumn(2);    // Price
                                        columns.RelativeColumn(2);    // Qty
                                        columns.RelativeColumn(2);    // Amount
                                    });
                                    
                                    // Header
                                    table.Header(header =>
                                    {
                                        header.Cell().Background("#f0f0f0").Padding(2).Text("SL").SemiBold().FontSize(7.5f);
                                        if (tScheme) header.Cell().Background("#f0f0f0").Padding(2).Text("Scheme").SemiBold().FontSize(7.5f);
                                        header.Cell().Background("#f0f0f0").Padding(2).Text("Name").SemiBold().FontSize(7.5f);
                                        if (tPack) header.Cell().Background("#f0f0f0").Padding(2).Text("Pack").SemiBold().FontSize(7.5f);
                                        header.Cell().Background("#f0f0f0").Padding(2).Text("Mkt.").SemiBold().FontSize(7.5f);
                                        if (tBatch) header.Cell().Background("#f0f0f0").Padding(2).Text("Batch No.").SemiBold().FontSize(7.5f);
                                        if (tExpiry) header.Cell().Background("#f0f0f0").Padding(2).Text("Expiry").SemiBold().FontSize(7.5f);
                                        if (tMrp) header.Cell().Background("#f0f0f0").Padding(2).AlignRight().Text("MRP").SemiBold().FontSize(7.5f);
                                        header.Cell().Background("#f0f0f0").Padding(2).AlignRight().Text("Price").SemiBold().FontSize(7.5f);
                                        header.Cell().Background("#f0f0f0").Padding(2).AlignRight().Text("Qty").SemiBold().FontSize(7.5f);
                                        header.Cell().Background("#f0f0f0").Padding(2).AlignRight().Text("Amount").SemiBold().FontSize(7.5f);
                                    });
                                    
                                    // Items
                                    int index = 1;
                                    // local helper to truncate text safely
                                    string Trunc(string? s, int max)
                                    {
                                        if (string.IsNullOrEmpty(s)) return string.Empty;
                                        return s.Length <= max ? s : s.Substring(0, max);
                                    }
                                    foreach (var item in invoice.Items)
                                    {
                                        var mrp = TryParseMrpStatic(item.Description);
                                        var exp = TryParseExpiryStatic(item.Description);
                                        string scheme = string.Empty, pack = string.Empty, mkt = string.Empty, batch = string.Empty, name = ResolveProductNameStatic(item);
                                        try
                                        {
                                            foreach (var part in (item.Description ?? string.Empty).Split('|'))
                                            {
                                                var kv = part.Split(':'); if (kv.Length == 2)
                                                {
                                                    var k = kv[0].Trim().ToUpperInvariant(); var v = kv[1].Trim();
                                                    if (k == "SCHEME") scheme = v;
                                                    else if (k == "PACK") pack = v;
                                                    else if (k == "MKT") mkt = v;
                                                    else if (k == "BATCH") batch = v;
                                                    else if (k == "NAME" && !string.IsNullOrWhiteSpace(v)) name = v;
                                                }
                                            }
                                        }
                                        catch { }
                                        table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).Text(index++.ToString()).FontSize(7.5f);
                                        if (tScheme) table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).Text(Trunc(scheme, 40)).FontSize(7.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).Text(Trunc(name, 60)).FontSize(7.5f);
                                        if (tPack) table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).Text(Trunc(pack, 40)).FontSize(7.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).Text(Trunc(mkt, 40)).FontSize(7.5f);
                                        if (tBatch) table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).Text(Trunc(batch, 40)).FontSize(7.5f);
                                        if (tExpiry) table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).Text(exp.HasValue ? exp.Value.ToString("dd/MM/yyyy") : "").FontSize(7.5f);
                                        if (tMrp) table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).AlignRight().Text(mrp.HasValue ? mrp.Value.ToString("0.00") : "").FontSize(7.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).AlignRight().Text(item.Price.ToString("0.00")).FontSize(7.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).AlignRight().Text(item.Quantity.ToString("0.##")).FontSize(7.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(2).AlignRight().Text(item.LineTotal.ToString("0.00")).FontSize(7.5f);
                                    }
                                });
                            });
                            
                            // Add totals
                            column.Item().AlignRight().Width(300).PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(150);
                                    columns.ConstantColumn(150);
                                });
                                
                                table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).Text("Subtotal:");
                                table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).AlignRight().Text(invoice.Subtotal.ToString("0.00"));

                                // compute extra / discount same as editor
                                var extraAbs = invoice.ExtraCostIsPercent ? Math.Round(invoice.Subtotal * (invoice.ExtraCostAmount / 100m), 2) : invoice.ExtraCostAmount;
                                var discountAbs = invoice.DiscountIsPercent ? Math.Round(invoice.Subtotal * (invoice.DiscountAmount / 100m), 2) : invoice.DiscountAmount;
                                if (extraAbs > 0)
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).Text("Extra Cost:");
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).AlignRight().Text(extraAbs.ToString("0.00"));
                                }
                                if (discountAbs > 0 && Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowDiscount))
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).Text("Discount:");
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).AlignRight().Text($"-{discountAbs:0.00}");
                                }
                                
                                if (invoice.TaxAmount > 0 && Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowTaxAmount))
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).Text("Tax:");
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).AlignRight().Text(invoice.TaxAmount.ToString("0.00"));
                                }
                                // Round Off and Total
                                var gross = invoice.Subtotal + (invoice.ExtraCostIsPercent ? Math.Round(invoice.Subtotal * (invoice.ExtraCostAmount / 100m), 2) : invoice.ExtraCostAmount) - (invoice.DiscountIsPercent ? Math.Round(invoice.Subtotal * (invoice.DiscountAmount / 100m), 2) : invoice.DiscountAmount) + invoice.TaxAmount;
                                var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);
                                var roundOff = rounded - gross;
                                if (Math.Abs(roundOff) > 0.0001m && Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowRoundOff))
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).Text("Round Off:");
                                    table.Cell().BorderBottom(1).BorderColor("#e0e0e0").Padding(5).AlignRight().Text(roundOff.ToString("0.00"));
                                }
                                
                                table.Cell().Padding(5).Text("Total:").Bold();
                                table.Cell().Padding(5).AlignRight().Text(rounded.ToString("0.00")).Bold();
                            });
                            
                            // Footer text comes from Settings (was a hardcoded literal that
                            // duplicated the settings value and could not be changed by the user).
                            var footer = Services.AppSettingsService.InvoiceFooterText;
                            if (!string.IsNullOrWhiteSpace(footer))
                                column.Item().PaddingTop(40).Text(footer).Italic();
                            if (Services.AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowSignature))
                                column.Item().PaddingTop(24).Text("Authorised Signature").FontSize(9);
                        });
                    });
                });
            }).GeneratePdf(stream);
            
            return stream.ToArray();
        }
    }
}
