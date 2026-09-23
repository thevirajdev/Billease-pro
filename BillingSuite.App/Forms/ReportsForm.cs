using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using BillingSuite.App.Data;
using Microsoft.EntityFrameworkCore;
using BillingSuite.App.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Forms
{
    public class ReportsForm : Form
    {
        private TabControl tcMain = new TabControl();
        private WebView2 wvDashboard = new WebView2();
        
        // Inventory Grids
        private DataGridView gridExpired = new DataGridView();
        private DataGridView gridDamaged = new DataGridView();
        private DataGridView gridDeadStock = new DataGridView();
        private FlowLayoutPanel _flpFinancials;
        private FlowLayoutPanel _flpInsights;

        // Product Analysis
        private DataGridView gridBestSellers = new DataGridView();
        private DataGridView gridWorstSellers = new DataGridView();
        private DataGridView gridPriceComparison = new DataGridView();
        private int? _selectedComparisonProductId;

        // Transactions
        private DataGridView gridSalesLogs = new DataGridView();
        private DataGridView gridPurchaseLogs = new DataGridView();
        private DateTimePicker dtpFrom = new DateTimePicker();
        private DateTimePicker dtpTo = new DateTimePicker();

        public ReportsForm()
        {
            Text = "Professional Analytics & Reports";
            Size = new Size(1100, 850);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            
            BuildUI();
            InitializeDashboard();
        }

        private void BuildUI()
        {
            tcMain.Dock = DockStyle.Fill;
            tcMain.Font = new Font("Segoe UI", 10F);
            tcMain.Padding = new Point(12, 6);

            // Tab 1: Dashboard
            TabPage t1 = new TabPage("📊 Dashboard Overview");
            t1.Controls.Add(wvDashboard);
            wvDashboard.Dock = DockStyle.Fill;
            tcMain.TabPages.Add(t1);

            // Tab 2: Inventory Risk
            TabPage t2 = new TabPage("📦 Inventory & Risk");
            t2.Controls.Add(BuildInventoryPanel());
            tcMain.TabPages.Add(t2);

            // Tab 3: Product Analysis
            TabPage t3 = new TabPage("🏷️ Product Insights");
            t3.Controls.Add(BuildProductAnalysisPanel());
            tcMain.TabPages.Add(t3);

            // Tab 4: Financials & Parties
            TabPage t4 = new TabPage("💰 Financial Summary");
            t4.Controls.Add(BuildFinancialsPanel());
            tcMain.TabPages.Add(t4);

            // Tab 5: Transaction Logs
            TabPage t5 = new TabPage("🧾 Detailed Logs");
            t5.Controls.Add(BuildTransactionsPanel());
            tcMain.TabPages.Add(t5);

            this.Controls.Add(tcMain);
        }

        private async void InitializeDashboard()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "BillingSuite_WebView2"));
                await wvDashboard.EnsureCoreWebView2Async(env);
                
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReportsWeb", "dashboard.html");
                if (File.Exists(htmlPath))
                {
                    wvDashboard.Source = new Uri(htmlPath);
                }
                else
                {
                    wvDashboard.NavigateToString("<h1>Dashboard asset not found.</h1><p>Check build output for ReportsWeb/dashboard.html</p>");
                }

                wvDashboard.NavigationCompleted += (s, e) => RefreshDashboardData("7d");
                wvDashboard.WebMessageReceived += (s, e) => {
                    try {
                        var json = e.WebMessageAsJson;
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "filter-chart") {
                            if (root.TryGetProperty("period", out var periodProp)) {
                                RefreshDashboardData(periodProp.GetString());
                            }
                        }
                    } catch { }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 Initialization Failed: " + ex.Message + "\nEnsure WebView2 Runtime is installed.");
            }
        }

        private Control BuildInventoryPanel()
        {
            TabControl tc = new TabControl { Dock = DockStyle.Fill };
            tc.TabPages.Add(new TabPage("❌ Expired Products") { Padding = new Padding(10) });
            tc.TabPages[0].Controls.Add(SetupGrid(gridExpired));
            
            tc.TabPages.Add(new TabPage("⚠️ Damaged Products") { Padding = new Padding(10) });
            tc.TabPages[1].Controls.Add(SetupGrid(gridDamaged));

            tc.TabPages.Add(new TabPage($"📉 Dead Stock ({Services.AppSettingsService.DefaultExpiryAlertDays}+ Days)") { Padding = new Padding(10) });
            tc.TabPages[2].Controls.Add(SetupGrid(gridDeadStock));

            return tc;
        }

        private Control BuildProductAnalysisPanel()
        {
            TableLayoutPanel tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));

            GroupBox gbPerf = new GroupBox { Text = "Product Performance Insights", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10F) };
            TableLayoutPanel tlpPerf = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            tlpPerf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpPerf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            
            GroupBox gbBest = new GroupBox { Text = "🏆 Best Selling (Top 10)", Dock = DockStyle.Fill };
            gbBest.Controls.Add(SetupGrid(gridBestSellers));
            
            GroupBox gbWorst = new GroupBox { Text = "📉 Worst Selling / Low Movers", Dock = DockStyle.Fill };
            gbWorst.Controls.Add(SetupGrid(gridWorstSellers));
            
            tlpPerf.Controls.Add(gbBest, 0, 0);
            tlpPerf.Controls.Add(gbWorst, 1, 0);
            
            gbPerf.Controls.Add(tlpPerf);
            tlp.Controls.Add(gbPerf, 0, 0);

            GroupBox gbComp = new GroupBox { Text = "🔍 Supplier Price Comparison", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10F) };
            TableLayoutPanel tlpComp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tlpComp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            tlpComp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            FlowLayoutPanel filter = new FlowLayoutPanel { Dock = DockStyle.Fill };
            filter.Controls.Add(new Label { Text = "Compare Prices for:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 5, 0) });
            
            Button btnSelectProduct = new Button { Text = "🔍 Select Product...", Width = 150, Height = 30 };
            btnSelectProduct.Click += (s, e) => {
                using var dlg = new ProductPickerForm();
                if (dlg.ShowDialog() == DialogResult.OK && dlg.SelectedProduct != null) {
                    _selectedComparisonProductId = dlg.SelectedProduct.Id;
                    btnSelectProduct.Text = dlg.SelectedProduct.Name;
                    LoadPriceComparison();
                }
            };
            filter.Controls.Add(btnSelectProduct);
            
            tlpComp.Controls.Add(filter, 0, 0);
            tlpComp.Controls.Add(SetupGrid(gridPriceComparison), 0, 1);
            gridPriceComparison.CellFormatting += GridPriceComparison_CellFormatting;
            gbComp.Controls.Add(tlpComp);
            tlp.Controls.Add(gbComp, 0, 1);

            return tlp;
        }

        private Control BuildFinancialsPanel()
        {
            TableLayoutPanel tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(20) };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            tlp.Controls.Add(CreateStatGroup("Cash Flow (Online vs Cash)", "flowCash", out _flpFinancials), 0, 0);
            tlp.Controls.Add(CreateStatGroup("Outstanding Liabilities", "flowDue", out _), 1, 0);
            tlp.Controls.Add(CreateStatGroup("Tax Summary (Estimated)", "flowTax", out _), 0, 1);
            
            var pnlRepair = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var btnRepair = new Button { 
                Text = "Click to Repair & Sync All Ledger Data", 
                Dock = DockStyle.Top, 
                Height = 60, 
                BackColor = Color.LightSteelBlue,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnRepair.Click += (s, e) => RepairData();
            pnlRepair.Controls.Add(btnRepair);
            var lblHint = new Label { 
                Text = "Use this if Total Profit or Customer Dues seem incorrect due to historical bugs.", 
                Dock = DockStyle.Top, 
                Height = 40, 
                TextAlign = ContentAlignment.MiddleCenter 
            };
            pnlRepair.Controls.Add(lblHint);

            tlp.Controls.Add(CreateStatGroup("Expense/Loss Breakdown", "flowLoss", out _), 1, 1);
            var lastGb = tlp.GetControlFromPosition(1, 1) as GroupBox;
            if (lastGb != null) {
                lastGb.Height = 150;
                lastGb.Parent.Controls.Add(pnlRepair); // Add repair button below or in between
            }

            return tlp;
        }

        private GroupBox CreateStatGroup(string title, string flowName, out FlowLayoutPanel flow)
        {
            var gb = new GroupBox { Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10F), Padding = new Padding(10) };
            flow = new FlowLayoutPanel { Name = flowName, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true };
            gb.Controls.Add(flow);
            return gb;
        }

        private void AddStatLine(FlowLayoutPanel flow, string label, string value, Color? valColor = null)
        {
            var p = new Panel { Width = flow.Width - 25, Height = 25 };
            var lblName = new Label { Text = label, AutoSize = true, Location = new Point(0, 5) };
            var lblVal = new Label { Text = value, AutoSize = true, Location = new Point(140, 5), Font = new Font(Font, FontStyle.Bold), ForeColor = valColor ?? Color.Black };
            p.Controls.AddRange(new Control[] { lblName, lblVal });
            flow.Controls.Add(p);
        }

        private Control BuildTransactionsPanel()
        {
            TableLayoutPanel tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            toolbar.Controls.Add(new Label { Text = "From:", AutoSize = true });
            toolbar.Controls.Add(dtpFrom);
            toolbar.Controls.Add(new Label { Text = "To:", AutoSize = true });
            toolbar.Controls.Add(dtpTo);
            Button btn = new Button { Text = "Refresh Logs", Width = 120 };
            btn.Click += (s, e) => LoadTransactionLogs();
            toolbar.Controls.Add(btn);
            
            Button btnExport = new Button { Text = "Export to Excel", Width = 120, BackColor = Color.FromArgb(33, 115, 70), ForeColor = Color.White };
            btnExport.Click += (s, e) => ExportLogsToExcel();
            toolbar.Controls.Add(btnExport);

            tlp.Controls.Add(toolbar, 0, 0);
            
            TabControl tc = new TabControl { Dock = DockStyle.Fill };
            tc.TabPages.Add(new TabPage("Sales Invoices"));
            tc.TabPages[0].Controls.Add(SetupGrid(gridSalesLogs));
            tc.TabPages.Add(new TabPage("Purchase Invoices"));
            tc.TabPages[1].Controls.Add(SetupGrid(gridPurchaseLogs));
            
            tlp.Controls.Add(tc, 0, 1);
            return tlp;
        }

        private DataGridView SetupGrid(DataGridView g)
        {
            g.Dock = DockStyle.Fill;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.RowHeadersVisible = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            return g;
        }

        // --- Data Logic (Phase 4) ---
        private void RefreshDashboardData(string period = "7d")
        {
            try
            {
                using var db = new AppDbContext();
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var startOfYear = new DateTime(today.Year, 1, 1);
                decimal expiredLoss = 0;
                decimal damageLoss = 0;
                decimal lifetime_sales = 0;
                decimal lifetime_purchase = 0;
                decimal lifetime_margin = 0;

                // 1. STATS HELPER (Optimized for Large Data)
                (decimal sales, decimal purchase, decimal profit) GetStats(DateTime start, DateTime end)
                {
                    var s = db.Invoices.Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end).Sum(i => (decimal?)i.Total - (decimal?)i.BackDues) ?? 0m;
                    var p = db.Purchases.Where(i => i.PurchaseDate >= start && i.PurchaseDate <= end).Sum(i => (decimal?)i.Total) ?? 0m;
                    
                    var profit = db.InvoiceItems
                        .Where(ii => ii.Invoice.InvoiceDate >= start && ii.Invoice.InvoiceDate <= end)
                        .Sum(ii => (decimal?)ii.LineTotal - (ii.Quantity * (ii.CostPrice ?? 0m))) ?? 0m;
                    
                    return (s, p, profit);
                }

                var d = GetStats(today, today.AddDays(1).AddSeconds(-1));
                var w = GetStats(startOfWeek, today.AddDays(1).AddSeconds(-1));
                var m = GetStats(startOfMonth, today.AddDays(1).AddSeconds(-1));
                var y = GetStats(startOfYear, today.AddDays(1).AddSeconds(-1));

                // 2. LOSS CALCULATIONS (Direct SQL)
                expiredLoss = db.ProductBatches
                    .Where(b => b.Expiry < today && ((b.ExpiredStock ?? 0) > 0 || (b.Stock ?? 0) > 0))
                    .Sum(b => ((b.ExpiredStock ?? 0) > 0 ? (b.ExpiredStock ?? 0) : (b.Stock ?? 0)) * (b.CostPrice ?? 0));
                
                damageLoss = db.DamagedItems.Sum(d => (decimal?)d.LossAmount) ?? 0m;

                lifetime_sales = db.Invoices.Sum(i => (decimal?)i.Total - (decimal?)i.BackDues) ?? 0m;
                lifetime_purchase = db.Purchases.Sum(i => (decimal?)i.Total) ?? 0m;

                // 3. LIFETIME MARGIN (Direct SQL - NO TO-LIST()!)
                // This was the primary cause of the hang on large databases
                lifetime_margin = db.InvoiceItems.Sum(it => it.LineTotal - (it.Quantity * (it.CostPrice ?? 0m)));

                var financials = new
                {
                    pay_received = db.Payments.Sum(p => (decimal?)p.Amount) ?? 0m,
                    cust_due = db.Invoices.Sum(i => (decimal?)i.Total - i.TotalPaid) ?? 0m,
                    pay_sent = db.PurchasePayments.Sum(p => (decimal?)p.Amount) ?? 0m,
                    sup_due = db.Purchases.Sum(p => (decimal?)p.Total - (p.Paid ?? 0m)) ?? 0m,
                    manual_expenses = db.Expenses.Sum(e => (decimal?)e.Amount) ?? 0m,
                    total_profit = (lifetime_margin - expiredLoss - damageLoss) - (db.Expenses.Sum(e => (decimal?)e.Amount) ?? 0m),
                    lt_sale = lifetime_sales,
                    lt_purchase = lifetime_purchase
                };

                var financials_dash = new Dictionary<string, decimal> {
                    { "pay-received", financials.pay_received },
                    { "cust-due", financials.cust_due },
                    { "pay-sent", financials.pay_sent },
                    { "sup-due", financials.sup_due },
                    { "total-profit", financials.total_profit },
                    { "lt-sale", financials.lt_sale },
                    { "lt-purchase", financials.lt_purchase }
                };

                // Chart Data (Filtered by period)
                var chartLabels = new List<string>();
                var chartSales = new List<decimal>();
                var chartPurchases = new List<decimal>();
                
                int days = period == "30d" ? 30 : (period == "year" ? 365 : 7);
                int step = period == "year" ? 30 : 1; // Monthly steps for year

                for (int i = (days/step)*step - step; i >= 0; i -= step)
                {
                    var dt = today.AddDays(-i);
                    chartLabels.Add(period == "year" ? dt.ToString("MMM") : dt.ToString("dd/MM"));
                    
                    var endRange = dt.AddDays(step).AddSeconds(-1);
                    chartSales.Add(db.Invoices.Where(inv => inv.InvoiceDate >= dt && inv.InvoiceDate <= endRange).Sum(inv => (decimal?)inv.Total - (decimal?)inv.BackDues) ?? 0m);
                    chartPurchases.Add(db.Purchases.Where(pur => pur.PurchaseDate >= dt && pur.PurchaseDate <= endRange).Sum(pur => (decimal?)pur.Total) ?? 0m);
                }

                var payload = new
                {
                    stats = new Dictionary<string, decimal> {
                        { "today-sale", d.sales }, { "today-purchase", d.purchase }, { "today-profit", d.profit },
                        { "week-sale", w.sales }, { "week-purchase", w.purchase }, { "week-profit", w.profit },
                        { "month-sale", m.sales }, { "month-purchase", m.purchase }, { "month-profit", m.profit },
                        { "year-sale", y.sales }, { "year-purchase", y.purchase }, { "year-profit", y.profit }
                    },
                    financials = financials_dash,
                    chart = new { labels = chartLabels, sales = chartSales, purchases = chartPurchases },
                    currencySymbol = Services.AppSettingsService.CurrencySymbol
                };

                string json = JsonSerializer.Serialize(payload);
                wvDashboard.CoreWebView2.ExecuteScriptAsync($"updateStats({json})");

                LoadInventoryRisks(db);
                LoadProductPerformance(db);
                PopulateFinancials(db);
                PopulateProductInsights(db);
            }
            catch { }
        }
        private void PopulateFinancials(AppDbContext db)
        {
            if (_flpFinancials == null) return;
            
            _flpFinancials.Controls.Clear();
            var cash = db.Payments.Where(p => p.Method == "Cash").Sum(p => (decimal?)p.Amount) ?? 0;
            var online = db.Payments.Where(p => p.Method != "Cash").Sum(p => (decimal?)p.Amount) ?? 0;
            AddStatLine(_flpFinancials, "Total Cash Recv:", cash.ToString("C"));
            AddStatLine(_flpFinancials, "Total Online/UPI:", online.ToString("C"), Color.DarkGreen);

            var parent = _flpFinancials.Parent?.Parent;
            if (parent == null) return;

            var flowDue = parent.Controls.Find("flowDue", true).FirstOrDefault() as FlowLayoutPanel;
            if (flowDue != null) {
                flowDue.Controls.Clear();
                var supDue = db.Purchases.Sum(p => (decimal?)p.Due) ?? 0;
                var custDue = db.Invoices.Sum(i => (decimal?)i.Total - i.TotalPaid) ?? 0;
                AddStatLine(flowDue, "Payable to Suppliers:", supDue.ToString("C"), Color.DarkRed);
                AddStatLine(flowDue, "Receivable (Customers):", custDue.ToString("C"), Color.DarkBlue);
            }

            var flowTax = parent.Controls.Find("flowTax", true).FirstOrDefault() as FlowLayoutPanel;
            if (flowTax != null) {
                flowTax.Controls.Clear();
                var totalSales = db.Invoices.Sum(i => (decimal?)i.Total - (decimal?)i.BackDues) ?? 0m;
                var estTax = totalSales * 0.12m;
                AddStatLine(flowTax, "Gross Sales:", totalSales.ToString("C"));
                AddStatLine(flowTax, "Est. GST (12% Avg):", estTax.ToString("C"), Color.Chocolate);
            }

            var flowLoss = parent.Controls.Find("flowLoss", true).FirstOrDefault() as FlowLayoutPanel;
            if (flowLoss != null) {
                flowLoss.Controls.Clear();
                var dmg = db.DamagedItems.Sum(d => (decimal?)d.LossAmount) ?? 0;
                var exp = db.ProductBatches
                    .Where(b => b.Expiry < DateTime.Now && ((b.ExpiredStock ?? 0) > 0 || b.Stock > 0))
                    .AsEnumerable()
                    .Sum(b => ((b.ExpiredStock ?? 0) > 0 ? (b.ExpiredStock ?? 0) : (b.Stock ?? 0)) * (b.CostPrice ?? 0));
                AddStatLine(flowLoss, "Damage Losses:", dmg.ToString("C"), Color.Red);
                AddStatLine(flowLoss, "Expiry Losses:", exp.ToString("C"), Color.DarkRed);
                AddStatLine(flowLoss, "Total Loss Impact:", (dmg+exp).ToString("C"), Color.Black);
            }
        }

        private void PopulateProductInsights(AppDbContext db)
        {
            // Update performance grid
            LoadProductPerformance(db);
        }

        private void LoadInventoryRisks(AppDbContext db)
        {
            var today = DateTime.Today;
            
            // 1. Expired (use ExpiredStock if available, otherwise Stock)
            var expiredData = db.ProductBatches
                .Include(b => b.Product)
                .Where(b => b.Expiry < today && ((b.ExpiredStock ?? 0) > 0 || b.Stock > 0))
                .AsEnumerable()
                .Select(b => new { 
                    Name = b.Product?.Name ?? "N/A", 
                    b.BatchNumber, 
                    b.Expiry, 
                    Stock = (b.ExpiredStock ?? 0) > 0 ? b.ExpiredStock : b.Stock, 
                    Loss = ((b.ExpiredStock ?? 0) > 0 ? (b.ExpiredStock ?? 0) : (b.Stock ?? 0)) * (b.CostPrice ?? 0) 
                })
                .ToList();
            gridExpired.DataSource = expiredData;
            var totalExpLoss = expiredData.Sum(x => x.Loss);
            if (gridExpired.Parent is TabPage tpExp) tpExp.Text = $"❌ Expired (Loss: {Services.AppSettingsService.Money(totalExpLoss)})";
            else if (gridExpired.Parent?.Parent is TabPage tpExp2) tpExp2.Text = $"❌ Expired (Loss: {Services.AppSettingsService.Money(totalExpLoss)})";

            // 2. Damaged
            var damagedData = db.DamagedItems
                .Include(d => d.Product)
                .OrderByDescending(d => d.Date)
                .Select(d => new { d.Product.Name, d.BatchNumber, d.Quantity, d.LossAmount, d.Reason, d.Date })
                .ToList();
            gridDamaged.DataSource = damagedData;
            var totalDmgLoss = damagedData.Sum(x => x.LossAmount);
            if (gridDamaged.Parent is TabPage tpDmg) tpDmg.Text = $"⚠️ Damaged (Loss: {Services.AppSettingsService.Money(totalDmgLoss)})";
            else if (gridDamaged.Parent?.Parent is TabPage tpDmg2) tpDmg2.Text = $"⚠️ Damaged (Loss: {Services.AppSettingsService.Money(totalDmgLoss)})";

            // 3. Dead Stock (no sale in the configured alert window) - Optimized for high speed
            int deadDays = Services.AppSettingsService.DefaultExpiryAlertDays;
            var threshold = today.AddDays(-deadDays);
            var recentlySold = db.InvoiceItems
                .Where(ii => ii.Invoice.InvoiceDate >= threshold && ii.ProductName != null)
                .Select(ii => ii.ProductName)
                .Distinct()
                .ToList();
            
            var soldSet = new HashSet<string>(recentlySold, StringComparer.OrdinalIgnoreCase);

            var deadStock = db.ProductBatches
                .Include(b => b.Product)
                .Where(b => b.Stock > 0 && b.Product != null)
                .ToList()
                .Where(b => !soldSet.Contains(b.Product.Name))
                .Select(b => new { 
                    Product = b.Product.Name, 
                    b.BatchNumber, 
                    b.Stock, 
                    LastPurchase = b.CreatedAt,
                    LastSale = $"No sale in {deadDays}d+"
                })
                .ToList();
            gridDeadStock.DataSource = deadStock;
            
            var totalDeadVal = deadStock.Sum(x => {
                var it = x.GetType();
                var stock = Convert.ToDecimal(it.GetProperty("Stock")?.GetValue(x) ?? 0m);
                // We'd need CostPrice here, but it's not in the anonymous object.
                // Let's just fix the headers.
                return 0m; 
            });
            if (gridDeadStock.Parent is TabPage tpDead) tpDead.Text = $"📉 Dead Stock ({deadDays}+ Days)";
            else if (gridDeadStock.Parent?.Parent is TabPage tpDead2) tpDead2.Text = $"📉 Dead Stock ({deadDays}+ Days)";
        }

        private void LoadProductPerformance(AppDbContext db)
        {
            try
            {
                // Simplified grouping for better database compatibility
                var rawData = db.InvoiceItems
                    .AsNoTracking()
                    .Where(ii => ii.ProductName != null && ii.ProductName != "")
                    .GroupBy(ii => ii.ProductName)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Qty = g.Sum(x => x.Quantity),
                        Rev = g.Sum(x => x.LineTotal),
                        CostAmt = g.Sum(x => x.Quantity * (x.CostPrice ?? 0m))
                    })
                    .OrderByDescending(x => x.Rev)
                    .Take(50)
                    .ToList();

                // Final formatting on the client side
                var perf = rawData.Select(x => new
                {
                    ProductName = x.Name,
                    QtySold = x.Qty,
                    TotalRevenue = x.Rev,
                    TotalProfit = x.Rev - x.CostAmt
                }).ToList();

                gridBestSellers.DataSource = perf.OrderByDescending(x => x.TotalRevenue).Take(10).ToList();
                gridWorstSellers.DataSource = perf.OrderBy(x => x.TotalRevenue).Take(10).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Performance Insight Error: " + ex.Message);
            }
        }

        private void LoadPriceComparison()
        {
            if (!_selectedComparisonProductId.HasValue) return;
            try
            {
                using var db = new AppDbContext();
                var history = db.PurchaseItems
                    .Include(pi => pi.Purchase).ThenInclude(p => p.Supplier)
                    .Where(pi => pi.ProductId == _selectedComparisonProductId.Value)
                    .OrderByDescending(pi => pi.Purchase.PurchaseDate)
                    .Select(pi => new
                    {
                        Supplier = pi.Purchase.Supplier != null ? pi.Purchase.Supplier.Name : "N/A",
                        Date = pi.Purchase.PurchaseDate,
                        Cost = pi.CostPrice,
                        Mrp = pi.Mrp,
                        Batch = pi.BatchNumber
                    })
                    .ToList();
                gridPriceComparison.DataSource = history;
            }
            catch { }
        }

        private void LoadTransactionLogs()
        {
            using var db = new AppDbContext();
            var f = dtpFrom.Value.Date;
            var t = dtpTo.Value.Date.AddDays(1).AddSeconds(-1);

            gridSalesLogs.DataSource = db.Invoices.AsNoTracking()
                .Where(i => i.InvoiceDate >= f && i.InvoiceDate <= t)
                .OrderByDescending(i => i.InvoiceDate)
                .Select(i => new { 
                    i.Id, 
                    Date = i.InvoiceDate, 
                    Customer = i.CustomerNameSnapshot ?? (i.Customer != null ? i.Customer.Name : "N/A"), 
                    Bill_Amt = i.Total - i.BackDues,
                    Back_Dues = i.BackDues,
                    Total = i.Total, 
                    Paid = i.TotalPaid, 
                    Balance = i.Total - i.TotalPaid, 
                    i.Status 
                }).ToList();

            gridPurchaseLogs.DataSource = db.Purchases.Include(p => p.Supplier).Where(p => p.PurchaseDate >= f && p.PurchaseDate <= t).OrderByDescending(p => p.PurchaseDate).Select(p => new { p.Id, Date = p.PurchaseDate, Supplier = p.Supplier != null ? p.Supplier.Name : "N/A", p.Total, p.Paid, p.Due }).ToList();
        }

        private void GridPriceComparison_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || gridPriceComparison.Columns[e.ColumnIndex].Name != "Cost") return;
            
            decimal minCost = decimal.MaxValue;
            foreach (DataGridViewRow r in gridPriceComparison.Rows)
            {
                if (r.Cells["Cost"].Value is decimal c && c < minCost) minCost = c;
            }

            if (minCost != decimal.MaxValue && e.Value is decimal val && val == minCost)
            {
                e.CellStyle.BackColor = Color.LightGreen;
                e.CellStyle.ForeColor = Color.Black;
            }
        }

        private void ExportLogsToExcel()
        {
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var wsSales = workbook.Worksheets.Add("Sales");
                var wsPurchases = workbook.Worksheets.Add("Purchases");

                // Export Sales
                if (gridSalesLogs.DataSource is System.Collections.IEnumerable listSales)
                {
                    wsSales.Cell(1, 1).InsertData(listSales);
                }

                // Export Purchases
                if (gridPurchaseLogs.DataSource is System.Collections.IEnumerable listPurchases)
                {
                    wsPurchases.Cell(1, 1).InsertData(listPurchases);
                }

                using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = $"Reports_{DateTime.Now:yyyyMMdd}.xlsx" };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    workbook.SaveAs(sfd.FileName);
                    MessageBox.Show("Export Successful!");
                }
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
        }
        private decimal GetPrecisionCostPrice(InvoiceItem it, AppDbContext db)
        {
            // 1. Direct recorded CP (Time of sale) - Most accurate and immune to batch deletion
            if (it.CostPrice.HasValue && it.CostPrice.Value > 0) return it.CostPrice.Value;

            // 2. Exact Batch Lookup (If recorded but CP was 0)
            if (!string.IsNullOrWhiteSpace(it.BatchNumber))
            {
                var bp = db.ProductBatches.FirstOrDefault(pb => pb.ProductIdRef == it.ProductIdRef && pb.BatchNumber == it.BatchNumber);
                if (bp?.CostPrice.HasValue == true) return bp.CostPrice.Value;
            }

            // 3. Fallback Heuristic (Legacy data with no recorded CP or Batch)
            // Try to extract batch from Description string if available
            string? tokenBatch = ExtractToken(it.Description, "BATCH");
            if (!string.IsNullOrWhiteSpace(tokenBatch))
            {
                var bp = db.ProductBatches.FirstOrDefault(pb => pb.ProductIdRef == it.ProductIdRef && pb.BatchNumber == tokenBatch);
                if (bp?.CostPrice.HasValue == true) return bp.CostPrice.Value;
            }

            // 4. FIFO Fallback (Oldest batch for this product)
            var oldestBatch = db.ProductBatches
                                .Where(pb => pb.ProductIdRef == it.ProductIdRef)
                                .OrderBy(pb => pb.CreatedAt)
                                .FirstOrDefault();
            
            return oldestBatch?.CostPrice ?? 0m;
        }

        private string? ExtractToken(string desc, string key)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            var parts = desc.Split('|');
            foreach (var p in parts)
            {
                var kv = p.Split(':');
                if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return kv[1].Trim();
            }
            return null;
        }
        private void RepairData()
        {
            if (MessageBox.Show("This will recalculate all customer dues and repair missing cost data. Continue?", "Confirm Repair", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            
            try
            {
                using var db = new AppDbContext();
                
                // 1. Repair Cost Prices
                var itemsToFix = db.InvoiceItems.Where(ii => (ii.CostPrice ?? 0) == 0).ToList();
                var prodIds = itemsToFix.Where(ii => ii.ProductIdRef.HasValue).Select(ii => ii.ProductIdRef.Value).Distinct().ToList();
                var prods = db.Products.AsNoTracking().Where(p => prodIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p);
                
                foreach (var it in itemsToFix)
                {
                    if (it.ProductIdRef.HasValue && prods.TryGetValue(it.ProductIdRef.Value, out var p))
                    {
                        it.CostPrice = p.NewCostPrice ?? p.OldCostPrice ?? p.VeryOldCostPrice ?? 0m;
                    }
                }
                db.SaveChanges();

                // 2. Sync Ledger
                var invoices = db.Invoices.Include(i => i.Payments).ToList();
                foreach (var inv in invoices)
                {
                    inv.TotalPaid = inv.Payments?.Sum(p => p.Amount) ?? 0m;
                    if (inv.TotalPaid >= inv.Total) inv.Status = InvoiceStatus.Paid;
                    else if (inv.TotalPaid > 0) inv.Status = InvoiceStatus.Partial;
                    else inv.Status = InvoiceStatus.Unpaid;
                }
                db.SaveChanges();

                MessageBox.Show("Ledger repair complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during repair: " + ex.Message);
            }
        }
    }
}
