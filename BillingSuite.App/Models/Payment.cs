using System;

namespace BillingSuite.App.Models
{
    public class Payment : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public string Method { get; set; } = "Cash";
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
    }
}
