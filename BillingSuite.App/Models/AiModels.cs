using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BillingSuite.App.Models
{
    public class AiActionResponse
    {
        [JsonProperty("action")]
        public string Action { get; set; } = "no_action";

        [JsonProperty("data")]
        public JObject Data { get; set; } = new JObject();

        [JsonProperty("message")]
        public string Message { get; set; } = "";
    }

    public class AiPurchaseItem
    {
        public string? Sku { get; set; }
        public string ProductName { get; set; } = "";
        public string? BatchNumber { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? Mrp { get; set; }
        public decimal? SellingPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal? Margin { get; set; }
        public decimal? Rate { get; set; }
        public string? Expiry { get; set; }
        public string? Hsn { get; set; }
        public string? Pack { get; set; }
        public string? Mkt { get; set; }
        public string? Bonus { get; set; }
        public decimal? Disc { get; set; }
        public decimal? Cgst { get; set; }
        public decimal? Sgst { get; set; }
        public decimal? Igst { get; set; }
        public string? Category { get; set; }
    }

    public class AiPurchaseData
    {
        public List<AiPurchaseItem> Items { get; set; } = new List<AiPurchaseItem>();
        public string? SupplierName { get; set; }
        public decimal? MarginPercent { get; set; }
        public string? InvoiceNumber { get; set; }
    }

    public class AiBillItem
    {
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal? Price { get; set; }
    }

    public class AiBillData
    {
        public List<AiBillItem> Items { get; set; } = new List<AiBillItem>();
        public string? CustomerName { get; set; }
    }

    public class AiUpdateProductData
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? Price { get; set; }
        public decimal? Qty { get; set; }
    }

    public class AiStockCheckData
    {
        public string ProductName { get; set; } = "";
    }
}
