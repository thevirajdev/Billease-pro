using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BillingSuite.App.Models;
using BillingSuite.App.Data;
using Newtonsoft.Json.Linq;

namespace BillingSuite.App.Forms
{
    public class AiActionReviewForm : Form
    {
        private readonly AiActionResponse _aiResponse;
        private readonly DataGridView _gridItems = new DataGridView();
        private readonly Button _btnApprove = new Button();
        private readonly Button _btnReject = new Button();
        private readonly Label _lblMessage = new Label();
        private readonly Panel _pnlHeader = new Panel();
        private readonly Panel _pnlFooter = new Panel();

        public AiActionResponse ResultResponse { get; private set; }

        public AiActionReviewForm(AiActionResponse aiResponse)
        {
            _aiResponse = aiResponse;
            ResultResponse = aiResponse;

            Text = "Review AI Action: " + aiResponse.Action;
            Size = new Size(1100, 700); // Increased size for more columns
            StartPosition = FormStartPosition.CenterParent;

            InitializeComponents();
            PopulateData();
        }

        private void InitializeComponents()
        {
            _pnlHeader.Dock = DockStyle.Top;
            _pnlHeader.Height = 60;
            _pnlHeader.Padding = new Padding(10);
            
            _lblMessage.Dock = DockStyle.Fill;
            _lblMessage.Text = _aiResponse.Message;
            _lblMessage.Font = new Font(Font, FontStyle.Italic);
            _pnlHeader.Controls.Add(_lblMessage);

            _pnlFooter.Dock = DockStyle.Bottom;
            _pnlFooter.Height = 50;
            _pnlFooter.Padding = new Padding(10);

            _btnApprove.Text = "Approve & Execute";
            _btnApprove.Dock = DockStyle.Right;
            _btnApprove.Width = 150;
            _btnApprove.BackColor = Color.LightGreen;
            _btnApprove.Click += (s, e) => 
            { 
                SyncDataFromGrid();
                DialogResult = DialogResult.OK; 
                Close(); 
            };

            _btnReject.Text = "Reject / Cancel";
            _btnReject.Dock = DockStyle.Right;
            _btnReject.Width = 120;
            _btnReject.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _pnlFooter.Controls.Add(_btnReject);
            var spacer = new Panel { Dock = DockStyle.Right, Width = 10 };
            _pnlFooter.Controls.Add(spacer);
            _pnlFooter.Controls.Add(_btnApprove);

            _gridItems.Dock = DockStyle.Fill;
            _gridItems.AllowUserToAddRows = false;
            _gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // Set to manual to avoid squishing
            _gridItems.RowHeadersVisible = false;

            _gridItems.EditingControlShowing += (s, e) =>
            {
                if (_gridItems.CurrentCell == null) return;
                var colName = _gridItems.Columns[_gridItems.CurrentCell.ColumnIndex].Name;

                if (colName == "Sku")
                {
                    if (e.Control is TextBox tb)
                    {
                        tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                        tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
                        var sc = new AutoCompleteStringCollection();
                        try
                        {
                            using var db = new AppDbContext();
                            sc.Add("[+] GENERATE NEW PRODUCT"); 
                            var prods = db.Products.Select(p => p.Name).ToList();
                            foreach (var p in prods) sc.Add(p);
                        }
                        catch { }
                        tb.AutoCompleteCustomSource = sc;
                    }
                }
            };

            _gridItems.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var colName = _gridItems.Columns[e.ColumnIndex].Name;
                if (colName == "Sku")
                {
                    var r = _gridItems.Rows[e.RowIndex];
                    var val = r.Cells["Sku"].Value?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(val))
                    {
                        using var db = new AppDbContext();
                        if (val == "[+] GENERATE NEW PRODUCT")
                        {
                            long nextSku = 10001;
                            var codes = db.Products.Select(p => p.Barcode).ToList();
                            foreach (var c in codes)
                            {
                                if (long.TryParse(c, out long num) && num >= nextSku) nextSku = num + 1;
                            }
                            r.Cells["Sku"].Value = nextSku.ToString();
                        }
                        else
                        {
                            var prod = db.Products.FirstOrDefault(x => x.Name == val || x.Barcode == val);
                            if (prod != null)
                            {
                                r.Cells["Sku"].Value = prod.Barcode;
                                r.Cells["ProductName"].Value = prod.Name;
                                r.Cells["Hsn"].Value = prod.Hsn;
                                r.Cells["Category"].Value = prod.Category;
                                r.Cells["Mkt"].Value = prod.MarketedBy;
                            }
                        }
                    }
                }
            };

            Controls.Add(_gridItems);
            Controls.Add(_pnlHeader);
            Controls.Add(_pnlFooter);
        }

        private void PopulateData()
        {
            try
            {
                switch (_aiResponse.Action)
                {
                    case "create_purchase":
                        SetupPurchaseGrid();
                        break;
                    case "create_bill":
                        SetupBillGrid();
                        break;
                    case "check_stock":
                    case "update_product":
                    default:
                        SetupGenericGrid();
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error populating review data: " + ex.Message);
            }
        }

        private void SetupPurchaseGrid()
        {
            var data = _aiResponse.Data.ToObject<AiPurchaseData>();
            if (data == null) return;

            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sku", HeaderText = "SKU", Width = 90 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductName", HeaderText = "Product", Width = 200 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "BatchNumber", HeaderText = "Batch", Width = 100 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expiry", HeaderText = "Expiry", Width = 80 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mrp", HeaderText = "MRP", Width = 70 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "SellingPrice", HeaderText = "SP", Width = 70 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "Rate", Width = 70 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostPrice", HeaderText = "Cost", Width = 70 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty", Width = 50 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pack", HeaderText = "Pack", Width = 70 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mkt", HeaderText = "Mkt", Width = 100 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bonus", HeaderText = "Bonus", Width = 60 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Disc", HeaderText = "Disc%", Width = 50 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cgst", HeaderText = "CGST%", Width = 50 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sgst", HeaderText = "SGST%", Width = 50 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Igst", HeaderText = "IGST%", Width = 50 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Hsn", HeaderText = "HSN", Width = 80 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", Width = 100 });

            foreach (var item in data.Items)
            {
                _gridItems.Rows.Add(
                    item.Sku,
                    item.ProductName,
                    item.BatchNumber,
                    item.Expiry,
                    item.Mrp,
                    item.SellingPrice,
                    item.Rate,
                    item.CostPrice,
                    item.Quantity,
                    item.Pack,
                    item.Mkt,
                    item.Bonus,
                    item.Disc,
                    item.Cgst,
                    item.Sgst,
                    item.Igst,
                    item.Hsn,
                    item.Category
                );
            }
        }

        private void SetupBillGrid()
        {
            var data = _aiResponse.Data.ToObject<AiBillData>();
            if (data == null) return;

            _gridItems.Columns.Add("ProductName", "Product");
            _gridItems.Columns.Add("Quantity", "Qty");
            _gridItems.Columns.Add("Price", "Price");

            foreach (var item in data.Items)
            {
                _gridItems.Rows.Add(item.ProductName, item.Quantity, item.Price);
            }
        }

        private void SetupGenericGrid()
        {
            _gridItems.Columns.Add("Key", "Property");
            _gridItems.Columns.Add("Value", "Value");

            foreach (var prop in _aiResponse.Data.Properties())
            {
                _gridItems.Rows.Add(prop.Name, prop.Value.ToString());
            }
        }

        private void SyncDataFromGrid()
        {
            if (_aiResponse.Action == "create_purchase")
            {
                var purchaseData = new AiPurchaseData { Items = new List<AiPurchaseItem>() };
                foreach (DataGridViewRow r in _gridItems.Rows)
                {
                    if (r.IsNewRow) continue;
                    purchaseData.Items.Add(new AiPurchaseItem
                    {
                        Sku = r.Cells["Sku"].Value?.ToString(),
                        ProductName = r.Cells["ProductName"].Value?.ToString() ?? "",
                        BatchNumber = r.Cells["BatchNumber"].Value?.ToString(),
                        Expiry = r.Cells["Expiry"].Value?.ToString(),
                        Mrp = TryParseDecimal(r.Cells["Mrp"].Value),
                        SellingPrice = TryParseDecimal(r.Cells["SellingPrice"].Value),
                        Rate = TryParseDecimal(r.Cells["Rate"].Value),
                        CostPrice = TryParseDecimal(r.Cells["CostPrice"].Value),
                        Quantity = TryParseDecimal(r.Cells["Quantity"].Value) ?? 0,
                        Pack = r.Cells["Pack"].Value?.ToString(),
                        Mkt = r.Cells["Mkt"].Value?.ToString(),
                        Bonus = r.Cells["Bonus"].Value?.ToString(),
                        Disc = TryParseDecimal(r.Cells["Disc"].Value),
                        Cgst = TryParseDecimal(r.Cells["Cgst"].Value),
                        Sgst = TryParseDecimal(r.Cells["Sgst"].Value),
                        Igst = TryParseDecimal(r.Cells["Igst"].Value),
                        Hsn = r.Cells["Hsn"].Value?.ToString(),
                        Category = r.Cells["Category"].Value?.ToString()
                    });
                }
                ResultResponse.Data = JObject.FromObject(purchaseData);
            }
        }

        private decimal? TryParseDecimal(object value)
        {
            if (value == null) return null;
            if (decimal.TryParse(value.ToString(), out decimal res)) return res;
            return null;
        }
    }
}
