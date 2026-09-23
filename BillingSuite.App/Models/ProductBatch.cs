using System;

namespace BillingSuite.App.Models
{
    public class ProductBatch : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public int ProductIdRef { get; set; }
        public Product? Product { get; set; }

        public string BatchNumber { get; set; } = string.Empty;
        public string? Hsn { get; set; }
        public DateTime? Expiry { get; set; }
        public decimal? Mrp { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? SellingPrice { get; set; }
        public decimal? Stock { get; set; }
        public string? Bonus { get; set; }
        public string? Pack { get; set; } // free text like pack size
        public string? MarketedBy { get; set; }
        
        // Financial Details
        public decimal? Rate { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? CgstPercent { get; set; }
        public decimal? SgstPercent { get; set; }
        public decimal? IgstPercent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public decimal? ExpiredStock { get; set; } // Stock value preserved when batch expired (Stock zeroed)
    }
}
