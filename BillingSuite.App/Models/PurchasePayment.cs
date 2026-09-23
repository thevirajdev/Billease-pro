using System;

namespace BillingSuite.App.Models
{
    public class PurchasePayment : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public int PurchaseId { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public string? Method { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
    }
}
