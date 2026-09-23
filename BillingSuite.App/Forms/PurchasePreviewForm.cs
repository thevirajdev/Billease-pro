using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Data;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BillingSuite.App.Forms
{
    public class PurchasePreviewForm : Form
    {
        private readonly WebBrowser webBrowser;
        private readonly Button btnPrint;
        private readonly Button btnSavePdf;
        private readonly Purchase purchase;
        private readonly string htmlContent;
        private readonly string template;

        public PurchasePreviewForm(Purchase purchase, string htmlContent, string template = "Classic")
        {
            this.purchase = purchase;
            this.htmlContent = htmlContent;
            this.template = template;

            Text = $"Purchase Preview - {purchase.InvoiceNumber}";
            this.Size = new System.Drawing.Size(900, 800);
            StartPosition = FormStartPosition.CenterParent;

            webBrowser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                WebBrowserShortcutsEnabled = false,
                AllowWebBrowserDrop = false,
                IsWebBrowserContextMenuEnabled = false,
                ScriptErrorsSuppressed = true
            };

            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            btnPrint = new Button
            {
                Text = "Print",
                Size = new System.Drawing.Size(100, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(680, 10)
            };
            btnPrint.Click += (s, e) => webBrowser.ShowPrintDialog();

            btnSavePdf = new Button
            {
                Text = "Save as PDF",
                Size = new System.Drawing.Size(100, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(790, 10)
            };
            btnSavePdf.Click += BtnSavePdf_Click;

            panel.Controls.Add(btnPrint);
            panel.Controls.Add(btnSavePdf);

            Controls.Add(webBrowser);
            Controls.Add(panel);

            webBrowser.DocumentText = htmlContent;
        }

        public static string BuildHtmlFor(Purchase p, string template = "Classic")
        {
            var baseCss = "body { font-family: Arial, sans-serif; margin: 0; padding: 12px; color: #333; } .viewport { min-width: 900px; } .header { margin-bottom: 8px; } .company-info h2 { margin: 0; font-size: 18px; } .invoice-info { text-align: right; } .invoice-info h2 { margin: 0; font-size: 16px; } .table-wrap { overflow-x: auto; } table { width: 100%; border-collapse: collapse; margin-bottom: 12px; table-layout: fixed; } th, td { border: 1px solid #ddd; padding: 6px; text-align: left; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; } th { background-color: #f2f2f2; } .text-right { text-align: right; } .text-center { text-align: center; } .total-row { font-weight: bold; } .section-title { font-weight:bold; margin: 10px 0 6px; }";
            var css = baseCss;
            if (string.Equals(template, "Compact", StringComparison.OrdinalIgnoreCase))
            {
                css += " table{font-size:12px;} th,td{padding:4px;} .viewport{min-width:760px;}";
            }
            else if (string.Equals(template, "Wide", StringComparison.OrdinalIgnoreCase))
            {
                css += " .viewport{min-width:1100px;}";
            }
            var supplierName = "";
            try { using var db = new AppDbContext(); supplierName = db.Suppliers.FirstOrDefault(s => s.Id == p.SupplierId)?.Name ?? ""; } catch { }
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8' /><title>Purchase {p.InvoiceNumber}</title>
<style>{css}</style></head><body><div class='viewport'>
<div class='header'><div style='display:flex; justify-content:space-between; align-items:flex-start;'><div class='company-info'><h2>{System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyName)}</h2><div>{System.Net.WebUtility.HtmlEncode(Services.AppSettingsService.CompanyAddressBlock())}</div><div style='margin-top:6px;'><strong>Supplier:</strong> {System.Net.WebUtility.HtmlEncode(supplierName)}</div></div><div class='invoice-info'><div><strong>Bill #:</strong> {p.InvoiceNumber}</div><div><strong>Date:</strong> {p.PurchaseDate:dd/MM/yyyy}</div></div></div></div>
<div class='table-wrap'><table><thead><tr>
<th style='width:40px;'>SL</th>
<th style='width:90px;'>Scheme</th>
<th>Name</th>
<th style='width:70px;'>Pack</th>
<th style='width:90px;'>Mkt.</th>
<th style='width:100px;'>Batch No.</th>
<th style='width:90px;'>Expiry</th>
<th style='width:80px;' class='text-right'>MRP</th>
<th style='width:90px;' class='text-right'>Cost</th>
<th style='width:80px;' class='text-right'>Qty</th>
<th style='width:110px;' class='text-right'>Amount</th>
</tr></thead><tbody>";
            int idx = 1;
            foreach (var it in p.Items)
            {
                var amount = (it.CostPrice ?? 0m) * (it.Quantity);
                var scheme = it.Bonus ?? string.Empty;
                var pack = it.Pack ?? string.Empty;
                var mkt = it.MarketedBy ?? string.Empty;
                html += $"<tr><td class='text-center'>{idx++}</td><td>{System.Net.WebUtility.HtmlEncode(scheme)}</td><td>{System.Net.WebUtility.HtmlEncode(it.ProductName ?? "")}</td><td>{System.Net.WebUtility.HtmlEncode(pack)}</td><td>{System.Net.WebUtility.HtmlEncode(mkt)}</td><td>{System.Net.WebUtility.HtmlEncode(it.BatchNumber ?? "")}</td><td>{(it.Expiry.HasValue ? it.Expiry.Value.ToString("dd/MM/yyyy") : "")}</td><td class='text-right'>{(it.Mrp.HasValue ? it.Mrp.Value.ToString("0.00") : "")}</td><td class='text-right'>{(it.CostPrice.HasValue ? it.CostPrice.Value.ToString("0.00") : "")}</td><td class='text-right'>{it.Quantity:0.##}</td><td class='text-right'>{amount:0.00}</td></tr>";
            }
            var subtotal = p.Items.Sum(x => (x.CostPrice ?? 0m) * x.Quantity);
            var discount = p.Discount ?? 0m;
            var tax = p.Tax ?? 0m;
            var shipping = p.Shipping ?? 0m;
            var gross = subtotal - discount + tax + shipping;
            var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);
            var roundOff = rounded - gross;
            html += $"</tbody><tfoot><tr class='total-row'><td colspan='10' class='text-right'>Subtotal:</td><td class='text-right'>{subtotal:0.00}</td></tr>";
            if (discount != 0m) html += $"<tr><td colspan='10' class='text-right'>Discount:</td><td class='text-right'>-{discount:0.00}</td></tr>";
            if (tax != 0m) html += $"<tr><td colspan='10' class='text-right'>Tax:</td><td class='text-right'>{tax:0.00}</td></tr>";
            if (shipping != 0m) html += $"<tr><td colspan='10' class='text-right'>Shipping:</td><td class='text-right'>{shipping:0.00}</td></tr>";
            if (Math.Abs(roundOff) > 0.0001m) html += $"<tr><td colspan='10' class='text-right'>Round Off:</td><td class='text-right'>{roundOff:0.00}</td></tr>";
            html += $"<tr class='total-row'><td colspan='10' class='text-right'>Total:</td><td class='text-right'>{rounded:0.00}</td></tr></tfoot></table></div>";
            // Payments summary section (mirrors Invoice preview)
            try
            {
                using var db2 = new AppDbContext();
                var pays = db2.PurchasePayments.Where(pp => pp.PurchaseId == p.Id).OrderBy(pp => pp.Date).ThenBy(pp => pp.Id).ToList();
                if (pays.Count > 0)
                {
                    html += "<div class='section-title'>Payments</div>";
                    html += "<div class='table-wrap'><table><thead><tr><th style='width:120px;'>Date</th><th>Method</th><th>Notes</th><th style='width:140px;' class='text-right'>Amount</th></tr></thead><tbody>";
                    foreach (var pay in pays)
                    {
                        var notes = System.Net.WebUtility.HtmlEncode(pay.Notes ?? "");
                        var method = System.Net.WebUtility.HtmlEncode(pay.Method ?? "");
                        html += $"<tr><td>{pay.Date:dd/MM/yyyy}</td><td>{method}</td><td>{notes}</td><td class='text-right'>{pay.Amount:0.00}</td></tr>";
                    }
                    var totalPaid = pays.Sum(x => x.Amount);
                    var balance = rounded - totalPaid;
                    html += $"</tbody><tfoot><tr class='total-row'><td colspan='3' class='text-right'>Total Paid:</td><td class='text-right'>{totalPaid:0.00}</td></tr><tr class='total-row'><td colspan='3' class='text-right'>Balance:</td><td class='text-right'>{balance:0.00}</td></tr></tfoot></table></div>";
                }
            }
            catch { }
            html += "</div></body></html>";
            return html;
        }

        private void BtnSavePdf_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", FileName = $"Purchase_{purchase.InvoiceNumber}_{DateTime.Now:yyyyMMdd}.pdf" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var pdf = GeneratePdfFor(purchase, htmlContent);
                System.IO.File.WriteAllBytes(sfd.FileName, pdf);
                MessageBox.Show("Purchase saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving PDF: {ex.Message}");
            }
        }

        public static byte[] GeneratePdfFor(Purchase p, string html)
        {
            using var stream = new System.IO.MemoryStream();
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(10);
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(Services.AppSettingsService.CompanyName).Bold().FontSize(11);
                            col.Item().Text(Services.AppSettingsService.CompanyAddressBlock()).FontSize(8);
                        });
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text($"Bill #: {p.InvoiceNumber}").FontSize(9);
                            col.Item().Text($"Date: {p.PurchaseDate:dd/MM/yyyy}").FontSize(9);
                        });
                    });
                    page.Content().Column(column =>
                    {
                        column.Item().Element(container =>
                        {
                            container.Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(28); // SL
                                    c.ConstantColumn(70); // Scheme
                                    c.RelativeColumn();    // Name
                                    c.ConstantColumn(60);  // Pack
                                    c.ConstantColumn(80);  // Mkt
                                    c.ConstantColumn(90);  // Batch
                                    c.ConstantColumn(70);  // Expiry
                                    c.ConstantColumn(60);  // MRP
                                    c.ConstantColumn(70);  // Cost
                                    c.ConstantColumn(50);  // Qty
                                    c.ConstantColumn(80);  // Amount
                                });
                                table.Header(h =>
                                {
                                    h.Cell().Text("SL").SemiBold().FontSize(9);
                                    h.Cell().Text("Scheme").SemiBold().FontSize(9);
                                    h.Cell().Text("Name").SemiBold().FontSize(9);
                                    h.Cell().Text("Pack").SemiBold().FontSize(9);
                                    h.Cell().Text("Mkt.").SemiBold().FontSize(9);
                                    h.Cell().Text("Batch No.").SemiBold().FontSize(9);
                                    h.Cell().Text("Expiry").SemiBold().FontSize(9);
                                    h.Cell().AlignRight().Text("MRP").SemiBold().FontSize(9);
                                    h.Cell().AlignRight().Text("Cost").SemiBold().FontSize(9);
                                    h.Cell().AlignRight().Text("Qty").SemiBold().FontSize(9);
                                    h.Cell().AlignRight().Text("Amount").SemiBold().FontSize(9);
                                });
                                int index = 1;
                                foreach (var it in p.Items)
                                {
                                    var amount = (it.CostPrice ?? 0m) * (it.Quantity);
                                    table.Cell().Padding(3).Text(index++.ToString()).FontSize(9);
                                    table.Cell().Padding(3).Text(it.Bonus ?? string.Empty).FontSize(9);
                                    table.Cell().Padding(3).Text(it.ProductName ?? string.Empty).FontSize(9);
                                    table.Cell().Padding(3).Text(it.Pack ?? string.Empty).FontSize(9);
                                    table.Cell().Padding(3).Text(it.MarketedBy ?? string.Empty).FontSize(9);
                                    table.Cell().Padding(3).Text(it.BatchNumber ?? string.Empty).FontSize(9);
                                    table.Cell().Padding(3).Text(it.Expiry.HasValue ? it.Expiry.Value.ToString("dd/MM/yyyy") : "").FontSize(9);
                                    table.Cell().Padding(3).AlignRight().Text(it.Mrp.HasValue ? it.Mrp.Value.ToString("0.00") : "").FontSize(9);
                                    table.Cell().Padding(3).AlignRight().Text(it.CostPrice.HasValue ? it.CostPrice.Value.ToString("0.00") : "").FontSize(9);
                                    table.Cell().Padding(3).AlignRight().Text(it.Quantity.ToString("0.##")).FontSize(9);
                                    table.Cell().Padding(3).AlignRight().Text(amount.ToString("0.00")).FontSize(9);
                                }
                            });
                        });
                        var subtotal = p.Items.Sum(x => (x.CostPrice ?? 0m) * x.Quantity);
                        column.Item().AlignRight().Width(300).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(150); c.ConstantColumn(150); });
                            t.Cell().Padding(5).Text("Subtotal:");
                            t.Cell().Padding(5).AlignRight().Text(subtotal.ToString("0.00"));
                            var discount = p.Discount ?? 0m; if (discount != 0m) { t.Cell().Padding(5).Text("Discount:"); t.Cell().Padding(5).AlignRight().Text($"-{discount:0.00}"); }
                            var tax = p.Tax ?? 0m; if (tax != 0m) { t.Cell().Padding(5).Text("Tax:"); t.Cell().Padding(5).AlignRight().Text(tax.ToString("0.00")); }
                            var shipping = p.Shipping ?? 0m; if (shipping != 0m) { t.Cell().Padding(5).Text("Shipping:"); t.Cell().Padding(5).AlignRight().Text(shipping.ToString("0.00")); }
                            var gross = subtotal - discount + tax + shipping;
                            var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);
                            var roundOff = rounded - gross;
                            if (Math.Abs(roundOff) > 0.0001m) { t.Cell().Padding(5).Text("Round Off:"); t.Cell().Padding(5).AlignRight().Text(roundOff.ToString("0.00")); }
                            t.Cell().Padding(5).Text("Total:").Bold();
                            t.Cell().Padding(5).AlignRight().Text(rounded.ToString("0.00")).Bold();
                        });
                    });
                });
            }).GeneratePdf(stream);
            return stream.ToArray();
        }
    }
}
