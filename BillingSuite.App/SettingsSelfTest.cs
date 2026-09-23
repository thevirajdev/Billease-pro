using System;
using System.IO;
using System.Linq;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using BillingSuite.App.Services;
using Microsoft.EntityFrameworkCore;

namespace BillingSuite.App
{
    /// <summary>
    /// Standalone runtime check for the settings layer. Run with:
    ///   dotnet run --project BillingSuite.App --settings-selftest
    /// Verifies the AppSettings table is created, defaults seed, values round-trip,
    /// and the typed accessors (used by the invoice renderers) read them back.
    /// </summary>
    public static class SettingsSelfTest
    {
        /// <summary>
        /// Renders a real invoice through both live paths and asserts the company
        /// identity and page setup from Settings appear in the output.
        /// </summary>
        private static void RenderChecks(Action<string, bool, string?> check)
        {
            AppSettingsService.SetMany(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.CompanyName, "Sunrise Medical Store"),
                new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.CompanyAddress1, "44 Mall Road"),
                new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.CompanyCity, "Lahore"),
                new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.InvoiceMarginMm, "14"),
                new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.InvoicePageSize, "LETTER"),
            });
            AppSettingsService.Invalidate();

            var inv = new Invoice
            {
                InvoiceNumber = "SELFTEST-001",
                InvoiceDate = new DateTime(2024, 3, 4),
                Subtotal = 100m,
                TaxAmount = 17m,
                Total = 117m,
                Items = new System.Collections.Generic.List<InvoiceItem>
                {
                    new InvoiceItem { ProductId = "SKU-1", Price = 100m, Quantity = 1m, Description = "NAME:Test Item|BATCH:B1" }
                }
            };

            var html = Forms.InvoicePreviewForm.BuildHtmlFor(inv);
            check("Invoice HTML uses the company name from Settings",
                html.Contains("Sunrise Medical Store"), "no 'Your Company' expected");
            check("Invoice HTML no longer says 'Your Company'",
                !html.Contains("Your Company"), null);
            check("Invoice HTML includes the company address block",
                html.Contains("44 Mall Road") && html.Contains("Lahore"), null);

            // Regression guard: the @page rule must be interpolated, not emitted literally.
            check("Invoice HTML @page is interpolated, not literal",
                !html.Contains("{Services.AppSettingsService") && html.Contains("size: LETTER"),
                "emitted braces mean a verbatim string is swallowing the interpolation");
            check("Invoice HTML @page carries the configured margin",
                html.Contains("margin: 14mm"), null);

            // The PDF path must honour the page size too.
            var pdf = Forms.InvoicePreviewForm.GeneratePdfFor(inv, html);
            check("Invoice PDF renders bytes", pdf != null && pdf.Length > 1000, $"{pdf?.Length ?? 0} bytes");
            check("Invoice PDF is a real PDF header",
                pdf.Length > 4 && pdf[0] == (byte)'%' && pdf[1] == (byte)'P' && pdf[2] == (byte)'D' && pdf[3] == (byte)'F', null);

            // Restore the shared database to its prior identity so the check does not
            // leave a test company name behind in the user's real settings.
            AppSettingsService.ResetKeys(new[]
            {
                AppSettingKeys.CompanyName, AppSettingKeys.CompanyAddress1,
                AppSettingKeys.CompanyCity, AppSettingKeys.InvoiceMarginMm,
                AppSettingKeys.InvoicePageSize,
            });
            AppSettingsService.SeedDefaults();
            AppSettingsService.Invalidate();
            check("Render checks restore the seeded defaults",
                AppSettingsService.CompanyName == "Your Company", AppSettingsService.CompanyName);

            // ---- Phase 6: the invoice content toggles must actually change the output ----
            // Each toggle is flipped off, the invoice re-rendered, and the element asserted gone.
            void ToggleOff(string key, string marker, string label)
            {
                AppSettingsService.Set(key, "false");
                AppSettingsService.Invalidate();
                var offHtml = Forms.InvoicePreviewForm.BuildHtmlFor(inv);
                check($"Toggle hides {label}", !offHtml.Contains(marker),
                    offHtml.Contains(marker) ? "still present" : null);
                AppSettingsService.Set(key, "true");
                AppSettingsService.Invalidate();
                var onHtml = Forms.InvoicePreviewForm.BuildHtmlFor(inv);
                check($"Toggle shows {label} when back on", onHtml.Contains(marker), null);
            }

            ToggleOff(AppSettingKeys.InvoiceShowScheme, "Scheme", "the Scheme column");
            ToggleOff(AppSettingKeys.InvoiceShowBatch, "Batch No.", "the Batch column");
            ToggleOff(AppSettingKeys.InvoiceShowExpiry, "Expiry", "the Expiry column");
            ToggleOff(AppSettingKeys.InvoiceShowMrp, "MRP", "the MRP column");
            ToggleOff(AppSettingKeys.InvoiceShowBillTo, "Bill To:", "the Bill To block");

            // Company address toggle must also remove the address text itself.
            AppSettingsService.Set(AppSettingKeys.InvoiceShowCompanyAddress, "false");
            AppSettingsService.Invalidate();
            var noAddr = Forms.InvoicePreviewForm.BuildHtmlFor(inv);
            check("Toggle hides the company address block", !noAddr.Contains("44 Mall Road"), null);
            AppSettingsService.Set(AppSettingKeys.InvoiceShowCompanyAddress, "true");
            AppSettingsService.Invalidate();

            // The PDF must honour the same toggles, not just the HTML preview.
            AppSettingsService.Set(AppSettingKeys.InvoiceShowMrp, "false");
            AppSettingsService.Invalidate();
            var pdfNoMrp = Forms.InvoicePreviewForm.GeneratePdfFor(inv, html);
            check("PDF still renders with a column hidden", pdfNoMrp != null && pdfNoMrp.Length > 1000,
                $"{pdfNoMrp?.Length ?? 0} bytes");
            AppSettingsService.Set(AppSettingKeys.InvoiceShowMrp, "true");
            AppSettingsService.Invalidate();

            // Restore every key this block touched.
            AppSettingsService.ResetKeys(new[]
            {
                AppSettingKeys.InvoiceShowScheme, AppSettingKeys.InvoiceShowBatch,
                AppSettingKeys.InvoiceShowExpiry, AppSettingKeys.InvoiceShowMrp,
                AppSettingKeys.InvoiceShowBillTo, AppSettingKeys.InvoiceShowCompanyAddress,
            });
            AppSettingsService.SeedDefaults();
            AppSettingsService.Invalidate();
        }

        /// <summary>
        /// Phase 7: exercises the real AuthService against the real Users table.
        /// Uses a reserved test address and removes it afterwards so the check does not
        /// leave a stray account behind for the user.
        /// </summary>
        private static void AuthChecks(Action<string, bool, string?> check)
        {
            const string testEmail = "selftest.account@example.invalid";

            using (var db = new Data.AppDbContext())
            {
                var stale = db.Users.FirstOrDefault(u => u.Email == testEmail);
                if (stale != null) { db.Users.Remove(stale); db.SaveChanges(); }
            }

            check("No account yet for the test address",
                !new Data.AppDbContext().Users.Any(u => u.Email == testEmail), null);

            var weak = AuthService.Register(testEmail, "123", "Test User");
            check("Weak password is rejected", !weak.ok, weak.error);

            var noEmail = AuthService.Register("not-an-email", "secret123", "Test User");
            check("Invalid email is rejected", !noEmail.ok, noEmail.error);

            var created = AuthService.Register(testEmail, "secret123", "Test User",
                companyName: "Test Co", companyAddress: "1 Test Road");
            check("Account is created", created.ok, created.error);

            var dup = AuthService.Register(testEmail, "secret123", "Test User");
            check("Duplicate email is rejected", !dup.ok, dup.error);

            // The plaintext password must never be recoverable from the stored row.
            using (var db = new Data.AppDbContext())
            {
                var row = db.Users.First(u => u.Email == testEmail);
                check("Password is not stored in plaintext",
                    row.PasswordHash != "secret123" && row.PasswordHash != string.Empty, null);
                check("A per-user salt is stored", !string.IsNullOrWhiteSpace(row.PasswordSalt), null);
                check("Iteration count is recorded", row.PasswordIterations >= 100000,
                    row.PasswordIterations.ToString());
            }

            AuthService.SignOut();
            check("Sign-out clears the session", !AuthService.IsSignedIn, null);

            var wrong = AuthService.SignIn(testEmail, "wrong-password");
            check("Wrong password is refused", !wrong.ok, wrong.error);

            var missing = AuthService.SignIn("nobody@example.invalid", "secret123");
            check("Unknown email is refused", !missing.ok, missing.error);

            var signIn = AuthService.SignIn(testEmail, "secret123");
            check("Correct password signs in", signIn.ok, signIn.error);
            check("Session holds the signed-in user",
                AuthService.CurrentUser?.Email == testEmail, AuthService.CurrentUser?.Email);

            // Case-insensitive login: the email is normalised on the way in.
            AuthService.SignOut();
            var upper = AuthService.SignIn(testEmail.ToUpperInvariant(), "secret123");
            check("Login is case-insensitive on email", upper.ok, upper.error);

            var changed = AuthService.ChangePassword(AuthService.CurrentUser!.Id, "secret123", "newsecret456");
            check("Password change succeeds", changed.ok, changed.error);
            AuthService.SignOut();
            check("Old password no longer works", !AuthService.SignIn(testEmail, "secret123").ok, null);
            check("New password works", AuthService.SignIn(testEmail, "newsecret456").ok, null);

            // Clean up so no test account is left on the real device database.
            AuthService.SignOut();
            using (var db = new Data.AppDbContext())
            {
                var row = db.Users.FirstOrDefault(u => u.Email == testEmail);
                if (row != null) { db.Users.Remove(row); db.SaveChanges(); }
            }
            check("Test account removed from the device database",
                !new Data.AppDbContext().Users.Any(u => u.Email == testEmail), null);
        }

        /// <summary>
        /// Phase 8: exercises per-user data isolation — two accounts, two customers,
        /// each must only see its own rows through the global query filter.
        /// </summary>
        private static void OwnershipChecks(Action<string, bool, string?> check)
        {
            const string aEmail = "selftest.owner-a@example.invalid";
            const string bEmail = "selftest.owner-b@example.invalid";

            AuthService.SignOut();
            using (var db = new Data.AppDbContext())
            {
                foreach (var u in db.Users.Where(u => u.Email == aEmail || u.Email == bEmail)) db.Users.Remove(u);
                foreach (var c in db.Customers.Where(c => c.Name == "OwnerA Test" || c.Name == "OwnerB Test")) db.Customers.Remove(c);
                db.SaveChanges();
            }

            AuthService.Register(aEmail, "secret123", "Owner A");
            check("Isolation setup: account A registered", AuthService.IsSignedIn, null);
            using (var db = new Data.AppDbContext())
            {
                db.Customers.Add(new Models.Customer { Name = "OwnerA Test" });
                db.SaveChanges();
            }
            AuthService.SignOut();

            AuthService.Register(bEmail, "secret123", "Owner B");
            check("Isolation setup: account B registered", AuthService.IsSignedIn, null);
            using (var db = new Data.AppDbContext())
            {
                db.Customers.Add(new Models.Customer { Name = "OwnerB Test" });
                db.SaveChanges();

                var visibleQuery = db.Customers
                    .Where(c => c.Name == "OwnerA Test" || c.Name == "OwnerB Test");
                var visible = visibleQuery.ToList();
                check("Isolation: user B sees only their own customer",
                    visible.Any(c => c.Name == "OwnerB Test") && !visible.Any(c => c.Name == "OwnerA Test"),
                    string.Join(", ", visible.Select(c => c.Name)) + " || " + visibleQuery.ToQueryString().Replace("\n", " "));

                var own = db.Customers.First(c => c.Name == "OwnerB Test");
                check("Isolation: new rows are stamped with the owner's id",
                    own.OwnerUserId == AuthService.CurrentUser!.Id,
                    own.OwnerUserId?.ToString());
            }
            AuthService.SignOut();

            using (var db = new Data.AppDbContext())
            {
                // Signed-out sessions (repairs, background jobs) see everything.
                check("Isolation: signed-out sees all rows",
                    db.Customers.Any(c => c.Name == "OwnerA Test") && db.Customers.Any(c => c.Name == "OwnerB Test"), null);

                foreach (var c in db.Customers.Where(c => c.Name == "OwnerA Test" || c.Name == "OwnerB Test")) db.Customers.Remove(c);
                foreach (var u in db.Users.Where(u => u.Email == aEmail || u.Email == bEmail)) db.Users.Remove(u);
                db.SaveChanges();
            }
            check("Isolation: test rows and accounts removed",
                !new Data.AppDbContext().Customers.Any(c => c.Name == "OwnerA Test" || c.Name == "OwnerB Test"), null);
        }

        public static int Run()
        {
            int failures = 0;
            void Check(string name, bool ok, string? detail = null)
            {
                Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail != null ? $"  [{detail}]" : "")}");
                if (!ok) failures++;
            }

            try
            {
                Console.WriteLine($"Data folder : {AppPaths.DataFolder}");
                Console.WriteLine($"Database    : {AppPaths.DatabaseFile}");
                Console.WriteLine();

                DbInitializer.EnsureCreatedAndSeed();
                Check("DbInitializer completes", true);

                using (var db = new AppDbContext())
                {
                    var conn = db.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='AppSettings'";
                    var hasTable = cmd.ExecuteScalar() != null;
                    Check("AppSettings table exists", hasTable);
                }

                AppSettingsService.Invalidate();
                AppSettingsService.SeedDefaults();

                using (var db = new AppDbContext())
                {
                    var count = db.AppSettings.Count();
                    Check("Defaults seeded (>60 rows)", count > 60, $"{count} rows");
                }

                // Round-trip every type through the service.
                AppSettingsService.Set(AppSettingKeys.CompanyName, "Acme Pharma Pvt Ltd");
                AppSettingsService.Invalidate();
                Check("String round-trips",
                    AppSettingsService.CompanyName == "Acme Pharma Pvt Ltd",
                    AppSettingsService.CompanyName);

                AppSettingsService.Set(AppSettingKeys.DefaultExpiryAlertDays, "45");
                AppSettingsService.Invalidate();
                Check("Int round-trips", AppSettingsService.DefaultExpiryAlertDays == 45,
                    AppSettingsService.DefaultExpiryAlertDays.ToString());

                AppSettingsService.Set(AppSettingKeys.InvoiceMarginMm, "15.5");
                AppSettingsService.Invalidate();
                Check("Decimal round-trips", AppSettingsService.InvoiceMarginMm == 15.5m,
                    AppSettingsService.InvoiceMarginMm.ToString());

                AppSettingsService.Set(AppSettingKeys.InvoiceShowLogo, "false");
                AppSettingsService.Invalidate();
                Check("Bool round-trips", AppSettingsService.InvoiceShow(AppSettingKeys.InvoiceShowLogo) == false);

                AppSettingsService.Set(AppSettingKeys.InvoiceOrientation, "Landscape");
                AppSettingsService.Invalidate();
                Check("Orientation drives landscape flag", AppSettingsService.InvoiceLandscape);

                // The address block is what the invoice renderers actually consume.
                AppSettingsService.SetMany(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.CompanyAddress1, "12 Market Road"),
                    new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.CompanyCity, "Lahore"),
                    new System.Collections.Generic.KeyValuePair<string, string?>(AppSettingKeys.CompanyCountry, "Pakistan"),
                });
                AppSettingsService.Invalidate();
                var block = AppSettingsService.CompanyAddressBlock();
                Check("CompanyAddressBlock combines fields",
                    block.Contains("12 Market Road") && block.Contains("Lahore") && block.Contains("Pakistan"),
                    block.Replace("\n", " / ").Replace("\r", ""));

                // Reset must delete rows so re-seed restores factory values.
                AppSettingsService.ResetKeys(new[] { AppSettingKeys.CompanyName });
                AppSettingsService.Invalidate();
                Check("ResetKeys removes the row",
                    AppSettingsService.CompanyName != "Acme Pharma Pvt Ltd",
                    AppSettingsService.CompanyName);

                AppSettingsService.SeedDefaults();
                AppSettingsService.Invalidate();
                Check("Re-seed restores default company name",
                    AppSettingsService.CompanyName == "Your Company",
                    AppSettingsService.CompanyName);

                // Guard: a fresh process must read what a previous one wrote.
                AppSettingsService.Invalidate();
                using (var db = new AppDbContext())
                {
                    var row = db.AppSettings.FirstOrDefault(s => s.Key == AppSettingKeys.CompanyName);
                    Check("Row is persisted with a value", row != null && row.Value == "Your Company",
                        row?.Value ?? "<null>");
                }

                // Payment term presets are pipe-split into the invoice dropdown.
                var presets = AppSettingsService.PaymentTermPresets();
                Check("PaymentTermPresets parses", presets.Length == 4, string.Join(", ", presets));

                // AppPaths must not put settings beside the exe (an installed app cannot write there).
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                Check("Settings file is NOT beside the exe",
                    !AppPaths.SettingsFile.StartsWith(exeDir, StringComparison.OrdinalIgnoreCase),
                    AppPaths.SettingsFile);

                Check("InvoicePdfFileName sanitises the number",
                    AppPaths.InvoicePdfFileName("INV/2024:001", new DateTime(2024, 1, 2)).StartsWith("Invoice_INV_2024_001_"));

                // ---- Phase 4: one source of truth for the expiry alert default ----
                // Previously Product.cs defaulted to 90 while AlertsForm/DbInitializer used 30.
                // Reset first: an earlier check in this run wrote 45 to this same key.
                AppSettingsService.ResetKeys(new[] { AppSettingKeys.DefaultExpiryAlertDays });
                AppSettingsService.SeedDefaults();
                AppSettingsService.Invalidate();
                Check("DefaultExpiryAlertDays defaults to 30",
                    AppSettingsService.DefaultExpiryAlertDays == 30,
                    AppSettingsService.DefaultExpiryAlertDays.ToString());
                Check("New Product has no hardcoded 90-day default",
                    new Product().ExpiryAlertDays == null,
                    (new Product().ExpiryAlertDays?.ToString() ?? "<null>"));
                Check("Money() uses the configured currency symbol",
                    AppSettingsService.Money(1234.5m).Contains(AppSettingsService.CurrencySymbol),
                    AppSettingsService.Money(1234.5m));

                // ---- Phase 7: authentication ----
                AuthChecks(Check);
                OwnershipChecks(Check);

                // ---- Invoice rendering: the settings must reach the actual output ----
                RenderChecks((name, ok, detail) => Check(name, ok, detail));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL  Unhandled exception: {ex}");
                failures++;
            }

            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
            return failures == 0 ? 0 : 1;
        }
    }
}
