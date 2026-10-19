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
            var prevEmail = CurrentUser?.Email;
            CurrentUser = null;
            try
            {
                AppSettingsService.Set("ActiveSessionUserId", "0");
                AuditLogService.Log(AuditLogService.Events.Logout, prevEmail);
            }
            catch { }
        }

        public static void SyncUserProfileToSettings(AppUser? user)
        {
            if (user == null) return;
            
            // Explicitly sync the logged-in user's profile to AppSettings.
            // If the user's company info is blank, reset to clean defaults rather than
            // leaking the previous account's company name/address/phone.
            AppSettingsService.Set(AppSettingKeys.CompanyName, !string.IsNullOrWhiteSpace(user.CompanyName) ? user.CompanyName : "Your Company");
            AppSettingsService.Set(AppSettingKeys.CompanyAddress1, user.CompanyAddress ?? "");
            AppSettingsService.Set(AppSettingKeys.CompanyPhone, user.CompanyPhone ?? "");
            AppSettingsService.Set(AppSettingKeys.CompanyEmail, !string.IsNullOrWhiteSpace(user.CompanyEmail) ? user.CompanyEmail : user.Email);
            AppSettingsService.Set(AppSettingKeys.CompanyTaxNumber, user.CompanyTaxNumber ?? "");

            // Sanitize Country: ensure it is NEVER Pakistan
            var currentCountry = AppSettingsService.GetString(AppSettingKeys.CompanyCountry);
            if (string.IsNullOrWhiteSpace(currentCountry) || string.Equals(currentCountry, "Pakistan", StringComparison.OrdinalIgnoreCase))
            {
                AppSettingsService.Set(AppSettingKeys.CompanyCountry, "India");
            }
            var currentCity = AppSettingsService.GetString(AppSettingKeys.CompanyCity);
            if (string.Equals(currentCity, "Lahore", StringComparison.OrdinalIgnoreCase))
            {
                AppSettingsService.Set(AppSettingKeys.CompanyCity, "");
            }
            var currentAddr = AppSettingsService.GetString(AppSettingKeys.CompanyAddress1);
            if (string.Equals(currentAddr, "12 Market Road", StringComparison.OrdinalIgnoreCase))
            {
                AppSettingsService.Set(AppSettingKeys.CompanyAddress1, "");
            }

            AppSettingsService.Set("onboarding.completed", "true");
            AppSettingsService.Invalidate();
        }

        public static void AlignLocalUserOwnerId(AppDbContext db, int oldId, int newId)
        {
            if (oldId <= 0 || newId <= 0 || oldId == newId) return;
            try
            {
                string[] tables = new[]
                {
                    "Products", "ProductBatches", "Customers", "Invoices", "InvoiceItems",
                    "Payments", "Suppliers", "Purchases", "PurchaseItems", "PurchasePayments",
                    "Expenses", "DamagedItems", "RecycleBin", "AiChatMessages"
                };
                foreach (var tbl in tables)
                {
                    string sql = "UPDATE \"" + tbl + "\" SET OwnerUserId = @p0 WHERE OwnerUserId = @p1;";
                    try { db.Database.ExecuteSqlRaw(sql, newId, oldId); } catch { }
                }
                try { db.Database.ExecuteSqlRaw("UPDATE Users SET Id = @p0 WHERE Id = @p1;", newId, oldId); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AlignLocalUserOwnerId error: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to auto-login the last active session user on startup.
        /// Validates cloud account presence if online, and syncs user company profile.
        /// Returns true if user session is active and valid.
        /// </summary>
        public static bool TryAutoLogin()
        {
            try
            {
                using var db = new AppDbContext();
                var users = db.Users.Where(u => !u.DisabledAt.HasValue).ToList();
                if (users.Count == 0) return false;

                var candidateUsers = users.Where(u => !u.Email.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)).ToList();
                if (candidateUsers.Count == 0) candidateUsers = users;

                int activeId = AppSettingsService.GetInt("ActiveSessionUserId", 0);
                AppUser? targetUser = null;

                if (activeId > 0)
                {
                    targetUser = candidateUsers.FirstOrDefault(u => u.Id == activeId) ?? users.FirstOrDefault(u => u.Id == activeId);
                }

                if (targetUser == null)
                {
                    targetUser = candidateUsers.OrderByDescending(u => u.LastLoginAt ?? u.CreatedAt).FirstOrDefault();
                }

                if (targetUser == null) return false;

                CurrentUser = targetUser;
                AppSettingsService.Set("ActiveSessionUserId", targetUser.Id.ToString());
                SyncUserProfileToSettings(targetUser);
                if (users.Count <= 1)
                {
                    DatabaseUtils.ClaimLegacyUnownedData(targetUser.Id);
                }

                // Non-blocking background sync of profile details if network is available
                var connStr = CloudDbConfig.GetConnectionString();
                if (!string.IsNullOrWhiteSpace(connStr) && SyncService.IsNetworkAvailable())
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var (cloudOk, cloudUser, _) = await OnlineDatabaseService.CloudGetUserByEmailAsync(connStr, targetUser.Email);
                            if (cloudOk && cloudUser != null)
                            {
                                using var dbBg = new AppDbContext();
                                var uLocal = await dbBg.Users.FirstOrDefaultAsync(u => u.Email == targetUser.Email);
                                if (uLocal != null)
                                {
                                    if (cloudUser.Id > 0 && uLocal.Id != cloudUser.Id)
                                    {
                                        AlignLocalUserOwnerId(dbBg, uLocal.Id, cloudUser.Id);
                                        targetUser.Id = cloudUser.Id;
                                        AppSettingsService.Set("ActiveSessionUserId", cloudUser.Id.ToString());
                                    }
                                    uLocal.FullName = cloudUser.FullName;
                                    uLocal.CompanyName = cloudUser.CompanyName;
                                    uLocal.CompanyAddress = cloudUser.CompanyAddress;
                                    uLocal.CompanyPhone = cloudUser.CompanyPhone;
                                    uLocal.CompanyEmail = cloudUser.CompanyEmail;
                                    uLocal.CompanyTaxNumber = cloudUser.CompanyTaxNumber;
                                    await dbBg.SaveChangesAsync();
                                    SyncUserProfileToSettings(targetUser);
                                }
                            }
                        }
                        catch { }
                    });
                }

                return true;
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

        private class PendingRegistrationRecord
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string OtpCode { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }

        private class PendingPasswordResetRecord
        {
            public string Email { get; set; } = string.Empty;
            public string OtpCode { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PendingRegistrationRecord> s_pendingRegistrations = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PendingPasswordResetRecord> s_pendingPasswordResets = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Validates user signup inputs, generates a 6-digit OTP code, stores pending registration,
        /// and sends the OTP verification email to the user.
        /// </summary>
        public static async System.Threading.Tasks.Task<(bool ok, string error)> InitiateRegistrationAsync(string email, string password, string fullName)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            fullName = (fullName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email)) return (false, "Email address is required.");
            if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
                return (false, "Please enter a valid email address.");
            if (string.IsNullOrWhiteSpace(fullName)) return (false, "Full name is required.");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "Password must be at least 6 characters long.");

            try
            {
                using var db = new AppDbContext();
                var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (existingUser != null)
                    return (false, "An account with that email already exists. Please sign in.");

                // If Supabase Auth API is configured (SupabaseAnonKey exists), use real Supabase Auth Email OTP
                if (SupabaseAuthService.IsConfigured)
                {
                    var (supaOk, supaMsg) = await SupabaseAuthService.SignUpAsync(email, password, fullName);
                    if (!supaOk)
                    {
                        return (false, supaMsg);
                    }

                    var pendingSupa = new PendingRegistrationRecord
                    {
                        Email = email,
                        Password = password,
                        FullName = fullName,
                        OtpCode = string.Empty,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                    };
                    s_pendingRegistrations[email] = pendingSupa;
                    return (true, supaMsg);
                }

                // Fallback: Generate 6-digit OTP code & send via SMTP / Local EmailOtpService
                var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

                var pending = new PendingRegistrationRecord
                {
                    Email = email,
                    Password = password,
                    FullName = fullName,
                    OtpCode = otpCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };

                s_pendingRegistrations[email] = pending;

                var (sentOk, sendMsg) = await EmailOtpService.SendOtpEmailAsync(email, fullName, otpCode);
                if (!sentOk)
                {
                    return (false, $"Could not send verification email: {sendMsg}");
                }

                return (true, sendMsg);
            }
            catch (Exception ex)
            {
                return (false, $"Registration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resends a fresh 6-digit OTP verification code to the pending user's email address.
        /// </summary>
        public static async System.Threading.Tasks.Task<(bool ok, string error)> ResendOtpAsync(string email)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !s_pendingRegistrations.TryGetValue(email, out var pending))
            {
                return (false, "No pending registration found for this email. Please re-enter your registration details.");
            }

            if (SupabaseAuthService.IsConfigured)
            {
                var (supaOk, supaMsg) = await SupabaseAuthService.ResendOtpAsync(email);
                return (supaOk, supaMsg);
            }

            var newOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            pending.OtpCode = newOtp;
            pending.ExpiresAt = DateTime.UtcNow.AddMinutes(10);

            var (sentOk, sendMsg) = await EmailOtpService.SendOtpEmailAsync(email, pending.FullName, newOtp);
            if (!sentOk)
            {
                return (false, $"Failed to resend code: {sendMsg}");
            }

            return (true, sendMsg);
        }

        /// <summary>
        /// Verifies the 6-digit OTP entered by the user. Upon successful verification,
        /// creates the user in Supabase Cloud Postgres and local SQLite DB, then logs them in.
        /// </summary>
        public static async System.Threading.Tasks.Task<(bool ok, string error)> ConfirmOtpRegistrationAsync(string email, string userOtp)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            userOtp = (userOtp ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(userOtp) || userOtp.Length != 6)
            {
                return (false, "Please enter the complete 6-digit verification code.");
            }

            if (!s_pendingRegistrations.TryGetValue(email, out var pending))
            {
                return (false, "Verification session expired. Please start the registration process again.");
            }

            if (DateTime.UtcNow > pending.ExpiresAt)
            {
                return (false, "Verification code has expired. Please click 'Resend Code' to receive a new code.");
            }

            // 1. If Supabase Auth is configured, verify OTP against Supabase Auth API
            if (SupabaseAuthService.IsConfigured)
            {
                var (supaVerifyOk, supaVerifyMsg, _) = await SupabaseAuthService.VerifyOtpAsync(email, userOtp);
                if (!supaVerifyOk)
                {
                    return (false, supaVerifyMsg);
                }
            }
            else
            {
                // Fallback to local OTP match
                if (!string.Equals(pending.OtpCode, userOtp, StringComparison.Ordinal))
                {
                    return (false, "Incorrect verification code. Please check your email inbox and try again.");
                }
            }

            // OTP verified successfully -> proceed to create/upsert account in Supabase Postgres & local DB
            var (ok, error) = await RegisterAsync(pending.Email, pending.Password, pending.FullName);
            if (ok)
            {
                s_pendingRegistrations.TryRemove(email, out _);
            }
            return (ok, error);
        }

        public static string? GetPendingRegistrationOtpCode(string email) => null;

        public static string? GetPendingPasswordResetOtpCode(string email) => null;

        public static async System.Threading.Tasks.Task<(bool ok, string error)> RegisterAsync(string email, string password, string fullName,
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
                var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (existingUser != null)
                    return (false, "An account with that email already exists.");

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

                // 1. Try Supabase Cloud Registration if cloud DB is configured
                var connStr = CloudDbConfig.GetConnectionString();
                if (!string.IsNullOrWhiteSpace(connStr) && SyncService.IsNetworkAvailable())
                {
                    try
                    {
                        var (cloudOk, cloudMsg) = await OnlineDatabaseService.CloudRegisterUserAsync(connStr, user);
                        if (!cloudOk)
                        {
                            Console.WriteLine("Cloud user registration warning: " + cloudMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Cloud user registration exception: " + ex.Message);
                    }
                }

                // 2. Save user locally in SQLite for offline access
                db.Users.Add(user);
                await db.SaveChangesAsync();

                CurrentUser = user;
                AppSettingsService.Set("ActiveSessionUserId", user.Id.ToString());
                SyncUserProfileToSettings(user);
                try { AuditLogService.Log(AuditLogService.Events.AccountCreated, email); } catch { }
                if (db.Users.Count() <= 1)
                {
                    DatabaseUtils.ClaimLegacyUnownedData(user.Id);
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Could not create the account: {ex.Message}");
            }
        }

        public static (bool ok, string error) Register(string email, string password, string fullName,
            string? phone = null, string? companyName = null, string? companyAddress = null,
            string? companyPhone = null, string? companyEmail = null, string? companyTaxNumber = null)
        {
            return RegisterAsync(email, password, fullName, phone, companyName, companyAddress, companyPhone, companyEmail, companyTaxNumber).GetAwaiter().GetResult();
        }

        public static async System.Threading.Tasks.Task<(bool ok, string error)> SignInAsync(string email, string password, bool triggerBackgroundPull = true)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return (false, "Enter your email and password.");

            try
            {
                var connStr = CloudDbConfig.GetConnectionString();

                // 1. Primary: Direct Supabase Auth REST API Login
                if (SupabaseAuthService.IsConfigured && SyncService.IsNetworkAvailable())
                {
                    var (supaOk, supaMsg, supaToken, supaName) = await SupabaseAuthService.SignInWithPasswordAsync(email, password);
                    if (supaOk)
                    {
                        using var db = new AppDbContext();
                        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

                        // Fetch authoritative cloud user profile and ID
                        AppUser? cloudUser = null;
                        if (!string.IsNullOrWhiteSpace(connStr))
                        {
                            try
                            {
                                var (cOk, cUser, _) = await OnlineDatabaseService.CloudGetUserByEmailAsync(connStr, email);
                                if (cOk && cUser != null) cloudUser = cUser;
                            }
                            catch { }
                        }

                        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
                        string saltBase64 = Convert.ToBase64String(salt);
                        int iterations = DefaultIterations;
                        string hashBase64 = Convert.ToBase64String(
                            Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes));

                        if (user == null)
                        {
                            user = new AppUser
                            {
                                Id = cloudUser != null && cloudUser.Id > 0 ? cloudUser.Id : 0,
                                Email = email,
                                FullName = string.IsNullOrWhiteSpace(supaName) ? (cloudUser?.FullName ?? "User") : supaName,
                                PasswordSalt = cloudUser?.PasswordSalt ?? saltBase64,
                                PasswordIterations = cloudUser?.PasswordIterations > 0 ? cloudUser.PasswordIterations : iterations,
                                PasswordHash = cloudUser?.PasswordHash ?? hashBase64,
                                Role = "Owner",
                                CompanyName = cloudUser?.CompanyName,
                                CompanyAddress = cloudUser?.CompanyAddress,
                                CompanyPhone = cloudUser?.CompanyPhone,
                                CompanyEmail = cloudUser?.CompanyEmail,
                                CompanyTaxNumber = cloudUser?.CompanyTaxNumber,
                                CreatedAt = cloudUser?.CreatedAt ?? DateTime.UtcNow
                            };
                            db.Users.Add(user);
                        }
                        else
                        {
                            if (cloudUser != null && cloudUser.Id > 0 && user.Id != cloudUser.Id)
                            {
                                AlignLocalUserOwnerId(db, user.Id, cloudUser.Id);
                                user.Id = cloudUser.Id;
                            }
                            user.PasswordSalt = saltBase64;
                            user.PasswordIterations = iterations;
                            user.PasswordHash = hashBase64;
                            if (cloudUser != null)
                            {
                                if (!string.IsNullOrWhiteSpace(cloudUser.CompanyName)) user.CompanyName = cloudUser.CompanyName;
                                if (!string.IsNullOrWhiteSpace(cloudUser.CompanyAddress)) user.CompanyAddress = cloudUser.CompanyAddress;
                                if (!string.IsNullOrWhiteSpace(cloudUser.CompanyPhone)) user.CompanyPhone = cloudUser.CompanyPhone;
                                if (!string.IsNullOrWhiteSpace(cloudUser.CompanyEmail)) user.CompanyEmail = cloudUser.CompanyEmail;
                                if (!string.IsNullOrWhiteSpace(cloudUser.CompanyTaxNumber)) user.CompanyTaxNumber = cloudUser.CompanyTaxNumber;
                            }
                            if (!string.IsNullOrWhiteSpace(supaName) && (string.IsNullOrWhiteSpace(user.FullName) || user.FullName == "User"))
                                user.FullName = supaName;
                        }
                        user.LastLoginAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();

                        // Sync user to cloud app_users table so PostgreSQL schema has up-to-date profile
                        if (!string.IsNullOrWhiteSpace(connStr))
                        {
                            try
                            {
                                await OnlineDatabaseService.CloudRegisterUserAsync(connStr, user);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("CloudRegisterUserAsync sync warning: " + ex.Message);
                            }
                        }

                        CurrentUser = user;
                        AppSettingsService.Set("ActiveSessionUserId", user.Id.ToString());
                        SyncUserProfileToSettings(user);
                        try { AuditLogService.Log(AuditLogService.Events.LoginSuccess, email, "Supabase Auth"); } catch { }
                        if (db.Users.Count() <= 1)
                        {
                            DatabaseUtils.ClaimLegacyUnownedData(user.Id);
                        }

                        if (triggerBackgroundPull && !string.IsNullOrWhiteSpace(connStr))
                        {
                            _ = OnlineDatabaseService.PullFromCloudAsync(connStr, user.Id);
                        }

                        return (true, string.Empty);
                    }

                }

                // 2. Fallback: Direct PostgreSQL app_users table authentication
                AppUser? cloudUserFallback = null;
                if (!string.IsNullOrWhiteSpace(connStr) && SyncService.IsNetworkAvailable())
                {
                    try
                    {
                        var (cloudOk, userFromCloud, _) = await OnlineDatabaseService.CloudAuthenticateUserAsync(connStr, email, password);
                        if (cloudOk && userFromCloud != null)
                        {
                            cloudUserFallback = userFromCloud;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Cloud authentication warning: " + ex.Message);
                    }
                }

                using var dbOffline = new AppDbContext();
                AppUser? localUser = null;

                if (cloudUserFallback != null)
                {
                    localUser = await dbOffline.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (localUser == null)
                    {
                        localUser = cloudUserFallback;
                        dbOffline.Users.Add(localUser);
                    }
                    else
                    {
                        if (cloudUserFallback.Id > 0 && localUser.Id != cloudUserFallback.Id)
                        {
                            AlignLocalUserOwnerId(dbOffline, localUser.Id, cloudUserFallback.Id);
                            localUser.Id = cloudUserFallback.Id;
                        }
                        localUser.PasswordHash = cloudUserFallback.PasswordHash;
                        localUser.PasswordSalt = cloudUserFallback.PasswordSalt;
                        localUser.PasswordIterations = cloudUserFallback.PasswordIterations;
                        localUser.FullName = cloudUserFallback.FullName;
                        localUser.CompanyName = cloudUserFallback.CompanyName;
                        localUser.CompanyAddress = cloudUserFallback.CompanyAddress;
                        localUser.CompanyPhone = cloudUserFallback.CompanyPhone;
                        localUser.CompanyEmail = cloudUserFallback.CompanyEmail;
                        localUser.CompanyTaxNumber = cloudUserFallback.CompanyTaxNumber;
                    }
                    localUser.LastLoginAt = DateTime.UtcNow;
                    await dbOffline.SaveChangesAsync();

                    if (triggerBackgroundPull && !string.IsNullOrWhiteSpace(connStr))
                    {
                        _ = OnlineDatabaseService.PullFromCloudAsync(connStr, localUser.Id);
                    }
                }
                else
                {
                    // 3. Fallback: Offline local SQLite database authentication
                    localUser = await dbOffline.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (localUser == null)
                        return (false, "No account found with that email. Please check your credentials or create an account.");
                    if (localUser.DisabledAt.HasValue)
                        return (false, "This account has been disabled.");

                    if (!Verify(password, localUser))
                    {
                        try { AuditLogService.Log(AuditLogService.Events.LoginFailed, email, "Incorrect password (offline)"); } catch { }
                        return (false, "Incorrect email or password.");
                    }

                    localUser.LastLoginAt = DateTime.UtcNow;
                    await dbOffline.SaveChangesAsync();
                }

                CurrentUser = localUser;
                AppSettingsService.Set("ActiveSessionUserId", localUser.Id.ToString());
                SyncUserProfileToSettings(localUser);
                try { AuditLogService.Log(AuditLogService.Events.LoginSuccess, email, "Local/Cloud Database"); } catch { }
                if (dbOffline.Users.Count() <= 1)
                {
                    DatabaseUtils.ClaimLegacyUnownedData(localUser.Id);
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Could not sign in: {ex.Message}");
            }
        }

        public static (bool ok, string error) SignIn(string email, string password)
        {
            return SignInAsync(email, password).GetAwaiter().GetResult();
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
                try { AuditLogService.Log(AuditLogService.Events.PasswordChange, user.Email); } catch { }

                var connStr = CloudDbConfig.GetConnectionString();
                if (!string.IsNullOrWhiteSpace(connStr) && SyncService.IsNetworkAvailable())
                {
                    try
                    {
                        OnlineDatabaseService.CloudRegisterUserAsync(connStr, user).GetAwaiter().GetResult();
                    }
                    catch { }
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Could not change the password: {ex.Message}");
            }
        }

        public static async System.Threading.Tasks.Task<(bool ok, string error)> InitiatePasswordResetAsync(string email)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return (false, "Please enter a valid email address.");

            try
            {
                // 1. Try Supabase Auth REST API password recovery
                if (SupabaseAuthService.IsConfigured && SyncService.IsNetworkAvailable())
                {
                    var (supaOk, supaMsg) = await SupabaseAuthService.SendPasswordResetEmailAsync(email);
                    if (supaOk)
                    {
                        var pendingSupa = new PendingPasswordResetRecord
                        {
                            Email = email,
                            OtpCode = string.Empty,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                        };
                        s_pendingPasswordResets[email] = pendingSupa;
                        return (true, supaMsg);
                    }
                }

                // 2. Fallback to local SQLite or cloud app_users check
                using var db = new AppDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    var connStr = CloudDbConfig.GetConnectionString();
                    if (!string.IsNullOrWhiteSpace(connStr) && SyncService.IsNetworkAvailable())
                    {
                        var (cloudOk, cloudUser, _) = await OnlineDatabaseService.CloudGetUserByEmailAsync(connStr, email);
                        if (cloudOk && cloudUser != null)
                        {
                            user = cloudUser;
                            db.Users.Add(user);
                            await db.SaveChangesAsync();
                        }
                    }
                }

                if (user == null)
                    return (false, "No registered account found with that email address.");

                var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                var pending = new PendingPasswordResetRecord
                {
                    Email = email,
                    OtpCode = otpCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };

                s_pendingPasswordResets[email] = pending;

                var (sentOk, sendMsg) = await EmailOtpService.SendPasswordResetOtpEmailAsync(email, user.FullName, otpCode);
                if (!sentOk)
                {
                    return (false, $"Could not send password reset email: {sendMsg}");
                }

                return (true, sendMsg);
            }
            catch (Exception ex)
            {
                return (false, $"Password reset request failed: {ex.Message}");
            }
        }

        public static async System.Threading.Tasks.Task<(bool ok, string error)> ResendPasswordResetOtpAsync(string email)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !s_pendingPasswordResets.TryGetValue(email, out var pending))
            {
                return (false, "No active password reset request found. Please re-enter your email.");
            }

            if (SupabaseAuthService.IsConfigured)
            {
                var (supaOk, supaMsg) = await SupabaseAuthService.SendPasswordResetEmailAsync(email);
                return (supaOk, supaMsg);
            }

            using var db = new AppDbContext();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            string fullName = user?.FullName ?? "User";

            var newOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            pending.OtpCode = newOtp;
            pending.ExpiresAt = DateTime.UtcNow.AddMinutes(10);

            var (sentOk, sendMsg) = await EmailOtpService.SendPasswordResetOtpEmailAsync(email, fullName, newOtp);
            if (!sentOk)
            {
                return (false, $"Failed to resend reset code: {sendMsg}");
            }

            return (true, sendMsg);
        }

        public static async System.Threading.Tasks.Task<(bool ok, string error)> ConfirmPasswordResetAsync(string email, string userOtp, string newPassword)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            userOtp = (userOtp ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(userOtp) || userOtp.Length != 6)
                return (false, "Please enter the complete 6-digit verification code.");
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "New password must be at least 6 characters.");

            if (!s_pendingPasswordResets.TryGetValue(email, out var pending))
                return (false, "Password reset session expired. Please start the password reset process again.");

            if (DateTime.UtcNow > pending.ExpiresAt)
                return (false, "Verification code has expired. Please click 'Resend Code' for a new code.");

            if (SupabaseAuthService.IsConfigured && SyncService.IsNetworkAvailable())
            {
                var (supaVerifyOk, supaVerifyMsg, supaAccessToken) = await SupabaseAuthService.VerifyOtpAsync(email, userOtp, "recovery");
                if (!supaVerifyOk)
                {
                    if (!string.IsNullOrWhiteSpace(pending.OtpCode) && string.Equals(pending.OtpCode, userOtp, StringComparison.Ordinal))
                    {
                        // Fallback match for local OTP code
                    }
                    else
                    {
                        return (false, supaVerifyMsg);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(supaAccessToken))
                {
                    var (updateOk, updateMsg) = await SupabaseAuthService.UpdateUserPasswordAsync(supaAccessToken, newPassword);
                    if (!updateOk)
                    {
                        Console.WriteLine("Supabase Auth password update warning: " + updateMsg);
                    }
                }
            }
            else
            {
                if (!string.Equals(pending.OtpCode, userOtp, StringComparison.Ordinal))
                    return (false, "Incorrect verification code. Please check your email inbox.");
            }

            try
            {
                using var db = new AppDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

                var salt = RandomNumberGenerator.GetBytes(SaltBytes);
                string saltBase64 = Convert.ToBase64String(salt);
                int iterations = DefaultIterations;
                string hashBase64 = Convert.ToBase64String(
                    Rfc2898DeriveBytes.Pbkdf2(newPassword, salt, iterations, HashAlgorithmName.SHA256, HashBytes));

                if (user != null)
                {
                    user.PasswordSalt = saltBase64;
                    user.PasswordIterations = iterations;
                    user.PasswordHash = hashBase64;
                    await db.SaveChangesAsync();
                }

                var connStr = CloudDbConfig.GetConnectionString();
                if (!string.IsNullOrWhiteSpace(connStr) && SyncService.IsNetworkAvailable())
                {
                    try
                    {
                        var (cloudOk, cloudUser, _) = await OnlineDatabaseService.CloudGetUserByEmailAsync(connStr, email);
                        if (cloudOk && cloudUser != null)
                        {
                            cloudUser.PasswordSalt = saltBase64;
                            cloudUser.PasswordIterations = iterations;
                            cloudUser.PasswordHash = hashBase64;
                            await OnlineDatabaseService.CloudRegisterUserAsync(connStr, cloudUser);
                        }
                        else
                        {
                            await RegisterAsync(email, newPassword, "User");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Cloud password reset update exception: " + ex.Message);
                    }
                }

                s_pendingPasswordResets.TryRemove(email, out _);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Password reset failed: {ex.Message}");
            }
        }
    }
}
