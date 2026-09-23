using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillingSuite.App.Models
{
    public class InvoiceItem : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int? ProductIdRef { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal LineTotal { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public string? BatchNumber { get; set; }
    }
}
