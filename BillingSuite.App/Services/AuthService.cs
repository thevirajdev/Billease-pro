using System;
using System.Linq;
using System.Security.Cryptography;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Local account authentication. Passwords are hashed with PBKDF2-SHA256 and a
    /// per-user random salt; the plaintext is never stored anywhere.
    ///
    /// Uses PBKDF2 rather than an external Argon2 package so the app gains no new
    /// dependency. The iteration count is stored per row, so it can be raised later
    /// without invalidating existing accounts.
    /// </summary>
    public static class AuthService
    {
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        public const int DefaultIterations = 210_000;

        /// <summary>The signed-in user for this process, or null when nobody is signed in.</summary>
        public static AppUser? CurrentUser { get; private set; }
        public static bool IsSignedIn => CurrentUser != null;

        public static void SignOut()
        {
            CurrentUser = null;
            try
            {
                AppSettingsService.Set("ActiveSessionUserId", "0");
            }
            catch { }
        }

        /// <summary>
        /// Attempts to auto-login the last signed-in user or single user account on startup.
        /// Returns true if user session is active.
        /// </summary>
        public static bool TryAutoLogin()
        {
            try
            {
                using var db = new AppDbContext();
                var users = db.Users.Where(u => !u.DisabledAt.HasValue).ToList();
                if (users.Count == 0) return false;

                int activeId = AppSettingsService.GetInt("ActiveSessionUserId", 0);
                AppUser? targetUser = null;

                if (activeId > 0)
                {
                    targetUser = users.FirstOrDefault(u => u.Id == activeId);
                }

                // Single account on device -> auto login
                if (targetUser == null && users.Count == 1)
                {
                    targetUser = users[0];
                }

                if (targetUser != null)
                {
                    CurrentUser = targetUser;
                    AppSettingsService.Set("ActiveSessionUserId", targetUser.Id.ToString());
                    DatabaseUtils.ClaimLegacyUnownedData(targetUser.Id);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TryAutoLogin warning: {ex.Message}");
            }
            return false;
        }

        /// <summary>True when at least one account exists, i.e. setup has been completed.</summary>
        public static bool AnyUserExists()
        {
            try
            {
                using var db = new AppDbContext();
                return db.Users.Any();
            }
            catch
            {
                // If the table is not ready yet, treat as "no accounts" so setup runs.
                return false;
            }
        }

        public static (bool ok, string error) Register(string email, string password, string fullName,
            string? phone = null, string? companyName = null, string? companyAddress = null,
            string? companyPhone = null, string? companyEmail = null, string? companyTaxNumber = null)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            fullName = (fullName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email)) return (false, "Email is required.");
            if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
                return (false, "Enter a valid email address.");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "Password must be at least 6 characters.");
            if (string.IsNullOrWhiteSpace(fullName)) return (false, "Full name is required.");

            try
            {
                using var db = new AppDbContext();
                if (db.Users.Any(u => u.Email == email))
                    return (false, "An account with that email already exists on this device.");

                var salt = RandomNumberGenerator.GetBytes(SaltBytes);
                var user = new AppUser
                {
                    Email = email,
                    FullName = fullName,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                    CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim(),
                    CompanyAddress = string.IsNullOrWhiteSpace(companyAddress) ? null : companyAddress.Trim(),
                    CompanyPhone = string.IsNullOrWhiteSpace(companyPhone) ? null : companyPhone.Trim(),
                    CompanyEmail = string.IsNullOrWhiteSpace(companyEmail) ? null : companyEmail.Trim(),
                    CompanyTaxNumber = string.IsNullOrWhiteSpace(companyTaxNumber) ? null : companyTaxNumber.Trim(),
                    PasswordSalt = Convert.ToBase64String(salt),
                    PasswordIterations = DefaultIterations,
                    Role = "Owner",
                    CreatedAt = DateTime.UtcNow,
                };
                user.PasswordHash = Convert.ToBase64String(
                    Rfc2898DeriveBytes.Pbkdf2(password, salt, user.PasswordIterations, HashAlgorithmName.SHA256, HashBytes));

                db.Users.Add(user);
                db.SaveChanges();

                CurrentUser = user;
                AppSettingsService.Set("ActiveSessionUserId", user.Id.ToString());
                DatabaseUtils.ClaimLegacyUnownedData(user.Id);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Could not create the account: {ex.Message}");
            }
        }

        public static (bool ok, string error) SignIn(string email, string password)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return (false, "Enter your email and password.");

            try
            {
                using var db = new AppDbContext();
                var user = db.Users.FirstOrDefault(u => u.Email == email);
                if (user == null)
                    return (false, "No account found with that email on this device.");
                if (user.DisabledAt.HasValue)
                    return (false, "This account has been disabled.");

                if (!Verify(password, user))
                    return (false, "Incorrect password.");

                user.LastLoginAt = DateTime.UtcNow;
                db.SaveChanges();

                CurrentUser = user;
                AppSettingsService.Set("ActiveSessionUserId", user.Id.ToString());
                DatabaseUtils.ClaimLegacyUnownedData(user.Id);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Could not sign in: {ex.Message}");
            }
        }

        /// <summary>
        /// Constant-time verification. Uses a fixed-time comparison so a wrong password
        /// cannot be distinguished from a right one by response timing.
        /// </summary>
        public static bool Verify(string password, AppUser user)
        {
            try
            {
                var salt = Convert.FromBase64String(user.PasswordSalt);
                var expected = Convert.FromBase64String(user.PasswordHash);
                var iterations = user.PasswordIterations > 0 ? user.PasswordIterations : DefaultIterations;
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Changes the password after verifying the current one.</summary>
        public static (bool ok, string error) ChangePassword(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "New password must be at least 6 characters.");

            try
            {
                using var db = new AppDbContext();
                var user = db.Users.FirstOrDefault(u => u.Id == userId);
                if (user == null) return (false, "Account not found.");
                if (!Verify(currentPassword, user)) return (false, "Current password is incorrect.");

                var salt = RandomNumberGenerator.GetBytes(SaltBytes);
                user.PasswordSalt = Convert.ToBase64String(salt);
                user.PasswordIterations = DefaultIterations;
                user.PasswordHash = Convert.ToBase64String(
                    Rfc2898DeriveBytes.Pbkdf2(newPassword, salt, user.PasswordIterations, HashAlgorithmName.SHA256, HashBytes));
                db.SaveChanges();

                if (CurrentUser != null && CurrentUser.Id == userId) CurrentUser = user;
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Could not change the password: {ex.Message}");
            }
        }
    }
}
