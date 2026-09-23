namespace BillingSuite.App.Models
{
    /// <summary>Phase 8: implemented by every business data entity that belongs to a user account.</summary>
    public interface IOwnedEntity
    {
        int? OwnerUserId { get; set; }
    }
}
