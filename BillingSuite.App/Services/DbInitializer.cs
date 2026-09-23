using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BillingSuite.App.Services
{
    public static class DbInitializer
    {
        public static void EnsureCreatedAndSeed(int retryCount = 0)
        {
            // PRO RESTORE: Check for pending restore BEFORE opening any DB connection
            try
            {
                var dbPath = AppPaths.DatabaseFile;
                var pendingPath = AppPaths.PendingRestoreFile;

                if (System.IO.File.Exists(pendingPath))
                {
                    // Force release any potential locks (safety)
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                    // Cleanup old files
                    if (System.IO.File.Exists(dbPath)) System.IO.File.Delete(dbPath);
                    if (System.IO.File.Exists(dbPath + "-wal")) System.IO.File.Delete(dbPath + "-wal");
                    if (System.IO.File.Exists(dbPath + "-shm")) System.IO.File.Delete(dbPath + "-shm");

                    // Swap
                    System.IO.File.Move(pendingPath, dbPath);
                    
                    MessageBox.Show("Database successfully restored from backup. Welcome back!", 
                        "System Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Seed/Merge initial data from billing.db / billing_seed.db so no rows/tables are missing
                EnsureSeedDataImported(dbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Pending restore failed: {ex.Message}");
            }

            if (retryCount > 2)
            {
                throw new InvalidOperationException("Failed to initialize or repair the database after multiple attempts.");
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    // Ensure database is up to date with latest schema
                    try 
                    { 
                        // IRONCLAD SAFETY: Sync legacy databases before migration
                        IroncladSafetySync(db);

                        if (retryCount > 0)
                        {
                            Console.WriteLine("Applying blueprints to fresh database...");
                        }
                        db.Database.Migrate(); 
                    }
                    catch (Exception ex) when (IsOutOfDiskSpace(ex))
                    {
                        // A full volume is not corruption. Without this guard the message reads like a
                        // schema fault and invites a database rebuild in response to a disk-space problem.
                        var msg = BuildDiskFullMessage(ex);
                        Console.WriteLine(msg);
                        throw new InvalidOperationException(msg, ex);
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Blueprint application failed: {ex.Message}";
                        Console.WriteLine(msg);
                        // If migration fails on a fresh start, we must treat it as a corruption
                        if (retryCount == 0) throw new InvalidOperationException(msg, ex);
                    }

                    // Verify all required tables exist
                    try
                    {
                        VerifyTablesExist(db);

                        // Auto-Heal: Hydrate CostPrices for accurate profit reporting
                        // Resolves the bug on the dashboard where Profit == Sales because old records had 0 as CostPrice
                        try
                        {
                            using (var cmd = db.Database.GetDbConnection().CreateCommand())
                            {
                                if (cmd.Connection.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
                                cmd.CommandText = @"
                                    UPDATE InvoiceItems 
                                    SET CostPrice = COALESCE(
                                        (SELECT CostPrice FROM ProductBatches WHERE ProductIdRef = InvoiceItems.ProductIdRef AND CostPrice > 0 ORDER BY CreatedAt DESC LIMIT 1),
                                        (SELECT NewCostPrice FROM Products WHERE Id = InvoiceItems.ProductIdRef AND NewCostPrice > 0),
                                        (SELECT OldCostPrice FROM Products WHERE Id = InvoiceItems.ProductIdRef AND OldCostPrice > 0),
                                        0.0
                                    )
                                    WHERE (CostPrice IS NULL OR CostPrice <= 0) AND ProductIdRef IS NOT NULL;";
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception healEx) { Console.WriteLine("Auto-heal warning: " + healEx.Message); }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Verification failed: Tables are missing after blueprint application.", ex);
                    }

                    // Seed initial data if database is empty
                    if (!db.Products.Any())
                    {
                        db.Products.AddRange(new Product
                        {
                            Barcode = "PENI",
                            Name = "PENIDURE 6 LAC VIAL",
                            Category = "VIAL",
                            OldStock = 11,
                            ExpiryAlertDays = 30,
                            LowStockThreshold = 5
                        }, new Product
                        {
                            Barcode = "MEGA",
                            Name = "MEGAZOL 600 TAB",
                            Category = "TABLET",
                            OldStock = 2,
                            ExpiryAlertDays = 30,
                            LowStockThreshold = 5
                        });
                        db.SaveChanges();
                    }

                    // Ensure all legacy products have a unique SKU (Barcode)
                    EnsureAllProductsHaveSkus(db);
                }
            }
            catch (Exception ex)
            {
                // AGGRESSIVE REPAIR
                if (ex.Message.Contains("missing") || ex.Message.Contains("no such table") || ex.Message.Contains("Verification failed"))
                {
                    try
                    {
                        // Show feedback to user for long operations
                        if (retryCount == 0)
                        {
                            MessageBox.Show("The application is rebuilding its database foundation to ensure stability. This will only take a moment.", 
                                "System Maintenance", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        // 1. Force release of all file locks
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                        // 3. PRESERVE the existing file before rebuilding. Never delete user
                        // data outright: keep a timestamped copy so a false-positive
                        // verification failure can never destroy a good database.
                        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var dbPath = System.IO.Path.Combine(appData, "BillingSuite", "billing.db");
                        if (System.IO.File.Exists(dbPath))
                        {
                            try
                            {
                                var rescuePath = dbPath + ".broken_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                                System.IO.File.Move(dbPath, rescuePath);
                            }
                            catch
                            {
                                // If we cannot move it aside, do NOT delete it.
                                throw new InvalidOperationException(
                                    "Cannot preserve the existing database file; aborting rebuild to protect your data.");
                            }
                        }

                        // 4. Recursive retry
                        EnsureCreatedAndSeed(retryCount + 1);
                        return;
                    }
                    catch (Exception repairEx)
                    {
                        Console.WriteLine($"Critical repair failure: {repairEx.Message}");
                    }
                }

                // Show final error message
                MessageBox.Show($"Database initialization failed.\n\nError: {ex.Message}\n\nTrace: {ex.StackTrace}", 
                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private static void IroncladSafetySync(AppDbContext db)
        {
            try
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();

                // 1. MANDATORY SAFETY BACKUP
                try
                {
                    var dataSource = conn.DataSource;
                    if (!string.IsNullOrEmpty(dataSource) && System.IO.File.Exists(dataSource))
                    {
                        var backupPath = dataSource + ".safety_backup";
                        if (!System.IO.File.Exists(backupPath))
                        {
                            System.IO.File.Copy(dataSource, backupPath, true);
                        }
                    }
                }
                catch { /* Backup failure shouldn't stop the app but we log it mentally */ }

                // 2. CHECK FOR LEGACY DATABASE
                // If AiChatMessages already exists (user's error case), it's a legacy DB
                bool isLegacy = false;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='AiChatMessages';";
                    isLegacy = cmd.ExecuteScalar() != null;
                }

                // 3. SCHEMA HANDSHAKE. Ensure __EFMigrationsHistory exists and carries the
                // Baseline stamp, so Migrate() treats a legacy database as already migrated
                // instead of trying to CREATE TABLE over tables that already exist.
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20251031120023_Baseline';";
                        bool hasBaseline = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

                        if (!hasBaseline)
                        {
                            cmd.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20251031120023_Baseline', '9.0.10');";
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception handshakeEx)
                {
                    Console.WriteLine($"Schema handshake warning: {handshakeEx.Message}");
                }

                // 4. CLEAR STALE MIGRATION LOCK. EF Core's Migrate() acquires a row in
                // __EFMigrationsLock and waits indefinitely for it. If a previous run was
                // killed mid-migration (crash, taskkill, power loss), that row survives and
                // every later launch hangs forever on startup with no window and no error.
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsLock';";
                        if (cmd.ExecuteScalar() != null)
                        {
                            cmd.CommandText = "DELETE FROM \"__EFMigrationsLock\";";
                            var cleared = cmd.ExecuteNonQuery();
                            if (cleared > 0) Console.WriteLine($"Cleared {cleared} stale migration lock row(s).");
                        }
                    }
                }
                catch (Exception lockEx)
                {
                    Console.WriteLine($"Migration lock cleanup warning: {lockEx.Message}");
                }

                // 5. SAFE PATCH: Add missing columns to ANY database whose physical schema
                // lags the EF model. This must NOT be gated on isLegacy: a database can be
                // stamped with the Baseline migration in __EFMigrationsHistory while its
                // Invoices table still lacks the Customer*Snapshot columns, in which case
                // Migrate() treats Baseline as applied and never repairs them, and every
                // subsequent query throws "no such column: CustomerAddressSnapshot".
                PatchMissingColumns(conn);

                // 6. AUTO-HEAL: Hydrate legacy data into new required fields
                if (isLegacy)
                {
                    ProHydrateAll(conn);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Safety Sync Warning: {ex.Message}");
            }
        }

        private static void PatchMissingColumns(System.Data.Common.DbConnection conn)
        {
            // Create any core baseline table that is entirely absent. A database stamped as
            // Baseline-migrated but physically missing tables would otherwise fail
            // VerifyTablesExist and fall into the destructive rebuild path.
            void EnsureTable(string table, string createSql)
            {
                try
                {
                    using var check = conn.CreateCommand();
                    check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$n;";
                    var p = check.CreateParameter();
                    p.ParameterName = "$n";
                    p.Value = table;
                    check.Parameters.Add(p);
                    if (check.ExecuteScalar() == null)
                    {
                        using var create = conn.CreateCommand();
                        create.CommandText = createSql;
                        create.ExecuteNonQuery();
                        Console.WriteLine($"Created missing table: {table}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Table ensure warning ({table}): {ex.Message}");
                }
            }

            EnsureTable("Users",
                "CREATE TABLE IF NOT EXISTS \"Users\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Users\" PRIMARY KEY AUTOINCREMENT, \"Email\" TEXT NOT NULL DEFAULT '', \"FullName\" TEXT NOT NULL DEFAULT '', \"PasswordHash\" TEXT NOT NULL DEFAULT '', \"PasswordSalt\" TEXT NOT NULL DEFAULT '', \"PasswordIterations\" INTEGER NOT NULL DEFAULT 0, \"Phone\" TEXT NULL, \"Role\" TEXT NULL, \"CompanyName\" TEXT NULL, \"CompanyAddress\" TEXT NULL, \"CompanyPhone\" TEXT NULL, \"CompanyEmail\" TEXT NULL, \"CompanyTaxNumber\" TEXT NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT '', \"LastLoginAt\" TEXT NULL, \"DisabledAt\" TEXT NULL);");
            EnsureTable("Products",
                "CREATE TABLE IF NOT EXISTS \"Products\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Products\" PRIMARY KEY AUTOINCREMENT, \"Barcode\" TEXT NOT NULL DEFAULT '', \"Name\" TEXT NOT NULL DEFAULT '', \"Description\" TEXT NULL, \"Price\" TEXT NOT NULL DEFAULT '0', \"MRP\" TEXT NOT NULL DEFAULT '0', \"Category\" TEXT NULL, \"Stock\" INTEGER NOT NULL DEFAULT 0, \"Hsn\" TEXT NULL, \"MarketedBy\" TEXT NULL, \"ExpiryAlertDays\" INTEGER NULL, \"LowStockThreshold\" REAL NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT '', \"UpdatedAt\" TEXT NULL, \"OwnerUserId\" INTEGER NULL);");
            EnsureTable("Customers",
                "CREATE TABLE IF NOT EXISTS \"Customers\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Customers\" PRIMARY KEY AUTOINCREMENT, \"Name\" TEXT NOT NULL DEFAULT '', \"Email\" TEXT NULL, \"Phone\" TEXT NULL, \"Address1\" TEXT NULL, \"Address2\" TEXT NULL, \"City\" TEXT NULL, \"State\" TEXT NULL, \"PostalCode\" TEXT NULL, \"Country\" TEXT NULL, \"GstVatNumber\" TEXT NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT '', \"UpdatedAt\" TEXT NULL, \"OwnerUserId\" INTEGER NULL);");
            EnsureTable("Invoices",
                "CREATE TABLE IF NOT EXISTS \"Invoices\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Invoices\" PRIMARY KEY AUTOINCREMENT, \"InvoiceNumber\" TEXT NOT NULL DEFAULT '', \"InvoiceDate\" TEXT NOT NULL DEFAULT '', \"DueDate\" TEXT NULL, \"CustomerId\" INTEGER NULL, \"Status\" INTEGER NOT NULL DEFAULT 0, \"Subtotal\" TEXT NOT NULL DEFAULT '0', \"TaxAmount\" TEXT NOT NULL DEFAULT '0', \"DiscountAmount\" TEXT NOT NULL DEFAULT '0', \"Total\" TEXT NOT NULL DEFAULT '0', \"TotalPaid\" TEXT NOT NULL DEFAULT '0', \"BackDues\" TEXT NOT NULL DEFAULT '0', \"PrivateNotes\" TEXT NULL, \"CustomerNameSnapshot\" TEXT NULL, \"CustomerPhoneSnapshot\" TEXT NULL, \"CustomerAddressSnapshot\" TEXT NULL, \"CustomerGstSnapshot\" TEXT NULL, \"CreatedAt\" TEXT NOT NULL DEFAULT '', \"UpdatedAt\" TEXT NULL, \"OwnerUserId\" INTEGER NULL);");
            EnsureTable("InvoiceItems",
                "CREATE TABLE IF NOT EXISTS \"InvoiceItems\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_InvoiceItems\" PRIMARY KEY AUTOINCREMENT, \"InvoiceId\" INTEGER NOT NULL DEFAULT 0, \"ProductIdRef\" INTEGER NULL, \"ProductId\" TEXT NOT NULL DEFAULT '', \"Description\" TEXT NOT NULL DEFAULT '', \"Price\" TEXT NOT NULL DEFAULT '0', \"CostPrice\" REAL NULL, \"Quantity\" TEXT NOT NULL DEFAULT '0', \"LineTotal\" TEXT NOT NULL DEFAULT '0', \"ProductName\" TEXT NOT NULL DEFAULT '', \"BatchNumber\" TEXT NULL, \"OwnerUserId\" INTEGER NULL);");
            EnsureTable("Payments",
                "CREATE TABLE IF NOT EXISTS \"Payments\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Payments\" PRIMARY KEY AUTOINCREMENT, \"InvoiceId\" INTEGER NOT NULL DEFAULT 0, \"Date\" TEXT NOT NULL DEFAULT '', \"Method\" TEXT NULL, \"Amount\" TEXT NOT NULL DEFAULT '0', \"Notes\" TEXT NULL, \"OwnerUserId\" INTEGER NULL);");
            // Key/value store for user settings. Created here for the same reason as the
            // tables above: the migration chain is squashed, so EnsureTable is what makes
            // the schema true on an existing database.
            EnsureTable("AppSettings",
                "CREATE TABLE IF NOT EXISTS \"AppSettings\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_AppSettings\" PRIMARY KEY AUTOINCREMENT, \"Key\" TEXT NOT NULL DEFAULT '', \"Value\" TEXT NULL, \"UpdatedAt\" TEXT NOT NULL DEFAULT '');");

            void AddColumn(string table, string column, string type)
            {
                try
                {
                    using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = $"PRAGMA table_info(\"{table}\");";
                    bool exists = false;
                    using (var rdr = checkCmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            if (string.Equals(rdr[1]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }
                    }

                    if (!exists)
                    {
                        using var addCmd = conn.CreateCommand();
                        addCmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type};";
                        addCmd.ExecuteNonQuery();
                    }
                }
                catch { }
            }

            // Patch Invoice Snapshots and ledger fields the EF model expects but
            // which legacy databases created before the snapshot feature do not have.
            AddColumn("Invoices", "BackDues", "TEXT NOT NULL DEFAULT '0'");
            AddColumn("Invoices", "CustomerNameSnapshot", "TEXT NULL");
            AddColumn("Invoices", "CustomerPhoneSnapshot", "TEXT NULL");
            AddColumn("Invoices", "CustomerAddressSnapshot", "TEXT NULL");
            AddColumn("Invoices", "CustomerGstSnapshot", "TEXT NULL");

            // Patch InvoiceItems Hidden Fields
            AddColumn("InvoiceItems", "ProductName", "TEXT NOT NULL DEFAULT ''");
            AddColumn("InvoiceItems", "BatchNumber", "TEXT NULL");
            AddColumn("InvoiceItems", "CostPrice", "REAL NULL");

            // Patch ProductBatch ExpiredStock
            AddColumn("ProductBatches", "ExpiredStock", "REAL NULL");

            // Patch Product Legacy Fields (Just in case)
            AddColumn("Products", "ExpiryAlertDays", "INTEGER NULL");
            AddColumn("Products", "LowStockThreshold", "REAL NULL");
            AddColumn("Products", "Hsn", "TEXT NULL");
            AddColumn("Products", "MarketedBy", "TEXT NULL");

            // Patch Financial Fields in Batches and Purchase Items
            foreach (var table in new[] { "ProductBatches", "PurchaseItems" })
            {
                AddColumn(table, "Rate", "REAL NULL");
                AddColumn(table, "DiscountPercent", "REAL NULL");
                AddColumn(table, "CgstPercent", "REAL NULL");
                AddColumn(table, "SgstPercent", "REAL NULL");
                AddColumn(table, "IgstPercent", "REAL NULL");
            }

            // Patch Purchase Summary
            AddColumn("Purchases", "TotalDiscount", "REAL NULL");
            AddColumn("Purchases", "TotalCgst", "REAL NULL");
            AddColumn("Purchases", "TotalSgst", "REAL NULL");
            AddColumn("Purchases", "TotalIgst", "REAL NULL");

            // Phase 8: per-user ownership column on every business data table.
            foreach (var table in new[] { "Products", "ProductBatches", "Customers", "Invoices", "InvoiceItems", "Payments",
                "Suppliers", "Purchases", "PurchaseItems", "PurchasePayments", "RecycleBin", "DamagedItems", "Expenses", "AiChatMessages" })
            {
                AddColumn(table, "OwnerUserId", "INTEGER NULL");
            }
        }

        private static void ProHydrateAll(System.Data.Common.DbConnection conn)
        {
            // Each hydration step is isolated: a stale column/table in one step must
            // never abort the remaining repairs (that is what broke startup).
            RunHydrationStep(conn, "MarketedBy", @"
                UPDATE Products 
                SET MarketedBy = (SELECT MarketedBy FROM ProductBatches WHERE ProductIdRef = Products.Id AND MarketedBy IS NOT NULL LIMIT 1)
                WHERE MarketedBy IS NULL OR MarketedBy = '';");

            // InvoiceItems.ProductId holds the product Barcode (SKU); ProductIdRef is the numeric FK.
            RunHydrationStep(conn, "InvoiceItems", @"
                UPDATE InvoiceItems 
                SET ProductName = COALESCE(NULLIF(ProductName, ''), (SELECT Name FROM Products WHERE Barcode = InvoiceItems.ProductId)),
                    ProductIdRef = COALESCE(ProductIdRef, (SELECT Id FROM Products WHERE Barcode = InvoiceItems.ProductId)),
                    CostPrice = COALESCE(CostPrice, 0.0)
                WHERE (ProductName IS NULL OR ProductName = '') AND ProductId IS NOT NULL;");

            // Customer columns are Address1/Address2 and GstVatNumber (NOT Address/GstNumber).
            // Build the address snapshot from whichever address columns actually exist.
            var addressExpr = HydrationPickColumn(conn, "Customers",
                new[] { "Address1", "Address", "AddressLine1" }, "''");
            var gstExpr = HydrationPickColumn(conn, "Customers",
                new[] { "GstVatNumber", "GstNumber", "GSTNumber", "Gst" }, "''");

            RunHydrationStep(conn, "InvoiceSnapshots", $@"
                UPDATE Invoices 
                SET CustomerNameSnapshot = (SELECT Name FROM Customers WHERE Id = Invoices.CustomerId),
                    CustomerPhoneSnapshot = (SELECT Phone FROM Customers WHERE Id = Invoices.CustomerId),
                    CustomerAddressSnapshot = (SELECT {addressExpr} FROM Customers WHERE Id = Invoices.CustomerId),
                    CustomerGstSnapshot = (SELECT {gstExpr} FROM Customers WHERE Id = Invoices.CustomerId)
                WHERE (CustomerNameSnapshot IS NULL OR CustomerNameSnapshot = '') AND CustomerId IS NOT NULL;");

            if (TableExists(conn, "PurchaseItems"))
            {
                // The PurchaseItem model uses ProductId; older/other schemas used ProductIdRef.
                var purchaseFk = HydrationPickColumn(conn, "PurchaseItems",
                    new[] { "ProductId", "ProductIdRef" }, null);
                if (purchaseFk != null)
                {
                    RunHydrationStep(conn, "PurchaseItems", $@"
                        UPDATE PurchaseItems 
                        SET ProductName = (SELECT Name FROM Products WHERE Id = PurchaseItems.{purchaseFk})
                        WHERE (ProductName IS NULL OR ProductName = '') AND {purchaseFk} IS NOT NULL;");
                }
            }
        }

        private static bool TableExists(System.Data.Common.DbConnection conn, string table)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$n;";
                var p = cmd.CreateParameter();
                p.ParameterName = "$n";
                p.Value = table;
                cmd.Parameters.Add(p);
                return cmd.ExecuteScalar() != null;
            }
            catch { return false; }
        }

        // Returns the first candidate column that exists on the table, else the fallback expression.
        private static string HydrationPickColumn(System.Data.Common.DbConnection conn, string table, string[] candidates, string fallback)
        {
            var present = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var name = rdr[1]?.ToString();
                    if (!string.IsNullOrEmpty(name)) present.Add(name);
                }
            }
            catch { return fallback; }

            foreach (var c in candidates)
            {
                if (present.Contains(c)) return $"\"{c}\"";
            }
            return fallback;
        }

        private static void RunHydrationStep(System.Data.Common.DbConnection conn, string label, string sql)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hydration Warning ({label}): {ex.Message}");
            }
        }

        /// <summary>
        /// Detects SQLITE_FULL / "database or disk is full". SQLite raises this on the first write
        /// when the volume has no free space, which is a storage problem, not a schema problem.
        /// </summary>
        internal static bool IsOutOfDiskSpace(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                var m = e.Message;
                if (m == null) continue;
                if (m.IndexOf("disk is full", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (m.IndexOf("database or disk is full", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (m.IndexOf("SQLITE_FULL", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (m.IndexOf("not enough space", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string BuildDiskFullMessage(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Database could not be opened: the drive holding your data is out of free space.");
            sb.AppendLine($"Data folder : {AppPaths.DataFolder}");
            sb.AppendLine($"Database    : {AppPaths.DatabaseFile}");

            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(AppPaths.DataFolder));
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    sb.AppendLine($"Drive {root} has {drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0):F2} GB free of {drive.TotalSize / (1024.0 * 1024.0 * 1024.0):F2} GB.");
                }
            }
            catch
            {
                // DriveInfo is best-effort diagnostics only; never mask the storage error itself.
            }

            sb.AppendLine("Free up disk space and restart. Do NOT delete or rebuild the database.");
            sb.AppendLine($"Underlying error: {ex.Message}");
            return sb.ToString();
        }

        private static void VerifyTablesExist(AppDbContext db)
        {
            // This will throw if the table/columns don't exist
            db.Products.FirstOrDefault();
            db.Customers.FirstOrDefault();
            db.Invoices.FirstOrDefault();
            db.InvoiceItems.FirstOrDefault();
            db.Payments.FirstOrDefault();
        }

        private static void EnsureAllProductsHaveSkus(AppDbContext db)
        {
            try
            {
                var missing = db.Products.Where(p => string.IsNullOrEmpty(p.Barcode)).ToList();
                if (missing.Count == 0) return;

                long maxSku = 10000;
                var allBarcodes = db.Products.Select(p => p.Barcode).ToList();
                foreach (var bc in allBarcodes)
                {
                    if (long.TryParse(bc, out long val))
                    {
                        if (val > maxSku) maxSku = val;
                    }
                }

                foreach (var p in missing)
                {
                    maxSku++;
                    p.Barcode = maxSku.ToString();
                }

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SKU Backfill Error: {ex.Message}");
            }
        }

        private static void EnsureSeedDataImported(string liveDbPath)
        {
            try
            {
                string? seedPath = null;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (File.Exists(Path.Combine(baseDir, "billing_seed.db")))
                    seedPath = Path.Combine(baseDir, "billing_seed.db");
                else if (File.Exists(Path.Combine(baseDir, "billing.db")))
                    seedPath = Path.Combine(baseDir, "billing.db");
                else if (File.Exists(@"D:\billease-pro\billease bro\billing.db"))
                    seedPath = @"D:\billease-pro\billease bro\billing.db";

                if (string.IsNullOrEmpty(seedPath) || !File.Exists(seedPath)) return;

                if (!File.Exists(liveDbPath))
                {
                    var liveDir = Path.GetDirectoryName(liveDbPath);
                    if (!string.IsNullOrEmpty(liveDir) && !Directory.Exists(liveDir))
                        Directory.CreateDirectory(liveDir);
                    File.Copy(seedPath, liveDbPath, true);
                    Console.WriteLine($"Initialized live database from seed file: {seedPath}");
                    return;
                }

                // If liveDbPath exists, perform safe merge of missing rows from seedPath using ATTACH DATABASE
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={liveDbPath}");
                conn.Open();

                using (var attachCmd = conn.CreateCommand())
                {
                    attachCmd.CommandText = $"ATTACH DATABASE '{seedPath.Replace("'", "''")}' AS seedDb;";
                    attachCmd.ExecuteNonQuery();
                }

                string[] mergeTables = new[]
                {
                    "Customers", "Products", "ProductBatches", "Invoices", "InvoiceItems",
                    "Payments", "Suppliers", "Purchases", "PurchaseItems", "PurchasePayments",
                    "Expenses", "DamagedItems", "RecycleBin", "AiChatMessages"
                };

                foreach (var table in mergeTables)
                {
                    try
                    {
                        using var chkCmd = conn.CreateCommand();
                        chkCmd.CommandText = $"SELECT COUNT(*) FROM seedDb.sqlite_master WHERE type='table' AND name='{table}';";
                        if (Convert.ToInt32(chkCmd.ExecuteScalar()) == 0) continue;

                        using var mergeCmd = conn.CreateCommand();
                        mergeCmd.CommandText = $"INSERT OR IGNORE INTO main.\"{table}\" SELECT * FROM seedDb.\"{table}\";";
                        int inserted = mergeCmd.ExecuteNonQuery();
                        if (inserted > 0)
                        {
                            Console.WriteLine($"Merged {inserted} missing records into {table} from seed database.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Merge table warning ({table}): {ex.Message}");
                    }
                }

                using (var detachCmd = conn.CreateCommand())
                {
                    detachCmd.CommandText = "DETACH DATABASE seedDb;";
                    detachCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EnsureSeedDataImported warning: {ex.Message}");
            }
        }
    }
}
