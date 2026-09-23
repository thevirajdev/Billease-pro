using System;

namespace BillingSuite.App.Models
{
    public class PurchaseItem : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public int PurchaseId { get; set; }
        public Purchase? Purchase { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? Expiry { get; set; }
        public decimal? Mrp { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? SellingPrice { get; set; }
        public decimal Quantity { get; set; }
        public string? Pack { get; set; }
        public string? Bonus { get; set; }
        public string? MarketedBy { get; set; }
        
        // Financial Details
        public decimal? Rate { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? CgstPercent { get; set; }
        public decimal? SgstPercent { get; set; }
        public decimal? IgstPercent { get; set; }
        public decimal? LineTotal { get; set; } // Quantity * CostPrice
    }
}
