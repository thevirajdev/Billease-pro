using System;

namespace BillingSuite.App.Models
{
    public class Product : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public string Barcode { get; set; } = string.Empty; // acts as SKU
        public string Name { get; set; } = string.Empty;
        public string? Hsn { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal MRP { get; set; }  // Maximum Retail Price
        public string? Category { get; set; }
        public int Stock { get; set; }
        // Old batch details
        public string? OldBatch { get; set; }
        public decimal? OldMrp { get; set; }
        public DateTime? OldExpiry { get; set; }
        public decimal? OldCostPrice { get; set; }
        public decimal? OldSellingPrice { get; set; }
        public decimal? OldStock { get; set; }
        // New batch details
        public string? NewBatch { get; set; }
        public decimal? NewMrp { get; set; }
        public DateTime? NewExpiry { get; set; }
        public decimal? NewCostPrice { get; set; }
        public decimal? NewSellingPrice { get; set; }
        public decimal? NewStock { get; set; }
        // Very old batch details (overflow when Old already occupied)
        public string? VeryOldBatch { get; set; }
        public decimal? VeryOldMrp { get; set; }
        public DateTime? VeryOldExpiry { get; set; }
        public decimal? VeryOldCostPrice { get; set; }
        public decimal? VeryOldSellingPrice { get; set; }
        public decimal? VeryOldStock { get; set; }
        // Alerts configuration
        // No literal default: a new product inherits alerts.defaultExpiryAlertDays
        // (AppSettingsService.DefaultExpiryAlertDays). 90 here contradicted the 30 used elsewhere.
        public int? ExpiryAlertDays { get; set; }
        public decimal? LowStockThreshold { get; set; }
        public string? MarketedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
