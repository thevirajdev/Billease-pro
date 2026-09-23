using System;
using System.Collections.Generic;

namespace BillingSuite.App.Models
{
    public class Purchase : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public int SupplierId { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        public string? InvoiceNumber { get; set; }

        public Supplier? Supplier { get; set; }
        public decimal? Subtotal { get; set; }
        public decimal? Discount { get; set; }
        public bool? DiscountIsPercent { get; set; }
        public decimal? Tax { get; set; }
        public decimal? Shipping { get; set; }
        public decimal? Total { get; set; }
        public decimal? Paid { get; set; }
        public decimal? Due { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Extra cost support (name + amount + percent flag)
        public string? ExtraCostName { get; set; }
        public decimal? ExtraCostAmount { get; set; }
        public bool? ExtraCostIsPercent { get; set; }
        
        // Detailed Financials
        public decimal? TotalDiscount { get; set; }
        public decimal? TotalCgst { get; set; }
        public decimal? TotalSgst { get; set; }
        public decimal? TotalIgst { get; set; }

        public List<PurchaseItem> Items { get; set; } = new();
    }
}
