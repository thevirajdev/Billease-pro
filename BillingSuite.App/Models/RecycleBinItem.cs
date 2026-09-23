using System;

namespace BillingSuite.App.Models
{
    public class RecycleBinItem : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public string EntityType { get; set; } = string.Empty; // e.g., Invoice, Product, Customer, Supplier, Purchase
        public int EntityId { get; set; }
        public string JsonData { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    }
}
