using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillingSuite.App.Models
{
    public enum InvoiceStatus
    {
        Draft = 0,
        Paid = 1,
        Unpaid = 2,
        Void = 3,
        PastDue = 4,
        Partial = 5
    }

    public class Invoice : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public DateTime? DueDate { get; set; }
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        [NotMapped]
        public bool DiscountIsPercent { get; set; }
        [NotMapped]
        public string? ExtraCostName { get; set; }
        [NotMapped]
        public decimal ExtraCostAmount { get; set; }
        [NotMapped]
        public bool ExtraCostIsPercent { get; set; }
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerPhoneSnapshot { get; set; }
        public string? CustomerAddressSnapshot { get; set; }
        public string? CustomerGstSnapshot { get; set; }
        public decimal Total { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal BackDues { get; set; }
        public decimal Balance => Total - TotalPaid;

        public List<InvoiceItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
        public string? PrivateNotes { get; set; }
        [NotMapped]
        public bool IncludedBackDues { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
