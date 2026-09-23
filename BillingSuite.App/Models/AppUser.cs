using System;

namespace BillingSuite.App.Models
{
    /// <summary>
    /// A local login account. Multiple accounts can exist on one device; each has its
    /// own password hash and profile. This is the Phase 7 auth foundation and the
    /// prerequisite for Phase 8 per-user data isolation.
    /// </summary>
    public class AppUser
    {
        public int Id { get; set; }

        /// <summary>Login identity. Stored lower-case so lookups are case-insensitive.</summary>
        public string Email { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        /// <summary>PBKDF2 hash, Base64. Never the password itself.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Random per-user salt, Base64.</summary>
        public string PasswordSalt { get; set; } = string.Empty;

        /// <summary>PBKDF2 iteration count, stored so it can be raised without breaking old accounts.</summary>
        public int PasswordIterations { get; set; }

        public string? Phone { get; set; }
        public string? Role { get; set; }

        /// <summary>Company details captured during first-run setup.</summary>
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? CompanyTaxNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        /// <summary>Set when the account is disabled; login is refused while non-null.</summary>
        public DateTime? DisabledAt { get; set; }
    }
}
