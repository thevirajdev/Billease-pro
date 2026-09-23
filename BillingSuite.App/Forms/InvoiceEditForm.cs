using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Diagnostics;
using System.IO;
using BillingSuite.App.Services;
using Microsoft.EntityFrameworkCore;

using BillingSuite.App.Interfaces;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BillingSuite.App.Forms
{
    public class InvoiceEditForm : Form, IAiControllable
    {
        private readonly ToolStrip tool = new ToolStrip();
        private readonly Panel pnlToolbar = new Panel();
        private readonly Panel contentPanel = new Panel();
        private readonly ToolStripButton tsSelectCustomer = new ToolStripButton();
        private readonly ToolStripButton tsAddLine = new ToolStripButton();
        private readonly ToolStripButton tsDeleteLine = new ToolStripButton();
        private readonly ToolStripButton tsPreview = new ToolStripButton();
        private readonly ToolStripButton tsPrint = new ToolStripButton();
        private readonly ToolStripDropDownButton tsTemplate = new ToolStripDropDownButton();
        private readonly ToolStripButton tsEmail = new ToolStripButton();
        private readonly ToolStripButton tsWhatsApp = new ToolStripButton();
        private readonly ToolStripButton tsMarkPaid = new ToolStripButton();
        private readonly ToolStripButton tsVoid = new ToolStripButton();

        private readonly GroupBox gbCustomer = new GroupBox();
        private readonly GroupBox gbInvoice = new GroupBox();
        private readonly TextBox txtOrderRef = new TextBox();
        private readonly ComboBox cmbCustomer = new ComboBox();
        public bool MarkPaidOnSave { get; set; }
        private Button? btnOk, btnCancel;
        private TextBox txtBillAddress = new TextBox();
        private TextBox txtShipAddress = new TextBox();
        private TextBox txtEmail = new TextBox();
        private TextBox txtSms = new TextBox();
        private NumericUpDown numTaxRate = new NumericUpDown();
        private Label lblSubtotal = new Label();
        private Label lblTax = new Label();
        private Label lblRoundOffBottom = new Label();
        private Label lblTotal = new Label();
        private Label lblExtraBottom = new Label();
        private Label lblDiscountBottom = new Label();
        private Label lblBackDuesBottom = new Label();
        private Label lblPaidBottom = new Label();
        private Label lblBalanceBottom = new Label();
        private Label lblTpBottom = new Label();
        private Label lblItemsCountBottom = new Label();
        private Label lblStatus = new Label();
        private DataGridView gridItems = new DataGridView();
        private TabControl tabControl = new TabControl();
        private Panel pnlSummary = new Panel();
        private ComboBox cmbTerms = new ComboBox();
        private DateTimePicker dtInvoice = new DateTimePicker();
        private DateTimePicker dtDue = new DateTimePicker();
        private TextBox txtInvoiceNo = new TextBox();
        private bool markPaidOnSave = false;
        private bool autoSaving = false;
        // Track back dues amount separately from items so it is not added as a product row
        private decimal backDuesAmount = 0m;
        private readonly bool isNewInvoice;
        private bool addingLoopActive = false;
        private bool initialShownHandled = false;
        private bool customerConfirmed = false;
        private bool initialFlowRunning = false;
        private System.Windows.Forms.Timer? initialKickTimer;
        
        // IAiControllable Implementation
        public object GetAiContext()
        {
            var items = new List<object>();
            if (gridItems.DataSource is BindingSource bs)
            {
                foreach (var item in bs.List.Cast<BillingSuite.App.Models.InvoiceItem>())
                {
                    items.Add(new { item.ProductName, item.Quantity, item.Price, item.BatchNumber });
                }
            }

            return new
            {
                Form = "InvoiceEditForm",
                InvoiceNumber = txtInvoiceNo.Text,
                Customer = cmbCustomer.Text,
                Items = items,
                Total = lblTotalVal.Text
            };
        }

        public async Task<bool> PerformAiAction(string action, object data)
        {
            try
            {
                var input = JObject.FromObject(data);
                if (action == "ui_update" || action == "add_product")
                {
                    // AI wants to add products
                    var products = input["products"] as JArray;
                    if (products == null && input["ProductName"] != null) products = new JArray { input };

                    if (products != null)
                    {
                        foreach (var p in products)
                        {
                            string name = p["ProductName"]?.ToString() ?? "";
                            decimal qty = p["Quantity"]?.Value<decimal>() ?? 1;
                            decimal? targetMrp = p["Mrp"]?.Value<decimal?>();

                            await AddProductByAiCriteria(name, qty, targetMrp);
                        }
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private async Task AddProductByAiCriteria(string name, decimal qty, decimal? targetMrp)
        {
            using var db = new AppDbContext();
            // Find product and best batch matching criteria
            var product = db.Products.FirstOrDefault(p => p.Name.ToLower().Contains(name.ToLower()));
            if (product == null) return;

            var batchQuery = db.ProductBatches.Where(b => b.ProductIdRef == product.Id && (b.Stock ?? 0) > 0);
            if (targetMrp.HasValue) batchQuery = batchQuery.Where(b => b.Mrp == targetMrp.Value);
            
            var batch = batchQuery.OrderByDescending(b => b.Expiry).FirstOrDefault();
            if (batch == null) batch = db.ProductBatches.Where(b => b.ProductIdRef == product.Id).OrderByDescending(b => b.Expiry).FirstOrDefault();

            if (batch != null)
            {
                this.Invoke((MethodInvoker)delegate {
                    if (gridItems.DataSource is BindingSource bs)
                    {
                        var it = new InvoiceItem
                        {
                            ProductId = product.Barcode,
                            ProductIdRef = product.Id,
                            ProductName = product.Name,
                            Quantity = qty,
                            Price = batch.SellingPrice ?? 0m,
                            CostPrice = batch.CostPrice ?? 0m,
                            BatchNumber = batch.BatchNumber,
                            Description = $"NAME:{product.Name}|MRP:{batch.Mrp}|BATCH:{batch.BatchNumber}|EXP:{batch.Expiry:dd/MM/yyyy}"
                        };
                        bs.Add(it);
                        Recalc();
                    }
                });
            }
        }

        /// <summary>
        /// Called by the AI to pre-fill this form with a customer and products without prompting any dialogs.
        /// </summary>
        public async Task PreFillForAi(string customerName, List<(string ProductName, decimal Qty, decimal? Mrp)> products)
        {
            try
            {
                // Step 1: Pre-select the customer silently
                using (var db = new AppDbContext())
                {
                    var customer = db.Customers.FirstOrDefault(c => c.Name.ToLower().Contains(customerName.ToLower()));
                    if (customer == null)
                    {
                        customer = new Customer { Name = customerName };
                        db.Customers.Add(customer);
                        db.SaveChanges();
                    }
                    var allCustomers = db.Customers.OrderBy(c => c.Name).ToList();
                    cmbCustomer.DataSource = allCustomers;
                    cmbCustomer.DisplayMember = "Name";
                    cmbCustomer.ValueMember = "Id";
                    cmbCustomer.SelectedValue = customer.Id;
                }
                // Mark as confirmed so no dialog fires on Shown
                customerConfirmed = true;
                initialShownHandled = true;

                // Step 2: Add products
                foreach (var (pName, qty, mrp) in products)
                    await AddProductByAiCriteria(pName, qty, mrp);
            }
            catch { }
        }


        // New UI Controls
        private TextBox txtExtraCostName = new TextBox();
        private NumericUpDown numExtraCost = new NumericUpDown();
        private CheckBox chkExtraPercent = new CheckBox();
        private NumericUpDown numDiscount = new NumericUpDown();
        private CheckBox chkDiscountPercent = new CheckBox();
        private CheckBox chkBackDuesAdj = new CheckBox();
        private bool suppressBackDuesEvent = false;
        private DataGridView paymentsGrid = new DataGridView();
        private ContextMenuStrip paymentsMenu = new ContextMenuStrip();
        private Panel paymentsPanel = new Panel();
        private Panel pnlBottomHost = new Panel();

        // Summary Labels
        private Label lblSubtotalVal = new Label();
        private Label lblExtraVal = new Label();
        private Label lblDiscountVal = new Label();
        private Label lblTaxVal = new Label();
        private Label lblRoundOffVal = new Label();
        private Label lblTotalVal = new Label();
        private Label lblPaidVal = new Label();
        private Label lblBalanceVal = new Label();

        // Event for Recalc
        private event EventHandler? RecalcRequested;

        private void Mrp_KeyPress(object? sender, KeyPressEventArgs e)
        {
            // Allow control characters, digits, and decimal point
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
            {
                e.Handled = true;
                return;
            };
            // Only allow one decimal point
            if (e.KeyChar == '.' && (sender as TextBox)?.Text.IndexOf('.') > -1)
            {
                e.Handled = true;
                return;
            };
        }

        private void TrySendWhatsApp()
        {
            try
            {
                // Build a lightweight invoice snapshot from current grid/model
                var inv = this.Model ?? new Invoice { Items = new System.Collections.Generic.List<InvoiceItem>() };
                if (gridItems.DataSource is BindingSource bs)
                {
                    inv.Items = bs.List.Cast<InvoiceItem>().Select(x => new InvoiceItem
                    {
                        ProductId = x.ProductId,
                        Description = x.Description,
                        Price = x.Price,
                        Quantity = x.Quantity,
                        LineTotal = x.Price * x.Quantity
                    }).ToList();
                }
                inv.Subtotal = inv.Items.Sum(i => i.LineTotal);
                inv.ExtraCostAmount = Model.ExtraCostAmount;
                inv.ExtraCostIsPercent = Model.ExtraCostIsPercent;
                inv.DiscountAmount = Model.DiscountAmount;
                inv.DiscountIsPercent = Model.DiscountIsPercent;
                var extraAbs = inv.ExtraCostIsPercent ? Math.Round(inv.Subtotal * (inv.ExtraCostAmount / 100m), 2) : inv.ExtraCostAmount;
                var discountAbs = inv.DiscountIsPercent ? Math.Round(inv.Subtotal * (inv.DiscountAmount / 100m), 2) : inv.DiscountAmount;
                var taxableBase = Math.Max(0m, inv.Subtotal + extraAbs - discountAbs);
                inv.TaxAmount = Math.Round(taxableBase * (numTaxRate.Value / 100m), 2);
                var gross = taxableBase + inv.TaxAmount + backDuesAmount;
                var totalRounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);

                // Customer and phone
                string phone = txtSms.Text?.Trim() ?? string.Empty;
                using (var db = new AppDbContext())
                {
                    if (cmbCustomer.SelectedValue is int cid)
                        inv.Customer = db.Customers.FirstOrDefault(c => c.Id == cid);
                }
                // If no phone, prompt for one; on OK, save into Customer and textbox
                if (string.IsNullOrWhiteSpace(phone))
                {
                    var prompt = new Form { Text = "Enter WhatsApp Number", StartPosition = FormStartPosition.CenterParent, Size = new Size(360, 140), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
                    var lbl = new Label { Text = "Phone (10 or country code):", AutoSize = true, Location = new Point(12, 18) };
                    var txt = new TextBox { Location = new Point(12, 40), Width = 320 };
                    var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(160, 70), Width = 70 };
                    var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(242, 70), Width = 90 };
                    prompt.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
                    prompt.AcceptButton = ok; prompt.CancelButton = cancel;
                    if (prompt.ShowDialog(this) != DialogResult.OK) return;
                    phone = txt.Text?.Trim() ?? string.Empty;
                    // Save into DB and UI if a customer is selected
                    if (!string.IsNullOrWhiteSpace(phone) && cmbCustomer.SelectedValue is int selCid)
                    {
                        try
                        {
                            using var dbp = new AppDbContext();
                            var cust = dbp.Customers.FirstOrDefault(c => c.Id == selCid);
                            if (cust != null)
                            {
                                cust.Phone = phone;
                                cust.UpdatedAt = DateTime.UtcNow;
                                dbp.SaveChanges();
                            }
                        }
                        catch { }
                        try { txtSms.Text = phone; } catch { }
                    }
                }
                inv.InvoiceNumber = string.IsNullOrWhiteSpace(Model.InvoiceNumber) ? txtInvoiceNo.Text : Model.InvoiceNumber;
                inv.InvoiceDate = dtInvoice.Value.Date;
                inv.DueDate = dtDue.Value.Date;

                // Generate PDF to temp (continue even if it fails)
                string? pdfPath = null;
                try
                {
                    var html = InvoicePreviewForm.BuildHtmlFor(inv);
                    var pdfBytes = InvoicePreviewForm.GeneratePdfFor(inv, html);
                    var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BillingSuite", "Invoices");
                    try { System.IO.Directory.CreateDirectory(tempDir); } catch { }
                    pdfPath = System.IO.Path.Combine(tempDir, $"Invoice_{(inv.InvoiceNumber ?? "").Replace(' ', '_')}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                    System.IO.File.WriteAllBytes(pdfPath, pdfBytes);
                }
                catch { pdfPath = null; }

                // Prepare WhatsApp message
                var name = inv.Customer?.Name ?? "Customer";
                var due = inv.DueDate.HasValue ? inv.DueDate.Value.ToString("dd/MM/yyyy") : "";
                var rawMsg = $"Hello {name},\nInvoice #: {inv.InvoiceNumber}\nDate: {inv.InvoiceDate:dd/MM/yyyy}\nDue: {due}\nTotal: {totalRounded:0.00}\nThank you.";
                var msg = Uri.EscapeDataString(rawMsg);
                string url;
                string? appUrl = null;
                if (string.IsNullOrWhiteSpace(phone))
                {
                    url = $"https://wa.me/?text={msg}";
                    appUrl = $"whatsapp://send?text={msg}";
                }
                else
                {
                    var raw = System.Text.RegularExpressions.Regex.Replace(phone, "[^0-9]", string.Empty);
                    // Default to India country code (+91) if not provided
                    if (raw.StartsWith("0")) raw = raw.TrimStart('0');
                    if (!raw.StartsWith("91") && raw.Length == 10) raw = "91" + raw;
                    url = $"https://wa.me/{raw}?text={msg}";
                    appUrl = $"whatsapp://send?phone={raw}&text={msg}";
                }

                // Put PDF on clipboard for quick paste in WhatsApp
                try
                {
                    if (!string.IsNullOrWhiteSpace(pdfPath) && System.IO.File.Exists(pdfPath))
                    {
                        var files = new System.Collections.Specialized.StringCollection();
                        files.Add(pdfPath);
                        Clipboard.SetFileDropList(files);
                    }
                }
                catch { }

                // Open WhatsApp Desktop (if installed) else Web
                var opened = false;
                if (!string.IsNullOrWhiteSpace(appUrl))
                {
                    try { Process.Start(new ProcessStartInfo(appUrl) { UseShellExecute = true }); opened = true; } catch { opened = false; }
                }
                if (!opened)
                {
                    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); opened = true; }
                    catch
                    {
                        try { Process.Start(new ProcessStartInfo("explorer.exe", url) { UseShellExecute = true }); opened = true; } catch { }
                    }
                }
                // Also open the PDF file to hint the user
                try
                {
                    if (!string.IsNullOrWhiteSpace(pdfPath) && System.IO.File.Exists(pdfPath))
                    {
                        // Open folder with file selected for easy drag-drop
                        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{pdfPath}\"") { UseShellExecute = true }); } catch { }
                        // Optional: show a small info to press Ctrl+V
                        try { MessageBox.Show("WhatsApp opened. PDF is copied to clipboard. In the chat, press Ctrl+V to attach the PDF, or drag it from the opened folder.", "WhatsApp Share", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WhatsApp share failed: {ex.Message}");
            }
        }

        private static string? ExtractToken(string? desc, string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(desc) || string.IsNullOrWhiteSpace(key)) return null;
                foreach (var part in desc.Split('|'))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        return kv[1].Trim();
                }
                return null;
            }
            catch { return null; }
        }

        private void EnsureCustomerSelectedWithQuickDialog(bool forcePrompt = false)
        {
            try
            {
                if (!forcePrompt && cmbCustomer.SelectedValue is int) return;
                using var dlg = new CustomerQuickDialog();
                var dr = dlg.ShowDialog(this);
                if (dr != DialogResult.OK)
                {
                    // Always close the invoice editor without saving when customer selection is cancelled
                    try
                    {
                        customerDialogCancelled = true;
                        preventSave = true;
                        try { initialKickTimer?.Stop(); initialKickTimer?.Dispose(); initialKickTimer = null; } catch { }
                        customerConfirmed = false;
                        BeginInvoke(new Action(() => { try { DialogResult = DialogResult.Cancel; } catch { } Close(); }));
                    }
                    catch { try { DialogResult = DialogResult.Cancel; } catch { } Close(); }
                    return;
                }
                var name = dlg.CustomerName;
                if (string.IsNullOrWhiteSpace(name)) return;
                using var db = new AppDbContext();
                int id;
                if (dlg.SelectedCustomerId.HasValue)
                {
                    id = dlg.SelectedCustomerId.Value;
                }
                else
                {
                    var existing = db.Customers.FirstOrDefault(c => c.Name.ToLower() == name.ToLower());
                    if (existing == null)
                    {
                        var c = new Customer { Name = name };
                        db.Customers.Add(c);
                        db.SaveChanges();
                        id = c.Id;
                    }
                    else id = existing.Id;
                }
                // Refresh combo
                var customers = db.Customers.OrderBy(c => c.Name).ToList();
                cmbCustomer.DataSource = customers;
                cmbCustomer.DisplayMember = "Name";
                cmbCustomer.ValueMember = "Id";
                cmbCustomer.SelectedValue = id;
                customerConfirmed = true;
            }
            catch { }
        }

        private void StartAddLoop()
        {
            try
            {
                if (addingLoopActive) return;
                if (customerDialogCancelled) return;
                addingLoopActive = true;
                // Ensure customer selection
                EnsureCustomerSelectedWithQuickDialog();
                if (customerDialogCancelled) { addingLoopActive = false; return; }
                if (cmbCustomer.SelectedValue is not int customerId) { addingLoopActive = false; return; }

                while (true)
                {
                    using var picker = new ProductPicker(customerId);
                    if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedProduct == null)
                        break; // user closed picker

                    var p = picker.SelectedProduct;
                    // Ensure new row
                    if (gridItems.DataSource is not BindingSource bs)
                        break;
                    var list = (System.Collections.Generic.List<InvoiceItem>)bs.List;
                    var rowIndex = AddEmptyRow();
                    var it = (InvoiceItem)bs[rowIndex];

                    using (var qtyDlg = new QuantityPrompt(1m))
                    {
                        if (qtyDlg.ShowDialog(this) != DialogResult.OK)
                        {
                            TryRemoveRowAt(rowIndex);
                            break;
                        }
                        // Assign selection directly from picker (per-batch)
                        it.ProductId = p.Barcode;
                        it.ProductIdRef = p.Id;
                        it.Quantity = qtyDlg.Quantity;
                        gridItems.Rows[rowIndex].Cells["colName"].Value = p.Name;
                        gridItems.Rows[rowIndex].Cells["colMrp"].Value = picker.SelectedMrp.HasValue ? picker.SelectedMrp.Value : null;
                        it.Price = picker.SelectedSellingPrice ?? 0m;
                        it.CostPrice = picker.SelectedCostPrice ?? 0m;
                        it.BatchNumber = picker.SelectedBatchNumber;
                        var mrpVal = picker.SelectedMrp.HasValue ? picker.SelectedMrp.Value.ToString("0.00") : "";
                        var batchCode = picker.SelectedBatchNumber ?? "";
                        var exp = picker.SelectedExpiry.HasValue ? picker.SelectedExpiry.Value.ToString("dd/MM/yyyy") : "";
                        // Pull CP, PACK, MKT, STOCK from ProductBatch when available
                        string? cpTxt = null, packTxt = null, mktTxt = null, stockTxt = null, schemeTxt = null;
                        try
                        {
                            using var dbb = new AppDbContext();
                            var pb = dbb.ProductBatches.FirstOrDefault(b => b.ProductIdRef == p.Id && b.BatchNumber == batchCode);
                            if (pb != null)
                            {
                                if (pb.CostPrice.HasValue) cpTxt = pb.CostPrice.Value.ToString("0.00");
                                packTxt = pb.Pack;
                                mktTxt = pb.MarketedBy;
                                if (pb.Stock.HasValue) stockTxt = pb.Stock.Value.ToString("0.##");
                                schemeTxt = pb.Bonus;
                            }
                        }
                        catch { }
                        // Persist batch info with tokens for downstream usage
                        it.Description = $"NAME:{p.Name}|MRP:{mrpVal}|BATCH:{batchCode}|EXP:{exp}"
                                           + (string.IsNullOrWhiteSpace(cpTxt) ? string.Empty : $"|CP:{cpTxt}")
                                           + (string.IsNullOrWhiteSpace(schemeTxt) ? string.Empty : $"|SCHEME:{schemeTxt}")
                                           + (string.IsNullOrWhiteSpace(packTxt) ? string.Empty : $"|PACK:{packTxt}")
                                           + (string.IsNullOrWhiteSpace(mktTxt) ? string.Empty : $"|MKT:{mktTxt}")
                                           + (string.IsNullOrWhiteSpace(stockTxt) ? string.Empty : $"|STOCK:{stockTxt}");
                    }
                    bs.ResetItem(rowIndex);
                    Recalc();
                    // Loop continues to reopen picker
                }
            }
            catch { }
            finally { addingLoopActive = false; }
        }

        private void EditCurrentProductAtRow(int rowIndex)
        {
            try
            {
                if (rowIndex < 0) return;
                if (gridItems.DataSource is not BindingSource bs) return;
                if (rowIndex >= bs.Count) return;
                var it = (InvoiceItem)bs[rowIndex];
                using var db = new AppDbContext();
                Product? p = null;
                if (it.ProductIdRef.HasValue)
                    p = db.Products.FirstOrDefault(x => x.Id == it.ProductIdRef.Value);
                if (p == null && !string.IsNullOrWhiteSpace(it.ProductId))
                    p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                if (p == null)
                {
                    // No product bound yet, open picker instead
                    OpenPickerForRow(rowIndex);
                    return;
                }
                using var dlg = new ProductEditForm(p);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Refresh from DB
                    var p2 = db.Products.FirstOrDefault(x => x.Id == p.Id);
                    if (p2 != null)
                    {
                        it.ProductIdRef = p2.Id;
                        it.ProductId = p2.Barcode;
                        // update display name
                        try { gridItems.Rows[rowIndex].Cells["colName"].Value = p2.Name; } catch { }
                        bs.ResetItem(rowIndex);
                        Recalc();
                    }
                }
            }
            catch { }
        }

        private Panel pnlCompactFooter = new Panel();
        private Label lblLeftCompact = new Label();
        private Label lblRightCompact = new Label();
        private bool customerDialogCancelled = false;
        private bool editingCell = false;
        private bool preventSave = false;

        public Invoice Model { get; private set; }
        private string currentTemplate = "Classic";

        public InvoiceEditForm(int? invoiceId = null)
        {
            Text = invoiceId == null ? "Create Invoice" : "Edit Invoice";
            isNewInvoice = invoiceId == null;
            StartPosition = FormStartPosition.CenterParent;
            // Fit to current screen working area to avoid header going off-screen
            var wa = Screen.FromHandle(this.Handle).WorkingArea;
            int targetW = Math.Min(1100, Math.Max(900, wa.Width - 40));
            int targetH = Math.Min(720, Math.Max(580, wa.Height - 40));
            Size = new System.Drawing.Size(targetW, targetH);
            MinimumSize = new Size(900, 580);

            using var db = new AppDbContext();
            var customers = db.Customers.OrderBy(c => c.Name).ToList();

            // Toolbar container fixed at top to avoid interfering with other controls
            pnlToolbar.Dock = DockStyle.Top;
            pnlToolbar.Height = 30;
            pnlToolbar.Padding = new Padding(0);
            Controls.Add(pnlToolbar);

            // Toolbar
            tool.GripStyle = ToolStripGripStyle.Hidden;
            tool.Dock = DockStyle.Fill;
            tool.AutoSize = false;
            tool.Height = 30;
            tool.Stretch = true;
            tool.Padding = new Padding(4, 2, 4, 2);
            tsSelectCustomer.Text = "Select Customer";
            tsAddLine.Text = "Add line item";
            tsDeleteLine.Text = "Delete line";
            tsPreview.Text = "Preview";
            tsPrint.Text = "Print";
            tsEmail.Text = "E-mail";
            tsWhatsApp.Text = "WhatsApp";
            tsMarkPaid.Text = "Mark Paid";
            tsVoid.Text = "Void";
            // Initialize template from Settings so the configured default is respected
            currentTemplate = AppSettingsService.InvoiceTemplate;
            tsTemplate.Text = $"Template: {currentTemplate}";
            tsTemplate.DropDownItems.Add("Classic", null, (s, e) => { currentTemplate = "Classic"; tsTemplate.Text = "Template: Classic"; });
            tsTemplate.DropDownItems.Add("Compact", null, (s, e) => { currentTemplate = "Compact"; tsTemplate.Text = "Template: Compact"; });
            tsTemplate.DropDownItems.Add("Wide", null, (s, e) => { currentTemplate = "Wide"; tsTemplate.Text = "Template: Wide"; });
            // Remove WhatsApp button from toolbar per request
            tsWhatsApp.Visible = false; tsWhatsApp.Enabled = false;
            tool.Items.AddRange(new ToolStripItem[] { tsSelectCustomer, new ToolStripSeparator(), tsAddLine, tsDeleteLine, new ToolStripSeparator(), tsPreview, tsPrint, tsTemplate, tsEmail, /* tsWhatsApp removed */ tsMarkPaid, tsVoid });
            pnlToolbar.Controls.Add(tool);
            // Ensure toolbar is always at the top-most z-order and index
            pnlToolbar.BringToFront();
            try { Controls.SetChildIndex(pnlToolbar, 0); } catch { }

            // Main content area occupies the rest of the form below the toolbar
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Padding = new Padding(0);
            contentPanel.AutoScroll = true;
            Controls.Add(contentPanel);
            try { Controls.SetChildIndex(contentPanel, 1); } catch { }

            // Compact bottom footer (left: item metrics, right: totals)
            pnlCompactFooter.Dock = DockStyle.Bottom;
            pnlCompactFooter.Height = 24;
            pnlCompactFooter.BackColor = SystemColors.ControlLightLight;
            pnlCompactFooter.Padding = new Padding(8, 2, 8, 2);
            lblLeftCompact.AutoSize = false; lblLeftCompact.TextAlign = ContentAlignment.MiddleLeft;
            lblLeftCompact.Dock = DockStyle.Left; lblLeftCompact.Width = 600; lblLeftCompact.Font = new Font(Font.FontFamily, 8.5f);
            lblRightCompact.AutoSize = false; lblRightCompact.TextAlign = ContentAlignment.MiddleRight;
            lblRightCompact.Dock = DockStyle.Fill; lblRightCompact.Font = new Font(Font.FontFamily, 8.5f);
            pnlCompactFooter.Controls.Add(lblRightCompact);
            pnlCompactFooter.Controls.Add(lblLeftCompact);
            Controls.Add(pnlCompactFooter);

            // Global keyboard shortcuts
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                // Add line: Ctrl+N or Insert
                if (e.Control && e.KeyCode == Keys.N || e.KeyCode == Keys.Insert)
                {
                    e.Handled = true;
                    if (tabControl != null && tabControl.SelectedIndex == 1) AddPaymentQuick();
                    else tsAddLine.PerformClick();
                    return;
                }

                // Delete line: Ctrl+D or Delete (when grid has focus)
                if ((e.Control && e.KeyCode == Keys.D) || e.KeyCode == Keys.Delete)
                {
                    e.Handled = true;
                    if (tabControl != null && tabControl.SelectedIndex == 1) DeleteSelectedPayment();
                    else tsDeleteLine.PerformClick();
                    return;
                }

                // Preview: Ctrl+R
                if (e.Control && e.KeyCode == Keys.R)
                { e.Handled = true; tsPreview.PerformClick(); return; }

                // Print: Ctrl+P
                if (e.Control && e.KeyCode == Keys.P)
                { e.Handled = true; tsPrint.PerformClick(); return; }

                // Add Payment: Ctrl+Y
                if (e.Control && e.KeyCode == Keys.Y)
                { e.Handled = true; AddPaymentQuick(); return; }

                // Edit current product: Ctrl+E
                if (e.Control && e.KeyCode == Keys.E)
                {
                    e.Handled = true;
                    if (gridItems.CurrentCell != null)
                        EditCurrentProductAtRow(gridItems.CurrentCell.RowIndex);
                    return;
                }

                // Open product picker for current row: Ctrl+Space
                if (e.Control && e.KeyCode == Keys.Space)
                {
                    e.Handled = true;
                    if (gridItems.Focused || gridItems.ContainsFocus)
                    {
                        if (gridItems.CurrentCell != null)
                            OpenPickerForRow(gridItems.CurrentCell.RowIndex);
                    }
                }

                // Switch tabs shortcuts
                if (e.Control && e.KeyCode == Keys.D1)
                { e.Handled = true; try { tabControl.SelectedIndex = 0; } catch { } return; }
                if (e.Control && e.KeyCode == Keys.D2)
                { e.Handled = true; try { tabControl.SelectedIndex = 1; } catch { } return; }
                // Ctrl+W close (triggers auto-save on FormClosing)
                if (e.Control && e.KeyCode == Keys.W)
                { e.Handled = true; this.Close(); return; }
            };

            // Hook selection change to update left compact info
            try { gridItems.SelectionChanged += (s, e) => UpdateLeftInfoForSelection(); } catch { }
            this.Shown += (s, e) => { try { UpdateLeftInfoForSelection(); UpdateCompactTotals(); } catch { } };

            // Customer box
            gbCustomer.Text = "Customer *";
            // Position inside content panel (relative coordinates)
            gbCustomer.SetBounds(10, 10, 700, 155);
            gbCustomer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            
            // Make combobox searchable
            cmbCustomer.DropDownStyle = ComboBoxStyle.DropDown;
            cmbCustomer.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbCustomer.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmbCustomer.DisplayMember = "Name";
            cmbCustomer.ValueMember = "Id";
            cmbCustomer.DataSource = new BindingSource(customers, null);
            cmbCustomer.DataSourceChanged += (s, e) => { try { if (Model?.CustomerId != null) SelectCustomerById(Model.CustomerId.Value); } catch { } };
            
            var lblInvTo = new Label { Text = "Invoice to *", Location = new Point(10, 25), AutoSize = true };
            cmbCustomer.SetBounds(80, 22, 200, 23);

            // Add "Add New Customer" button next to the combo
            var btnAddCustomer = new Button {
                Text = "+",
                Size = new System.Drawing.Size(25, 23),
                Location = new Point(80 + 200 + 5, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            void Mrp_KeyPress(object? sender, KeyPressEventArgs e)
            {
                // Allow control characters, digits, and decimal point
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
                {
                    e.Handled = true;
                    return;
                }

                // Only allow one decimal point
                if (e.KeyChar == '.' && (sender as TextBox)?.Text.IndexOf('.') > -1)
                {
                    e.Handled = true;
                    return;
                }
            }
            btnAddCustomer.Click += (s, e) => {
                // Open customer add form
                var addForm = new CustomerEditForm();
                if (addForm.ShowDialog(this) == DialogResult.OK) {
                    // Refresh customer list
                    using var db = new AppDbContext();
                    var updatedCustomers = db.Customers.OrderBy(c => c.Name).ToList();
                    cmbCustomer.DataSource = new BindingSource(updatedCustomers, null);
                    cmbCustomer.SelectedValue = addForm.Model.Id;
                }
            };
            var lblBillAddr = new Label { Text = "Address", Location = new Point(10, 55), AutoSize = true };
            txtBillAddress.SetBounds(80, 52, 260, 64); txtBillAddress.Multiline = true;
            var lblShipTo = new Label { Text = "Ship to", Location = new Point(360, 25), AutoSize = true };
            txtShipAddress.SetBounds(420, 22, 260, 94); txtShipAddress.Multiline = true;
            // Anchor right-side fields so they stay within gbCustomer on resize
            lblShipTo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtShipAddress.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var lblEmail = new Label { Text = "Email", Location = new Point(10, 120), AutoSize = true };
            txtEmail.SetBounds(80, 118, 260, 23);
            var lblSms = new Label { Text = "SMS Number", Location = new Point(360, 120), AutoSize = true };
            txtSms.SetBounds(450, 118, 230, 23);
            lblSms.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtSms.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            gbCustomer.Controls.AddRange(new Control[] { lblInvTo, cmbCustomer, btnAddCustomer, lblBillAddr, txtBillAddress, lblShipTo, txtShipAddress, lblEmail, txtEmail, lblSms, txtSms });
            contentPanel.Controls.Add(gbCustomer);

            // Register with AI Context Manager
            this.Load += (s, e) => AiContextManager.RegisterActiveForm(this);
            this.FormClosing += (s, e) => AiContextManager.UnregisterActiveForm(this);

            // Invoice box (right)
            gbInvoice.Text = "Invoice";
            gbInvoice.SetBounds(720, 10, 360, 160);
            gbInvoice.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var lblInvNo = new Label { Text = "Invoice #", Location = new Point(10, 25), AutoSize = true };
            txtInvoiceNo.SetBounds(90, 22, 250, 23);
            var lblInvDate = new Label { Text = "Invoice date", Location = new Point(10, 55), AutoSize = true };
            dtInvoice.SetBounds(90, 52, 250, 23);
            dtInvoice.Format = DateTimePickerFormat.Custom; dtInvoice.CustomFormat = "dd/MM/yyyy";
            var lblDue = new Label { Text = "Due date", Location = new Point(10, 85), AutoSize = true };
            dtDue.SetBounds(90, 82, 250, 23);
            dtDue.Format = DateTimePickerFormat.Custom; dtDue.CustomFormat = "dd/MM/yyyy";
            var lblTerms = new Label { Text = "Terms", Location = new Point(10, 115), AutoSize = true };
            cmbTerms.SetBounds(90, 112, 250, 23); cmbTerms.Items.AddRange(new object[] { "Due on receipt", "Net 7", "Net 15", "Net 30" });
            var lblOrderRef = new Label { Text = "Order Ref", Location = new Point(10, 145), AutoSize = true };
            txtOrderRef.SetBounds(90, 142, 250, 23);
            gbInvoice.Controls.AddRange(new Control[] { lblInvNo, txtInvoiceNo, lblInvDate, dtInvoice, lblDue, dtDue, lblTerms, cmbTerms, lblOrderRef, txtOrderRef });
            contentPanel.Controls.Add(gbInvoice);
            try { gbInvoice.BringToFront(); } catch { }

            // Finally, re-parent any stray controls added to the form (except toolbar and contentPanel)
            try
            {
                var stray = Controls.Cast<Control>().Where(c => c != pnlToolbar && c != contentPanel).ToList();
                foreach (var c in stray)
                {
                    Controls.Remove(c);
                    contentPanel.Controls.Add(c);
                }
            }
            catch { }

            // Top host (Dock=Top) will contain the two group boxes; Center fills remaining; Bottom hosts summary
            var pnlTopHost = new Panel { Dock = DockStyle.Top, Height = 180, Padding = new Padding(0) };
            // Move group boxes into top host to ensure clean docking
            try { contentPanel.Controls.Remove(gbCustomer); pnlTopHost.Controls.Add(gbCustomer); } catch { }
            try { contentPanel.Controls.Remove(gbInvoice); pnlTopHost.Controls.Add(gbInvoice); } catch { }
            // Center area hosts the TabControl and grid
            var pnlCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BorderStyle = BorderStyle.FixedSingle };
            var pnlItemsFooter = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = SystemColors.Control };
            // thin black line at the very top of the footer so the grid looks boxed on all 4 sides
            var pnlBottomLine = new Panel { Height = 1, BackColor = Color.Black, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            pnlItemsFooter.Controls.Add(pnlBottomLine);
            pnlItemsFooter.Resize += (s, e) => { try { pnlBottomLine.SetBounds(1, 0, Math.Max(0, pnlItemsFooter.ClientSize.Width - 2), 1); } catch { } };
            lblItemsCountBottom.AutoSize = true; lblItemsCountBottom.Text = "Items: 0"; lblItemsCountBottom.Location = new Point(6, 5);
            pnlItemsFooter.Controls.Add(lblItemsCountBottom);
            gridItems.Dock = DockStyle.Fill;
            gridItems.AllowUserToAddRows = false; gridItems.RowHeadersVisible = false; gridItems.AutoGenerateColumns = false;
            gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            // Recompute totals and compact info when user edits cells (e.g., Price)
            try
            {
                gridItems.CellBeginEdit += (s, e) => { try { editingCell = true; } catch { } };
                gridItems.CellEndEdit += (s, e) => { try { editingCell = false; } catch { } try { UpdateLeftInfoForSelection(); } catch { } try { Recalc(); } catch { } };
                gridItems.CurrentCellDirtyStateChanged += (s, e) =>
                {
                    try
                    {
                        if (!gridItems.IsCurrentCellDirty) return;
                        if (gridItems.CurrentCell is DataGridViewCheckBoxCell)
                            gridItems.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    }
                    catch { }
                };
            }
            catch { }
            gridItems.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            gridItems.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            gridItems.ScrollBars = ScrollBars.Both;
            // New columns per spec
            var colSerial = new DataGridViewTextBoxColumn { HeaderText = "Sl", Name = "colSerial", ReadOnly = true, FillWeight = 6, MinimumWidth = 40 };
            var colScheme = new DataGridViewTextBoxColumn { HeaderText = "Scheme", Name = "colScheme", ReadOnly = false, FillWeight = 10, MinimumWidth = 80 };
            var colName = new DataGridViewTextBoxColumn { HeaderText = "Name", Name = "colName", ReadOnly = false, FillWeight = 24, MinimumWidth = 200 };
            var colPack = new DataGridViewTextBoxColumn { HeaderText = "Pack", Name = "colPack", ReadOnly = false, FillWeight = 8, MinimumWidth = 70 };
            var colMkt = new DataGridViewTextBoxColumn { HeaderText = "Mkt.", Name = "colMkt", ReadOnly = false, FillWeight = 10, MinimumWidth = 90 };
            var colBatch = new DataGridViewTextBoxColumn { HeaderText = "Batch No.", Name = "colBatch", ReadOnly = false, FillWeight = 12, MinimumWidth = 100 };
            var colExpiry = new DataGridViewTextBoxColumn { HeaderText = "Expiry", Name = "colExpiry", ReadOnly = false, FillWeight = 10, MinimumWidth = 90 };
            var colMrp = new DataGridViewTextBoxColumn { HeaderText = "MRP", Name = "colMrp", ReadOnly = false, FillWeight = 10, MinimumWidth = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, NullValue = "" } };
            var colPrice = new DataGridViewTextBoxColumn { HeaderText = "Price", DataPropertyName = "Price", Name = "colPrice", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
            var colQty = new DataGridViewTextBoxColumn { HeaderText = "Quantity", DataPropertyName = "Quantity", Name = "colQty", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } };
            var colAmount = new DataGridViewTextBoxColumn { HeaderText = "Amount", DataPropertyName = "LineTotal", ReadOnly = true, FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight } };
            gridItems.Columns.Add(colSerial);
            gridItems.Columns.Add(colScheme);
            gridItems.Columns.Add(colName);
            gridItems.Columns.Add(colPack);
            gridItems.Columns.Add(colMkt);
            gridItems.Columns.Add(colBatch);
            gridItems.Columns.Add(colExpiry);
            gridItems.Columns.Add(colMrp);
            gridItems.Columns.Add(colPrice);
            gridItems.Columns.Add(colQty);
            gridItems.Columns.Add(colAmount);
            // do not add grid/footer to center panel here; they will be hosted inside the Items tab
            gridItems.CellEndEdit += GridItems_CellEndEdit;
            gridItems.EditingControlShowing += GridItems_EditingControlShowing;
            gridItems.CellFormatting += (s, e) =>
            {
                try
                {
                    if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                    var row = gridItems.Rows[e.RowIndex]; if (row == null) return;
                    var it = row.DataBoundItem as InvoiceItem; if (it == null) return;
                    var colNameSafe = gridItems.Columns[e.ColumnIndex].Name;
                    string? Read(string key)
                    {
                        try
                        {
                            foreach (var part in (it.Description ?? string.Empty).Split('|'))
                            {
                                var kv = part.Split(':'); if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return kv[1].Trim();
                            }
                        }
                        catch { }
                        return null;
                    }
                    if (colNameSafe == "colSerial") { e.Value = (e.RowIndex + 1).ToString(); e.FormattingApplied = true; return; }
                    if (colNameSafe == "colScheme") { e.Value = Read("SCHEME") ?? ""; e.FormattingApplied = true; return; }
                    if (colNameSafe == "colPack") { e.Value = Read("PACK") ?? ""; e.FormattingApplied = true; return; }
                    if (colNameSafe == "colMkt") { e.Value = Read("MKT") ?? ""; e.FormattingApplied = true; return; }
                    if (colNameSafe == "colBatch") { e.Value = Read("BATCH") ?? ""; e.FormattingApplied = true; return; }
                    if (colNameSafe == "colExpiry") { e.Value = Read("EXP") ?? ""; e.FormattingApplied = true; return; }
                    if (colNameSafe == "colMrp") { e.Value = Read("MRP") ?? ""; e.FormattingApplied = true; return; }
                }
                catch { }
            };
            gridItems.RowsAdded += (s, e) => { try { lblItemsCountBottom.Text = $"Items: {gridItems.Rows.Count}"; } catch { } };
            gridItems.RowsRemoved += (s, e) => { try { lblItemsCountBottom.Text = $"Items: {Math.Max(0, gridItems.Rows.Count)}"; } catch { } };
            // Keyboard shortcuts (F2 to pick, Enter behavior handled in handlers)
            gridItems.KeyDown += GridItems_KeyDown;
            gridItems.CellDoubleClick += (s, e) => { try { if (e.RowIndex >= 0) EditCurrentProductAtRow(e.RowIndex); } catch { } };
            gridItems.UserDeletingRow += (s, e) => 
            {
                try 
                {
                    if (e.Row?.DataBoundItem is InvoiceItem item && Model?.Items != null)
                    {
                        Model.Items.Remove(item);
                        RecalcRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting row: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                }
            };

            gridItems.CellFormatting += (s, e) =>
            {
                try
                {
                    if (e.RowIndex < 0) return;
                    if (e.ColumnIndex < 0) return;
                    if (e.ColumnIndex >= gridItems.Columns.Count) return;
                    if (e.RowIndex >= gridItems.Rows.Count) return;
                    var row = gridItems.Rows[e.RowIndex];
                    if (row == null) return;
                    var it = row.DataBoundItem as InvoiceItem;
                    if (it == null) return;
                    string colNameSafe = string.Empty;
                    try { colNameSafe = gridItems.Columns[e.ColumnIndex].Name; } catch { return; }
                    if (colNameSafe == "colSerial")
                    {
                        e.Value = (e.RowIndex + 1).ToString(); e.FormattingApplied = true;
                    }
                    else if (colNameSafe == "colName")
                    {
                        try
                        {
                            // Prefer custom NAME: from Description if present
                            string? name = null;
                            var desc = it.Description ?? string.Empty;
                            foreach (var part in desc.Split('|'))
                            {
                                var kv = part.Split(':');
                                if (kv.Length == 2 && kv[0].Trim().Equals("NAME", StringComparison.OrdinalIgnoreCase))
                                { name = kv[1].Trim(); break; }
                            }
                            using (var db2 = new AppDbContext())
                            {
                                if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(it.ProductId))
                                {
                                    name = db2.Products.FirstOrDefault(p => p.Barcode == it.ProductId)?.Name;
                                }
                                if (string.IsNullOrWhiteSpace(name) && it.ProductIdRef.HasValue)
                                {
                                    name = db2.Products.FirstOrDefault(p => p.Id == it.ProductIdRef.Value)?.Name;
                                }
                                if (string.IsNullOrWhiteSpace(name) && int.TryParse(it.ProductId ?? string.Empty, out var pid))
                                {
                                    name = db2.Products.FirstOrDefault(p => p.Id == pid)?.Name;
                                }
                            }
                            e.Value = string.IsNullOrWhiteSpace(name) ? (it.ProductId ?? string.Empty) : name;
                            e.FormattingApplied = true;
                        }
                        catch { }
                    }
                    else if (colNameSafe == "colBatch")
                    {
                        e.Value = ExtractToken(it.Description, "BATCH"); e.FormattingApplied = true;
                    }
                    else if (colNameSafe == "colExpiry")
                    {
                        e.Value = ExtractToken(it.Description, "EXP"); e.FormattingApplied = true;
                    }
                    else if (colNameSafe == "colMrp")
                    {
                        var mrp = ExtractToken(it.Description, "MRP");
                        e.Value = decimal.TryParse(mrp, out var v) ? v.ToString("0.00") : string.Empty;
                        e.FormattingApplied = true;
                    }
                    else if (colNameSafe == "colPack")
                    {
                        e.Value = ExtractToken(it.Description, "PACK"); e.FormattingApplied = true;
                    }
                    else if (colNameSafe == "colMkt")
                    {
                        e.Value = ExtractToken(it.Description, "MKT"); e.FormattingApplied = true;
                    }
                }
                catch { /* Suppress formatting errors during row deletion */ }
            };

            gridItems.DataError += (s, e) =>
            {
                // Suppress default error dialogs caused by transient formatting/index issues
                e.ThrowException = false;
            };

            // Toolbar handlers
            tsAddLine.Click += (s, e) => {
                if (tabControl != null && tabControl.SelectedIndex == 1) AddPaymentQuick();
                else StartAddLoop();
            };
            tsDeleteLine.Click += (s, e) => {
                if (tabControl != null && tabControl.SelectedIndex == 1) DeleteSelectedPayment();
                else RemoveSelectedRow();
            };
            tsSelectCustomer.Click += (s, e) => { cmbCustomer.DroppedDown = true; };
            tsPreview.Click += (s, e) => { PreviewPdf(); };
            tsPrint.Click += (s, e) => { PreviewPdf(true); };
            tsEmail.Click += (s, e) => { ComposeEmail(); };
            tsWhatsApp.Click += (s, e) => { TrySendWhatsApp(); };
            tsMarkPaid.Click += (s, e) => { markPaidOnSave = true; MarkPaidOnSave = true; Recalc(); };
            tsVoid.Click += (s, e) => { markPaidOnSave = false; Model.Status = InvoiceStatus.Void; lblStatus.Text = "Status: Void"; };

            var lblTaxRate = new Label { Text = "Tax %", AutoSize = true, Location = new Point(10, 580) };
            lblTaxRate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            numTaxRate.Location = new Point(60, 576); numTaxRate.DecimalPlaces = 2; numTaxRate.Maximum = 100;
            numTaxRate.Value = Math.Min(100, (decimal)Services.AppSettingsService.DefaultTaxRate);
            numTaxRate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            numTaxRate.ValueChanged += (s, e) => Recalc();

            lblSubtotal.Location = new Point(760, 576); lblSubtotal.AutoSize = true; lblSubtotal.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblTax.Location = new Point(760, 596); lblTax.AutoSize = true; lblTax.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblTotal.Location = new Point(760, 616); lblTotal.Font = new Font(lblTotal.Font, FontStyle.Bold); lblTotal.AutoSize = true; lblTotal.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            lblExtraBottom.Location = new Point(10, 576); lblExtraBottom.AutoSize = true; lblExtraBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblDiscountBottom.Location = new Point(10, 596); lblDiscountBottom.AutoSize = true; lblDiscountBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblBackDuesBottom.Location = new Point(10, 616); lblBackDuesBottom.AutoSize = true; lblBackDuesBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblPaidBottom.Location = new Point(320, 596); lblPaidBottom.AutoSize = true; lblPaidBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblBalanceBottom.Location = new Point(320, 616); lblBalanceBottom.AutoSize = true; lblBalanceBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            // Place TP at bottom-right so it remains visible even when window is smaller
            lblTpBottom.Location = new Point(760, 556); lblTpBottom.AutoSize = true; lblTpBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; lblTpBottom.Text = "TP: 0.00";

            // Add bottom summary labels to the content panel so they render
            try
            {
                contentPanel.Controls.Add(lblSubtotal);
                contentPanel.Controls.Add(lblTax);
                contentPanel.Controls.Add(lblTotal);
                contentPanel.Controls.Add(lblExtraBottom);
                contentPanel.Controls.Add(lblDiscountBottom);
                contentPanel.Controls.Add(lblBackDuesBottom);
                contentPanel.Controls.Add(lblPaidBottom);
                contentPanel.Controls.Add(lblBalanceBottom);
                contentPanel.Controls.Add(lblTpBottom);
            }
            catch { }

            // (Removed bottom buttons; these live in Adjustments section)
            // Back Dues checkbox handler (will be wired to adjustments checkbox)
            EventHandler backDuesChanged = (s, e) =>
            {
                try
                {
                    // Toggle back dues computation without adding a product row
                    if (cmbCustomer.SelectedValue is not int cid)
                    {
                        MessageBox.Show("Select customer first.");
                        if (s is CheckBox cb) cb.Checked = false; return;
                    }
                    using var db = new AppDbContext();
                    var currentId = Model != null ? Model.Id : 0;
                    var dues = db.Invoices
                        .Where(i => i.CustomerId == cid && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Void && i.Id != currentId)
                        .Select(i => (decimal?)(i.Total - i.TotalPaid)).Sum() ?? 0m;
                    var isChecked = (s as CheckBox)?.Checked == true;
                    if (!isChecked)
                    {
                        // Turn off
                        backDuesAmount = 0m; Model.IncludedBackDues = false; Recalc(); return;
                    }
                    if (dues <= 0m)
                    {
                        MessageBox.Show("No back dues."); if (s is CheckBox cb2) cb2.Checked = false; backDuesAmount = 0m; Model.IncludedBackDues = false; Recalc(); return;
                    }
                    backDuesAmount = dues;
                    Model.IncludedBackDues = true;
                    Recalc();
                    RecalcRequested?.Invoke(this, EventArgs.Empty);
                }
                catch { }
            };

            // Second-stage selection after the form is displayed, to guard against any late DataSource resets
            this.Resize += (s, e) =>
            {
                try
                {
                    ApplyTopLayout();
                }
                catch { }
            };
            // Ensure initial layout fits the screen working area
            this.Shown += (s, e) => { try { var wa2 = Screen.FromHandle(this.Handle).WorkingArea; if (Width > wa2.Width) Width = wa2.Width - 20; if (Height > wa2.Height) Height = wa2.Height - 20; ApplyTopLayout(); } catch { } };
            // On first show for a new invoice, force customer prompt then open product picker loop
            this.Shown += (s, e) =>
            {
                if (initialShownHandled) return; initialShownHandled = true;
                BeginInvoke(new Action(TriggerInitialFlow));
                // Also schedule a backup trigger shortly after to avoid edge cases
                initialKickTimer = new System.Windows.Forms.Timer { Interval = 300 };
                initialKickTimer.Tick += (s2, e2) =>
                {
                    try
                    {
                        initialKickTimer!.Stop();
                        initialKickTimer.Dispose();
                        initialKickTimer = null;
                        if (!customerDialogCancelled && !customerConfirmed && isNewInvoice)
                        {
                            TriggerInitialFlow();
                        }
                    }
                    catch { }
                };
                initialKickTimer.Start();
            };

            void TriggerInitialFlow()
            {
                if (initialFlowRunning) return; if (customerDialogCancelled) return; initialFlowRunning = true;
                try
                {
                    if (isNewInvoice)
                    {
                        EnsureCustomerSelectedWithQuickDialog(true);
                        if (customerDialogCancelled) return;
                        if (cmbCustomer.SelectedValue is int)
                        {
                            BeginInvoke(new Action(StartAddLoop));
                        }
                    }
                }
                catch { }
                finally { initialFlowRunning = false; }
            }

            // Create TabControl (class field)
            tabControl = new TabControl { Dock = DockStyle.Fill, Location = new Point(0, 0), Size = new System.Drawing.Size(1070, 380) };
            
            // Tab 1: Items (grid + footer inside the tab)
            var tabItems = new TabPage("Items");
            // Add grid first, then footer so Dock order shows footer at the bottom and grid fills above
            tabItems.Controls.Add(gridItems);
            tabItems.Controls.Add(pnlItemsFooter);
            
            // Tab 2: Payments & Summary
            var tabPayments = new TabPage("Payments & Summary");
            tabPayments.Padding = new Padding(5);
            
            // Payments Grid
            paymentsGrid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 150,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            paymentsGrid.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Date", Name = "colDate", DataPropertyName = "Date", FillWeight = 20 },
                new DataGridViewTextBoxColumn { HeaderText = "Method", Name = "colMethod", DataPropertyName = "Method", FillWeight = 20 },
                new DataGridViewTextBoxColumn { HeaderText = "Notes", Name = "colNotes", DataPropertyName = "Notes", FillWeight = 40 },
                new DataGridViewTextBoxColumn { 
                    HeaderText = "Amount", 
                    Name = "colAmount", 
                    DataPropertyName = "Amount", 
                    FillWeight = 20,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }
                }
            );

            // Payments context menu (Edit/Delete)
            var miEditPay = new ToolStripMenuItem("Edit", null, (s, e) => EditSelectedPayment());
            var miDeletePay = new ToolStripMenuItem("Delete", null, (s, e) => DeleteSelectedPayment());
            paymentsMenu.Items.AddRange(new ToolStripItem[] { miEditPay, miDeletePay });
            paymentsGrid.ContextMenuStrip = paymentsMenu;
            paymentsGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelectedPayment(); };
            paymentsGrid.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var hit = paymentsGrid.HitTest(e.X, e.Y);
                    if (hit.RowIndex >= 0)
                        paymentsGrid.ClearSelection();
                        try { paymentsGrid.Rows[hit.RowIndex].Selected = true; } catch { }
                }
            };
            
            // Adjustments GroupBox
            var gbAdjustments = new GroupBox { Text = "Adjustments", Dock = DockStyle.Top, Height = 100, Margin = new Padding(0, 10, 0, 0) };
            
            // Extra Cost controls (use class-level fields)
            var lblExtraCost = new Label { Text = "Extra Cost:", Location = new Point(10, 25), AutoSize = true };
            txtExtraCostName = new TextBox { Location = new Point(100, 22), Width = 200, Tag = "extra" };
            numExtraCost = new NumericUpDown { Location = new Point(310, 22), Width = 100, DecimalPlaces = 2, Maximum = 9999999, Minimum = 0 };
            chkExtraPercent = new CheckBox { Text = "%", Location = new Point(420, 24), AutoSize = true, Tag = "extra" };
            
            // Discount controls (use class-level fields)
            var lblDiscount = new Label { Text = "Discount:", Location = new Point(10, 55), AutoSize = true };
            numDiscount = new NumericUpDown { Location = new Point(100, 52), Width = 100, DecimalPlaces = 2, Maximum = 9999999, Minimum = 0 };
            chkDiscountPercent = new CheckBox { Text = "%", Location = new Point(210, 54), AutoSize = true };
            
            // Tax %, Add Payment, and Back Dues controls inside adjustments
            var lblTaxPctAdj = new Label { Text = "Tax %:", Location = new Point(500, 25), AutoSize = true };
            numTaxRate.Parent = gbAdjustments; numTaxRate.Location = new Point(550, 22); numTaxRate.Width = 80;

            var btnAddPaymentAdj = new Button { Text = "Add Payment", Location = new Point(500, 52), Size = new Size(100, 26) };
            btnAddPaymentAdj.Click += (s, e) => { AddPaymentQuick(); };

            chkBackDuesAdj = new CheckBox { Text = "Add Back Dues", Location = new Point(620, 56), AutoSize = true };
            chkBackDuesAdj.CheckedChanged += (s, e) => { if (suppressBackDuesEvent) return; backDuesChanged(s, e); };

            // Add controls to Adjustments GroupBox
            gbAdjustments.Controls.AddRange(new Control[] {
                lblExtraCost, txtExtraCostName, numExtraCost, chkExtraPercent,
                lblDiscount, numDiscount, chkDiscountPercent,
                lblTaxPctAdj, numTaxRate, btnAddPaymentAdj, chkBackDuesAdj
            });
            
            // Summary GroupBox
            var gbSummary = new GroupBox { Text = "Summary", Dock = DockStyle.Top, Height = 220, Margin = new Padding(0, 10, 0, 0) };
            
            // Summary labels and values
            var lblSubtotalSum = new Label { Text = "Subtotal:", Location = new Point(10, 25), AutoSize = true, Font = new Font(DefaultFont, FontStyle.Bold) };
            this.lblSubtotalVal.Location = new Point(200, 25); this.lblSubtotalVal.AutoSize = true; this.lblSubtotalVal.Text = "0.00"; this.lblSubtotalVal.TextAlign = ContentAlignment.MiddleRight;
            
            var lblExtraSum = new Label { Text = "Extra:", Location = new Point(10, 50), AutoSize = true };
            this.lblExtraVal.Location = new Point(200, 50); this.lblExtraVal.AutoSize = true; this.lblExtraVal.Text = "0.00"; this.lblExtraVal.TextAlign = ContentAlignment.MiddleRight;
            
            var lblDiscountSum = new Label { Text = "Discount:", Location = new Point(10, 75), AutoSize = true };
            this.lblDiscountVal.Location = new Point(200, 75); this.lblDiscountVal.AutoSize = true; this.lblDiscountVal.Text = "0.00"; this.lblDiscountVal.TextAlign = ContentAlignment.MiddleRight;
            
            var lblTaxSum = new Label { Text = "Tax:", Location = new Point(10, 100), AutoSize = true, Font = new Font(DefaultFont, FontStyle.Bold) };
            this.lblTaxVal.Location = new Point(200, 100); this.lblTaxVal.AutoSize = true; this.lblTaxVal.Font = new Font(DefaultFont, FontStyle.Bold); this.lblTaxVal.Text = "0.00"; this.lblTaxVal.TextAlign = ContentAlignment.MiddleRight;

            var lblRoundOffSum = new Label { Text = "Round Off:", Location = new Point(10, 125), AutoSize = true };
            this.lblRoundOffVal.Location = new Point(200, 125); this.lblRoundOffVal.AutoSize = true; this.lblRoundOffVal.Text = "0.00"; this.lblRoundOffVal.TextAlign = ContentAlignment.MiddleRight;
            
            var lblTotalSum = new Label { Text = "Total:", Location = new Point(10, 150), AutoSize = true, Font = new Font(DefaultFont, FontStyle.Bold) };
            this.lblTotalVal.Location = new Point(200, 150); this.lblTotalVal.AutoSize = true; this.lblTotalVal.Font = new Font(DefaultFont, FontStyle.Bold); this.lblTotalVal.Text = "0.00"; this.lblTotalVal.TextAlign = ContentAlignment.MiddleRight;
            
            var lblPaidSum = new Label { Text = "Paid:", Location = new Point(10, 175), AutoSize = true };
            this.lblPaidVal.Location = new Point(200, 175); this.lblPaidVal.AutoSize = true; this.lblPaidVal.Text = "0.00"; this.lblPaidVal.TextAlign = ContentAlignment.MiddleRight;
            
            var lblBalanceSum = new Label { Text = "Balance:", Location = new Point(10, 200), AutoSize = true, Font = new Font(DefaultFont, FontStyle.Bold) };
            this.lblBalanceVal.Location = new Point(200, 200); this.lblBalanceVal.AutoSize = true; this.lblBalanceVal.Font = new Font(DefaultFont, FontStyle.Bold); this.lblBalanceVal.Text = "0.00"; this.lblBalanceVal.TextAlign = ContentAlignment.MiddleRight;
            
            // Add controls to Summary GroupBox
            gbSummary.Controls.AddRange(new Control[] {
                lblSubtotalSum, this.lblSubtotalVal,
                lblExtraSum, this.lblExtraVal,
                lblDiscountSum, this.lblDiscountVal,
                lblTaxSum, this.lblTaxVal,
                lblRoundOffSum, this.lblRoundOffVal,
                lblTotalSum, this.lblTotalVal,
                lblPaidSum, this.lblPaidVal,
                lblBalanceSum, this.lblBalanceVal
            });
            
            // Add controls to Payments tab
            paymentsPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            paymentsPanel.Controls.Add(gbSummary);
            paymentsPanel.Controls.Add(gbAdjustments);
            paymentsPanel.Controls.Add(paymentsGrid);
            tabPayments.Controls.Add(paymentsPanel);
            
            // Add tabs to TabControl
            tabControl.TabPages.Add(tabItems);
            tabControl.TabPages.Add(tabPayments);
            // No tab-based visibility change; summary stays visible globally
            tabControl.SelectedIndexChanged += (s, e) => { try { ApplyTopLayout(); } catch { } };
            
            // Add TabControl to panel
            pnlCenter.Controls.Add(tabControl);
            
            // Bottom summary host docked at bottom to avoid overlap (even more compact)
            pnlBottomHost = new Panel { Dock = DockStyle.Bottom, Height = 90, Padding = new Padding(10, 4, 10, 6) };
            // Add a thin top border to bottom host to restore visual separation
            var pnlBottomHostTopLine = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.Black };
            pnlBottomHost.Controls.Add(pnlBottomHostTopLine);
            // Bottom-right summary panel (inside bottom host, more compact)
            pnlSummary.Size = new Size(280, 80);
            pnlSummary.BackColor = Color.Transparent;
            pnlSummary.Visible = true;
            pnlSummary.Dock = DockStyle.Bottom;
            pnlSummary.Height = 50; // Adjust height to fit two rows of labels
            // Arrange labels in a horizontal row at the bottom
            int xPos = 10;
            int yPos = 5;
            int labelWidth = 150;
            int labelHeight = 20;
            int spacing = 10;
            
            // Subtotal
            lblSubtotal.AutoSize = false;
            lblSubtotal.Size = new Size(labelWidth, labelHeight);
            lblSubtotal.Location = new Point(xPos, yPos);
            xPos += labelWidth + spacing;
            
            // Extra
            lblExtraBottom.AutoSize = false;
            lblExtraBottom.Size = new Size(labelWidth, labelHeight);
            lblExtraBottom.Location = new Point(xPos, yPos);
            lblExtraBottom.Visible = true;
            xPos += labelWidth + spacing;
            
            // Discount
            lblDiscountBottom.AutoSize = false;
            lblDiscountBottom.Size = new Size(labelWidth, labelHeight);
            lblDiscountBottom.Location = new Point(xPos, yPos);
            lblDiscountBottom.Visible = true;
            xPos += labelWidth + spacing;
            
            // Back Dues
            lblBackDuesBottom.AutoSize = false;
            lblBackDuesBottom.Size = new Size(labelWidth, labelHeight);
            lblBackDuesBottom.Location = new Point(xPos, yPos);
            lblBackDuesBottom.Visible = true; // Always show, even if 0
            xPos += labelWidth + spacing;
            
            // Tax
            lblTax.AutoSize = false;
            lblTax.Size = new Size(labelWidth, labelHeight);
            lblTax.Location = new Point(xPos, yPos);
            xPos += labelWidth + spacing;

            // Round Off
            lblRoundOffBottom.AutoSize = false;
            lblRoundOffBottom.Size = new Size(labelWidth, labelHeight);
            lblRoundOffBottom.Location = new Point(xPos, yPos);
            xPos = 10; // Reset x position for second row
            yPos += labelHeight + 5;
            
            // Total
            lblTotal.AutoSize = false;
            lblTotal.Size = new Size(labelWidth, labelHeight);
            lblTotal.Location = new Point(xPos, yPos);
            lblTotal.Font = new Font(lblTotal.Font, FontStyle.Bold);
            xPos += labelWidth + spacing;
            
            // Paid Amount
            lblPaidBottom.AutoSize = false;
            lblPaidBottom.Size = new Size(labelWidth, labelHeight);
            lblPaidBottom.Location = new Point(xPos, yPos);
            lblPaidBottom.Visible = true;
            xPos += labelWidth + spacing;
            
            // Balance
            lblBalanceBottom.AutoSize = false;
            lblBalanceBottom.Size = new Size(labelWidth, labelHeight);
            lblBalanceBottom.Location = new Point(xPos, yPos);
            lblBalanceBottom.Font = new Font(DefaultFont, FontStyle.Bold);
            lblBalanceBottom.Visible = true;
            pnlSummary.Controls.AddRange(new Control[] { lblSubtotal, lblExtraBottom, lblDiscountBottom, lblBackDuesBottom, lblTax, lblRoundOffBottom, lblTotal, lblPaidBottom, lblBalanceBottom });
            // Assemble bottom host
            pnlBottomHost.Controls.Add(pnlSummary);
            // Add all controls to content panel (summary visible for both tabs)
            contentPanel.Controls.AddRange(new Control[]
            {
                pnlCenter,
                pnlBottomHost,
                lblStatus
            });
            // Ensure docking order: Top host docks first (highest index), Bottom host next, Center fills last
            try {
                if (!contentPanel.Controls.Contains(pnlTopHost)) contentPanel.Controls.Add(pnlTopHost);
                // Set z-order: center lowest (0), bottom next (1), top highest (2)
                contentPanel.Controls.SetChildIndex(pnlCenter, 0);
                contentPanel.Controls.SetChildIndex(pnlBottomHost, 1);
                contentPanel.Controls.SetChildIndex(pnlTopHost, 2);
            } catch { }

            // Responsive top layout: place invoice box at the right of customer box and move grid below them
            void ApplyTopLayout()
            {
                try
                {
                    int spacing = 10;
                    // Align invoice box to the right of customer box
                    gbCustomer.SetBounds(spacing, spacing, Math.Max(700, contentPanel.ClientSize.Width - 380 - 3 * spacing), 155);
                    gbInvoice.SetBounds(contentPanel.ClientSize.Width - 360 - spacing, spacing, 360, 160);
                    // Compute top host height based on group boxes
                    int belowY = Math.Max(gbCustomer.Bottom, gbInvoice.Bottom) + spacing;
                    pnlTopHost.Height = belowY;
                    // Reserve a compact fixed space for the bottom host
                    // Allow summary panel to use full available width so all labels are visible
                    pnlSummary.Width = Math.Max(400, contentPanel.ClientSize.Width - 20);
                    pnlBottomHost.Height = 90;
                    int reservedBottom = 90;
                    // Center panel fills remaining space via Dock=Fill
                    // left indicators now handled by pnlItemsFooter
                    // ensure scrollbars appear when needed
                    try { contentPanel.AutoScrollMinSize = new Size(0, belowY + reservedBottom + 60); } catch { }
                }
                catch { }
            }

            // no separate positioning helper needed; ApplyTopLayout handles summary

            ApplyTopLayout();
            contentPanel.Resize += (s, e) => ApplyTopLayout();
            this.Resize += (s, e) =>
            {
                ApplyTopLayout();
            };

            // Auto-save on close
            this.FormClosing += (s, e) =>
            {
                try
                {
                    autoSaving = true;
                    // Avoid any UI prompts or Close() calls within save
                    BtnOk_Click(this, EventArgs.Empty);
                    // ensure parent list can detect changes
                    this.DialogResult = DialogResult.OK;
                }
                catch { }
                finally { autoSaving = false; }
            };
            // Event handlers for adjustment controls
            void UpdateAdjustments()
            {
                if (Model == null) return;
                
                Model.ExtraCostName = txtExtraCostName.Text;
                Model.ExtraCostAmount = numExtraCost.Value;
                Model.ExtraCostIsPercent = chkExtraPercent.Checked;
                Model.DiscountAmount = numDiscount.Value;
                Model.DiscountIsPercent = chkDiscountPercent.Checked;
                // refresh summary and totals immediately in editor
                RecalcRequested?.Invoke(this, EventArgs.Empty);
                Recalc();
            }
            
            txtExtraCostName.TextChanged += (s, e) => UpdateAdjustments();
            numExtraCost.ValueChanged += (s, e) => UpdateAdjustments();
            chkExtraPercent.CheckedChanged += (s, e) => UpdateAdjustments();
            numDiscount.ValueChanged += (s, e) => UpdateAdjustments();
            chkDiscountPercent.CheckedChanged += (s, e) => UpdateAdjustments();
            
            // Update payments grid when model changes
            this.Shown += (s, e) => UpdatePaymentsGrid();
            
            // Helper method to update payments grid
            void UpdatePaymentsGrid()
            {
                if (paymentsGrid == null || Model?.Payments == null) return;
                
                paymentsGrid.DataSource = null;
                if (Model.Payments.Count > 0)
                {
                    var payments = Model.Payments.Select(p => new 
                    { 
                        p.Date, 
                        p.Method, 
                        p.Notes, 
                        p.Amount 
                    }).ToList();
                    paymentsGrid.DataSource = payments;
                }
            }
            
            // Update summary when model changes
            this.RecalcRequested += (s, e) => 
            {
                // Update summary labels with right-aligned values
                lblSubtotalVal.Text = Model.Subtotal.ToString("0.00").PadLeft(10);
                
                decimal extraAbs = Model.ExtraCostIsPercent ? 
                    Math.Round(Model.Subtotal * (Model.ExtraCostAmount / 100m), 2) : 
                    Model.ExtraCostAmount;
                lblExtraVal.Text = extraAbs.ToString("0.00").PadLeft(10);
                
                decimal discountAbs = Model.DiscountIsPercent ? 
                    Math.Round(Model.Subtotal * (Model.DiscountAmount / 100m), 2) : 
                    Model.DiscountAmount;
                lblDiscountVal.Text = discountAbs.ToString("0.00");
                
                lblTaxVal.Text = Model.TaxAmount.ToString("0.00");
                lblTotalVal.Text = Model.Total.ToString("0.00");
                
                decimal totalPaid = Model.Payments?.Sum(p => p.Amount) ?? 0m;
                lblPaidVal.Text = totalPaid.ToString("0.00");
                
                decimal balance = Model.Total - totalPaid;
                lblBalanceVal.Text = balance.ToString("0.00");
                
                // Update status label
                if (Model.Status == InvoiceStatus.Void)
                    lblStatus.Text = "Status: Void";
                else if (markPaidOnSave)
                    lblStatus.Text = "Status: Paid (will mark on save)";
                else if (totalPaid >= Model.Total)
                    lblStatus.Text = "Status: Paid";
                else if (totalPaid > 0)
                    lblStatus.Text = "Status: Partial";
                else if (dtDue.Value.Date < DateTime.Today)
                    lblStatus.Text = "Status: Past Due";
                else
                    lblStatus.Text = "Status: Unpaid";
                
                // Update payments grid
                UpdatePaymentsGrid();
            };

            if (invoiceId == null)
            {
                Model = new Invoice { InvoiceDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) };
                BindItemsFromModel();
                Recalc();
            }
            else
            {
                Model = db.Invoices.Where(i => i.Id == invoiceId).Select(i => new Invoice
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    CustomerId = i.CustomerId,
                    Status = i.Status,
                    Subtotal = i.Subtotal,
                    TaxAmount = i.TaxAmount,
                    DiscountAmount = i.DiscountAmount,
                    Total = i.Total,
                    TotalPaid = i.TotalPaid,
                    PrivateNotes = i.PrivateNotes,
                    Items = i.Items.Select(x => new InvoiceItem
                    {
                        Id = x.Id,
                        ProductId = x.ProductId,
                        ProductIdRef = x.ProductIdRef,
                        Description = x.Description,
                        Price = x.Price,
                        CostPrice = x.CostPrice,
                        ProductName = x.ProductName,
                        BatchNumber = x.BatchNumber,
                        Quantity = x.Quantity,
                        LineTotal = x.LineTotal
                    }).ToList(),
                    Payments = i.Payments.Select(p => new Payment
                    {
                        Id = p.Id,
                        Date = p.Date,
                        Method = p.Method,
                        Amount = p.Amount,
                        Notes = p.Notes
                    }).ToList()
                }).First();
                // Rebind customers fresh and select by saved id to avoid defaulting to first item
                try
                {
                    using var db2 = new AppDbContext();
                    var customers2 = db2.Customers.OrderBy(c => c.Name).ToList();
                    cmbCustomer.BeginInvoke(new Action(() =>
                    {
                        var id = Model.CustomerId;
                        cmbCustomer.DataSource = new BindingSource(customers2, null);
                        cmbCustomer.DisplayMember = "Name";
                        cmbCustomer.ValueMember = "Id";
                        if (id.HasValue) SelectCustomerById(id.Value);
                    }));
                }
                catch { }

                dtInvoice.Value = Model.InvoiceDate;
                dtDue.Value = Model.DueDate ?? DateTime.Today;
                if (Model.CustomerId.HasValue) SelectCustomerById(Model.CustomerId.Value);
                txtInvoiceNo.Text = Model.InvoiceNumber ?? string.Empty;
                BindItemsFromModel();
                // Parse adjustment values and back dues from PrivateNotes if present
                try
                {
                    var notes = Model.PrivateNotes ?? string.Empty;
                    // Format: Adj:ExtraName=...;ExtraAmt=...;ExtraPct=0/1;DiscAmt=...;DiscPct=0/1;
                    var adjIdx = notes.IndexOf("Adj:", StringComparison.OrdinalIgnoreCase);
                    if (adjIdx >= 0)
                    {
                        var seg = notes.Substring(adjIdx + 4).Trim();
                        var parts = seg.Split(';');
                        foreach (var p in parts)
                        {
                            var kv = p.Split('='); if (kv.Length != 2) continue;
                            var k = kv[0].Trim(); var v = kv[1].Trim();
                            if (k.Equals("ExtraName", StringComparison.OrdinalIgnoreCase)) txtExtraCostName.Text = v;
                            else if (k.Equals("ExtraAmt", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(v, out var ea)) numExtraCost.Value = Math.Max(0, ea);
                            else if (k.Equals("ExtraPct", StringComparison.OrdinalIgnoreCase)) chkExtraPercent.Checked = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (k.Equals("DiscAmt", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(v, out var da)) numDiscount.Value = Math.Max(0, da);
                            else if (k.Equals("DiscPct", StringComparison.OrdinalIgnoreCase)) chkDiscountPercent.Checked = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (k.Equals("BackAmt", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(v, out var bdAmt)) backDuesAmount = Math.Max(0, bdAmt);
                            else if (k.Equals("BackInc", StringComparison.OrdinalIgnoreCase)) Model.IncludedBackDues = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }
                        // Sync parsed values into Model so totals reflect immediately
                        Model.ExtraCostName = txtExtraCostName.Text;
                        Model.ExtraCostAmount = numExtraCost.Value;
                        Model.ExtraCostIsPercent = chkExtraPercent.Checked;
                        Model.DiscountAmount = numDiscount.Value;
                        Model.DiscountIsPercent = chkDiscountPercent.Checked;
                        // Set back dues checkbox without triggering recompute from DB
                        suppressBackDuesEvent = true;
                        try { chkBackDuesAdj.Checked = Model.IncludedBackDues && backDuesAmount > 0; } finally { suppressBackDuesEvent = false; }
                    }
                }
                catch { }
                // Ensure payments grid shows saved history
                UpdatePaymentsGrid();
                RecalcRequested?.Invoke(this, EventArgs.Empty);
                Recalc();
            }
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

        private (string? batch, string? src) ParseBatchAndSrc(string? desc)
        {
            string? batch = null; string? src = null;
            if (string.IsNullOrWhiteSpace(desc)) return (batch, src);
            foreach (var part in (desc ?? string.Empty).Split('|'))
            {
                var kv = part.Split(':');
                if (kv.Length == 2)
                {
                    var key = kv[0].Trim().ToUpperInvariant();
                    var val = kv[1].Trim();
                    if (key == "BATCH") batch = val;
                    else if (key == "SRC") src = val;
                }
            }
            return (batch, src);
        }

        private decimal? ResolveMrp(InvoiceItem it)
        {
            var mrp = TryParseMrp(it.Description);
            if (mrp.HasValue) return mrp;
            // Fallback from product based on batch/src
            try
            {
                using var db = new AppDbContext();
                Models.Product? p = null;
                if (it.ProductIdRef.HasValue) p = db.Products.FirstOrDefault(x => x.Id == it.ProductIdRef.Value);
                if (p == null && !string.IsNullOrWhiteSpace(it.ProductId)) p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                if (p == null) return null;
                var (batch, src) = ParseBatchAndSrc(it.Description);
                if (!string.IsNullOrWhiteSpace(src))
                {
                    if (src.Equals("New", StringComparison.OrdinalIgnoreCase)) return p.NewMrp ?? p.OldMrp ?? p.VeryOldMrp;
                    if (src.Equals("Old", StringComparison.OrdinalIgnoreCase)) return p.OldMrp ?? p.NewMrp ?? p.VeryOldMrp;
                }
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    if (p.NewBatch == batch) return p.NewMrp ?? p.OldMrp ?? p.VeryOldMrp;
                    if (p.OldBatch == batch) return p.OldMrp ?? p.NewMrp ?? p.VeryOldMrp;
                    if (p.VeryOldBatch == batch) return p.VeryOldMrp ?? p.OldMrp ?? p.NewMrp;
                }
                return p.NewMrp ?? p.OldMrp ?? p.VeryOldMrp;
            }
            catch { return null; }
        }

        private DateTime? ResolveExpiry(InvoiceItem it)
        {
            var exp = TryParseExpiry(it.Description);
            if (exp.HasValue) return exp;
            try
            {
                using var db = new AppDbContext();
                Models.Product? p = null;
                if (it.ProductIdRef.HasValue) p = db.Products.FirstOrDefault(x => x.Id == it.ProductIdRef.Value);
                if (p == null && !string.IsNullOrWhiteSpace(it.ProductId)) p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                if (p == null) return null;
                var (batch, src) = ParseBatchAndSrc(it.Description);
                if (!string.IsNullOrWhiteSpace(src))
                {
                    if (src.Equals("New", StringComparison.OrdinalIgnoreCase)) return p.NewExpiry ?? p.OldExpiry ?? p.VeryOldExpiry;
                    if (src.Equals("Old", StringComparison.OrdinalIgnoreCase)) return p.OldExpiry ?? p.NewExpiry ?? p.VeryOldExpiry;
                }
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    if (p.NewBatch == batch) return p.NewExpiry ?? p.OldExpiry ?? p.VeryOldExpiry;
                    if (p.OldBatch == batch) return p.OldExpiry ?? p.NewExpiry ?? p.VeryOldExpiry;
                    if (p.VeryOldBatch == batch) return p.VeryOldExpiry ?? p.OldExpiry ?? p.NewExpiry;
                }
                return p.NewExpiry ?? p.OldExpiry ?? p.VeryOldExpiry;
            }
            catch { return null; }
        }

        private void SelectCustomerById(int customerId)
        {
            try
            {
                cmbCustomer.SelectedValue = customerId;
                if (cmbCustomer.SelectedIndex < 0)
                {
                    if (cmbCustomer.DataSource is BindingSource bs && bs.List != null)
                    {
                        for (int i = 0; i < bs.Count; i++)
                        {
                            if (bs[i] is BillingSuite.App.Models.Customer c && c.Id == customerId)
                            {
                                cmbCustomer.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private int AddEmptyRow()
        {
            var bs = gridItems.DataSource as BindingSource;
            if (bs == null)
            {
                bs = new BindingSource();
                bs.DataSource = new System.Collections.Generic.List<InvoiceItem>();
                gridItems.DataSource = bs;
            }
            var list = (System.Collections.Generic.List<InvoiceItem>)bs.List;
            list.Add(new InvoiceItem { Quantity = 1, Price = 0 });
            bs.ResetBindings(false);
            Recalc();
            return list.Count - 1;
        }

        private void RemoveSelectedRow()
        {
            try
            {
                if (gridItems.CurrentRow == null) return;
                if (gridItems.DataSource is not BindingSource bs) return;
                if (gridItems.CurrentRow.Index < 0 || gridItems.CurrentRow.Index >= bs.Count) return;
                bs.RemoveAt(gridItems.CurrentRow.Index);
                bs.ResetBindings(false);
                Recalc();
            }
            catch { /* swallow to avoid control paint crash */ }
        }

        private void TryRemoveRowAt(int rowIndex)
        {
            try
            {
                if (rowIndex < 0) return;
                if (gridItems.DataSource is not BindingSource bs) return;
                if (rowIndex >= bs.Count) return;
                bs.RemoveAt(rowIndex);
                bs.ResetBindings(false);
                Recalc();
            }
            catch { }
        }

        private void BindItemsFromModel()
        {
            var bs = new BindingSource();
            bs.DataSource = Model.Items.Select(x => new InvoiceItem
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductIdRef = x.ProductIdRef,
                Description = x.Description,
                Price = x.Price,
                CostPrice = x.CostPrice,
                ProductName = x.ProductName,
                BatchNumber = x.BatchNumber,
                Quantity = x.Quantity,
                LineTotal = x.LineTotal
            }).ToList();
            gridItems.DataSource = bs;
            
            // Initialize extra cost and discount controls
            txtExtraCostName.Text = Model.ExtraCostName ?? string.Empty;
            numExtraCost.Value = Model.ExtraCostAmount > 0 ? Model.ExtraCostAmount : 0;
            chkExtraPercent.Checked = Model.ExtraCostIsPercent;
            
            numDiscount.Value = Model.DiscountAmount > 0 ? Model.DiscountAmount : 0;
            chkDiscountPercent.Checked = Model.DiscountIsPercent;
            // Update items count
            try { lblItemsCountBottom.Text = $"Items: {bs.Count}"; } catch { }
            
            // resolve product names and mrp
            using (var db = new AppDbContext())
            {
                var dictByBarcode = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                var dictById = new System.Collections.Generic.Dictionary<int, string>();
                foreach (var p in db.Products.OrderBy(p => p.Name))
                {
                    var key = p.Barcode ?? string.Empty;
                    if (!dictByBarcode.ContainsKey(key))
                        dictByBarcode[key] = p.Name ?? string.Empty;
                    if (!dictById.ContainsKey(p.Id))
                        dictById[p.Id] = p.Name ?? string.Empty;
                }
                foreach (DataGridViewRow r in gridItems.Rows)
                {
                    var it = r.DataBoundItem as InvoiceItem;
                    if (it == null) continue;
                    var key = it.ProductId ?? string.Empty;
                    string? name = null;
                    if (!string.IsNullOrWhiteSpace(key) && dictByBarcode.TryGetValue(key, out var nm)) name = nm;
                    else if (it.ProductIdRef.HasValue && dictById.TryGetValue(it.ProductIdRef.Value, out var nm2)) name = nm2;
                    else if (int.TryParse(key, out var pid) && dictById.TryGetValue(pid, out var nm3)) name = nm3;
                    r.Cells["colName"].Value = !string.IsNullOrWhiteSpace(name) ? name : (it.ProductId ?? string.Empty);
                    var mv = TryParseMrp(it.Description);
                    r.Cells["colMrp"].Value = mv.HasValue ? mv.Value.ToString("0.00") : string.Empty;
                }
            }
            
            // Update payments grid and summary
            UpdatePaymentsGrid();
            // Trigger initial calculation
            RecalcRequested?.Invoke(this, EventArgs.Empty);
            
            // Mrp_KeyPress is now at class level
        }

        private void Recalc()
        {
            if (Model == null) return;
            
            decimal subtotal = 0;
            if (gridItems.DataSource is BindingSource bs && bs.List != null)
            {
                foreach (InvoiceItem it in bs.List)
                {
                    it.LineTotal = it.Price * it.Quantity;
                    subtotal += it.LineTotal;
                }
                if (!editingCell && !gridItems.IsCurrentCellInEditMode)
                {
                    gridItems.Refresh();
                }
                try { lblItemsCountBottom.Text = $"Items: {bs.Count}"; } catch { }
            }
            // Compute extra/discount consistently
            decimal extraAbs = Model.ExtraCostAmount > 0 ? (Model.ExtraCostIsPercent ? Math.Round(subtotal * (Model.ExtraCostAmount / 100m), 2) : Model.ExtraCostAmount) : 0m;
            decimal discountAbs = Model.DiscountAmount > 0 ? (Model.DiscountIsPercent ? Math.Round(subtotal * (Model.DiscountAmount / 100m), 2) : Model.DiscountAmount) : 0m;
            // Keep absolute discount stored when using absolute; when percent, keep percent value
            if (!Model.DiscountIsPercent) Model.DiscountAmount = discountAbs;
            var taxableBase = subtotal + extraAbs - discountAbs;
            if (taxableBase < 0) taxableBase = 0;
            var tax = Math.Round(taxableBase * (numTaxRate.Value / 100m), 2);
            // Back dues are NOT taxed and simply added to final
            var grossTotal = taxableBase + tax + backDuesAmount;
            // Automatic round off to nearest 1.00
            var roundedTotal = Math.Round(grossTotal, 0, MidpointRounding.AwayFromZero);
            var roundOff = roundedTotal - grossTotal;
            // Update all bottom labels with right-aligned values
            lblSubtotal.Text = $"Subtotal: {subtotal,10:0.00}";
            lblExtraBottom.Text = $"Extra: {(extraAbs >= 0 ? "+" : "-")}{Math.Abs(extraAbs),8:0.00}{(Model.ExtraCostIsPercent ? "%" : "")}";
            lblDiscountBottom.Text = $"Discount: -{discountAbs,6:0.00}{(Model.DiscountIsPercent ? "%" : "")}";
            lblBackDuesBottom.Text = $"Back Dues: {backDuesAmount,7:0.00}";
            lblTax.Text = $"Tax: {tax,14:0.00}";
            lblRoundOffBottom.Text = $"Round Off: {roundOff,8:0.00}";
            lblTotal.Text = $"TOTAL: {roundedTotal,12:0.00}";
            // Update Summary box labels
            try
            {
                lblSubtotalVal.Text = subtotal.ToString("0.00");
                lblExtraVal.Text = (extraAbs >= 0 ? "+" : "-") + Math.Abs(extraAbs).ToString("0.00") + (Model.ExtraCostIsPercent ? "%" : "");
                lblDiscountVal.Text = (discountAbs > 0 ? "-" : "") + Math.Abs(discountAbs).ToString("0.00") + (Model.DiscountIsPercent ? "%" : "");
                lblTaxVal.Text = tax.ToString("0.00");
                lblRoundOffVal.Text = roundOff.ToString("0.00");
                lblTotalVal.Text = roundedTotal.ToString("0.00");
                // Paid/Balance filled after paidNow/balanceNow computed below
            }
            catch { }
            // Refresh model monetary state
            var paidNow = Model.Payments?.Sum(p => p.Amount) ?? Model.TotalPaid;
            Model.TotalPaid = paidNow;
            Model.Total = roundedTotal;
            var balanceNow = roundedTotal - paidNow;
            lblPaidBottom.Text = $"Paid: {paidNow,13:0.00}";
            lblBalanceBottom.Text = $"BALANCE: {balanceNow,9:0.00}";
            try { lblPaidVal.Text = paidNow.ToString("0.00"); lblBalanceVal.Text = balanceNow.ToString("0.00"); } catch { }
            Model.Subtotal = subtotal; Model.TaxAmount = tax;
            // Compute total profit (TP) across items
            decimal totalProfit = 0m;
            try
            {
                if (gridItems.DataSource is BindingSource bs2 && bs2.List != null)
                {
                    using var dbp = new AppDbContext();
                    foreach (InvoiceItem it2 in bs2.List)
                    {
                        decimal? cp2 = null;
                        var cpTok = ExtractToken(it2.Description, "CP"); if (decimal.TryParse(cpTok, out var cptv)) cp2 = cptv;
                        if (!cp2.HasValue)
                        {
                            try
                            {
                                var batch2 = ParseBatchAndSrc(it2.Description).batch;
                                if (it2.ProductIdRef.HasValue && !string.IsNullOrWhiteSpace(batch2))
                                {
                                    var pb = dbp.ProductBatches.FirstOrDefault(b => b.ProductIdRef == it2.ProductIdRef.Value && b.BatchNumber == batch2);
                                    if (pb?.CostPrice != null) cp2 = pb.CostPrice;
                                }
                                if (!cp2.HasValue)
                                {
                                    var p = (it2.ProductIdRef.HasValue ? dbp.Products.FirstOrDefault(x => x.Id == it2.ProductIdRef.Value) : null) ?? (!string.IsNullOrWhiteSpace(it2.ProductId) ? dbp.Products.FirstOrDefault(x => x.Barcode == it2.ProductId) : null);
                                    if (p != null && !string.IsNullOrWhiteSpace(batch2))
                                    {
                                        if (string.Equals(batch2, p.NewBatch, StringComparison.OrdinalIgnoreCase)) cp2 = p.NewCostPrice ?? p.NewSellingPrice ?? p.NewMrp;
                                        else if (string.Equals(batch2, p.OldBatch, StringComparison.OrdinalIgnoreCase)) cp2 = p.OldCostPrice ?? p.OldSellingPrice ?? p.OldMrp;
                                        else if (string.Equals(batch2, p.VeryOldBatch, StringComparison.OrdinalIgnoreCase)) cp2 = p.VeryOldCostPrice ?? p.VeryOldSellingPrice ?? p.VeryOldMrp;
                                        if (!cp2.HasValue) cp2 = p.NewCostPrice ?? p.OldCostPrice ?? p.VeryOldCostPrice ?? p.Price;
                                    }
                                }
                            }
                            catch { }
                        }
                        if (cp2.HasValue)
                        {
                            totalProfit += (it2.Price - cp2.Value) * it2.Quantity;
                        }
                    }
                }
            }
            catch { }
            // Update TP label and compact footer right text with TP
            try
            {
                lblTpBottom.Text = $"TP: {totalProfit:0.00}";
                lblRightCompact.Text = $"Sub {subtotal:0.00} | +Ex {extraAbs:0.00} | -Dis {discountAbs:0.00} | Tax {tax:0.00} | BD {backDuesAmount:0.00} | RO {roundOff:0.00} | Tot {roundedTotal:0.00} | Paid {paidNow:0.00} | Bal {balanceNow:0.00} | TP {totalProfit:0.00}";
            }
            catch { }
            // decide and persist status
            if (Model.Status == InvoiceStatus.Void)
            {
                lblStatus.Text = "Status: Void";
            }
            else if (paidNow >= Model.Total)
            {
                Model.Status = InvoiceStatus.Paid;
                lblStatus.Text = "Status: Paid";
            }
            else if (paidNow > 0)
            {
                Model.Status = InvoiceStatus.Partial;
                lblStatus.Text = "Status: Partial";
            }
            else if (dtDue.Value.Date < DateTime.Today)
            {
                Model.Status = InvoiceStatus.Unpaid;
                lblStatus.Text = "Status: Past Due";
            }
            else
            {
                Model.Status = InvoiceStatus.Unpaid;
                lblStatus.Text = "Status: Unpaid";
            }
            // Ensure summary panel reflects current totals
            RecalcRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateCompactTotals()
        {
            try
            {
                // Trigger Recalc will also update lblRightCompact
                Recalc();
            }
            catch { }
        }

        private void UpdateLeftInfoForSelection()
        {
            try
            {
                if (gridItems.CurrentRow == null || gridItems.DataSource is not BindingSource bs) { lblLeftCompact.Text = string.Empty; return; }
                if (gridItems.CurrentRow.Index < 0 || gridItems.CurrentRow.Index >= bs.Count) { lblLeftCompact.Text = string.Empty; return; }
                var it = (InvoiceItem)bs[gridItems.CurrentRow.Index];
                using var db = new AppDbContext();
                Models.Product? p = null;
                if (it.ProductIdRef.HasValue) p = db.Products.FirstOrDefault(x => x.Id == it.ProductIdRef.Value);
                if (p == null && !string.IsNullOrWhiteSpace(it.ProductId)) p = db.Products.FirstOrDefault(x => x.Barcode == it.ProductId);
                string batch = ParseBatchAndSrc(it.Description).batch ?? string.Empty;
                decimal? cp = null;
                decimal? savedCp = null; decimal? savedSp = null; // for Fixed Margin (FM)
                decimal? stockNow = null;
                if (p != null)
                {
                    // Prefer ProductBatch (new schema)
                    try
                    {
                        var pb = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == p.Id && b.BatchNumber == batch);
                        if (pb != null)
                        {
                            cp = pb.CostPrice ?? cp;
                            savedCp = pb.CostPrice ?? savedCp;
                            savedSp = pb.SellingPrice ?? savedSp;
                            stockNow = pb.Stock ?? stockNow;
                        }
                    }
                    catch { }
                    // Legacy fallback when ProductBatches missing
                    if (cp == null)
                    {
                        if (!string.IsNullOrEmpty(batch))
                        {
                            if (string.Equals(batch, p.NewBatch, StringComparison.OrdinalIgnoreCase)) cp = p.NewCostPrice ?? p.NewSellingPrice ?? p.NewMrp;
                            else if (string.Equals(batch, p.OldBatch, StringComparison.OrdinalIgnoreCase)) cp = p.OldCostPrice ?? p.OldSellingPrice ?? p.OldMrp;
                            else if (string.Equals(batch, p.VeryOldBatch, StringComparison.OrdinalIgnoreCase)) cp = p.VeryOldCostPrice ?? p.VeryOldSellingPrice ?? p.VeryOldMrp;
                        }
                        if (cp == null)
                            cp = p.NewCostPrice ?? p.OldCostPrice ?? p.VeryOldCostPrice ?? p.Price;
                    }
                }
                var pr = cp.HasValue ? (it.Price - cp.Value) : (decimal?)null;
                decimal? fm = null; if (savedCp.HasValue && savedSp.HasValue) fm = savedSp.Value - savedCp.Value;
                // last sale price and mrp for same batch & customer
                decimal? ls = null; decimal? lsmp = null; decimal? lcp = null;
                try
                {
                    // Prefer Model.CustomerId but fall back to selected combo value for new invoices
                    int? cid = Model.CustomerId;
                    if (!cid.HasValue)
                    {
                        try { if (cmbCustomer?.SelectedValue is int v) cid = v; } catch { cid = null; }
                    }
                    if (cid.HasValue && p != null && !string.IsNullOrWhiteSpace(batch))
                    {
                        var q = (from ii in db.InvoiceItems.AsNoTracking()
                                 join inv in db.Invoices.AsNoTracking() on ii.InvoiceId equals inv.Id
                                 where inv.CustomerId == cid && ii.ProductIdRef == p.Id && ii.Description != null && ii.Description.Contains($"BATCH:{batch}")
                                 orderby inv.InvoiceDate descending, ii.InvoiceId descending, ii.Id descending
                                 select new { ii.Price, ii.Description }).FirstOrDefault();
                        if (q != null)
                        {
                            ls = q.Price;
                            var mv = TryParseMrp(q.Description);
                            if (mv.HasValue) lsmp = mv.Value;
                            var cpt = ExtractToken(q.Description, "CP");
                            if (decimal.TryParse(cpt, out var cpv)) lcp = cpv;
                        }
                    }
                }
                catch { }
                string S(decimal? v) => v.HasValue ? v.Value.ToString("0.00") : "-";
                var batchShort = string.IsNullOrWhiteSpace(batch) ? "-" : batch;
                if (!lcp.HasValue) lcp = cp; // fallback last cost
                lblLeftCompact.Text = $"CP {S(cp)} | PR {S(pr)} | FM {S(fm)} | Stock {S(stockNow)} | BN {batchShort} | LS {S(ls)} | LMRP {S(lsmp)} | LCP {S(lcp)}";
            }
            catch { lblLeftCompact.Text = string.Empty; }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (preventSave) { try { DialogResult = DialogResult.Cancel; } catch { } return; }
            if (!autoSaving && cmbCustomer.SelectedItem == null) { MessageBox.Show("Select a customer."); return; }
            if (cmbCustomer.SelectedValue is int selCustId)
                Model.CustomerId = selCustId;
            Model.InvoiceDate = dtInvoice.Value.Date;
            Model.DueDate = dtDue.Value.Date;
            Model.InvoiceNumber = txtInvoiceNo.Text?.Trim();
            // Persist latest adjustments from controls
            Model.ExtraCostName = txtExtraCostName.Text;
            Model.ExtraCostAmount = numExtraCost.Value;
            Model.ExtraCostIsPercent = chkExtraPercent.Checked;
            Model.DiscountAmount = numDiscount.Value;
            Model.DiscountIsPercent = chkDiscountPercent.Checked;
            // Save terms in PrivateNotes (simple storage for now)
            if (!string.IsNullOrWhiteSpace(cmbTerms.Text))
                Model.PrivateNotes = $"Terms: {cmbTerms.Text}";
            if (string.IsNullOrWhiteSpace(Model.InvoiceNumber))
            {
                // Generate from Settings: prefix + zero-padded counter
                var prefix  = AppSettingsService.GetString(AppSettingKeys.InvoicePrefix, "INV-");
                var next    = AppSettingsService.GetInt(AppSettingKeys.InvoiceNextNumber, 1);
                var padding = AppSettingsService.GetInt(AppSettingKeys.InvoiceNumberPadding, 4);
                Model.InvoiceNumber = $"{prefix}{next.ToString().PadLeft(padding, '0')}";
                // Increment the counter so the next invoice gets a unique number
                AppSettingsService.Set(AppSettingKeys.InvoiceNextNumber, (next + 1).ToString());
            }

            // Snapshot Customer Details
            if (cmbCustomer.SelectedItem is Models.Customer cust)
            {
                Model.CustomerNameSnapshot = cust.Name;
                Model.CustomerPhoneSnapshot = cust.Phone;
                Model.CustomerAddressSnapshot = cust.Address1;
                Model.CustomerGstSnapshot = cust.GstVatNumber;
            }
            else if (cmbCustomer.SelectedValue is int cidSnapshot)
            {
                using var dbCust = new AppDbContext();
                var custDb = dbCust.Customers.FirstOrDefault(c => c.Id == cidSnapshot);
                if (custDb != null)
                {
                    Model.CustomerNameSnapshot = custDb.Name;
                    Model.CustomerPhoneSnapshot = custDb.Phone;
                    Model.CustomerAddressSnapshot = custDb.Address1;
                    Model.CustomerGstSnapshot = custDb.GstVatNumber;
                }
            }

            // collect items
            Model.Items.Clear();
            decimal subtotal = 0;
            if (gridItems.DataSource is BindingSource bs)
            {
                for (int i = 0; i < bs.Count; i++)
                {
                    var it = (InvoiceItem)bs[i];
                    it.LineTotal = it.Price * it.Quantity;
                    subtotal += it.LineTotal;
                    Model.Items.Add(new InvoiceItem
                    {
                        ProductId = it.ProductId ?? string.Empty,
                        ProductIdRef = it.ProductIdRef,
                        ProductName = it.ProductName ?? string.Empty,
                        Description = it.Description ?? string.Empty,
                        Price = it.Price,
                        CostPrice = it.CostPrice,
                        BatchNumber = it.BatchNumber,
                        Quantity = it.Quantity,
                        LineTotal = it.LineTotal
                    });
                }
            }
            // Recompute final totals using same logic as Recalc
            decimal extraAbs2 = Model.ExtraCostAmount > 0 ? (Model.ExtraCostIsPercent ? Math.Round(subtotal * (Model.ExtraCostAmount / 100m), 2) : Model.ExtraCostAmount) : 0m;
            decimal discountAbs2 = Model.DiscountAmount > 0 ? (Model.DiscountIsPercent ? Math.Round(subtotal * (Model.DiscountAmount / 100m), 2) : Model.DiscountAmount) : 0m;
            Model.Subtotal = subtotal;
            var taxableBase2 = Math.Max(0m, subtotal + extraAbs2 - discountAbs2);
            Model.TaxAmount = Math.Round(taxableBase2 * (numTaxRate.Value / 100m), 2);
            var gross2 = taxableBase2 + Model.TaxAmount + backDuesAmount;
            var rounded2 = Math.Round(gross2, 0, MidpointRounding.AwayFromZero);
            var roundOff2 = rounded2 - gross2;
            Model.Total = rounded2;
            Model.BackDues = backDuesAmount;
            // Re-evaluate status based on payments
            Model.TotalPaid = Model.Payments?.Sum(p => p.Amount) ?? 0m;
            if (Model.TotalPaid >= Model.Total)
                Model.Status = InvoiceStatus.Paid;
            else if (Model.TotalPaid > 0)
                Model.Status = InvoiceStatus.Partial;
            else
                Model.Status = InvoiceStatus.Unpaid;

            // Handle Back Dues explicitly (always unsettled first, then settled if included)
            try
            {
                using var dbSettle = new AppDbContext();
                
                // 1. Unsettle any past carry forwards linked to this specific invoice
                string searchStr = (Model.InvoiceNumber ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(searchStr)) return; // Safety: nothing to unsettle if no number yet

                var pastPayments = dbSettle.Payments
                    .Include(p => p.Invoice)
                    .Where(p => p.Notes != null && p.Notes.Contains(searchStr) && p.Notes.StartsWith("Cleared via invoice"))
                    .ToList();
                
                foreach (var p in pastPayments)
                {
                    if (p.Invoice != null)
                    {
                        p.Invoice.TotalPaid -= p.Amount;
                        if (p.Invoice.TotalPaid < 0) p.Invoice.TotalPaid = 0m;
                        // Re-evaluate the status of the old invoice
                        if (p.Invoice.TotalPaid <= 0) p.Invoice.Status = InvoiceStatus.Unpaid;
                        else if (p.Invoice.TotalPaid >= p.Invoice.Total) p.Invoice.Status = InvoiceStatus.Paid;
                        else p.Invoice.Status = InvoiceStatus.Partial;
                        
                        dbSettle.Invoices.Update(p.Invoice); // Explicitly update
                    }
                    dbSettle.Payments.Remove(p);
                }
                
                // Save unsettle step first to clean state
                dbSettle.SaveChanges();

                // 2. If back dues are checked, settle the newly outstanding past invoices
                if (Model.IncludedBackDues && cmbCustomer.SelectedValue is int cidBD)
                {
                    var oldInvoices = dbSettle.Invoices
                        .Where(i => i.CustomerId == cidBD && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Void && i.Id != Model.Id)
                        .OrderBy(i => i.InvoiceDate)
                        .ToList();
                        
                    foreach (var invOld in oldInvoices)
                    {
                        var balance = invOld.Total - invOld.TotalPaid;
                        if (balance <= 0) { invOld.Status = InvoiceStatus.Paid; continue; }
                        
                        invOld.TotalPaid += balance;
                        invOld.Status = InvoiceStatus.Paid;
                        if (invOld.Payments == null) invOld.Payments = new System.Collections.Generic.List<Payment>();
                        invOld.Payments.Add(new Payment { Date = DateTime.Today, Method = "Carry Forward", Amount = balance, Notes = $"Cleared via invoice {searchStr}" });
                        
                        dbSettle.Invoices.Update(invOld);
                    }
                    dbSettle.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Warning: Failed to fully process Back Dues settlement.\n" + ex.Message, "Back Dues Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (markPaidOnSave)
            {
                Model.TotalPaid = Model.Total;
                Model.Status = InvoiceStatus.Paid;
                if (Model.Payments == null) Model.Payments = new System.Collections.Generic.List<Payment>();
                // Add a payment equal to balance
                var amt = Model.Total;
                Model.Payments.Add(new Payment { Date = DateTime.Today, Method = "Cash", Amount = amt, Notes = "Marked paid from editor" });
            }

            // Take a snapshot of original items to compute stock delta later
            var originalItemsSnapshot = new System.Collections.Generic.List<InvoiceItem>();
            try
            {
                using var dbSnap = new AppDbContext();
                if (Model.Id > 0)
                {
                    originalItemsSnapshot = dbSnap.InvoiceItems
                        .Where(ii => ii.InvoiceId == Model.Id)
                        .Select(ii => new InvoiceItem { ProductId = ii.ProductId, ProductIdRef = ii.ProductIdRef, Description = ii.Description, Quantity = ii.Quantity })
                        .ToList();
                }
            }
            catch { originalItemsSnapshot = new System.Collections.Generic.List<InvoiceItem>(); }

            // Persist invoice, items and payments
            try
            {
                using var dbSave = new AppDbContext();
                Invoice invEntity;
                if (Model.Id > 0)
                {
                    invEntity = dbSave.Invoices
                        .Include(x => x.Items)
                        .Include(x => x.Payments)
                        .FirstOrDefault(x => x.Id == Model.Id) ?? new Invoice();
                }
                else invEntity = new Invoice();

                invEntity.InvoiceNumber = Model.InvoiceNumber ?? string.Empty;
                invEntity.InvoiceDate = Model.InvoiceDate;
                invEntity.DueDate = Model.DueDate;
                invEntity.CustomerId = Model.CustomerId;
                invEntity.Status = Model.Status;
                invEntity.Subtotal = Model.Subtotal;
                invEntity.TaxAmount = Model.TaxAmount;
                invEntity.DiscountAmount = Model.DiscountAmount; // always absolute stored
                invEntity.Total = Model.Total;
                invEntity.TotalPaid = Model.TotalPaid;
                invEntity.BackDues = Model.BackDues;
                invEntity.UpdatedAt = DateTime.UtcNow;
                // Encode adjustments + terms in PrivateNotes
                var terms = string.IsNullOrWhiteSpace(cmbTerms.Text) ? string.Empty : $"Terms: {cmbTerms.Text};";
                var adj = $"Adj:ExtraName={txtExtraCostName.Text};ExtraAmt={numExtraCost.Value};ExtraPct={(chkExtraPercent.Checked?1:0)};DiscAmt={numDiscount.Value};DiscPct={(chkDiscountPercent.Checked?1:0)};BackAmt={backDuesAmount};BackInc={(Model.IncludedBackDues?1:0)};RoundOff={roundOff2};";
                invEntity.PrivateNotes = terms + adj;

                // Replace items
                invEntity.Items = Model.Items.Select(it => new InvoiceItem
                {
                    ProductId = it.ProductId,
                    ProductIdRef = it.ProductIdRef,
                    ProductName = it.ProductName,
                    BatchNumber = it.BatchNumber,
                    Description = it.Description,
                    Price = it.Price,
                    CostPrice = it.CostPrice ?? 0m, // FIX: Default to 0 for old DB compatibility
                    Quantity = it.Quantity,
                    LineTotal = it.LineTotal
                }).ToList();

                // Replace payments
                invEntity.Payments = (Model.Payments ?? new System.Collections.Generic.List<Payment>()).Select(p => new Payment
                {
                    Date = p.Date,
                    Method = p.Method,
                    Amount = p.Amount,
                    Notes = p.Notes
                }).ToList();

                if (invEntity.Id == 0) dbSave.Invoices.Add(invEntity); else dbSave.Invoices.Update(invEntity);
                dbSave.SaveChanges();
                Model.Id = invEntity.Id;
                Model.PrivateNotes = invEntity.PrivateNotes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save invoice: {ex.Message}\n\nInner error: {ex.InnerException?.Message}", 
                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Adjust stock by delta between original items and new items (per product+batch)
            try
            {
                // Local helper to extract batch code token from description
                static string? ReadBatch(string? desc)
                {
                    if (string.IsNullOrWhiteSpace(desc)) return null;
                    foreach (var seg in desc.Split('|'))
                    {
                        var kv = seg.Split(':');
                        if (kv.Length == 2 && kv[0].Trim().Equals("BATCH", StringComparison.OrdinalIgnoreCase))
                            return kv[1].Trim();
                    }
                    return null;
                }

                var oldSums = new System.Collections.Generic.Dictionary<(int? RefId, string ProdCode, string? Batch), decimal>();
                var newSums = new System.Collections.Generic.Dictionary<(int? RefId, string ProdCode, string? Batch), decimal>();

                using var db = new AppDbContext();
                var oldItems = originalItemsSnapshot ?? new System.Collections.Generic.List<InvoiceItem>();

                foreach (var it in oldItems)
                {
                    var key = (it.ProductIdRef, it.ProductId ?? string.Empty, ReadBatch(it.Description));
                    if (!oldSums.ContainsKey(key)) oldSums[key] = 0m;
                    oldSums[key] += it.Quantity;
                }
                foreach (var it in Model.Items)
                {
                    var key = (it.ProductIdRef, it.ProductId ?? string.Empty, ReadBatch(it.Description));
                    if (!newSums.ContainsKey(key)) newSums[key] = 0m;
                    newSums[key] += it.Quantity;
                }

                // Determine effective contribution based on status (voided invoices should not affect stock)
                var prevStatus = InvoiceStatus.Unpaid;
                try
                {
                    var invPrev = db.Invoices.AsNoTracking().FirstOrDefault(i => i.Id == Model.Id);
                    if (invPrev != null) prevStatus = invPrev.Status;
                }
                catch { }
                var oldEff = (prevStatus == InvoiceStatus.Void) ? 0m : 1m;
                var newEff = (Model.Status == InvoiceStatus.Void) ? 0m : 1m;

                var allKeys = oldSums.Keys.Union(newSums.Keys).ToList();
                foreach (var key in allKeys)
                {
                    var oldQ = (oldSums.ContainsKey(key) ? oldSums[key] : 0m) * oldEff;
                    var newQ = (newSums.ContainsKey(key) ? newSums[key] : 0m) * newEff;
                    var delta = newQ - oldQ; // >0 sold more; <0 reduce sale (return)
                    if (delta == 0m) continue;

                    // Resolve product
                    Product? prod = null;
                    if (key.RefId.HasValue) prod = db.Products.FirstOrDefault(p => p.Id == key.RefId.Value);
                    if (prod == null && !string.IsNullOrWhiteSpace(key.ProdCode)) prod = db.Products.FirstOrDefault(p => p.Barcode == key.ProdCode);
                    if (prod == null) continue;

                    var batchCode = key.Batch;
                    if (!string.IsNullOrWhiteSpace(batchCode))
                    {
                        try
                        {
                            var pb = db.ProductBatches.FirstOrDefault(b => b.ProductIdRef == prod.Id && b.BatchNumber == batchCode);
                            if (pb != null)
                            {
                                // initialize when null, then apply delta
                                if (!pb.Stock.HasValue) pb.Stock = 0m;
                                // delta > 0 => decrease stock; delta < 0 => increase stock
                                pb.Stock = Math.Max(0, pb.Stock.Value - delta);
                                pb.UpdatedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                // Legacy fields fallback by matching batch to old/new
                                if (string.Equals(batchCode, prod.OldBatch, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (prod.OldStock.HasValue)
                                        prod.OldStock = Math.Max(0, prod.OldStock.Value - delta);
                                }
                                else if (string.Equals(batchCode, prod.NewBatch, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (prod.NewStock.HasValue)
                                        prod.NewStock = Math.Max(0, prod.NewStock.Value - delta);
                                }
                            }
                        }
                        catch { }
                    }
                }
                try { db.SaveChanges(); } catch { }
            }
            catch { }
        }

        private void GridItems_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            // Remove previous handlers to avoid duplicates
            if (e.Control is TextBox textBox)
            {
                textBox.KeyPress -= Mrp_KeyPress;
                // Only constrain numeric input on numeric columns
                string colName = string.Empty;
                try { colName = gridItems.Columns[gridItems.CurrentCell.ColumnIndex].Name; } catch { colName = string.Empty; }
                if (colName == "colPrice" || colName == "colQty" || colName == "colMrp" || colName == "colPcs")
                {
                    textBox.KeyPress += Mrp_KeyPress;
                }
                // Prevent Enter from moving to the next row while editing
                textBox.KeyDown -= GridTextBox_KeyDown;
                textBox.KeyDown += GridTextBox_KeyDown;
            }
            // No automatic picker on begin edit; user can press Ctrl+E/Ctrl+Space to open picker
        }

        // Helper method to update payments grid
        private void UpdatePaymentsGrid()
        {
            if (Model == null) return;
            if (Model.Payments == null) Model.Payments = new System.Collections.Generic.List<Payment>();
            paymentsGrid.DataSource = null;
            paymentsGrid.DataSource = new BindingSource(Model.Payments, null);
        }

        private void AddPaymentQuick()
        {
            try
            {
                using var pd = new PaymentDialog();
                if (pd.ShowDialog(this) == DialogResult.OK)
                {
                    if (Model.Payments == null) Model.Payments = new System.Collections.Generic.List<Payment>();
                    Model.Payments.Add(new Payment { Date = DateTime.Today, Method = pd.Method, Amount = pd.Amount, Notes = pd.Notes });
                    Model.TotalPaid = Model.Payments.Sum(p => p.Amount);
                    UpdatePaymentsGrid();
                    RecalcRequested?.Invoke(this, EventArgs.Empty);
                    Recalc();
                }
            }
            catch { }
        }

        private void EditSelectedPayment()
        {
            try
            {
                if (Model?.Payments == null) return;
                if (paymentsGrid.CurrentRow == null) return;
                var idx = paymentsGrid.CurrentRow.Index;
                if (idx < 0 || idx >= Model.Payments.Count) return;
                var pay = Model.Payments[idx];
                using var pd = new PaymentDialog();
                // preload values via reflection of dialog controls
                // As dialog exposes properties only on OK, we can set by simulating: create new dialog and set defaults
                // Instead, create a quick inline form for editing
                var f = new Form { Text = "Edit Payment", StartPosition = FormStartPosition.CenterParent, Size = new Size(320, 180) };
                var lblAmt = new Label { Text = "Amount", Location = new Point(12, 18), AutoSize = true };
                var numAmt = new NumericUpDown { DecimalPlaces = 2, Maximum = 10000000, Location = new Point(80, 16), Width = 120, Value = pay.Amount };
                var lblMeth = new Label { Text = "Method", Location = new Point(12, 50), AutoSize = true };
                var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(80, 46), Width = 120 };
                cmb.Items.AddRange(new object[] { "Cash", "UPI", "Card", "Bank Transfer", "Other" });
                cmb.SelectedItem = string.IsNullOrWhiteSpace(pay.Method) ? "Cash" : pay.Method;
                var lblNotes = new Label { Text = "Notes", Location = new Point(12, 80), AutoSize = true };
                var txt = new TextBox { Location = new Point(80, 76), Width = 200, Text = pay.Notes ?? string.Empty };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(70, 110) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(150, 110) };
                f.Controls.AddRange(new Control[] { lblAmt, numAmt, lblMeth, cmb, lblNotes, txt, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    pay.Amount = numAmt.Value;
                    pay.Method = cmb.SelectedItem?.ToString() ?? pay.Method;
                    pay.Notes = txt.Text;
                    // recompute totals
                    Model.TotalPaid = Model.Payments.Sum(p => p.Amount);
                    UpdatePaymentsGrid();
                    RecalcRequested?.Invoke(this, EventArgs.Empty);
                    Recalc();
                }
            }
            catch { }
        }

        private void DeleteSelectedPayment()
        {
            try
            {
                if (Model?.Payments == null) return;
                if (paymentsGrid.CurrentRow == null) return;
                var idx = paymentsGrid.CurrentRow.Index;
                if (idx < 0 || idx >= Model.Payments.Count) return;
                if (MessageBox.Show("Delete selected payment?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Model.Payments.RemoveAt(idx);
                    Model.TotalPaid = Model.Payments.Sum(p => p.Amount);
                    UpdatePaymentsGrid();
                    RecalcRequested?.Invoke(this, EventArgs.Empty);
                    Recalc();
                }
            }
            catch { }
        }

        // Prevent Enter from changing grid selection while typing; finalize edit and stay
        private void GridTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                try { gridItems.EndEdit(); } catch { }
                // Keep focus on current cell
                if (gridItems.CurrentCell != null)
                {
                    var r = gridItems.CurrentCell.RowIndex;
                    var c = gridItems.CurrentCell.ColumnIndex;
                    gridItems.CurrentCell = gridItems[c, r];
                    gridItems.BeginEdit(false);
                }
            }
            // Open picker using Ctrl+E or Ctrl+Space to avoid system Fn conflicts
            else if (e.Control && (e.KeyCode == Keys.E || e.KeyCode == Keys.Space))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (gridItems.CurrentCell != null)
                {
                    OpenPickerForRow(gridItems.CurrentCell.RowIndex);
                }
            }
        }

        private void GridItems_KeyDown(object? sender, KeyEventArgs e)
        {
            if (gridItems.CurrentCell == null) return;
            var col = gridItems.CurrentCell.ColumnIndex;
            if (e.KeyCode == Keys.Enter)
            {
                // If on product name column, open product editor directly
                try
                {
                    var nameCol = gridItems.Columns["colName"]?.Index ?? -1;
                    if (nameCol >= 0 && col == nameCol)
                    {
                        e.Handled = true; e.SuppressKeyPress = true;
                        EditCurrentProductAtRow(gridItems.CurrentCell.RowIndex);
                        return;
                    }
                }
                catch { }
                // Default: commit edit and stay
                e.Handled = true; e.SuppressKeyPress = true; try { gridItems.EndEdit(); } catch { }
                return;
            }
            // Ctrl+E or Ctrl+Space opens picker at grid level as well
            if (e.Control && (e.KeyCode == Keys.E || e.KeyCode == Keys.Space))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                OpenPickerForRow(gridItems.CurrentCell.RowIndex);
            }
        }

        private async void OpenPickerForRow(int rowIndex)
        {
            if (rowIndex < 0) return;
            if (cmbCustomer.SelectedValue is not int customerId) { MessageBox.Show("Please select a customer first."); return; }
            var picker = new ProductPicker(customerId);
            var dlgResult = picker.ShowDialog();
            if (dlgResult == DialogResult.OK && picker.SelectedProduct != null)
            {
                var p = picker.SelectedProduct;
                var bs = (BindingSource)gridItems.DataSource;
                // Ensure row exists
                if (rowIndex >= bs.Count)
                {
                    var list = (System.Collections.Generic.List<InvoiceItem>)bs.List;
                    list.Add(new InvoiceItem { Quantity = 1, Price = 0 });
                    bs.ResetBindings(false);
                }
                var it = (InvoiceItem)bs[rowIndex];
                using (var batchDlg = new BatchSelectForm(p, Model?.CustomerId))
                {
                    if (batchDlg.ShowDialog(this) != DialogResult.OK)
                    {
                        // User cancelled: remove the placeholder row entirely
                        TryRemoveRowAt(rowIndex);
                        try { gridItems.Focus(); } catch { }
                        return;
                    }
                    // Assign selected product only after batch selection is confirmed
                    it.ProductId = p.Barcode;
                    it.ProductIdRef = p.Id;
                    gridItems.Rows[rowIndex].Cells["colName"].Value = p.Name;
                    gridItems.Rows[rowIndex].Cells["colMrp"].Value = batchDlg.SelectedMrp.HasValue ? batchDlg.SelectedMrp.Value : null;
                    it.Price = batchDlg.SelectedSellingPrice ?? 0m;
                    var mrpVal = batchDlg.SelectedMrp.HasValue ? batchDlg.SelectedMrp.Value.ToString("0.00") : "";
                    var batchCode = batchDlg.SelectedBatchCode ?? "";
                    var src = (batchDlg.SelectedBatchCode == p.NewBatch) ? "New" : "Old";
                    var exp = batchDlg.SelectedExpiry.HasValue ? batchDlg.SelectedExpiry.Value.ToString("dd/MM/yyyy") : "";
                    // Pull CP, PACK, MKT, STOCK, SCHEME from ProductBatch when available
                    string? cpTxt = null, packTxt = null, mktTxt = null, stockTxt = null, schemeTxt = null;
                    try
                    {
                        using var dbb = new AppDbContext();
                        var pb = dbb.ProductBatches.FirstOrDefault(b => b.ProductIdRef == p.Id && b.BatchNumber == batchCode);
                        if (pb != null)
                        {
                            if (pb.CostPrice.HasValue) cpTxt = pb.CostPrice.Value.ToString("0.00");
                            packTxt = pb.Pack;
                            mktTxt = pb.MarketedBy;
                            if (pb.Stock.HasValue) stockTxt = pb.Stock.Value.ToString("0.##");
                            schemeTxt = pb.Bonus;
                        }
                    }
                    catch { }
                    // write tokens
                    it.Description = $"NAME:{p.Name}|MRP:{mrpVal}|BATCH:{batchCode}|EXP:{exp}"
                                       + (string.IsNullOrWhiteSpace(cpTxt) ? string.Empty : $"|CP:{cpTxt}")
                                       + (string.IsNullOrWhiteSpace(schemeTxt) ? string.Empty : $"|SCHEME:{schemeTxt}")
                                       + (string.IsNullOrWhiteSpace(packTxt) ? string.Empty : $"|PACK:{packTxt}")
                                       + (string.IsNullOrWhiteSpace(mktTxt) ? string.Empty : $"|MKT:{mktTxt}")
                                       + (string.IsNullOrWhiteSpace(stockTxt) ? string.Empty : $"|STOCK:{stockTxt}")
                                       + $"|SRC:{src}";
                    // reflect to visible columns
                    try { gridItems.Rows[rowIndex].Cells["colScheme"].Value = schemeTxt ?? string.Empty; } catch { }
                    try { gridItems.Rows[rowIndex].Cells["colPack"].Value = packTxt ?? string.Empty; } catch { }
                    try { gridItems.Rows[rowIndex].Cells["colMkt"].Value = mktTxt ?? string.Empty; } catch { }
                }
                bs.ResetItem(rowIndex);
                Recalc();
                // Focus grid and place caret at quantity for quick edit
                try
                {
                    gridItems.Focus();
                    var qtyCol = gridItems.Columns["colQty"]?.Index ?? -1;
                    if (qtyCol >= 0)
                    {
                        gridItems.CurrentCell = gridItems[qtyCol, rowIndex];
                        gridItems.BeginEdit(true);
                    }
                }
                catch { }
            }
            else
            {
                // Picker was canceled; remove placeholder empty row if any
                TryRemoveEmptyRow(rowIndex);
            }
        }

        private void TryRemoveEmptyRow(int rowIndex)
        {
            if (gridItems.DataSource is not BindingSource bs) return;
            if (rowIndex < 0 || rowIndex >= bs.Count) return;
            var it = (InvoiceItem)bs[rowIndex];
            bool looksEmpty = string.IsNullOrWhiteSpace(it.ProductId) && !it.ProductIdRef.HasValue && string.IsNullOrWhiteSpace(it.Description) && it.Price == 0m && it.Quantity == 1m;
            if (looksEmpty)
            {
                var list = (System.Collections.Generic.List<InvoiceItem>)bs.List;
                list.RemoveAt(rowIndex);
                bs.ResetBindings(false);
                Recalc();
            }
        }

        // Ensure shortcuts work even when a control has focus, and arrow keys immediately drive the grid
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Arrow keys: move focus to items grid so navigation works without clicking
            if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right)
            {
                if (!gridItems.Focused && !gridItems.ContainsFocus)
                {
                    try
                    {
                        gridItems.Focus();
                        if (gridItems.CurrentCell == null && gridItems.Rows.Count > 0)
                            gridItems.CurrentCell = gridItems[0, 0];
                    }
                    catch { }
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            Keys code = (keyData & Keys.KeyCode);

            // Add line: Ctrl+N or Insert
            if ((ctrl && code == Keys.N) || code == Keys.Insert)
            { tsAddLine.PerformClick(); return true; }

            // Delete line: Ctrl+D or Delete
            if ((ctrl && code == Keys.D) || code == Keys.Delete)
            { tsDeleteLine.PerformClick(); return true; }

            // Preview: Ctrl+R
            if (ctrl && code == Keys.R)
            { tsPreview.PerformClick(); return true; }

            // Print: Ctrl+P
            if (ctrl && code == Keys.P)
            { tsPrint.PerformClick(); return true; }

            // Open product picker: Ctrl+E / Ctrl+Space (avoid F2 due to system conflicts)
            if (ctrl && (code == Keys.E || code == Keys.Space))
            {
                int targetRow = -1;
                if (gridItems.CurrentCell == null)
                {
                    // Ensure there is at least one row to edit
                    try
                    {
                        targetRow = AddEmptyRow();
                    }
                    catch { targetRow = -1; }
                }
                else
                {
                    targetRow = gridItems.CurrentCell.RowIndex;
                }
                if (targetRow >= 0)
                {
                    OpenPickerForRow(targetRow);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private decimal? TryParseMrp(string? desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            // Accept plain decimal or formatted "MRP:<val>|..."
            if (decimal.TryParse(desc, out var d)) return d;
            var parts = desc.Split('|');
            foreach (var part in parts)
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
            var parts = desc.Split('|');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && kv[0].Trim().Equals("EXP", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(kv[1], out var dt)) return dt;
                }
            }
            return null;
        }

        private void UpdateMrpInDescription(InvoiceItem it, string? newMrpText)
        {
            if (it == null) return;
            
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(it.Description))
            {
                parts.AddRange(it.Description.Split('|')
                    .Where(p => !p.Trim().StartsWith("MRP:", StringComparison.OrdinalIgnoreCase)));
            }
            if (!string.IsNullOrWhiteSpace(newMrpText) && decimal.TryParse(newMrpText, out var mrpDec))
                parts.Insert(0, $"MRP:{mrpDec.ToString("0.00")}");
            it.Description = string.Join("|", parts);
        }

        private void GridItems_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            
            var grid = sender as DataGridView;
            if (grid?.Rows[e.RowIndex]?.DataBoundItem is not InvoiceItem item) return;

            try
            {
                // Validate and update price/quantity
                if (grid.Columns[e.ColumnIndex].Name == "colPrice")
                {
                    if (decimal.TryParse(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString(), 
                        out decimal price) && price >= 0)
                    {
                        item.Price = price;
                    }
                    else
                    {
                        grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = item.Price;
                    }
                }
                else if (grid.Columns[e.ColumnIndex].Name == "colQty")
                {
                    if (decimal.TryParse(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString(), 
                        out decimal qty) && qty > 0)
                    {
                        item.Quantity = qty;
                    }
                    else
                    {
                        grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = item.Quantity;
                    }
                }
                
                // Recalculate totals
                RecalcRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating value: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                grid.CancelEdit();
            }
            if (e.RowIndex < 0) return;
            if (gridItems.DataSource is BindingSource bs)
            {
                var it = (InvoiceItem)bs[e.RowIndex];
                var colNameNow = gridItems.Columns[e.ColumnIndex].Name;
                if (colNameNow == "colMrp")
                {
                    var val = gridItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                    UpdateMrpInDescription(it, val);
                }
                else if (colNameNow == "colName")
                {
                    var nameVal = gridItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                    UpdateNameInDescription(it, nameVal);
                }
                else if (colNameNow == "colScheme")
                {
                    UpdateTokenInDescription(it, "SCHEME", gridItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
                }
                else if (colNameNow == "colPack")
                {
                    UpdateTokenInDescription(it, "PACK", gridItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
                }
                else if (colNameNow == "colMkt")
                {
                    UpdateTokenInDescription(it, "MKT", gridItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
                }
                else if (colNameNow == "colBatch")
                {
                    UpdateTokenInDescription(it, "BATCH", gridItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
                }
                else if (colNameNow == "colExpiry")
                {
                    UpdateTokenInDescription(it, "EXP", gridItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
                }
            }
            Recalc();
        }

        private void UpdateNameInDescription(InvoiceItem it, string? newName)
        {
            var parts = (it.Description ?? string.Empty).Split('|').Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
            int nameIndex = parts.FindIndex(p => p.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase));
            string nameVal = string.IsNullOrWhiteSpace(newName) ? string.Empty : newName.Trim();
            if (nameIndex >= 0)
            {
                if (string.IsNullOrEmpty(nameVal)) parts.RemoveAt(nameIndex);
                else parts[nameIndex] = $"NAME:{nameVal}";
            }
            else if (!string.IsNullOrEmpty(nameVal))
            {
                parts.Add($"NAME:{nameVal}");
            }
            it.Description = string.Join("|", parts);
        }

        private void UpdateTokenInDescription(InvoiceItem it, string key, string? value)
        {
            var parts = (it.Description ?? string.Empty).Split('|').Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
            int idx = parts.FindIndex(p => p.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase));
            string val = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (idx >= 0)
            {
                if (string.IsNullOrEmpty(val)) parts.RemoveAt(idx);
                else parts[idx] = $"{key}:{val}";
            }
            else if (!string.IsNullOrEmpty(val))
            {
                parts.Add($"{key}:{val}");
            }
            it.Description = string.Join("|", parts);
        }

        private void PreviewPdf(bool print = false)
        {
            // build a lightweight document from current Model state
            var inv = this.Model;
            if (inv == null)
            {
                inv = new Invoice { Items = new System.Collections.Generic.List<InvoiceItem>() };
            }
            if (gridItems.DataSource is BindingSource bs)
            {
                inv.Items = bs.List.Cast<InvoiceItem>().Select(x => new InvoiceItem
                {
                    ProductId = x.ProductId,
                    Description = x.Description,
                    Price = x.Price,
                    Quantity = x.Quantity,
                    LineTotal = x.Price * x.Quantity
                }).ToList();
            }
            inv.Subtotal = inv.Items.Sum(i => i.LineTotal);
            // Carry editor adjustment flags/values
            inv.ExtraCostAmount = Model.ExtraCostAmount;
            inv.ExtraCostIsPercent = Model.ExtraCostIsPercent;
            inv.DiscountAmount = Model.DiscountAmount;
            inv.DiscountIsPercent = Model.DiscountIsPercent;
            // Compute consistent totals
            var extraAbsP = inv.ExtraCostAmount > 0 ? (inv.ExtraCostIsPercent ? Math.Round(inv.Subtotal * (inv.ExtraCostAmount / 100m), 2) : inv.ExtraCostAmount) : 0m;
            var discountAbsP = inv.DiscountAmount > 0 ? (inv.DiscountIsPercent ? Math.Round(inv.Subtotal * (inv.DiscountAmount / 100m), 2) : inv.DiscountAmount) : 0m;
            var taxableBaseP = Math.Max(0m, inv.Subtotal + extraAbsP - discountAbsP);
            inv.TaxAmount = Math.Round(taxableBaseP * (numTaxRate.Value / 100m), 2);
            var totalP = taxableBaseP + inv.TaxAmount + backDuesAmount;
            inv.Total = totalP;
            inv.TotalPaid = Model.TotalPaid;

            // Get customer details
            if (cmbCustomer.SelectedValue is int selId)
            {
                using var db = new AppDbContext();
                inv.Customer = db.Customers.FirstOrDefault(c => c.Id == selId);
            }

            // Encode current adjustments (including Back Dues) into PrivateNotes so preview reflects them without saving
            try
            {
                var adj = $"Adj:ExtraName={txtExtraCostName.Text};ExtraAmt={numExtraCost.Value};ExtraPct={(chkExtraPercent.Checked?1:0)};DiscAmt={numDiscount.Value};DiscPct={(chkDiscountPercent.Checked?1:0)};BackAmt={backDuesAmount};BackInc={(chkBackDuesAdj.Checked?1:0)};";
                inv.PrivateNotes = adj;
            }
            catch { }
            // Generate HTML content for preview
            string htmlContent = GenerateInvoiceHtml(inv);

            // Show the preview form
            using (var previewForm = new InvoicePreviewForm(inv, htmlContent))
            {
                previewForm.ShowDialog(this);
            }
        }

        // Simple dialogs
        private sealed class ValueModeDialog : Form
        {
            public bool IsPercent { get; private set; }
            public decimal Value { get; private set; }
            public ValueModeDialog(string title, bool isPercent, decimal value)
            {
                Text = title; Size = new System.Drawing.Size(280, 150); StartPosition = FormStartPosition.CenterParent;
                var rbPercent = new RadioButton { Text = "%", Checked = isPercent, Location = new Point(15, 15) };
                var rbAmount = new RadioButton { Text = "Amount", Checked = !isPercent, Location = new Point(70, 15) };
                var num = new NumericUpDown { DecimalPlaces = 2, Maximum = 1000000, Location = new Point(15, 45), Width = 120, Value = (decimal)value };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(60, 80) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(140, 80) };
                Controls.AddRange(new Control[] { rbPercent, rbAmount, num, ok, cancel });
                AcceptButton = ok; CancelButton = cancel;
                ok.Click += (s, e) => { IsPercent = rbPercent.Checked; Value = num.Value; };
            }
        }

        private sealed class PaymentDialog : Form
        {
            public decimal Amount { get; private set; }
            public string Method { get; private set; } = "Cash";
            public string? Notes { get; private set; }
            public PaymentDialog()
            {
                Text = "Add Payment"; Size = new System.Drawing.Size(320, 180); StartPosition = FormStartPosition.CenterParent;
                var lblAmt = new Label { Text = "Amount", Location = new Point(12, 18), AutoSize = true };
                var numAmt = new NumericUpDown { DecimalPlaces = 2, Maximum = 10000000, Location = new Point(80, 16), Width = 120 };
                var lblMeth = new Label { Text = "Method", Location = new Point(12, 50), AutoSize = true };
                var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(80, 46), Width = 120 };
                cmb.Items.AddRange(new object[] { "Cash", "UPI", "Card", "Bank Transfer", "Other" }); cmb.SelectedIndex = 0;
                var lblNotes = new Label { Text = "Notes", Location = new Point(12, 80), AutoSize = true };
                var txt = new TextBox { Location = new Point(80, 76), Width = 200 };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(70, 110) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(150, 110) };
                Controls.AddRange(new Control[] { lblAmt, numAmt, lblMeth, cmb, lblNotes, txt, ok, cancel });
                AcceptButton = ok; CancelButton = cancel;
                ok.Click += (s, e) => { Amount = numAmt.Value; Method = cmb.SelectedItem?.ToString() ?? "Cash"; Notes = txt.Text; };
            }
        }

        private string GenerateInvoiceHtml(Invoice inv)
        {
            // Delegate to the unified preview builder so editor preview matches preview window format and totals
            return InvoicePreviewForm.BuildHtmlFor(inv);
        }

        // Removed: GenerateInvoicePdf/BuildClassic/BuildCompact/BuildWide/BuildItemsTable
        // and the "Classic/Compact/Wide" template builders.
        // They were unreachable — GenerateInvoicePdf had no caller anywhere in the solution
        // (verified by full-text search) — and they hardcoded "Your Company"/"Your Address"
        // plus a literal "₹". All invoice PDF output now goes through
        // InvoicePreviewForm.GeneratePdfFor, which reads company identity, currency and page
        // setup from Settings. Removed so the dead copy cannot drift or be revived by mistake.

        // Open default mail client compose (simple mailto)
        private void ComposeEmail()
        {
            try
            {
                var subject = Uri.EscapeDataString($"Invoice {txtInvoiceNo.Text}");
                var body = Uri.EscapeDataString("Please find the invoice attached.");
                Process.Start(new ProcessStartInfo($"mailto:?subject={subject}&body={body}") { UseShellExecute = true });
            }
            catch { }
        }
    }
}