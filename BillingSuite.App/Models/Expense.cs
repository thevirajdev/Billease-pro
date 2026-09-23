using System;

namespace BillingSuite.App.Models
{
    public class Expense : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Category { get; set; } = "General"; // Salary, Transport, Rent, etc.
        public DateTime Date { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Note { get; set; }
    }
}
