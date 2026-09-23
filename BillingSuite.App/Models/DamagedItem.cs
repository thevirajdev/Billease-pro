using System;

namespace BillingSuite.App.Models
{
    public class DamagedItem : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public int ProductIdRef { get; set; }
        public Product? Product { get; set; }

        public string? BatchNumber { get; set; }
        public decimal Quantity { get; set; }
        public string? Reason { get; set; }
        public decimal LossAmount { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
