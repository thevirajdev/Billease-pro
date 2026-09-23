using System;

namespace BillingSuite.App.Models
{
    public class AiChatMessage : IOwnedEntity
    {
        public int Id { get; set; }
        public int? OwnerUserId { get; set; }
        public string Role { get; set; } = "User"; // User, AI, System
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
