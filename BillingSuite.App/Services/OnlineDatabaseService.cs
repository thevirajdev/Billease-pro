using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BillingSuite.App.Data;
using BillingSuite.App.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Service for connecting to an online PostgreSQL/Supabase database,
    /// creating schema, and performing bidirectional UPSERT synchronization.
    /// </summary>
    public static class OnlineDatabaseService
    {
        /// <summary>
        /// Normalizes raw connection strings (supporting postgresql:// URI format,
        /// Supabase direct strings, and standard ADO.NET key-value pairs).
        /// Ensures SSL Mode=Require and timeouts are set properly.
        /// </summary>
        public static string NormalizeConnectionString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            raw = raw.Trim();

            // 1. URI Format (postgres:// or postgresql://)
            if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(raw);
                    var userInfo = uri.UserInfo.Split(':');
                    var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
                    var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                    var db = uri.AbsolutePath.TrimStart('/');
                    if (string.IsNullOrEmpty(db)) db = "postgres";
                    var port = uri.Port > 0 ? uri.Port : 5432;

                    return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;Timeout=15;Command Timeout=30;";
                }
                catch { }
            }

            // 2. Libpq Space-Delimited Format (host=... port=... dbname=... user=... password=...)
            if (!raw.Contains(';') && raw.Contains('='))
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var kv = part.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var key = kv[0].Trim().ToLowerInvariant();
                            var val = kv[1].Trim();
                            string normKey = key switch
                            {
                                "host" => "Host",
                                "port" => "Port",
                                "dbname" => "Database",
                                "database" => "Database",
                                "user" => "Username",
                                "username" => "Username",
                                "password" => "Password",
                                "pass" => "Password",
                                "sslmode" => "SSL Mode",
                                "ssl_mode" => "SSL Mode",
                                _ => key
                            };
                            if (normKey == "SSL Mode" && (val.Equals("require", StringComparison.OrdinalIgnoreCase) || val.Equals("prefer", StringComparison.OrdinalIgnoreCase)))
                                val = "Require";
                            sb.Append($"{normKey}={val};");
                        }
                    }
                    if (sb.Length > 0)
                    {
                        raw = sb.ToString();
                    }
                }
                catch { }
            }

            // 3. Ensure Npgsql ADO.NET requirements
            try
            {
                var csb = new NpgsqlConnectionStringBuilder(raw);
                if (!raw.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase))
                    csb.SslMode = SslMode.Require;
                if (!raw.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
                    csb.Timeout = 30;
                if (!raw.Contains("Command Timeout", StringComparison.OrdinalIgnoreCase))
                    csb.CommandTimeout = 90;
                csb.KeepAlive = 15;
                csb.TcpKeepAlive = true;
                csb.ConnectionIdleLifetime = 15;
                csb.ConnectionPruningInterval = 10;
                return csb.ConnectionString;
            }
            catch
            {
                return raw;
            }
        }

        /// <summary>
        /// Tests the online PostgreSQL connection string and retrieves the server version.
        /// </summary>
        public static async Task<(bool Success, string Message)> TestConnectionAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                var csb = new NpgsqlConnectionStringBuilder(normalized)
                {
                    Timeout = 12,
                    CommandTimeout = 12
                };

                await using var conn = new NpgsqlConnection(csb.ConnectionString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand("SELECT version();", conn);
                var version = (await cmd.ExecuteScalarAsync())?.ToString() ?? "Unknown PostgreSQL";

                var shortVersion = version.Split('\n')[0];
                if (shortVersion.Length > 80) shortVersion = shortVersion.Substring(0, 80) + "...";

                return (true, $"Connected successfully to PostgreSQL!\n{shortVersion}");
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }

        private static volatile bool _schemaEnsured = false;

        /// <summary>
        /// Ensures all necessary tables, columns, and indexes exist on the online Supabase PostgreSQL database.
        /// Covers 100% of all 20 tables and columns present in billing.db.
        /// Cached in memory so it only runs once per process lifetime unless force=true is requested.
        /// </summary>
        public static async Task<(bool Success, string Message)> EnsureSchemaAsync(string connectionString, bool force = false)
        {
            if (_schemaEnsured && !force)
                return (true, "Cloud database schema already verified.");

            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                var csb = new NpgsqlConnectionStringBuilder(normalized)
                {
                    Timeout = 15,
                    CommandTimeout = 30
                };

                await using var conn = new NpgsqlConnection(csb.ConnectionString);
                await conn.OpenAsync();

                var ddl = @"
                -- 1. app_users
                CREATE TABLE IF NOT EXISTS app_users (
                    id SERIAL PRIMARY KEY,
                    email VARCHAR(255) NOT NULL UNIQUE,
                    password_hash TEXT NOT NULL,
                    salt TEXT NOT NULL,
                    iterations INT NOT NULL DEFAULT 210000,
                    full_name VARCHAR(255),
                    phone VARCHAR(50),
                    company_name VARCHAR(255),
                    company_address TEXT,
                    company_phone VARCHAR(50),
                    company_email VARCHAR(255),
                    company_tax_number VARCHAR(100),
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    last_login_at TIMESTAMPTZ
                );

                -- 2. customers
                CREATE TABLE IF NOT EXISTS customers (
                    id INT PRIMARY KEY,
                    name VARCHAR(255) NOT NULL,
                    phone VARCHAR(50),
                    email VARCHAR(255),
                    address1 TEXT,
                    address2 TEXT,
                    city VARCHAR(100),
                    state VARCHAR(100),
                    postal_code VARCHAR(20),
                    country VARCHAR(100),
                    gst_vat_number VARCHAR(100),
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_customers_owner ON customers(owner_user_id);

                -- 3. products
                CREATE TABLE IF NOT EXISTS products (
                    id INT PRIMARY KEY,
                    barcode VARCHAR(100),
                    name VARCHAR(255) NOT NULL,
                    hsn VARCHAR(50),
                    description TEXT,
                    price NUMERIC(18,2) DEFAULT 0,
                    mrp NUMERIC(18,2) DEFAULT 0,
                    category VARCHAR(100),
                    stock INT DEFAULT 0,
                    expiry_alert_days INT,
                    low_stock_threshold NUMERIC(18,2) DEFAULT 0,
                    marketed_by VARCHAR(255),
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_products_owner ON products(owner_user_id);

                -- 4. product_batches
                CREATE TABLE IF NOT EXISTS product_batches (
                    id INT PRIMARY KEY,
                    product_id_ref INT NOT NULL,
                    batch_number VARCHAR(100),
                    hsn VARCHAR(50),
                    expiry TIMESTAMPTZ,
                    mrp NUMERIC(18,2) DEFAULT 0,
                    cost_price NUMERIC(18,2) DEFAULT 0,
                    selling_price NUMERIC(18,2) DEFAULT 0,
                    stock NUMERIC(18,2) DEFAULT 0,
                    expired_stock NUMERIC(18,2) DEFAULT 0,
                    bonus VARCHAR(50),
                    pack VARCHAR(50),
                    marketed_by VARCHAR(255),
                    rate NUMERIC(18,2) DEFAULT 0,
                    discount_percent NUMERIC(18,2) DEFAULT 0,
                    cgst_percent NUMERIC(18,2) DEFAULT 0,
                    sgst_percent NUMERIC(18,2) DEFAULT 0,
                    igst_percent NUMERIC(18,2) DEFAULT 0,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_batches_owner ON product_batches(owner_user_id);
                ALTER TABLE product_batches ADD COLUMN IF NOT EXISTS expired_stock NUMERIC(18,2) DEFAULT 0;

                -- 5. invoices
                CREATE TABLE IF NOT EXISTS invoices (
                    id INT PRIMARY KEY,
                    invoice_number VARCHAR(100) NOT NULL,
                    invoice_date TIMESTAMPTZ NOT NULL,
                    due_date TIMESTAMPTZ,
                    customer_id INT,
                    status INT NOT NULL DEFAULT 0,
                    subtotal NUMERIC(18,2) DEFAULT 0,
                    tax_amount NUMERIC(18,2) DEFAULT 0,
                    discount_amount NUMERIC(18,2) DEFAULT 0,
                    customer_name_snapshot VARCHAR(255),
                    customer_phone_snapshot VARCHAR(50),
                    customer_address_snapshot TEXT,
                    customer_gst_snapshot VARCHAR(100),
                    total NUMERIC(18,2) DEFAULT 0,
                    total_paid NUMERIC(18,2) DEFAULT 0,
                    back_dues NUMERIC(18,2) DEFAULT 0,
                    private_notes TEXT,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_invoices_owner ON invoices(owner_user_id);

                -- 6. invoice_items
                CREATE TABLE IF NOT EXISTS invoice_items (
                    id INT PRIMARY KEY,
                    invoice_id INT NOT NULL,
                    product_id_ref INT,
                    product_id VARCHAR(100),
                    description TEXT,
                    price NUMERIC(18,2) DEFAULT 0,
                    cost_price NUMERIC(18,2) DEFAULT 0,
                    quantity NUMERIC(18,2) DEFAULT 1,
                    line_total NUMERIC(18,2) DEFAULT 0,
                    product_name VARCHAR(255),
                    batch_number VARCHAR(100),
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_inv_items_owner ON invoice_items(owner_user_id);

                -- 7. purchases
                CREATE TABLE IF NOT EXISTS purchases (
                    id INT PRIMARY KEY,
                    supplier_id INT NOT NULL,
                    purchase_date TIMESTAMPTZ NOT NULL,
                    invoice_number VARCHAR(100),
                    subtotal NUMERIC(18,2) DEFAULT 0,
                    discount NUMERIC(18,2) DEFAULT 0,
                    tax NUMERIC(18,2) DEFAULT 0,
                    shipping NUMERIC(18,2) DEFAULT 0,
                    total NUMERIC(18,2) DEFAULT 0,
                    paid NUMERIC(18,2) DEFAULT 0,
                    due NUMERIC(18,2) DEFAULT 0,
                    notes TEXT,
                    extra_cost_name VARCHAR(100),
                    extra_cost_amount NUMERIC(18,2) DEFAULT 0,
                    extra_cost_is_percent INT DEFAULT 0,
                    discount_is_percent INT DEFAULT 0,
                    total_discount NUMERIC(18,2) DEFAULT 0,
                    total_cgst NUMERIC(18,2) DEFAULT 0,
                    total_sgst NUMERIC(18,2) DEFAULT 0,
                    total_igst NUMERIC(18,2) DEFAULT 0,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_purchases_owner ON purchases(owner_user_id);

                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS extra_cost_name VARCHAR(100);
                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS extra_cost_amount NUMERIC(18,2) DEFAULT 0;
                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS extra_cost_is_percent INT DEFAULT 0;
                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS discount_is_percent INT DEFAULT 0;
                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS total_discount NUMERIC(18,2) DEFAULT 0;
                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS total_cgst NUMERIC(18,2) DEFAULT 0;
                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS total_sgst NUMERIC(18,2) DEFAULT 0;
                ALTER TABLE purchases ADD COLUMN IF NOT EXISTS total_igst NUMERIC(18,2) DEFAULT 0;

                -- 8. purchase_items
                CREATE TABLE IF NOT EXISTS purchase_items (
                    id INT PRIMARY KEY,
                    purchase_id INT NOT NULL,
                    product_id INT,
                    product_name VARCHAR(255),
                    batch_number VARCHAR(100),
                    expiry TIMESTAMPTZ,
                    mrp NUMERIC(18,2) DEFAULT 0,
                    cost_price NUMERIC(18,2) DEFAULT 0,
                    selling_price NUMERIC(18,2) DEFAULT 0,
                    quantity NUMERIC(18,2) DEFAULT 1,
                    pack VARCHAR(50),
                    bonus VARCHAR(50),
                    marketed_by VARCHAR(255),
                    rate NUMERIC(18,2) DEFAULT 0,
                    discount_percent NUMERIC(18,2) DEFAULT 0,
                    cgst_percent NUMERIC(18,2) DEFAULT 0,
                    sgst_percent NUMERIC(18,2) DEFAULT 0,
                    igst_percent NUMERIC(18,2) DEFAULT 0,
                    line_total NUMERIC(18,2) DEFAULT 0,
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_purchase_items_owner ON purchase_items(owner_user_id);

                ALTER TABLE purchase_items ADD COLUMN IF NOT EXISTS cgst_percent NUMERIC(18,2) DEFAULT 0;
                ALTER TABLE purchase_items ADD COLUMN IF NOT EXISTS sgst_percent NUMERIC(18,2) DEFAULT 0;
                ALTER TABLE purchase_items ADD COLUMN IF NOT EXISTS igst_percent NUMERIC(18,2) DEFAULT 0;

                -- 9. suppliers
                CREATE TABLE IF NOT EXISTS suppliers (
                    id INT PRIMARY KEY,
                    name VARCHAR(255) NOT NULL,
                    email VARCHAR(255),
                    phone VARCHAR(50),
                    city VARCHAR(100),
                    state VARCHAR(100),
                    gst_number VARCHAR(100),
                    back_dues NUMERIC(18,2) DEFAULT 0,
                    last_purchase_at TIMESTAMPTZ,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_suppliers_owner ON suppliers(owner_user_id);

                -- 10. expenses
                CREATE TABLE IF NOT EXISTS expenses (
                    id INT PRIMARY KEY,
                    description TEXT NOT NULL,
                    amount NUMERIC(18,2) NOT NULL,
                    category VARCHAR(100) NOT NULL,
                    date TIMESTAMPTZ NOT NULL,
                    note TEXT,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_expenses_owner ON expenses(owner_user_id);

                -- 11. payments
                CREATE TABLE IF NOT EXISTS payments (
                    id INT PRIMARY KEY,
                    invoice_id INT NOT NULL,
                    date TIMESTAMPTZ NOT NULL,
                    method VARCHAR(50),
                    amount NUMERIC(18,2) NOT NULL,
                    notes TEXT,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_payments_owner ON payments(owner_user_id);

                -- 12. purchase_payments
                CREATE TABLE IF NOT EXISTS purchase_payments (
                    id INT PRIMARY KEY,
                    purchase_id INT NOT NULL,
                    date TIMESTAMPTZ NOT NULL,
                    method VARCHAR(50),
                    amount NUMERIC(18,2) NOT NULL,
                    notes TEXT,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_pur_payments_owner ON purchase_payments(owner_user_id);

                -- 13. damaged_items
                CREATE TABLE IF NOT EXISTS damaged_items (
                    id INT PRIMARY KEY,
                    product_id_ref INT,
                    batch_number VARCHAR(100),
                    quantity NUMERIC(18,2) DEFAULT 0,
                    reason TEXT,
                    loss_amount NUMERIC(18,2) DEFAULT 0,
                    date TIMESTAMPTZ,
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_damaged_owner ON damaged_items(owner_user_id);

                -- 14. recycle_bin
                CREATE TABLE IF NOT EXISTS recycle_bin (
                    id INT PRIMARY KEY,
                    entity_type VARCHAR(100) NOT NULL,
                    entity_id INT NOT NULL,
                    json_data TEXT,
                    owner_user_id INT,
                    deleted_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_recycle_owner ON recycle_bin(owner_user_id);

                -- 15. ai_chat_messages
                CREATE TABLE IF NOT EXISTS ai_chat_messages (
                    id INT PRIMARY KEY,
                    role VARCHAR(50) NOT NULL,
                    content TEXT,
                    owner_user_id INT,
                    timestamp TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_ai_chat_owner ON ai_chat_messages(owner_user_id);

                -- 16. estimates
                CREATE TABLE IF NOT EXISTS estimates (
                    id INT PRIMARY KEY,
                    estimate_number VARCHAR(100) NOT NULL,
                    estimate_date TIMESTAMPTZ NOT NULL,
                    due_date TIMESTAMPTZ,
                    customer_id INT,
                    status VARCHAR(50),
                    total NUMERIC(18,2) DEFAULT 0,
                    emailed_on TIMESTAMPTZ,
                    printed_on TIMESTAMPTZ,
                    sms_on TIMESTAMPTZ,
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_estimates_owner ON estimates(owner_user_id);

                -- 17. estimate_items
                CREATE TABLE IF NOT EXISTS estimate_items (
                    id INT PRIMARY KEY,
                    estimate_id INT NOT NULL,
                    product_id VARCHAR(100),
                    name VARCHAR(255),
                    description TEXT,
                    price NUMERIC(18,2) DEFAULT 0,
                    quantity NUMERIC(18,2) DEFAULT 1,
                    line_total NUMERIC(18,2) DEFAULT 0,
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_est_items_owner ON estimate_items(owner_user_id);

                -- 18. orders
                CREATE TABLE IF NOT EXISTS orders (
                    id INT PRIMARY KEY,
                    order_number VARCHAR(100) NOT NULL,
                    order_date TIMESTAMPTZ NOT NULL,
                    due_date TIMESTAMPTZ,
                    customer_id INT,
                    status VARCHAR(50),
                    total NUMERIC(18,2) DEFAULT 0,
                    emailed_on TIMESTAMPTZ,
                    printed_on TIMESTAMPTZ,
                    sms_on TIMESTAMPTZ,
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_orders_owner ON orders(owner_user_id);

                -- 19. order_items
                CREATE TABLE IF NOT EXISTS order_items (
                    id INT PRIMARY KEY,
                    order_id INT NOT NULL,
                    product_id VARCHAR(100),
                    name VARCHAR(255),
                    description TEXT,
                    price NUMERIC(18,2) DEFAULT 0,
                    quantity NUMERIC(18,2) DEFAULT 1,
                    line_total NUMERIC(18,2) DEFAULT 0,
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_ord_items_owner ON order_items(owner_user_id);

                -- 20. recurring_invoices
                CREATE TABLE IF NOT EXISTS recurring_invoices (
                    id INT PRIMARY KEY,
                    invoice_number VARCHAR(100),
                    customer_id INT,
                    next_invoice TIMESTAMPTZ,
                    period VARCHAR(50),
                    stop_after INT,
                    total NUMERIC(18,2) DEFAULT 0,
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_recurring_owner ON recurring_invoices(owner_user_id);

                -- 21. app_settings
                CREATE TABLE IF NOT EXISTS app_settings (
                    key VARCHAR(255) PRIMARY KEY,
                    value TEXT,
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                ";

                await using var cmd = new NpgsqlCommand(ddl, conn);
                await cmd.ExecuteNonQueryAsync();

                try
                {
                    var cleanSql = @"
                        UPDATE app_settings SET value = 'India' WHERE key = 'company.country' AND (value = 'Pakistan' OR value = '' OR value IS NULL);
                        UPDATE app_settings SET value = '' WHERE key = 'company.city' AND value = 'Lahore';
                        UPDATE app_settings SET value = '' WHERE key = 'company.address1' AND value = '12 Market Road';
                    ";
                    await using var cleanCmd = new NpgsqlCommand(cleanSql, conn);
                    await cleanCmd.ExecuteNonQueryAsync();
                }
                catch { }

                _schemaEnsured = true;
                return (true, "Cloud database schema initialized successfully. All 20 tables and user indexes are ready.");
            }
            catch (Exception ex)
            {
                return (false, $"Schema initialization failed: {ex.Message}");
            }
        }

        private static async Task<int> ExecuteBatchChunkAsync(NpgsqlConnection conn, List<NpgsqlBatchCommand> batchCommands, int chunkSize = 50)
        {
            if (batchCommands.Count == 0) return 0;
            int count = 0;
            for (int i = 0; i < batchCommands.Count; i += chunkSize)
            {
                var chunk = batchCommands.Skip(i).Take(chunkSize).ToList();
                int retries = 3;
                while (retries > 0)
                {
                    try
                    {
                        if (conn.State != ConnectionState.Open)
                        {
                            await conn.OpenAsync();
                        }

                        await using var batch = new NpgsqlBatch(conn);
                        batch.Timeout = 90;
                        foreach (var cmd in chunk)
                        {
                            var cloned = new NpgsqlBatchCommand(cmd.CommandText);
                            foreach (NpgsqlParameter p in cmd.Parameters)
                            {
                                cloned.Parameters.AddWithValue(p.ParameterName, p.Value ?? DBNull.Value);
                            }
                            batch.BatchCommands.Add(cloned);
                        }
                        await batch.ExecuteNonQueryAsync();
                        count += chunk.Count;
                        break;
                    }
                    catch (Exception ex) when (retries > 1 && (ex is System.IO.IOException || ex is NpgsqlException || ex is TimeoutException))
                    {
                        retries--;
                        try
                        {
                            if (conn.State == ConnectionState.Open)
                                await conn.CloseAsync();
                        }
                        catch { }
                        await Task.Delay(600);
                        try
                        {
                            await conn.OpenAsync();
                        }
                        catch { }
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Pushes local SQLite business records to the online PostgreSQL database using UPSERT and NpgsqlBatch.
        /// Bundles records into 50-command TCP packets for maximum network reliability and pooler compatibility.
        /// </summary>
        public static async Task<(bool Success, string Message)> PushToCloudAsync(string connectionString, int? currentUserId = null, Action<string, int>? progressCallback = null, DateTime? sinceUtc = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            try
            {
                if (AppSettingsService.SyncAutoMigrate && !_schemaEnsured)
                {
                    progressCallback?.Invoke("Verifying cloud schema...", 5);
                    await EnsureSchemaAsync(connectionString, force: false);
                }

                DateTime? deltaThreshold = sinceUtc.HasValue ? sinceUtc.Value.AddMinutes(-5) : null;

                progressCallback?.Invoke("Connecting to cloud database...", 10);
                var normalized = NormalizeConnectionString(connectionString);
                var csb = new NpgsqlConnectionStringBuilder(normalized)
                {
                    Timeout = 30,
                    CommandTimeout = 90,
                    KeepAlive = 15,
                    TcpKeepAlive = true,
                    ConnectionIdleLifetime = 15,
                    ConnectionPruningInterval = 10
                };

                await using var conn = new NpgsqlConnection(csb.ConnectionString);
                await conn.OpenAsync();

                int? effectiveUserId = currentUserId ?? AuthService.CurrentUser?.Id;
                using var db = new AppDbContext(effectiveUserId);
                try
                {
                    db.Database.ExecuteSqlRaw(@"
                        UPDATE AppSettings SET UpdatedAt = datetime('now') WHERE UpdatedAt = '' OR UpdatedAt IS NULL;
                        UPDATE Customers SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Customers SET UpdatedAt = NULL WHERE UpdatedAt = '';
                        UPDATE Products SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Products SET UpdatedAt = NULL WHERE UpdatedAt = '';
                        UPDATE Invoices SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Purchases SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Purchases SET UpdatedAt = NULL WHERE UpdatedAt = '';
                        UPDATE Expenses SET Date = datetime('now') WHERE Date = '' OR Date IS NULL;
                        UPDATE Expenses SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE DamagedItems SET Date = datetime('now') WHERE Date = '' OR Date IS NULL;
                        UPDATE DamagedItems SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE RecycleBin SET DeletedAt = datetime('now') WHERE DeletedAt = '' OR DeletedAt IS NULL;
                        UPDATE AiChatMessages SET Timestamp = datetime('now') WHERE Timestamp = '' OR Timestamp IS NULL;
                        UPDATE InvoiceItems SET ProductName = '' WHERE ProductName IS NULL;
                        UPDATE InvoiceItems SET ProductId = '' WHERE ProductId IS NULL;
                        UPDATE InvoiceItems SET Description = '' WHERE Description IS NULL;
                        UPDATE Customers SET Name = '' WHERE Name IS NULL;
                        UPDATE Products SET Name = '' WHERE Name IS NULL;
                        UPDATE Products SET Barcode = '' WHERE Barcode IS NULL;
                        UPDATE Invoices SET InvoiceNumber = '' WHERE InvoiceNumber IS NULL;
                        UPDATE Suppliers SET Name = '' WHERE Name IS NULL;
                        UPDATE Expenses SET Description = '' WHERE Description IS NULL;
                        UPDATE Expenses SET Category = 'General' WHERE Category IS NULL;
                        UPDATE RecycleBin SET EntityType = '' WHERE EntityType IS NULL;
                        UPDATE AiChatMessages SET Role = 'User' WHERE Role IS NULL;
                        UPDATE AiChatMessages SET Content = '' WHERE Content IS NULL;
                        DELETE FROM InvoiceItems 
                        WHERE Id NOT IN (
                            SELECT MIN(Id) 
                            FROM InvoiceItems 
                            GROUP BY InvoiceId, ProductId, BatchNumber, Description, Quantity, Price, LineTotal
                        );
                        DELETE FROM Payments 
                        WHERE Id NOT IN (
                            SELECT MIN(Id) 
                            FROM Payments 
                            GROUP BY InvoiceId, Date, Amount, Method, Notes
                        );
                        UPDATE Invoices 
                        SET Subtotal = COALESCE((SELECT SUM(LineTotal) FROM InvoiceItems WHERE InvoiceId = Invoices.Id), Subtotal),
                            Total = COALESCE((SELECT SUM(LineTotal) FROM InvoiceItems WHERE InvoiceId = Invoices.Id), Subtotal) + TaxAmount - DiscountAmount,
                            TotalPaid = COALESCE((SELECT SUM(Amount) FROM Payments WHERE InvoiceId = Invoices.Id), TotalPaid)
                        WHERE Id >= 4345;
                        UPDATE RecycleBin SET EntityType = 'Purchase' WHERE LOWER(EntityType) = 'purchase';
                        DELETE FROM Purchases WHERE Id IN (SELECT EntityId FROM RecycleBin WHERE LOWER(EntityType) = 'purchase');
                        DELETE FROM PurchaseItems WHERE PurchaseId NOT IN (SELECT Id FROM Purchases);
                        DELETE FROM PurchasePayments WHERE PurchaseId NOT IN (SELECT Id FROM Purchases);
                    ");
                }
                catch { }

                var pushDeletedPurIds = await db.RecycleBin.IgnoreQueryFilters()
                    .Where(r => r.EntityType != null && r.EntityType.ToLower() == "purchase")
                    .Select(r => r.EntityId)
                    .ToListAsync();
                var pushDeletedPurSet = new HashSet<int>(pushDeletedPurIds);

                int totalSynced = 0;

                // 1. Customers
                progressCallback?.Invoke("Syncing Customers (1/14 tables)...", 15);
                var custQuery = db.Customers.AsNoTracking();
                if (deltaThreshold.HasValue)
                    custQuery = custQuery.Where(c => (c.UpdatedAt ?? c.CreatedAt) >= deltaThreshold.Value);
                var customers = await custQuery.ToListAsync();
                if (customers.Count > 0)
                {
                    var customerCmds = new List<NpgsqlBatchCommand>();
                    foreach (var c in customers)
                    {
                        var sql = @"
                            INSERT INTO customers (id, name, phone, email, address1, address2, city, state, postal_code, country, gst_vat_number, owner_user_id, created_at, updated_at)
                            VALUES (@id, @name, @phone, @email, @address1, @address2, @city, @state, @postal_code, @country, @gst_vat_number, @owner_user_id, @created_at, @updated_at)
                            ON CONFLICT (id) DO UPDATE SET
                                name = EXCLUDED.name, phone = EXCLUDED.phone, email = EXCLUDED.email, address1 = EXCLUDED.address1,
                                address2 = EXCLUDED.address2, city = EXCLUDED.city, state = EXCLUDED.state, postal_code = EXCLUDED.postal_code,
                                country = EXCLUDED.country, gst_vat_number = EXCLUDED.gst_vat_number, owner_user_id = EXCLUDED.owner_user_id,
                                updated_at = EXCLUDED.updated_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", c.Id);
                        cmd.Parameters.AddWithValue("name", c.Name ?? "");
                        cmd.Parameters.AddWithValue("phone", (object?)c.Phone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("email", (object?)c.Email ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("address1", (object?)c.Address1 ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("address2", (object?)c.Address2 ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("city", (object?)c.City ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("state", (object?)c.State ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("postal_code", (object?)c.PostalCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("country", (object?)c.Country ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("gst_vat_number", (object?)c.GstVatNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)c.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", c.CreatedAt);
                        cmd.Parameters.AddWithValue("updated_at", (object?)c.UpdatedAt ?? DateTime.UtcNow);
                        customerCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, customerCmds);
                }

                // 2. Products
                progressCallback?.Invoke("Syncing Products (2/14 tables)...", 22);
                var prodQuery = db.Products.AsNoTracking();
                if (deltaThreshold.HasValue)
                    prodQuery = prodQuery.Where(p => (p.UpdatedAt ?? p.CreatedAt) >= deltaThreshold.Value);
                var products = await prodQuery.ToListAsync();
                if (products.Count > 0)
                {
                    var productCmds = new List<NpgsqlBatchCommand>();
                    foreach (var p in products)
                    {
                        var sql = @"
                            INSERT INTO products (
                                id, barcode, name, hsn, description, price, mrp, category, stock,
                                expiry_alert_days, low_stock_threshold, marketed_by, owner_user_id, created_at, updated_at
                            )
                            VALUES (
                                @id, @barcode, @name, @hsn, @description, @price, @mrp, @category, @stock,
                                @expiry_alert_days, @low_stock_threshold, @marketed_by, @owner_user_id, @created_at, @updated_at
                            )
                            ON CONFLICT (id) DO UPDATE SET
                                barcode = EXCLUDED.barcode, name = EXCLUDED.name, hsn = EXCLUDED.hsn, description = EXCLUDED.description,
                                price = EXCLUDED.price, mrp = EXCLUDED.mrp, category = EXCLUDED.category, stock = EXCLUDED.stock,
                                expiry_alert_days = EXCLUDED.expiry_alert_days, low_stock_threshold = EXCLUDED.low_stock_threshold,
                                marketed_by = EXCLUDED.marketed_by, owner_user_id = EXCLUDED.owner_user_id, updated_at = EXCLUDED.updated_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", p.Id);
                        cmd.Parameters.AddWithValue("barcode", (object?)p.Barcode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("name", p.Name ?? "");
                        cmd.Parameters.AddWithValue("hsn", (object?)p.Hsn ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("description", (object?)p.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("price", p.Price);
                        cmd.Parameters.AddWithValue("mrp", p.MRP);
                        cmd.Parameters.AddWithValue("category", (object?)p.Category ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("stock", p.Stock);
                        cmd.Parameters.AddWithValue("expiry_alert_days", (object?)p.ExpiryAlertDays ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("low_stock_threshold", (object?)p.LowStockThreshold ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("marketed_by", (object?)p.MarketedBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)p.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", p.CreatedAt);
                        cmd.Parameters.AddWithValue("updated_at", (object?)p.UpdatedAt ?? DateTime.UtcNow);
                        productCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, productCmds);
                }

                // 3. Product Batches
                progressCallback?.Invoke("Syncing Batches (3/14 tables)...", 30);
                var batchQuery = db.ProductBatches.AsNoTracking();
                if (deltaThreshold.HasValue)
                    batchQuery = batchQuery.Where(b => (b.UpdatedAt ?? b.CreatedAt) >= deltaThreshold.Value);
                var batches = await batchQuery.ToListAsync();
                if (batches.Count > 0)
                {
                    var batchCmds = new List<NpgsqlBatchCommand>();
                    foreach (var b in batches)
                    {
                        var sql = @"
                            INSERT INTO product_batches (id, product_id_ref, batch_number, hsn, expiry, mrp, cost_price, selling_price, stock, expired_stock, bonus, pack, marketed_by, rate, discount_percent, cgst_percent, sgst_percent, igst_percent, owner_user_id, created_at, updated_at)
                            VALUES (@id, @product_id_ref, @batch_number, @hsn, @expiry, @mrp, @cost_price, @selling_price, @stock, @expired_stock, @bonus, @pack, @marketed_by, @rate, @discount_percent, @cgst_percent, @sgst_percent, @igst_percent, @owner_user_id, @created_at, @updated_at)
                            ON CONFLICT (id) DO UPDATE SET
                                product_id_ref = EXCLUDED.product_id_ref, batch_number = EXCLUDED.batch_number, hsn = EXCLUDED.hsn,
                                expiry = EXCLUDED.expiry, mrp = EXCLUDED.mrp, cost_price = EXCLUDED.cost_price,
                                selling_price = EXCLUDED.selling_price, stock = EXCLUDED.stock, expired_stock = EXCLUDED.expired_stock,
                                bonus = EXCLUDED.bonus, pack = EXCLUDED.pack, marketed_by = EXCLUDED.marketed_by, rate = EXCLUDED.rate,
                                discount_percent = EXCLUDED.discount_percent, cgst_percent = EXCLUDED.cgst_percent,
                                sgst_percent = EXCLUDED.sgst_percent, igst_percent = EXCLUDED.igst_percent,
                                owner_user_id = EXCLUDED.owner_user_id, updated_at = EXCLUDED.updated_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", b.Id);
                        cmd.Parameters.AddWithValue("product_id_ref", b.ProductIdRef);
                        cmd.Parameters.AddWithValue("batch_number", (object?)b.BatchNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("hsn", (object?)b.Hsn ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("expiry", (object?)b.Expiry ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("mrp", (object?)b.Mrp ?? 0m);
                        cmd.Parameters.AddWithValue("cost_price", (object?)b.CostPrice ?? 0m);
                        cmd.Parameters.AddWithValue("selling_price", (object?)b.SellingPrice ?? 0m);
                        cmd.Parameters.AddWithValue("stock", (object?)b.Stock ?? 0m);
                        cmd.Parameters.AddWithValue("expired_stock", (object?)b.ExpiredStock ?? 0m);
                        cmd.Parameters.AddWithValue("bonus", (object?)b.Bonus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("pack", (object?)b.Pack ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("marketed_by", (object?)b.MarketedBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("rate", (object?)b.Rate ?? 0m);
                        cmd.Parameters.AddWithValue("discount_percent", (object?)b.DiscountPercent ?? 0m);
                        cmd.Parameters.AddWithValue("cgst_percent", (object?)b.CgstPercent ?? 0m);
                        cmd.Parameters.AddWithValue("sgst_percent", (object?)b.SgstPercent ?? 0m);
                        cmd.Parameters.AddWithValue("igst_percent", (object?)b.IgstPercent ?? 0m);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)b.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", b.CreatedAt);
                        cmd.Parameters.AddWithValue("updated_at", (object?)b.UpdatedAt ?? DateTime.UtcNow);
                        batchCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, batchCmds);
                }

                // 4. Invoices
                progressCallback?.Invoke("Syncing Invoices (4/14 tables)...", 40);
                var invQuery = db.Invoices.AsNoTracking();
                if (deltaThreshold.HasValue)
                    invQuery = invQuery.Where(i => (i.UpdatedAt ?? i.CreatedAt) >= deltaThreshold.Value);
                var invoices = await invQuery.ToListAsync();
                if (invoices.Count > 0)
                {
                    var invoiceCmds = new List<NpgsqlBatchCommand>();
                    foreach (var inv in invoices)
                    {
                        var sql = @"
                            INSERT INTO invoices (id, invoice_number, invoice_date, due_date, customer_id, status, subtotal, tax_amount, discount_amount, customer_name_snapshot, customer_phone_snapshot, customer_address_snapshot, customer_gst_snapshot, total, total_paid, back_dues, private_notes, owner_user_id, created_at, updated_at)
                            VALUES (@id, @invoice_number, @invoice_date, @due_date, @customer_id, @status, @subtotal, @tax_amount, @discount_amount, @customer_name_snapshot, @customer_phone_snapshot, @customer_address_snapshot, @customer_gst_snapshot, @total, @total_paid, @back_dues, @private_notes, @owner_user_id, @created_at, @updated_at)
                            ON CONFLICT (id) DO UPDATE SET
                                invoice_number = EXCLUDED.invoice_number, invoice_date = EXCLUDED.invoice_date, due_date = EXCLUDED.due_date,
                                customer_id = EXCLUDED.customer_id, status = EXCLUDED.status, subtotal = EXCLUDED.subtotal,
                                tax_amount = EXCLUDED.tax_amount, discount_amount = EXCLUDED.discount_amount,
                                customer_name_snapshot = EXCLUDED.customer_name_snapshot, customer_phone_snapshot = EXCLUDED.customer_phone_snapshot,
                                customer_address_snapshot = EXCLUDED.customer_address_snapshot, customer_gst_snapshot = EXCLUDED.customer_gst_snapshot,
                                total = EXCLUDED.total, total_paid = EXCLUDED.total_paid, back_dues = EXCLUDED.back_dues,
                                private_notes = EXCLUDED.private_notes, owner_user_id = EXCLUDED.owner_user_id, updated_at = EXCLUDED.updated_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", inv.Id);
                        cmd.Parameters.AddWithValue("invoice_number", inv.InvoiceNumber ?? "");
                        cmd.Parameters.AddWithValue("invoice_date", inv.InvoiceDate);
                        cmd.Parameters.AddWithValue("due_date", (object?)inv.DueDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("customer_id", (object?)inv.CustomerId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("status", (int)inv.Status);
                        cmd.Parameters.AddWithValue("subtotal", inv.Subtotal);
                        cmd.Parameters.AddWithValue("tax_amount", inv.TaxAmount);
                        cmd.Parameters.AddWithValue("discount_amount", inv.DiscountAmount);
                        cmd.Parameters.AddWithValue("customer_name_snapshot", (object?)inv.CustomerNameSnapshot ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("customer_phone_snapshot", (object?)inv.CustomerPhoneSnapshot ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("customer_address_snapshot", (object?)inv.CustomerAddressSnapshot ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("customer_gst_snapshot", (object?)inv.CustomerGstSnapshot ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("total", inv.Total);
                        cmd.Parameters.AddWithValue("total_paid", inv.TotalPaid);
                        cmd.Parameters.AddWithValue("back_dues", inv.BackDues);
                        cmd.Parameters.AddWithValue("private_notes", (object?)inv.PrivateNotes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)inv.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", inv.CreatedAt);
                        cmd.Parameters.AddWithValue("updated_at", (object?)inv.UpdatedAt ?? DateTime.UtcNow);
                        invoiceCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, invoiceCmds);
                }

                // 5. Invoice Items
                progressCallback?.Invoke("Syncing Invoice Items (5/14 tables)...", 50);
                var itemQuery = db.InvoiceItems.AsNoTracking();
                List<int>? changedInvIds = null;
                if (deltaThreshold.HasValue)
                {
                    changedInvIds = await db.Invoices.AsNoTracking()
                        .Where(i => (i.UpdatedAt ?? i.CreatedAt) >= deltaThreshold.Value)
                        .Select(i => i.Id)
                        .ToListAsync();
                    itemQuery = itemQuery.Where(ii => changedInvIds.Contains(ii.InvoiceId));
                }
                var invoiceItems = await itemQuery.ToListAsync();
                if (invoiceItems.Count > 0 || (deltaThreshold.HasValue && changedInvIds != null && changedInvIds.Count > 0))
                {
                    var invoiceItemCmds = new List<NpgsqlBatchCommand>();

                    // Clean up orphaned items on Postgres for affected invoices
                    var affectedInvIds = (deltaThreshold.HasValue && changedInvIds != null)
                        ? changedInvIds
                        : invoiceItems.Select(ii => ii.InvoiceId).Distinct().ToList();

                    foreach (var invId in affectedInvIds)
                    {
                        var activeLocalIds = invoiceItems.Where(ii => ii.InvoiceId == invId).Select(ii => ii.Id).ToList();
                        if (activeLocalIds.Count > 0)
                        {
                            var inClause = string.Join(",", activeLocalIds);
                            invoiceItemCmds.Add(new NpgsqlBatchCommand($"DELETE FROM invoice_items WHERE invoice_id = {invId} AND id NOT IN ({inClause});"));
                        }
                        else
                        {
                            invoiceItemCmds.Add(new NpgsqlBatchCommand($"DELETE FROM invoice_items WHERE invoice_id = {invId};"));
                        }
                    }

                    foreach (var item in invoiceItems)
                    {
                        var sql = @"
                            INSERT INTO invoice_items (id, invoice_id, product_id_ref, product_id, description, price, cost_price, quantity, line_total, product_name, batch_number, owner_user_id)
                            VALUES (@id, @invoice_id, @product_id_ref, @product_id, @description, @price, @cost_price, @quantity, @line_total, @product_name, @batch_number, @owner_user_id)
                            ON CONFLICT (id) DO UPDATE SET
                                invoice_id = EXCLUDED.invoice_id, product_id_ref = EXCLUDED.product_id_ref, product_id = EXCLUDED.product_id,
                                description = EXCLUDED.description, price = EXCLUDED.price, cost_price = EXCLUDED.cost_price,
                                quantity = EXCLUDED.quantity, line_total = EXCLUDED.line_total, product_name = EXCLUDED.product_name,
                                batch_number = EXCLUDED.batch_number, owner_user_id = EXCLUDED.owner_user_id;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", item.Id);
                        cmd.Parameters.AddWithValue("invoice_id", item.InvoiceId);
                        cmd.Parameters.AddWithValue("product_id_ref", (object?)item.ProductIdRef ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("product_id", item.ProductId ?? "");
                        cmd.Parameters.AddWithValue("description", item.Description ?? "");
                        cmd.Parameters.AddWithValue("price", item.Price);
                        cmd.Parameters.AddWithValue("cost_price", (object?)item.CostPrice ?? 0m);
                        cmd.Parameters.AddWithValue("quantity", item.Quantity);
                        cmd.Parameters.AddWithValue("line_total", item.LineTotal);
                        cmd.Parameters.AddWithValue("product_name", item.ProductName ?? "");
                        cmd.Parameters.AddWithValue("batch_number", (object?)item.BatchNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)item.OwnerUserId ?? DBNull.Value);
                        invoiceItemCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, invoiceItemCmds);
                }

                // 6. Purchases
                progressCallback?.Invoke("Syncing Purchases (6/14 tables)...", 60);
                var purQuery = db.Purchases.AsNoTracking();
                if (deltaThreshold.HasValue)
                    purQuery = purQuery.Where(p => (p.UpdatedAt ?? p.CreatedAt) >= deltaThreshold.Value);
                var purchases = await purQuery.ToListAsync();
                if (pushDeletedPurSet.Count > 0)
                {
                    purchases = purchases.Where(p => !pushDeletedPurSet.Contains(p.Id)).ToList();
                }
                if (purchases.Count > 0)
                {
                    var purchaseCmds = new List<NpgsqlBatchCommand>();
                    foreach (var pur in purchases)
                    {
                        var sql = @"
                            INSERT INTO purchases (
                                id, supplier_id, purchase_date, invoice_number, subtotal, discount, discount_is_percent, tax, shipping, total, paid, due, notes,
                                extra_cost_name, extra_cost_amount, extra_cost_is_percent, total_discount, total_cgst, total_sgst, total_igst, owner_user_id, created_at, updated_at
                            )
                            VALUES (
                                @id, @supplier_id, @purchase_date, @invoice_number, @subtotal, @discount, @discount_is_percent, @tax, @shipping, @total, @paid, @due, @notes,
                                @extra_cost_name, @extra_cost_amount, @extra_cost_is_percent, @total_discount, @total_cgst, @total_sgst, @total_igst, @owner_user_id, @created_at, @updated_at
                            )
                            ON CONFLICT (id) DO UPDATE SET
                                supplier_id = EXCLUDED.supplier_id, purchase_date = EXCLUDED.purchase_date,
                                invoice_number = EXCLUDED.invoice_number, subtotal = EXCLUDED.subtotal, discount = EXCLUDED.discount,
                                discount_is_percent = EXCLUDED.discount_is_percent, tax = EXCLUDED.tax, shipping = EXCLUDED.shipping,
                                total = EXCLUDED.total, paid = EXCLUDED.paid, due = EXCLUDED.due, notes = EXCLUDED.notes,
                                extra_cost_name = EXCLUDED.extra_cost_name, extra_cost_amount = EXCLUDED.extra_cost_amount,
                                extra_cost_is_percent = EXCLUDED.extra_cost_is_percent, total_discount = EXCLUDED.total_discount,
                                total_cgst = EXCLUDED.total_cgst, total_sgst = EXCLUDED.total_sgst, total_igst = EXCLUDED.total_igst,
                                owner_user_id = EXCLUDED.owner_user_id, updated_at = EXCLUDED.updated_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", pur.Id);
                        cmd.Parameters.AddWithValue("supplier_id", pur.SupplierId);
                        cmd.Parameters.AddWithValue("purchase_date", pur.PurchaseDate);
                        cmd.Parameters.AddWithValue("invoice_number", (object?)pur.InvoiceNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("subtotal", (object?)pur.Subtotal ?? 0m);
                        cmd.Parameters.AddWithValue("discount", (object?)pur.Discount ?? 0m);
                        cmd.Parameters.AddWithValue("discount_is_percent", pur.DiscountIsPercent.HasValue ? (pur.DiscountIsPercent.Value ? 1 : 0) : 0);
                        cmd.Parameters.AddWithValue("tax", (object?)pur.Tax ?? 0m);
                        cmd.Parameters.AddWithValue("shipping", (object?)pur.Shipping ?? 0m);
                        cmd.Parameters.AddWithValue("total", (object?)pur.Total ?? 0m);
                        cmd.Parameters.AddWithValue("paid", (object?)pur.Paid ?? 0m);
                        cmd.Parameters.AddWithValue("due", (object?)pur.Due ?? 0m);
                        cmd.Parameters.AddWithValue("notes", (object?)pur.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("extra_cost_name", (object?)pur.ExtraCostName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("extra_cost_amount", (object?)pur.ExtraCostAmount ?? 0m);
                        cmd.Parameters.AddWithValue("extra_cost_is_percent", pur.ExtraCostIsPercent.HasValue ? (pur.ExtraCostIsPercent.Value ? 1 : 0) : 0);
                        cmd.Parameters.AddWithValue("total_discount", (object?)pur.TotalDiscount ?? 0m);
                        cmd.Parameters.AddWithValue("total_cgst", (object?)pur.TotalCgst ?? 0m);
                        cmd.Parameters.AddWithValue("total_sgst", (object?)pur.TotalSgst ?? 0m);
                        cmd.Parameters.AddWithValue("total_igst", (object?)pur.TotalIgst ?? 0m);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)pur.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", pur.CreatedAt);
                        cmd.Parameters.AddWithValue("updated_at", (object?)pur.UpdatedAt ?? DateTime.UtcNow);
                        purchaseCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, purchaseCmds);
                }

                // 7. Purchase Items
                progressCallback?.Invoke("Syncing Purchase Items (7/14 tables)...", 68);
                var pItemQuery = db.PurchaseItems.AsNoTracking();
                List<int>? changedPurIds = null;
                if (deltaThreshold.HasValue)
                {
                    changedPurIds = await db.Purchases.AsNoTracking()
                        .Where(p => (p.UpdatedAt ?? p.CreatedAt) >= deltaThreshold.Value)
                        .Select(p => p.Id)
                        .ToListAsync();
                    pItemQuery = pItemQuery.Where(pi => changedPurIds.Contains(pi.PurchaseId));
                }
                var purchaseItems = await pItemQuery.ToListAsync();
                if (pushDeletedPurSet.Count > 0)
                {
                    purchaseItems = purchaseItems.Where(pi => !pushDeletedPurSet.Contains(pi.PurchaseId)).ToList();
                }
                if (purchaseItems.Count > 0 || (deltaThreshold.HasValue && changedPurIds != null && changedPurIds.Count > 0))
                {
                    var purchaseItemCmds = new List<NpgsqlBatchCommand>();

                    // Clean up orphaned items on Postgres for affected purchases
                    var affectedPurIds = (deltaThreshold.HasValue && changedPurIds != null)
                        ? changedPurIds
                        : purchaseItems.Select(pi => pi.PurchaseId).Distinct().ToList();

                    foreach (var pid in affectedPurIds)
                    {
                        var activeLocalIds = purchaseItems.Where(pi => pi.PurchaseId == pid).Select(pi => pi.Id).ToList();
                        if (activeLocalIds.Count > 0)
                        {
                            var inClause = string.Join(",", activeLocalIds);
                            purchaseItemCmds.Add(new NpgsqlBatchCommand($"DELETE FROM purchase_items WHERE purchase_id = {pid} AND id NOT IN ({inClause});"));
                        }
                        else
                        {
                            purchaseItemCmds.Add(new NpgsqlBatchCommand($"DELETE FROM purchase_items WHERE purchase_id = {pid};"));
                        }
                    }

                    foreach (var pi in purchaseItems)
                    {
                        var sql = @"
                            INSERT INTO purchase_items (
                                id, purchase_id, product_id, product_name, batch_number, expiry, mrp, cost_price, selling_price, quantity,
                                pack, bonus, marketed_by, rate, discount_percent, cgst_percent, sgst_percent, igst_percent, line_total, owner_user_id
                            )
                            VALUES (
                                @id, @purchase_id, @product_id, @product_name, @batch_number, @expiry, @mrp, @cost_price, @selling_price, @quantity,
                                @pack, @bonus, @marketed_by, @rate, @discount_percent, @cgst_percent, @sgst_percent, @igst_percent, @line_total, @owner_user_id
                            )
                            ON CONFLICT (id) DO UPDATE SET
                                purchase_id = EXCLUDED.purchase_id, product_id = EXCLUDED.product_id, product_name = EXCLUDED.product_name,
                                batch_number = EXCLUDED.batch_number, expiry = EXCLUDED.expiry, mrp = EXCLUDED.mrp, cost_price = EXCLUDED.cost_price,
                                selling_price = EXCLUDED.selling_price, quantity = EXCLUDED.quantity, pack = EXCLUDED.pack, bonus = EXCLUDED.bonus,
                                marketed_by = EXCLUDED.marketed_by, rate = EXCLUDED.rate, discount_percent = EXCLUDED.discount_percent,
                                cgst_percent = EXCLUDED.cgst_percent, sgst_percent = EXCLUDED.sgst_percent, igst_percent = EXCLUDED.igst_percent,
                                line_total = EXCLUDED.line_total, owner_user_id = EXCLUDED.owner_user_id;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", pi.Id);
                        cmd.Parameters.AddWithValue("purchase_id", pi.PurchaseId);
                        cmd.Parameters.AddWithValue("product_id", (object?)pi.ProductId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("product_name", (object?)pi.ProductName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("batch_number", (object?)pi.BatchNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("expiry", (object?)pi.Expiry ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("mrp", (object?)pi.Mrp ?? 0m);
                        cmd.Parameters.AddWithValue("cost_price", (object?)pi.CostPrice ?? 0m);
                        cmd.Parameters.AddWithValue("selling_price", (object?)pi.SellingPrice ?? 0m);
                        cmd.Parameters.AddWithValue("quantity", pi.Quantity);
                        cmd.Parameters.AddWithValue("pack", (object?)pi.Pack ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("bonus", (object?)pi.Bonus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("marketed_by", (object?)pi.MarketedBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("rate", (object?)pi.Rate ?? 0m);
                        cmd.Parameters.AddWithValue("discount_percent", (object?)pi.DiscountPercent ?? 0m);
                        cmd.Parameters.AddWithValue("cgst_percent", (object?)pi.CgstPercent ?? 0m);
                        cmd.Parameters.AddWithValue("sgst_percent", (object?)pi.SgstPercent ?? 0m);
                        cmd.Parameters.AddWithValue("igst_percent", (object?)pi.IgstPercent ?? 0m);
                        cmd.Parameters.AddWithValue("line_total", (object?)pi.LineTotal ?? 0m);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)pi.OwnerUserId ?? DBNull.Value);
                        purchaseItemCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, purchaseItemCmds);
                }

                // 8. Suppliers
                progressCallback?.Invoke("Syncing Suppliers (8/14 tables)...", 75);
                var supQuery = db.Suppliers.AsNoTracking();
                if (deltaThreshold.HasValue)
                    supQuery = supQuery.Where(s => (s.UpdatedAt ?? s.CreatedAt) >= deltaThreshold.Value);
                var suppliers = await supQuery.ToListAsync();
                if (suppliers.Count > 0)
                {
                    var supplierCmds = new List<NpgsqlBatchCommand>();
                    foreach (var sup in suppliers)
                    {
                        var sql = @"
                            INSERT INTO suppliers (id, name, email, phone, city, state, gst_number, back_dues, last_purchase_at, owner_user_id, created_at, updated_at)
                            VALUES (@id, @name, @email, @phone, @city, @state, @gst_number, @back_dues, @last_purchase_at, @owner_user_id, @created_at, @updated_at)
                            ON CONFLICT (id) DO UPDATE SET
                                name = EXCLUDED.name, email = EXCLUDED.email, phone = EXCLUDED.phone, city = EXCLUDED.city,
                                state = EXCLUDED.state, gst_number = EXCLUDED.gst_number, back_dues = EXCLUDED.back_dues,
                                last_purchase_at = EXCLUDED.last_purchase_at, owner_user_id = EXCLUDED.owner_user_id,
                                updated_at = EXCLUDED.updated_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", sup.Id);
                        cmd.Parameters.AddWithValue("name", sup.Name ?? "");
                        cmd.Parameters.AddWithValue("email", (object?)sup.Email ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("phone", (object?)sup.Phone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("city", (object?)sup.City ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("state", (object?)sup.State ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("gst_number", (object?)sup.GstNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("back_dues", (object?)sup.BackDues ?? 0m);
                        cmd.Parameters.AddWithValue("last_purchase_at", (object?)sup.LastPurchaseAt ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)sup.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", sup.CreatedAt);
                        cmd.Parameters.AddWithValue("updated_at", (object?)sup.UpdatedAt ?? DateTime.UtcNow);
                        supplierCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, supplierCmds);
                }

                // 9. Expenses
                progressCallback?.Invoke("Syncing Expenses (9/14 tables)...", 80);
                var expQuery = db.Expenses.AsNoTracking();
                if (deltaThreshold.HasValue)
                    expQuery = expQuery.Where(e => e.Date >= deltaThreshold.Value || e.CreatedAt >= deltaThreshold.Value);
                var expenses = await expQuery.ToListAsync();
                if (expenses.Count > 0)
                {
                    var expenseCmds = new List<NpgsqlBatchCommand>();
                    foreach (var exp in expenses)
                    {
                        var sql = @"
                            INSERT INTO expenses (id, description, amount, category, date, note, owner_user_id, created_at)
                            VALUES (@id, @description, @amount, @category, @date, @note, @owner_user_id, @created_at)
                            ON CONFLICT (id) DO UPDATE SET
                                description = EXCLUDED.description, amount = EXCLUDED.amount, category = EXCLUDED.category,
                                date = EXCLUDED.date, note = EXCLUDED.note, owner_user_id = EXCLUDED.owner_user_id;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", exp.Id);
                        cmd.Parameters.AddWithValue("description", exp.Description ?? "");
                        cmd.Parameters.AddWithValue("amount", exp.Amount);
                        cmd.Parameters.AddWithValue("category", exp.Category ?? "General");
                        cmd.Parameters.AddWithValue("date", exp.Date);
                        cmd.Parameters.AddWithValue("note", (object?)exp.Note ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)exp.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", exp.CreatedAt);
                        expenseCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, expenseCmds);
                }

                // 10. Payments
                progressCallback?.Invoke("Syncing Payments (10/14 tables)...", 85);
                var payQuery = db.Payments.AsNoTracking();
                if (deltaThreshold.HasValue)
                    payQuery = payQuery.Where(p => p.Date >= deltaThreshold.Value);
                var payments = await payQuery.ToListAsync();
                if (payments.Count > 0)
                {
                    var paymentCmds = new List<NpgsqlBatchCommand>();
                    foreach (var pay in payments)
                    {
                        var sql = @"
                            INSERT INTO payments (id, invoice_id, date, method, amount, notes, owner_user_id, created_at)
                            VALUES (@id, @invoice_id, @date, @method, @amount, @notes, @owner_user_id, NOW())
                            ON CONFLICT (id) DO UPDATE SET
                                invoice_id = EXCLUDED.invoice_id, date = EXCLUDED.date, method = EXCLUDED.method,
                                amount = EXCLUDED.amount, notes = EXCLUDED.notes, owner_user_id = EXCLUDED.owner_user_id;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", pay.Id);
                        cmd.Parameters.AddWithValue("invoice_id", pay.InvoiceId);
                        cmd.Parameters.AddWithValue("date", pay.Date);
                        cmd.Parameters.AddWithValue("method", (object?)pay.Method ?? "Cash");
                        cmd.Parameters.AddWithValue("amount", pay.Amount);
                        cmd.Parameters.AddWithValue("notes", (object?)pay.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)pay.OwnerUserId ?? DBNull.Value);
                        paymentCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, paymentCmds);
                }

                // 11. Purchase Payments
                progressCallback?.Invoke("Syncing Purchase Payments (11/14 tables)...", 90);
                var ppQuery = db.PurchasePayments.AsNoTracking();
                if (deltaThreshold.HasValue)
                    ppQuery = ppQuery.Where(pp => pp.Date >= deltaThreshold.Value);
                var purchasePayments = await ppQuery.ToListAsync();
                if (pushDeletedPurSet.Count > 0)
                {
                    purchasePayments = purchasePayments.Where(pp => !pushDeletedPurSet.Contains(pp.PurchaseId)).ToList();
                }
                if (purchasePayments.Count > 0)
                {
                    var purchasePaymentCmds = new List<NpgsqlBatchCommand>();
                    foreach (var pp in purchasePayments)
                    {
                        var sql = @"
                            INSERT INTO purchase_payments (id, purchase_id, date, method, amount, notes, owner_user_id, created_at)
                            VALUES (@id, @purchase_id, @date, @method, @amount, @notes, @owner_user_id, NOW())
                            ON CONFLICT (id) DO UPDATE SET
                                purchase_id = EXCLUDED.purchase_id, date = EXCLUDED.date, method = EXCLUDED.method,
                                amount = EXCLUDED.amount, notes = EXCLUDED.notes, owner_user_id = EXCLUDED.owner_user_id;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", pp.Id);
                        cmd.Parameters.AddWithValue("purchase_id", pp.PurchaseId);
                        cmd.Parameters.AddWithValue("date", pp.Date);
                        cmd.Parameters.AddWithValue("method", (object?)pp.Method ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("amount", pp.Amount);
                        cmd.Parameters.AddWithValue("notes", (object?)pp.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)pp.OwnerUserId ?? DBNull.Value);
                        purchasePaymentCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, purchasePaymentCmds);
                }

                // 12. Damaged Items
                progressCallback?.Invoke("Syncing Damaged Items (12/14 tables)...", 93);
                var damQuery = db.DamagedItems.AsNoTracking();
                if (deltaThreshold.HasValue)
                    damQuery = damQuery.Where(d => d.Date >= deltaThreshold.Value || d.CreatedAt >= deltaThreshold.Value);
                var damaged = await damQuery.ToListAsync();
                if (damaged.Count > 0)
                {
                    var damagedCmds = new List<NpgsqlBatchCommand>();
                    foreach (var d in damaged)
                    {
                        var sql = @"
                            INSERT INTO damaged_items (id, product_id_ref, batch_number, quantity, reason, loss_amount, date, owner_user_id, created_at)
                            VALUES (@id, @product_id_ref, @batch_number, @quantity, @reason, @loss_amount, @date, @owner_user_id, @created_at)
                            ON CONFLICT (id) DO UPDATE SET
                                product_id_ref = EXCLUDED.product_id_ref, batch_number = EXCLUDED.batch_number,
                                quantity = EXCLUDED.quantity, reason = EXCLUDED.reason, loss_amount = EXCLUDED.loss_amount,
                                date = EXCLUDED.date, owner_user_id = EXCLUDED.owner_user_id;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", d.Id);
                        cmd.Parameters.AddWithValue("product_id_ref", d.ProductIdRef);
                        cmd.Parameters.AddWithValue("batch_number", (object?)d.BatchNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("quantity", d.Quantity);
                        cmd.Parameters.AddWithValue("reason", (object?)d.Reason ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("loss_amount", d.LossAmount);
                        cmd.Parameters.AddWithValue("date", d.Date);
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)d.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("created_at", d.CreatedAt);
                        damagedCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, damagedCmds);
                }

                // 13. Recycle Bin
                progressCallback?.Invoke("Syncing Recycle Bin (13/14 tables)...", 95);
                var recQuery = db.RecycleBin.AsNoTracking();
                if (deltaThreshold.HasValue)
                    recQuery = recQuery.Where(r => r.DeletedAt >= deltaThreshold.Value);
                var rec = await recQuery.ToListAsync();
                if (rec.Count > 0)
                {
                    var recycleCmds = new List<NpgsqlBatchCommand>();
                    foreach (var r in rec)
                    {
                        var sql = @"
                            INSERT INTO recycle_bin (id, entity_type, entity_id, json_data, owner_user_id, deleted_at)
                            VALUES (@id, @entity_type, @entity_id, @json_data, @owner_user_id, @deleted_at)
                            ON CONFLICT (id) DO UPDATE SET
                                entity_type = EXCLUDED.entity_type, entity_id = EXCLUDED.entity_id,
                                json_data = EXCLUDED.json_data, owner_user_id = EXCLUDED.owner_user_id,
                                deleted_at = EXCLUDED.deleted_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", r.Id);
                        cmd.Parameters.AddWithValue("entity_type", r.EntityType ?? "");
                        cmd.Parameters.AddWithValue("entity_id", r.EntityId);
                        cmd.Parameters.AddWithValue("json_data", r.JsonData ?? "");
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)r.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("deleted_at", r.DeletedAt);
                        recycleCmds.Add(cmd);

                        // Ensure active cloud table no longer retains this deleted entity
                        switch (r.EntityType?.ToLowerInvariant())
                        {
                            case "customer":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM customers WHERE id = {r.EntityId}"));
                                break;
                            case "product":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM products WHERE id = {r.EntityId}"));
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM product_batches WHERE product_id_ref = {r.EntityId}"));
                                break;
                            case "invoice":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM invoices WHERE id = {r.EntityId}"));
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM invoice_items WHERE invoice_id = {r.EntityId}"));
                                break;
                            case "purchase":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM purchases WHERE id = {r.EntityId}"));
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM purchase_items WHERE purchase_id = {r.EntityId}"));
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM purchase_payments WHERE purchase_id = {r.EntityId}"));
                                break;
                            case "purchase_payment":
                            case "purchasepayment":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM purchase_payments WHERE id = {r.EntityId}"));
                                break;
                            case "purchase_item":
                            case "purchaseitem":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM purchase_items WHERE id = {r.EntityId}"));
                                break;
                            case "supplier":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM suppliers WHERE id = {r.EntityId}"));
                                break;
                            case "expense":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM expenses WHERE id = {r.EntityId}"));
                                break;
                            case "damageditem":
                                recycleCmds.Add(new NpgsqlBatchCommand($"DELETE FROM damaged_items WHERE id = {r.EntityId}"));
                                break;
                        }
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, recycleCmds);
                }

                // Purge all deleted purchases from PostgreSQL in bulk
                if (pushDeletedPurSet.Count > 0)
                {
                    var idList = string.Join(",", pushDeletedPurSet);
                    var purgeCmds = new List<NpgsqlBatchCommand>
                    {
                        new NpgsqlBatchCommand($"DELETE FROM purchases WHERE id IN ({idList});"),
                        new NpgsqlBatchCommand($"DELETE FROM purchase_items WHERE purchase_id IN ({idList});"),
                        new NpgsqlBatchCommand($"DELETE FROM purchase_payments WHERE purchase_id IN ({idList});")
                    };
                    await ExecuteBatchChunkAsync(conn, purgeCmds);
                }

                // 14. AI Chat Messages
                progressCallback?.Invoke("Syncing AI Messages (14/14 tables)...", 97);
                var chatQuery = db.AiChatMessages.AsNoTracking();
                if (deltaThreshold.HasValue)
                    chatQuery = chatQuery.Where(m => m.Timestamp >= deltaThreshold.Value);
                var chatMsgs = await chatQuery.ToListAsync();
                if (chatMsgs.Count > 0)
                {
                    var chatCmds = new List<NpgsqlBatchCommand>();
                    foreach (var msg in chatMsgs)
                    {
                        var sql = @"
                            INSERT INTO ai_chat_messages (id, role, content, owner_user_id, timestamp)
                            VALUES (@id, @role, @content, @owner_user_id, @timestamp)
                            ON CONFLICT (id) DO UPDATE SET
                                role = EXCLUDED.role, content = EXCLUDED.content, owner_user_id = EXCLUDED.owner_user_id,
                                timestamp = EXCLUDED.timestamp;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("id", msg.Id);
                        cmd.Parameters.AddWithValue("role", msg.Role ?? "User");
                        cmd.Parameters.AddWithValue("content", msg.Content ?? "");
                        cmd.Parameters.AddWithValue("owner_user_id", (object?)msg.OwnerUserId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("timestamp", msg.Timestamp);
                        chatCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, chatCmds);
                }

                // 15. App Settings
                progressCallback?.Invoke("Syncing App Settings (15/15 tables)...", 96);
                var settingsQuery = db.AppSettings.AsNoTracking();
                if (deltaThreshold.HasValue)
                    settingsQuery = settingsQuery.Where(s => s.UpdatedAt >= deltaThreshold.Value);
                var settingsList = await settingsQuery.ToListAsync();
                if (settingsList.Count > 0)
                {
                    var settingsCmds = new List<NpgsqlBatchCommand>();
                    foreach (var s in settingsList)
                    {
                        var val = s.Value;
                        if (s.Key == AppSettingKeys.CompanyCountry)
                        {
                            if (string.IsNullOrWhiteSpace(val) || string.Equals(val, "Pakistan", StringComparison.OrdinalIgnoreCase))
                                val = "India";
                        }
                        if (s.Key == AppSettingKeys.CompanyCity && string.Equals(val, "Lahore", StringComparison.OrdinalIgnoreCase))
                            val = "";
                        if (s.Key == AppSettingKeys.CompanyAddress1 && string.Equals(val, "12 Market Road", StringComparison.OrdinalIgnoreCase))
                            val = "";

                        var sql = @"
                            INSERT INTO app_settings (key, value, updated_at)
                            VALUES (@key, @value, @updated_at)
                            ON CONFLICT (key) DO UPDATE SET
                                value = EXCLUDED.value, updated_at = EXCLUDED.updated_at;";
                        var cmd = new NpgsqlBatchCommand(sql);
                        cmd.Parameters.AddWithValue("key", s.Key);
                        cmd.Parameters.AddWithValue("value", (object?)val ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("updated_at", s.UpdatedAt);
                        settingsCmds.Add(cmd);
                    }
                    totalSynced += await ExecuteBatchChunkAsync(conn, settingsCmds);
                }

                SyncService.NotifySyncCompleted();
                return (true, $"Cloud sync complete: {totalSynced} records synchronized to online database.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PushToCloud Exception Details] {ex}");
                return (false, $"Cloud sync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes all business data entries from the online PostgreSQL cloud database.
        /// </summary>
        public static async Task<(bool Success, string Message)> ClearAllOnlineDataAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                await using var conn = new NpgsqlConnection(normalized);
                await conn.OpenAsync();

                var sql = @"
                    DO $$ DECLARE
                        r RECORD;
                    BEGIN
                        FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename != 'app_users') LOOP
                            EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE;';
                        END LOOP;
                    END $$;";

                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                return (true, "All online cloud database business records have been cleared successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to clear online database: {ex.Message}");
            }
        }

        /// <summary>
        /// Pulls business records from the online PostgreSQL database and updates the local SQLite database.
        /// </summary>
        private static readonly SemaphoreSlim _pullLock = new(1, 1);

        public static async Task<(bool Success, string Message)> PullFromCloudAsync(string connectionString, int? currentUserId = null, Action<string, int>? progressCallback = null, DateTime? sinceUtc = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            await _pullLock.WaitAsync();
            SyncService._isPulling = true;
            try
            {
                return await PullFromCloudInternalAsync(connectionString, currentUserId, progressCallback, sinceUtc);
            }
            finally
            {
                SyncService._isPulling = false;
                _pullLock.Release();
            }
        }

        private static async Task<(bool Success, string Message)> PullFromCloudInternalAsync(string connectionString, int? currentUserId, Action<string, int>? progressCallback, DateTime? sinceUtc = null)
        {
            try
            {
                progressCallback?.Invoke("Connecting to cloud database...", 10);
                var normalized = NormalizeConnectionString(connectionString);
                var csb = new NpgsqlConnectionStringBuilder(normalized)
                {
                    Timeout = 30,
                    CommandTimeout = 90,
                    KeepAlive = 15,
                    TcpKeepAlive = true,
                    ConnectionIdleLifetime = 15,
                    ConnectionPruningInterval = 10
                };

                await using var conn = new NpgsqlConnection(csb.ConnectionString);
                await conn.OpenAsync();

                int? effectiveUserId = currentUserId ?? AuthService.CurrentUser?.Id;
                using var db = new AppDbContext(effectiveUserId);
                await db.Database.OpenConnectionAsync();
                try
                {
                    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 30000; PRAGMA foreign_keys = OFF;");
                }
                catch { }

                try
                {
                    db.Database.ExecuteSqlRaw(@"
                        UPDATE AppSettings SET UpdatedAt = datetime('now') WHERE UpdatedAt = '' OR UpdatedAt IS NULL;
                        UPDATE Customers SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Customers SET UpdatedAt = NULL WHERE UpdatedAt = '';
                        UPDATE Products SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Products SET UpdatedAt = NULL WHERE UpdatedAt = '';
                        UPDATE Invoices SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Purchases SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE Purchases SET UpdatedAt = NULL WHERE UpdatedAt = '';
                        UPDATE Expenses SET Date = datetime('now') WHERE Date = '' OR Date IS NULL;
                        UPDATE Expenses SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE DamagedItems SET Date = datetime('now') WHERE Date = '' OR Date IS NULL;
                        UPDATE DamagedItems SET CreatedAt = datetime('now') WHERE CreatedAt = '' OR CreatedAt IS NULL;
                        UPDATE RecycleBin SET DeletedAt = datetime('now') WHERE DeletedAt = '' OR DeletedAt IS NULL;
                        UPDATE AiChatMessages SET Timestamp = datetime('now') WHERE Timestamp = '' OR Timestamp IS NULL;
                        UPDATE RecycleBin SET EntityType = 'Purchase' WHERE LOWER(EntityType) = 'purchase';
                        DELETE FROM Purchases WHERE Id IN (SELECT EntityId FROM RecycleBin WHERE LOWER(EntityType) = 'purchase');
                        DELETE FROM PurchaseItems WHERE PurchaseId NOT IN (SELECT Id FROM Purchases);
                        DELETE FROM PurchasePayments WHERE PurchaseId NOT IN (SELECT Id FROM Purchases);
                    ");
                }
                catch { }

                var localDeletedPurchases = await db.RecycleBin.IgnoreQueryFilters()
                    .Where(r => r.EntityType != null && r.EntityType.ToLower() == "purchase")
                    .Select(r => r.EntityId)
                    .ToListAsync();
                var allDeletedPurSet = new HashSet<int>(localDeletedPurchases);

                try
                {
                    var recPurSql = $"SELECT entity_id FROM recycle_bin WHERE LOWER(TRIM(entity_type)) = 'purchase'{(effectiveUserId.HasValue ? $" AND owner_user_id = {effectiveUserId.Value}" : "")};";
                    await using var recPurCmd = new NpgsqlCommand(recPurSql, conn);
                    await using var recPurRdr = await recPurCmd.ExecuteReaderAsync();
                    while (await recPurRdr.ReadAsync())
                    {
                        allDeletedPurSet.Add(recPurRdr.GetInt32(0));
                    }
                }
                catch { }

                int restoredCount = 0;

                // Delta pull: only fetch rows updated/created since last sync minus a 5-minute safety buffer for clock drift
                DateTime? deltaThreshold = sinceUtc.HasValue ? sinceUtc.Value.AddMinutes(-5) : null;

                string BuildFilter(string? dateCondition = null, string alias = "")
                {
                    var clauses = new List<string>();
                    if (effectiveUserId.HasValue)
                    {
                        clauses.Add($"{alias}owner_user_id = {effectiveUserId.Value}");
                    }
                    if (deltaThreshold.HasValue && !string.IsNullOrWhiteSpace(dateCondition))
                    {
                        clauses.Add($"({dateCondition})");
                    }
                    return clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : "";
                }

                void AddSinceParam(NpgsqlCommand cmd)
                {
                    if (deltaThreshold.HasValue)
                    {
                        cmd.Parameters.AddWithValue("since", deltaThreshold.Value);
                    }
                }

                // 1. Pull Customers
                progressCallback?.Invoke("Downloading Customers (1/14 tables)...", 15);
                var custSql = $"SELECT id, name, phone, email, address1, address2, city, state, postal_code, country, gst_vat_number, owner_user_id, created_at, updated_at FROM customers{BuildFilter("updated_at >= @since OR created_at >= @since")};";
                await using (var cmd = new NpgsqlCommand(custSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
                        if (existing == null)
                        {
                            existing = new Customer { Id = id };
                            db.Customers.Add(existing);
                        }
                        existing.Name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        existing.Phone = reader.IsDBNull(2) ? null : reader.GetString(2);
                        existing.Email = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.Address1 = reader.IsDBNull(4) ? null : reader.GetString(4);
                        existing.Address2 = reader.IsDBNull(5) ? null : reader.GetString(5);
                        existing.City = reader.IsDBNull(6) ? null : reader.GetString(6);
                        existing.State = reader.IsDBNull(7) ? null : reader.GetString(7);
                        existing.PostalCode = reader.IsDBNull(8) ? null : reader.GetString(8);
                        existing.Country = reader.IsDBNull(9) ? null : reader.GetString(9);
                        existing.GstVatNumber = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.OwnerUserId = reader.IsDBNull(11) ? null : reader.GetInt32(11);
                        existing.CreatedAt = reader.IsDBNull(12) ? DateTime.UtcNow : reader.GetDateTime(12);
                        existing.UpdatedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine($"[Pull:Customers] {ex.InnerException?.Message ?? ex.Message}"); }
                db.ChangeTracker.Clear();

                // 2. Pull Products
                progressCallback?.Invoke("Downloading Products (2/14 tables)...", 22);
                var prodSql = $"SELECT id, barcode, name, hsn, description, price, mrp, category, stock, expiry_alert_days, low_stock_threshold, marketed_by, owner_user_id, created_at, updated_at FROM products{BuildFilter("updated_at >= @since OR created_at >= @since")};";
                await using (var cmd = new NpgsqlCommand(prodSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
                        if (existing == null)
                        {
                            existing = new Product { Id = id };
                            db.Products.Add(existing);
                        }
                        existing.Barcode = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        existing.Name = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        existing.Hsn = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.Description = reader.IsDBNull(4) ? null : reader.GetString(4);
                        existing.Price = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5);
                        existing.MRP = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6);
                        existing.Category = reader.IsDBNull(7) ? null : reader.GetString(7);
                        existing.Stock = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
                        existing.ExpiryAlertDays = reader.IsDBNull(9) ? null : reader.GetInt32(9);
                        existing.LowStockThreshold = reader.IsDBNull(10) ? null : reader.GetDecimal(10);
                        existing.MarketedBy = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.OwnerUserId = reader.IsDBNull(12) ? null : reader.GetInt32(12);
                        existing.CreatedAt = reader.IsDBNull(13) ? DateTime.UtcNow : reader.GetDateTime(13);
                        existing.UpdatedAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 3. Pull Batches
                progressCallback?.Invoke("Downloading Batches (3/14 tables)...", 30);
                var batchSql = $"SELECT id, product_id_ref, batch_number, hsn, expiry, mrp, cost_price, selling_price, stock, expired_stock, bonus, pack, marketed_by, rate, discount_percent, cgst_percent, sgst_percent, igst_percent, owner_user_id, created_at, updated_at FROM product_batches{BuildFilter("updated_at >= @since OR created_at >= @since")};";
                await using (var cmd = new NpgsqlCommand(batchSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.ProductBatches.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id);
                        if (existing == null)
                        {
                            existing = new ProductBatch { Id = id };
                            db.ProductBatches.Add(existing);
                        }
                        existing.ProductIdRef = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        existing.BatchNumber = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        existing.Hsn = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.Expiry = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                        existing.Mrp = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
                        existing.CostPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6);
                        existing.SellingPrice = reader.IsDBNull(7) ? null : reader.GetDecimal(7);
                        existing.Stock = reader.IsDBNull(8) ? null : reader.GetDecimal(8);
                        existing.ExpiredStock = reader.IsDBNull(9) ? null : reader.GetDecimal(9);
                        existing.Bonus = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.Pack = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.MarketedBy = reader.IsDBNull(12) ? null : reader.GetString(12);
                        existing.Rate = reader.IsDBNull(13) ? null : reader.GetDecimal(13);
                        existing.DiscountPercent = reader.IsDBNull(14) ? null : reader.GetDecimal(14);
                        existing.CgstPercent = reader.IsDBNull(15) ? null : reader.GetDecimal(15);
                        existing.SgstPercent = reader.IsDBNull(16) ? null : reader.GetDecimal(16);
                        existing.IgstPercent = reader.IsDBNull(17) ? null : reader.GetDecimal(17);
                        existing.OwnerUserId = reader.IsDBNull(18) ? null : reader.GetInt32(18);
                        existing.CreatedAt = reader.IsDBNull(19) ? DateTime.UtcNow : reader.GetDateTime(19);
                        existing.UpdatedAt = reader.IsDBNull(20) ? null : reader.GetDateTime(20);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 4. Pull Invoices
                progressCallback?.Invoke("Downloading Invoices (4/14 tables)...", 40);
                var invSql = $"SELECT id, invoice_number, invoice_date, due_date, customer_id, status, subtotal, tax_amount, discount_amount, customer_name_snapshot, customer_phone_snapshot, customer_address_snapshot, customer_gst_snapshot, total, total_paid, back_dues, private_notes, owner_user_id, created_at, updated_at FROM invoices{BuildFilter("updated_at >= @since OR created_at >= @since")};";
                await using (var cmd = new NpgsqlCommand(invSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Invoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id);
                        if (existing == null)
                        {
                            existing = new Invoice { Id = id };
                            db.Invoices.Add(existing);
                        }
                        existing.InvoiceNumber = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        existing.InvoiceDate = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);
                        existing.DueDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                        existing.CustomerId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                        existing.Status = reader.IsDBNull(5) ? InvoiceStatus.Draft : (InvoiceStatus)reader.GetInt32(5);
                        existing.Subtotal = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6);
                        existing.TaxAmount = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7);
                        existing.DiscountAmount = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8);
                        existing.CustomerNameSnapshot = reader.IsDBNull(9) ? null : reader.GetString(9);
                        existing.CustomerPhoneSnapshot = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.CustomerAddressSnapshot = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.CustomerGstSnapshot = reader.IsDBNull(12) ? null : reader.GetString(12);
                        existing.Total = reader.IsDBNull(13) ? 0m : reader.GetDecimal(13);
                        existing.TotalPaid = reader.IsDBNull(14) ? 0m : reader.GetDecimal(14);
                        existing.BackDues = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15);
                        existing.PrivateNotes = reader.IsDBNull(16) ? null : reader.GetString(16);
                        existing.OwnerUserId = reader.IsDBNull(17) ? null : reader.GetInt32(17);
                        existing.CreatedAt = reader.IsDBNull(18) ? DateTime.UtcNow : reader.GetDateTime(18);
                        existing.UpdatedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 5. Pull Invoice Items
                progressCallback?.Invoke("Downloading Invoice Items (5/14 tables)...", 50);
                string? invItemDateFilter = deltaThreshold.HasValue
                    ? $"invoice_id IN (SELECT id FROM invoices WHERE (updated_at >= @since OR created_at >= @since){(effectiveUserId.HasValue ? $" AND owner_user_id = {effectiveUserId.Value}" : "")})"
                    : null;
                var invItemSql = $"SELECT id, invoice_id, product_id_ref, product_id, description, price, cost_price, quantity, line_total, product_name, batch_number, owner_user_id FROM invoice_items{BuildFilter(invItemDateFilter)};";
                await using (var cmd = new NpgsqlCommand(invItemSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.InvoiceItems.IgnoreQueryFilters().FirstOrDefaultAsync(ii => ii.Id == id);
                        if (existing == null)
                        {
                            existing = new InvoiceItem { Id = id };
                            db.InvoiceItems.Add(existing);
                        }
                        existing.InvoiceId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        existing.ProductIdRef = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                        existing.ProductId = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        existing.Description = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        existing.Price = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5);
                        existing.CostPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6);
                        existing.Quantity = reader.IsDBNull(7) ? 1m : reader.GetDecimal(7);
                        existing.LineTotal = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8);
                        existing.ProductName = reader.IsDBNull(9) ? "" : reader.GetString(9);
                        existing.BatchNumber = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.OwnerUserId = reader.IsDBNull(11) ? null : reader.GetInt32(11);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 6. Pull Purchases
                progressCallback?.Invoke("Downloading Purchases (6/14 tables)...", 60);
                var purSql = $"SELECT id, supplier_id, purchase_date, invoice_number, subtotal, discount, discount_is_percent, tax, shipping, total, paid, due, notes, extra_cost_name, extra_cost_amount, extra_cost_is_percent, total_discount, total_cgst, total_sgst, total_igst, owner_user_id, created_at, updated_at FROM purchases{BuildFilter("updated_at >= @since OR created_at >= @since")};";
                await using (var cmd = new NpgsqlCommand(purSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        if (allDeletedPurSet.Contains(id))
                        {
                            var ling = await db.Purchases.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
                            if (ling != null) db.Purchases.Remove(ling);
                            continue;
                        }
                        var existing = await db.Purchases.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
                        if (existing == null)
                        {
                            existing = new Purchase { Id = id };
                            db.Purchases.Add(existing);
                        }
                        existing.SupplierId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        existing.PurchaseDate = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);
                        existing.InvoiceNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.Subtotal = reader.IsDBNull(4) ? null : reader.GetDecimal(4);
                        existing.Discount = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
                        existing.DiscountIsPercent = reader.IsDBNull(6) ? null : (reader.GetInt32(6) == 1);
                        existing.Tax = reader.IsDBNull(7) ? null : reader.GetDecimal(7);
                        existing.Shipping = reader.IsDBNull(8) ? null : reader.GetDecimal(8);
                        existing.Total = reader.IsDBNull(9) ? null : reader.GetDecimal(9);
                        existing.Paid = reader.IsDBNull(10) ? null : reader.GetDecimal(10);
                        existing.Due = reader.IsDBNull(11) ? null : reader.GetDecimal(11);
                        existing.Notes = reader.IsDBNull(12) ? null : reader.GetString(12);
                        existing.ExtraCostName = reader.IsDBNull(13) ? null : reader.GetString(13);
                        existing.ExtraCostAmount = reader.IsDBNull(14) ? null : reader.GetDecimal(14);
                        existing.ExtraCostIsPercent = reader.IsDBNull(15) ? null : (reader.GetInt32(15) == 1);
                        existing.TotalDiscount = reader.IsDBNull(16) ? null : reader.GetDecimal(16);
                        existing.TotalCgst = reader.IsDBNull(17) ? null : reader.GetDecimal(17);
                        existing.TotalSgst = reader.IsDBNull(18) ? null : reader.GetDecimal(18);
                        existing.TotalIgst = reader.IsDBNull(19) ? null : reader.GetDecimal(19);
                        existing.OwnerUserId = reader.IsDBNull(20) ? null : reader.GetInt32(20);
                        existing.CreatedAt = reader.IsDBNull(21) ? DateTime.UtcNow : reader.GetDateTime(21);
                        existing.UpdatedAt = reader.IsDBNull(22) ? null : reader.GetDateTime(22);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 7. Pull Purchase Items
                progressCallback?.Invoke("Downloading Purchase Items (7/14 tables)...", 68);
                string? purItemDateFilter = deltaThreshold.HasValue
                    ? $"purchase_id IN (SELECT id FROM purchases WHERE (updated_at >= @since OR created_at >= @since){(effectiveUserId.HasValue ? $" AND owner_user_id = {effectiveUserId.Value}" : "")})"
                    : null;
                var purItemSql = $"SELECT id, purchase_id, product_id, product_name, batch_number, expiry, mrp, cost_price, selling_price, quantity, pack, bonus, marketed_by, rate, discount_percent, cgst_percent, sgst_percent, igst_percent, line_total, owner_user_id FROM purchase_items{BuildFilter(purItemDateFilter)};";
                await using (var cmd = new NpgsqlCommand(purItemSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        int purId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        if (allDeletedPurSet.Contains(purId))
                        {
                            var ling = await db.PurchaseItems.IgnoreQueryFilters().FirstOrDefaultAsync(pi => pi.Id == id);
                            if (ling != null) db.PurchaseItems.Remove(ling);
                            continue;
                        }
                        var existing = await db.PurchaseItems.IgnoreQueryFilters().FirstOrDefaultAsync(pi => pi.Id == id);
                        if (existing == null)
                        {
                            existing = new PurchaseItem { Id = id };
                            db.PurchaseItems.Add(existing);
                        }
                        existing.PurchaseId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        existing.ProductId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                        existing.ProductName = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.BatchNumber = reader.IsDBNull(4) ? null : reader.GetString(4);
                        existing.Expiry = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                        existing.Mrp = reader.IsDBNull(6) ? null : reader.GetDecimal(6);
                        existing.CostPrice = reader.IsDBNull(7) ? null : reader.GetDecimal(7);
                        existing.SellingPrice = reader.IsDBNull(8) ? null : reader.GetDecimal(8);
                        existing.Quantity = reader.IsDBNull(9) ? 1m : reader.GetDecimal(9);
                        existing.Pack = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.Bonus = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.MarketedBy = reader.IsDBNull(12) ? null : reader.GetString(12);
                        existing.Rate = reader.IsDBNull(13) ? null : reader.GetDecimal(13);
                        existing.DiscountPercent = reader.IsDBNull(14) ? null : reader.GetDecimal(14);
                        existing.CgstPercent = reader.IsDBNull(15) ? null : reader.GetDecimal(15);
                        existing.SgstPercent = reader.IsDBNull(16) ? null : reader.GetDecimal(16);
                        existing.IgstPercent = reader.IsDBNull(17) ? null : reader.GetDecimal(17);
                        existing.LineTotal = reader.IsDBNull(18) ? null : reader.GetDecimal(18);
                        existing.OwnerUserId = reader.IsDBNull(19) ? null : reader.GetInt32(19);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 8. Pull Suppliers
                progressCallback?.Invoke("Downloading Suppliers (8/14 tables)...", 75);
                var supSql = $"SELECT id, name, email, phone, city, state, gst_number, back_dues, last_purchase_at, owner_user_id, created_at, updated_at FROM suppliers{BuildFilter("updated_at >= @since OR created_at >= @since")};";
                await using (var cmd = new NpgsqlCommand(supSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Suppliers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
                        if (existing == null)
                        {
                            existing = new Supplier { Id = id };
                            db.Suppliers.Add(existing);
                        }
                        existing.Name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        existing.Email = reader.IsDBNull(2) ? null : reader.GetString(2);
                        existing.Phone = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.City = reader.IsDBNull(4) ? null : reader.GetString(4);
                        existing.State = reader.IsDBNull(5) ? null : reader.GetString(5);
                        existing.GstNumber = reader.IsDBNull(6) ? null : reader.GetString(6);
                        existing.BackDues = reader.IsDBNull(7) ? null : reader.GetDecimal(7);
                        existing.LastPurchaseAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8);
                        existing.OwnerUserId = reader.IsDBNull(9) ? null : reader.GetInt32(9);
                        existing.CreatedAt = reader.IsDBNull(10) ? DateTime.UtcNow : reader.GetDateTime(10);
                        existing.UpdatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 9. Pull Expenses
                progressCallback?.Invoke("Downloading Expenses (9/14 tables)...", 80);
                var expSql = $"SELECT id, description, amount, category, date, note, owner_user_id, created_at FROM expenses{BuildFilter("created_at >= @since OR date >= @since")};";
                await using (var cmd = new NpgsqlCommand(expSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Expenses.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
                        if (existing == null)
                        {
                            existing = new Expense { Id = id };
                            db.Expenses.Add(existing);
                        }
                        existing.Description = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        existing.Amount = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                        existing.Category = reader.IsDBNull(3) ? "General" : reader.GetString(3);
                        existing.Date = reader.IsDBNull(4) ? DateTime.UtcNow : reader.GetDateTime(4);
                        existing.Note = reader.IsDBNull(5) ? null : reader.GetString(5);
                        existing.OwnerUserId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
                        existing.CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 10. Pull Payments
                progressCallback?.Invoke("Downloading Payments (10/14 tables)...", 85);
                var paySql = $"SELECT id, invoice_id, date, method, amount, notes, owner_user_id FROM payments{BuildFilter("created_at >= @since OR date >= @since")};";
                await using (var cmd = new NpgsqlCommand(paySql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Payments.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
                        if (existing == null)
                        {
                            existing = new Payment { Id = id };
                            db.Payments.Add(existing);
                        }
                        existing.InvoiceId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        existing.Date = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);
                        existing.Method = reader.IsDBNull(3) ? "Cash" : reader.GetString(3);
                        existing.Amount = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
                        existing.Notes = reader.IsDBNull(5) ? null : reader.GetString(5);
                        existing.OwnerUserId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 11. Pull Purchase Payments
                progressCallback?.Invoke("Downloading Purchase Payments (11/14 tables)...", 90);
                var purPaySql = $"SELECT id, purchase_id, date, method, amount, notes, owner_user_id FROM purchase_payments{BuildFilter("created_at >= @since OR date >= @since")};";
                await using (var cmd = new NpgsqlCommand(purPaySql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        int purId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        if (allDeletedPurSet.Contains(purId))
                        {
                            var ling = await db.PurchasePayments.IgnoreQueryFilters().FirstOrDefaultAsync(pp => pp.Id == id);
                            if (ling != null) db.PurchasePayments.Remove(ling);
                            continue;
                        }
                        var existing = await db.PurchasePayments.IgnoreQueryFilters().FirstOrDefaultAsync(pp => pp.Id == id);
                        if (existing == null)
                        {
                            existing = new PurchasePayment { Id = id };
                            db.PurchasePayments.Add(existing);
                        }
                        existing.PurchaseId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        existing.Date = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);
                        existing.Method = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.Amount = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
                        existing.Notes = reader.IsDBNull(5) ? null : reader.GetString(5);
                        existing.OwnerUserId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 12. Pull Damaged Items
                progressCallback?.Invoke("Downloading Damaged Items (12/14 tables)...", 93);
                var damSql = $"SELECT id, product_id_ref, batch_number, quantity, reason, loss_amount, date, owner_user_id, created_at FROM damaged_items{BuildFilter("created_at >= @since OR date >= @since")};";
                await using (var cmd = new NpgsqlCommand(damSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.DamagedItems.IgnoreQueryFilters().FirstOrDefaultAsync(di => di.Id == id);
                        if (existing == null)
                        {
                            existing = new DamagedItem { Id = id };
                            db.DamagedItems.Add(existing);
                        }
                        existing.ProductIdRef = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        existing.BatchNumber = reader.IsDBNull(2) ? null : reader.GetString(2);
                        existing.Quantity = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
                        existing.Reason = reader.IsDBNull(4) ? null : reader.GetString(4);
                        existing.LossAmount = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5);
                        existing.Date = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6);
                        existing.OwnerUserId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
                        existing.CreatedAt = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 13. Pull Recycle Bin
                progressCallback?.Invoke("Downloading Recycle Bin (13/14 tables)...", 96);
                var recSql = $"SELECT id, entity_type, entity_id, json_data, owner_user_id, deleted_at FROM recycle_bin{BuildFilter("deleted_at >= @since")};";
                await using (var cmd = new NpgsqlCommand(recSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.RecycleBin.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
                        if (existing == null)
                        {
                            existing = new RecycleBinItem { Id = id };
                            db.RecycleBin.Add(existing);
                        }
                        existing.EntityType = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        if (string.Equals(existing.EntityType.Trim(), "purchase", StringComparison.OrdinalIgnoreCase))
                        {
                            existing.EntityType = "Purchase";
                        }
                        existing.EntityId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        existing.JsonData = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        existing.OwnerUserId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                        existing.DeletedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5);
                        restoredCount++;

                        // Clean up local active entity if it was deleted on another device
                        if (existing.EntityId > 0 && !string.IsNullOrWhiteSpace(existing.EntityType))
                        {
                            switch (existing.EntityType.ToLowerInvariant())
                            {
                                case "customer":
                                    var c = await db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (c != null) db.Customers.Remove(c);
                                    break;
                                case "product":
                                    var p = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (p != null) db.Products.Remove(p);
                                    break;
                                case "invoice":
                                    var inv = await db.Invoices.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (inv != null) db.Invoices.Remove(inv);
                                    break;
                                case "purchase":
                                    var pur = await db.Purchases.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (pur != null) db.Purchases.Remove(pur);
                                    var purItems = await db.PurchaseItems.IgnoreQueryFilters().Where(x => x.PurchaseId == existing.EntityId).ToListAsync();
                                    if (purItems.Count > 0) db.PurchaseItems.RemoveRange(purItems);
                                    var purPays = await db.PurchasePayments.IgnoreQueryFilters().Where(x => x.PurchaseId == existing.EntityId).ToListAsync();
                                    if (purPays.Count > 0) db.PurchasePayments.RemoveRange(purPays);
                                    break;
                                case "purchase_payment":
                                case "purchasepayment":
                                    var purPay = await db.PurchasePayments.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (purPay != null) db.PurchasePayments.Remove(purPay);
                                    break;
                                case "purchase_item":
                                case "purchaseitem":
                                    var purItem = await db.PurchaseItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (purItem != null) db.PurchaseItems.Remove(purItem);
                                    break;
                                case "supplier":
                                    var s = await db.Suppliers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (s != null) db.Suppliers.Remove(s);
                                    break;
                                case "expense":
                                    var exp = await db.Expenses.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (exp != null) db.Expenses.Remove(exp);
                                    break;
                                case "damageditem":
                                    var dam = await db.DamagedItems.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == existing.EntityId);
                                    if (dam != null) db.DamagedItems.Remove(dam);
                                    break;
                            }
                        }
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 14. Pull AI Chat Messages
                progressCallback?.Invoke("Downloading AI Messages (14/14 tables)...", 98);
                var aiSql = $"SELECT id, role, content, owner_user_id, timestamp FROM ai_chat_messages{BuildFilter("timestamp >= @since")};";
                await using (var cmd = new NpgsqlCommand(aiSql, conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.AiChatMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == id);
                        if (existing == null)
                        {
                            existing = new AiChatMessage { Id = id };
                            db.AiChatMessages.Add(existing);
                        }
                        existing.Role = reader.IsDBNull(1) ? "User" : reader.GetString(1);
                        existing.Content = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        existing.OwnerUserId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                        existing.Timestamp = reader.IsDBNull(4) ? DateTime.UtcNow : reader.GetDateTime(4);
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // 15. Pull App Settings
                progressCallback?.Invoke("Downloading App Settings (15/15 tables)...", 99);
                var appSettingsFilter = deltaThreshold.HasValue ? " WHERE updated_at >= @since" : "";
                await using (var cmd = new NpgsqlCommand($"SELECT key, value, updated_at FROM app_settings{appSettingsFilter};", conn))
                {
                    AddSinceParam(cmd);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string key = reader.GetString(0);
                        string? val = reader.IsDBNull(1) ? null : reader.GetString(1);
                        DateTime? cloudUpdatedAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);

                        // Country / City / Address sanitization
                        if (key == AppSettingKeys.CompanyCountry)
                        {
                            if (string.IsNullOrWhiteSpace(val) || string.Equals(val, "Pakistan", StringComparison.OrdinalIgnoreCase))
                            {
                                val = "India";
                            }
                        }
                        if (key == AppSettingKeys.CompanyCity && string.Equals(val, "Lahore", StringComparison.OrdinalIgnoreCase))
                        {
                            val = "";
                        }
                        if (key == AppSettingKeys.CompanyAddress1 && string.Equals(val, "12 Market Road", StringComparison.OrdinalIgnoreCase))
                        {
                            val = "";
                        }

                        var existingRows = await db.AppSettings.IgnoreQueryFilters().Where(s => s.Key == key).ToListAsync();
                        if (existingRows.Count == 0)
                        {
                            var existing = new AppSetting { Key = key, Value = val, UpdatedAt = cloudUpdatedAt ?? DateTime.UtcNow };
                            db.AppSettings.Add(existing);
                        }
                        else
                        {
                            var existing = existingRows[0];
                            if (cloudUpdatedAt.HasValue && cloudUpdatedAt.Value >= existing.UpdatedAt)
                            {
                                existing.Value = val;
                                existing.UpdatedAt = cloudUpdatedAt.Value;
                            }
                            if (existingRows.Count > 1)
                            {
                                db.AppSettings.RemoveRange(existingRows.Skip(1));
                            }
                        }
                        restoredCount++;
                    }
                }
                try { await db.SaveChangesAsync(); } catch (Exception ex) { Console.WriteLine("[Pull] Save error: " + (ex.InnerException?.Message ?? ex.Message)); }
                db.ChangeTracker.Clear();

                // Re-sync current logged-in user's profile so foreign company settings never overwrite user profile
                if (AuthService.CurrentUser != null)
                {
                    AuthService.SyncUserProfileToSettings(AuthService.CurrentUser);
                }

                if (restoredCount > 0)
                {
                    // Align SQLite auto-increment sequences to MAX(Id) so newly generated IDs never collide
                    try
                    {
                        db.Database.ExecuteSqlRaw(@"
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM Invoices) WHERE name = 'Invoices';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM Customers) WHERE name = 'Customers';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM Products) WHERE name = 'Products';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM ProductBatches) WHERE name = 'ProductBatches';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM InvoiceItems) WHERE name = 'InvoiceItems';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM Purchases) WHERE name = 'Purchases';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM PurchaseItems) WHERE name = 'PurchaseItems';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM Payments) WHERE name = 'Payments';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM PurchasePayments) WHERE name = 'PurchasePayments';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM Suppliers) WHERE name = 'Suppliers';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM Expenses) WHERE name = 'Expenses';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM DamagedItems) WHERE name = 'DamagedItems';
                            UPDATE sqlite_sequence SET seq = (SELECT COALESCE(MAX(Id), 1) FROM RecycleBin) WHERE name = 'RecycleBin';
                        ");
                    }
                    catch { }

                    // Deduplicate items and recalculate invoice totals after pull
                    try
                    {
                        db.Database.ExecuteSqlRaw(@"
                            DELETE FROM InvoiceItems 
                            WHERE Id NOT IN (
                                SELECT MIN(Id) 
                                FROM InvoiceItems 
                                GROUP BY InvoiceId, ProductId, BatchNumber, Description, Quantity, Price, LineTotal
                            );
                            DELETE FROM Payments 
                            WHERE Id NOT IN (
                                SELECT MIN(Id) 
                                FROM Payments 
                                GROUP BY InvoiceId, Date, Amount, Method, Notes
                            );
                            UPDATE Invoices 
                            SET Subtotal = COALESCE((SELECT SUM(LineTotal) FROM InvoiceItems WHERE InvoiceId = Invoices.Id), Subtotal),
                                Total = COALESCE((SELECT SUM(LineTotal) FROM InvoiceItems WHERE InvoiceId = Invoices.Id), Subtotal) + TaxAmount - DiscountAmount,
                                TotalPaid = COALESCE((SELECT SUM(Amount) FROM Payments WHERE InvoiceId = Invoices.Id), TotalPaid)
                            WHERE Id >= 4345;

                            DELETE FROM PurchaseItems 
                            WHERE Id NOT IN (
                                SELECT MIN(Id) 
                                FROM PurchaseItems 
                                GROUP BY PurchaseId, COALESCE(ProductId, 0), COALESCE(ProductName, ''), COALESCE(BatchNumber, ''), Quantity, CostPrice, LineTotal
                            );
                            DELETE FROM PurchasePayments 
                            WHERE Id NOT IN (
                                SELECT MIN(Id) 
                                FROM PurchasePayments 
                                GROUP BY PurchaseId, Date, Amount, Method, Notes
                            );
                            UPDATE Purchases 
                            SET Subtotal = COALESCE((SELECT SUM(LineTotal) FROM PurchaseItems WHERE PurchaseId = Purchases.Id), Subtotal),
                                Total = COALESCE((SELECT SUM(LineTotal) FROM PurchaseItems WHERE PurchaseId = Purchases.Id), Subtotal) + COALESCE(Tax, 0) - COALESCE(Discount, 0) + COALESCE(ExtraCostAmount, 0),
                                Paid = COALESCE((SELECT SUM(Amount) FROM PurchasePayments WHERE PurchaseId = Purchases.Id), Paid),
                                Due = (COALESCE((SELECT SUM(LineTotal) FROM PurchaseItems WHERE PurchaseId = Purchases.Id), Subtotal) + COALESCE(Tax, 0) - COALESCE(Discount, 0) + COALESCE(ExtraCostAmount, 0)) - COALESCE((SELECT SUM(Amount) FROM PurchasePayments WHERE PurchaseId = Purchases.Id), Paid);
                            UPDATE RecycleBin SET EntityType = 'Purchase' WHERE LOWER(EntityType) = 'purchase';
                            DELETE FROM Purchases WHERE Id IN (SELECT EntityId FROM RecycleBin WHERE LOWER(EntityType) = 'purchase');
                            DELETE FROM PurchaseItems WHERE PurchaseId NOT IN (SELECT Id FROM Purchases);
                            DELETE FROM PurchasePayments WHERE PurchaseId NOT IN (SELECT Id FROM Purchases);
                        ");
                    }
                    catch { }
                }

                // Purge all deleted purchases from PostgreSQL in bulk
                if (allDeletedPurSet.Count > 0)
                {
                    try
                    {
                        var idList = string.Join(",", allDeletedPurSet);
                        await using var cleanCmd = new NpgsqlCommand(
                            $"DELETE FROM purchases WHERE id IN ({idList}); " +
                            $"DELETE FROM purchase_items WHERE purchase_id IN ({idList}); " +
                            $"DELETE FROM purchase_payments WHERE purchase_id IN ({idList});", conn);
                        await cleanCmd.ExecuteNonQueryAsync();
                    }
                    catch { }
                }

                try { await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;"); } catch { }

                AppSettingsService.Invalidate();
                SyncService.NotifySyncCompleted();

                return (true, $"Cloud restore complete: {restoredCount} records restored from online database.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PullFromCloud Exception Details] {ex}");
                return (false, $"Cloud restore failed: {ex.Message} -> {ex.InnerException?.Message}");
            }
        }

        // =========================================================================
        // SUPABASE CLOUD USER AUTHENTICATION & SYNC
        // =========================================================================

        public static async Task<(bool Success, string Message)> CloudRegisterUserAsync(string connectionString, AppUser user)
        {
            if (string.IsNullOrWhiteSpace(connectionString) || user == null)
                return (false, "Cloud connection or user profile invalid.");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                await using var conn = new NpgsqlConnection(normalized);
                await conn.OpenAsync();

                // Ensure app_users table exists
                await EnsureSchemaAsync(connectionString);

                var sql = @"
                    INSERT INTO app_users (email, password_hash, salt, iterations, full_name, phone, company_name, company_address, company_phone, company_email, company_tax_number, created_at)
                    VALUES (@email, @hash, @salt, @iters, @name, @phone, @cname, @caddr, @cphone, @cemail, @ctax, @created_at)
                    ON CONFLICT (email) DO UPDATE SET
                        password_hash = EXCLUDED.password_hash,
                        salt = EXCLUDED.salt,
                        iterations = EXCLUDED.iterations,
                        full_name = EXCLUDED.full_name,
                        phone = EXCLUDED.phone,
                        company_name = EXCLUDED.company_name,
                        company_address = EXCLUDED.company_address,
                        company_phone = EXCLUDED.company_phone,
                        company_email = EXCLUDED.company_email,
                        company_tax_number = EXCLUDED.company_tax_number
                    RETURNING id;";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("email", user.Email ?? "");
                cmd.Parameters.AddWithValue("hash", user.PasswordHash ?? "");
                cmd.Parameters.AddWithValue("salt", user.PasswordSalt ?? "");
                cmd.Parameters.AddWithValue("iters", user.PasswordIterations > 0 ? user.PasswordIterations : 210000);
                cmd.Parameters.AddWithValue("name", (object?)user.FullName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("phone", (object?)user.Phone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("cname", (object?)user.CompanyName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("caddr", (object?)user.CompanyAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("cphone", (object?)user.CompanyPhone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("cemail", (object?)user.CompanyEmail ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ctax", (object?)user.CompanyTaxNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("created_at", user.CreatedAt);

                var cloudId = await cmd.ExecuteScalarAsync();
                if (cloudId != null && int.TryParse(cloudId.ToString(), out var id) && id > 0)
                {
                    user.Id = id;
                }

                return (true, "Account registered in Supabase Cloud successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Cloud registration failed: {ex.Message}");
            }
        }

        public static async Task<(bool Success, AppUser? User, string Message)> CloudAuthenticateUserAsync(string connectionString, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, null, "Connection string is empty.");

            email = (email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return (false, null, "Email and password are required.");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                await using var conn = new NpgsqlConnection(normalized);
                await conn.OpenAsync();

                var sql = "SELECT id, email, password_hash, salt, iterations, full_name, phone, company_name, company_address, company_phone, company_email, company_tax_number, created_at FROM app_users WHERE email = @email LIMIT 1;";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("email", email);

                await using var rdr = await cmd.ExecuteReaderAsync();
                if (!await rdr.ReadAsync())
                {
                    return (false, null, "No account found with that email in Supabase Cloud.");
                }

                var user = new AppUser
                {
                    Id = rdr.GetInt32(0),
                    Email = rdr.GetString(1),
                    PasswordHash = rdr.GetString(2),
                    PasswordSalt = rdr.GetString(3),
                    PasswordIterations = rdr.GetInt32(4),
                    FullName = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                    Phone = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                    CompanyName = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                    CompanyAddress = rdr.IsDBNull(8) ? null : rdr.GetString(8),
                    CompanyPhone = rdr.IsDBNull(9) ? null : rdr.GetString(9),
                    CompanyEmail = rdr.IsDBNull(10) ? null : rdr.GetString(10),
                    CompanyTaxNumber = rdr.IsDBNull(11) ? null : rdr.GetString(11),
                    CreatedAt = rdr.IsDBNull(12) ? DateTime.UtcNow : rdr.GetDateTime(12)
                };

                if (!AuthService.Verify(password, user))
                {
                    return (false, null, "Incorrect password.");
                }

                return (true, user, "Authenticated via Supabase Cloud successfully.");
            }
            catch (Exception ex)
            {
                return (false, null, $"Cloud authentication error: {ex.Message}");
            }
        }

        public static async Task<(bool Success, string Message)> CloudResetPasswordAsync(string connectionString, string email, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            email = (email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "Enter a valid email and new password (min 6 chars).");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                await using var conn = new NpgsqlConnection(normalized);
                await conn.OpenAsync();

                var saltBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
                var salt = Convert.ToBase64String(saltBytes);
                var iterations = 210000;
                var hashBytes = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(newPassword, saltBytes, iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
                var hash = Convert.ToBase64String(hashBytes);

                var sql = "UPDATE app_users SET password_hash = @hash, salt = @salt, iterations = @iters WHERE email = @email;";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("hash", hash);
                cmd.Parameters.AddWithValue("salt", salt);
                cmd.Parameters.AddWithValue("iters", iterations);
                cmd.Parameters.AddWithValue("email", email);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return (false, "No account found with that email in Supabase Cloud.");

                return (true, "Password reset successfully in Supabase Cloud.");
            }
            catch (Exception ex)
            {
                return (false, $"Password reset failed: {ex.Message}");
            }
        }

        public static async Task<(bool Success, AppUser? User, string Message)> CloudGetUserByEmailAsync(string connectionString, string email)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, null, "Connection string is empty.");

            email = (email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return (false, null, "Email is required.");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                await using var conn = new NpgsqlConnection(normalized);
                await conn.OpenAsync();

                var sql = "SELECT id, email, password_hash, salt, iterations, full_name, phone, company_name, company_address, company_phone, company_email, company_tax_number, created_at FROM app_users WHERE email = @email LIMIT 1;";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("email", email);

                await using var rdr = await cmd.ExecuteReaderAsync();
                if (!await rdr.ReadAsync())
                {
                    return (false, null, "No account found with that email in Supabase Cloud.");
                }

                var user = new AppUser
                {
                    Id = rdr.GetInt32(0),
                    Email = rdr.GetString(1),
                    PasswordHash = rdr.GetString(2),
                    PasswordSalt = rdr.GetString(3),
                    PasswordIterations = rdr.GetInt32(4),
                    FullName = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                    Phone = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                    CompanyName = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                    CompanyAddress = rdr.IsDBNull(8) ? null : rdr.GetString(8),
                    CompanyPhone = rdr.IsDBNull(9) ? null : rdr.GetString(9),
                    CompanyEmail = rdr.IsDBNull(10) ? null : rdr.GetString(10),
                    CompanyTaxNumber = rdr.IsDBNull(11) ? null : rdr.GetString(11),
                    CreatedAt = rdr.IsDBNull(12) ? DateTime.UtcNow : rdr.GetDateTime(12)
                };

                return (true, user, "User found in Supabase Cloud.");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error fetching user from Supabase Cloud: {ex.Message}");
            }
        }
    }
}
