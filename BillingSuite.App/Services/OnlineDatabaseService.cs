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
                catch
                {
                    // Fallback to raw if URI parse fails
                }
            }

            try
            {
                var csb = new NpgsqlConnectionStringBuilder(raw);
                if (!raw.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase))
                    csb.SslMode = SslMode.Require;
                if (!raw.Contains("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
                    csb.TrustServerCertificate = true;
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

        /// <summary>
        /// Ensures all necessary tables and indexes exist on the online PostgreSQL database.
        /// </summary>
        public static async Task<(bool Success, string Message)> EnsureSchemaAsync(string connectionString)
        {
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
                    owner_user_id INT,
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    updated_at TIMESTAMPTZ DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS idx_purchases_owner ON purchases(owner_user_id);

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
                    line_total NUMERIC(18,2) DEFAULT 0,
                    owner_user_id INT
                );
                CREATE INDEX IF NOT EXISTS idx_purchase_items_owner ON purchase_items(owner_user_id);

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
                ";

                await using var cmd = new NpgsqlCommand(ddl, conn);
                await cmd.ExecuteNonQueryAsync();

                return (true, "Cloud database schema initialized successfully. All tables and user indexes are ready.");
            }
            catch (Exception ex)
            {
                return (false, $"Schema initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Pushes local SQLite business records to the online PostgreSQL database using UPSERT.
        /// Ensures data is properly isolated by OwnerUserId.
        /// </summary>
        public static async Task<(bool Success, string Message)> PushToCloudAsync(string connectionString, int? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            try
            {
                if (AppSettingsService.SyncAutoMigrate)
                {
                    await EnsureSchemaAsync(connectionString);
                }

                var normalized = NormalizeConnectionString(connectionString);
                var csb = new NpgsqlConnectionStringBuilder(normalized)
                {
                    Timeout = 15,
                    CommandTimeout = 60
                };

                await using var conn = new NpgsqlConnection(csb.ConnectionString);
                await conn.OpenAsync();

                using var db = new AppDbContext();
                int totalSynced = 0;

                // 1. Customers
                var customers = await db.Customers.AsNoTracking().ToListAsync();
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
                    await using var cmd = new NpgsqlCommand(sql, conn);
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
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                // 2. Products
                var products = await db.Products.AsNoTracking().ToListAsync();
                foreach (var p in products)
                {
                    var sql = @"
                        INSERT INTO products (id, barcode, name, hsn, description, price, mrp, category, stock, expiry_alert_days, low_stock_threshold, marketed_by, owner_user_id, created_at, updated_at)
                        VALUES (@id, @barcode, @name, @hsn, @description, @price, @mrp, @category, @stock, @expiry_alert_days, @low_stock_threshold, @marketed_by, @owner_user_id, @created_at, @updated_at)
                        ON CONFLICT (id) DO UPDATE SET
                            barcode = EXCLUDED.barcode, name = EXCLUDED.name, hsn = EXCLUDED.hsn, description = EXCLUDED.description,
                            price = EXCLUDED.price, mrp = EXCLUDED.mrp, category = EXCLUDED.category, stock = EXCLUDED.stock,
                            expiry_alert_days = EXCLUDED.expiry_alert_days, low_stock_threshold = EXCLUDED.low_stock_threshold,
                            marketed_by = EXCLUDED.marketed_by, owner_user_id = EXCLUDED.owner_user_id, updated_at = EXCLUDED.updated_at;";
                    await using var cmd = new NpgsqlCommand(sql, conn);
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
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                // 3. Product Batches
                var batches = await db.ProductBatches.AsNoTracking().ToListAsync();
                foreach (var b in batches)
                {
                    var sql = @"
                        INSERT INTO product_batches (id, product_id_ref, batch_number, hsn, expiry, mrp, cost_price, selling_price, stock, bonus, pack, marketed_by, rate, discount_percent, cgst_percent, sgst_percent, igst_percent, owner_user_id, created_at, updated_at)
                        VALUES (@id, @product_id_ref, @batch_number, @hsn, @expiry, @mrp, @cost_price, @selling_price, @stock, @bonus, @pack, @marketed_by, @rate, @discount_percent, @cgst_percent, @sgst_percent, @igst_percent, @owner_user_id, @created_at, @updated_at)
                        ON CONFLICT (id) DO UPDATE SET
                            product_id_ref = EXCLUDED.product_id_ref, batch_number = EXCLUDED.batch_number, hsn = EXCLUDED.hsn,
                            expiry = EXCLUDED.expiry, mrp = EXCLUDED.mrp, cost_price = EXCLUDED.cost_price,
                            selling_price = EXCLUDED.selling_price, stock = EXCLUDED.stock, bonus = EXCLUDED.bonus,
                            pack = EXCLUDED.pack, marketed_by = EXCLUDED.marketed_by, rate = EXCLUDED.rate,
                            discount_percent = EXCLUDED.discount_percent, cgst_percent = EXCLUDED.cgst_percent,
                            sgst_percent = EXCLUDED.sgst_percent, igst_percent = EXCLUDED.igst_percent,
                            owner_user_id = EXCLUDED.owner_user_id, updated_at = EXCLUDED.updated_at;";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("id", b.Id);
                    cmd.Parameters.AddWithValue("product_id_ref", b.ProductIdRef);
                    cmd.Parameters.AddWithValue("batch_number", (object?)b.BatchNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("hsn", (object?)b.Hsn ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("expiry", (object?)b.Expiry ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("mrp", (object?)b.Mrp ?? 0m);
                    cmd.Parameters.AddWithValue("cost_price", (object?)b.CostPrice ?? 0m);
                    cmd.Parameters.AddWithValue("selling_price", (object?)b.SellingPrice ?? 0m);
                    cmd.Parameters.AddWithValue("stock", (object?)b.Stock ?? 0m);
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
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                // 4. Invoices
                var invoices = await db.Invoices.AsNoTracking().ToListAsync();
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
                    await using var cmd = new NpgsqlCommand(sql, conn);
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
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                // 5. Invoice Items
                var invoiceItems = await db.InvoiceItems.AsNoTracking().ToListAsync();
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
                    await using var cmd = new NpgsqlCommand(sql, conn);
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
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                // 6. Purchases
                var purchases = await db.Purchases.AsNoTracking().ToListAsync();
                foreach (var pur in purchases)
                {
                    var sql = @"
                        INSERT INTO purchases (id, supplier_id, purchase_date, invoice_number, subtotal, discount, tax, shipping, total, paid, due, notes, owner_user_id, created_at, updated_at)
                        VALUES (@id, @supplier_id, @purchase_date, @invoice_number, @subtotal, @discount, @tax, @shipping, @total, @paid, @due, @notes, @owner_user_id, @created_at, @updated_at)
                        ON CONFLICT (id) DO UPDATE SET
                            supplier_id = EXCLUDED.supplier_id, purchase_date = EXCLUDED.purchase_date,
                            invoice_number = EXCLUDED.invoice_number, subtotal = EXCLUDED.subtotal, discount = EXCLUDED.discount,
                            tax = EXCLUDED.tax, shipping = EXCLUDED.shipping, total = EXCLUDED.total, paid = EXCLUDED.paid,
                            due = EXCLUDED.due, notes = EXCLUDED.notes, owner_user_id = EXCLUDED.owner_user_id,
                            updated_at = EXCLUDED.updated_at;";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("id", pur.Id);
                    cmd.Parameters.AddWithValue("supplier_id", pur.SupplierId);
                    cmd.Parameters.AddWithValue("purchase_date", pur.PurchaseDate);
                    cmd.Parameters.AddWithValue("invoice_number", (object?)pur.InvoiceNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("subtotal", (object?)pur.Subtotal ?? 0m);
                    cmd.Parameters.AddWithValue("discount", (object?)pur.Discount ?? 0m);
                    cmd.Parameters.AddWithValue("tax", (object?)pur.Tax ?? 0m);
                    cmd.Parameters.AddWithValue("shipping", (object?)pur.Shipping ?? 0m);
                    cmd.Parameters.AddWithValue("total", (object?)pur.Total ?? 0m);
                    cmd.Parameters.AddWithValue("paid", (object?)pur.Paid ?? 0m);
                    cmd.Parameters.AddWithValue("due", (object?)pur.Due ?? 0m);
                    cmd.Parameters.AddWithValue("notes", (object?)pur.Notes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("owner_user_id", (object?)pur.OwnerUserId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("created_at", pur.CreatedAt);
                    cmd.Parameters.AddWithValue("updated_at", (object?)pur.UpdatedAt ?? DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                // 7. Suppliers
                var suppliers = await db.Suppliers.AsNoTracking().ToListAsync();
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
                    await using var cmd = new NpgsqlCommand(sql, conn);
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
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                // 8. Expenses
                var expenses = await db.Expenses.AsNoTracking().ToListAsync();
                foreach (var exp in expenses)
                {
                    var sql = @"
                        INSERT INTO expenses (id, description, amount, category, date, note, owner_user_id, created_at)
                        VALUES (@id, @description, @amount, @category, @date, @note, @owner_user_id, @created_at)
                        ON CONFLICT (id) DO UPDATE SET
                            description = EXCLUDED.description, amount = EXCLUDED.amount, category = EXCLUDED.category,
                            date = EXCLUDED.date, note = EXCLUDED.note, owner_user_id = EXCLUDED.owner_user_id;";
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("id", exp.Id);
                    cmd.Parameters.AddWithValue("description", exp.Description ?? "");
                    cmd.Parameters.AddWithValue("amount", exp.Amount);
                    cmd.Parameters.AddWithValue("category", exp.Category ?? "General");
                    cmd.Parameters.AddWithValue("date", exp.Date);
                    cmd.Parameters.AddWithValue("note", (object?)exp.Note ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("owner_user_id", (object?)exp.OwnerUserId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("created_at", exp.CreatedAt);
                    await cmd.ExecuteNonQueryAsync();
                    totalSynced++;
                }

                return (true, $"Cloud sync complete: {totalSynced} records synchronized to online database.");
            }
            catch (Exception ex)
            {
                return (false, $"Cloud sync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Pulls business records from the online PostgreSQL database and updates the local SQLite database.
        /// </summary>
        public static async Task<(bool Success, string Message)> PullFromCloudAsync(string connectionString, int? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (false, "Connection string is empty.");

            try
            {
                var normalized = NormalizeConnectionString(connectionString);
                var csb = new NpgsqlConnectionStringBuilder(normalized)
                {
                    Timeout = 15,
                    CommandTimeout = 60
                };

                await using var conn = new NpgsqlConnection(csb.ConnectionString);
                await conn.OpenAsync();

                using var db = new AppDbContext();
                int restoredCount = 0;

                string OwnerFilter(string alias = "") =>
                    currentUserId.HasValue ? $" WHERE {alias}owner_user_id = {currentUserId.Value}" : "";

                // 1. Pull Customers
                await using (var cmd = new NpgsqlCommand($"SELECT id, name, phone, email, address1, address2, city, state, postal_code, country, gst_vat_number, owner_user_id, created_at, updated_at FROM customers{OwnerFilter()};", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
                        if (existing == null)
                        {
                            existing = new Customer { Id = id };
                            db.Customers.Add(existing);
                        }
                        existing.Name = reader.GetString(1);
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
                        existing.CreatedAt = reader.GetDateTime(12);
                        existing.UpdatedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13);
                        restoredCount++;
                    }
                }
                await db.SaveChangesAsync();

                // 2. Pull Products
                await using (var cmd = new NpgsqlCommand($"SELECT id, barcode, name, hsn, description, price, mrp, category, stock, expiry_alert_days, low_stock_threshold, marketed_by, owner_user_id, created_at, updated_at FROM products{OwnerFilter()};", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
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
                        existing.Name = reader.GetString(2);
                        existing.Hsn = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.Description = reader.IsDBNull(4) ? null : reader.GetString(4);
                        existing.Price = reader.GetDecimal(5);
                        existing.MRP = reader.GetDecimal(6);
                        existing.Category = reader.IsDBNull(7) ? null : reader.GetString(7);
                        existing.Stock = reader.GetInt32(8);
                        existing.ExpiryAlertDays = reader.IsDBNull(9) ? null : reader.GetInt32(9);
                        existing.LowStockThreshold = reader.IsDBNull(10) ? null : reader.GetDecimal(10);
                        existing.MarketedBy = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.OwnerUserId = reader.IsDBNull(12) ? null : reader.GetInt32(12);
                        existing.CreatedAt = reader.GetDateTime(13);
                        existing.UpdatedAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14);
                        restoredCount++;
                    }
                }
                await db.SaveChangesAsync();

                // 3. Pull Batches
                await using (var cmd = new NpgsqlCommand($"SELECT id, product_id_ref, batch_number, hsn, expiry, mrp, cost_price, selling_price, stock, bonus, pack, marketed_by, rate, discount_percent, cgst_percent, sgst_percent, igst_percent, owner_user_id, created_at, updated_at FROM product_batches{OwnerFilter()};", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.ProductBatches.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id);
                        if (existing == null)
                        {
                            existing = new ProductBatch { Id = id };
                            db.ProductBatches.Add(existing);
                        }
                        existing.ProductIdRef = reader.GetInt32(1);
                        existing.BatchNumber = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        existing.Hsn = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.Expiry = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                        existing.Mrp = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
                        existing.CostPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6);
                        existing.SellingPrice = reader.IsDBNull(7) ? null : reader.GetDecimal(7);
                        existing.Stock = reader.IsDBNull(8) ? null : reader.GetDecimal(8);
                        existing.Bonus = reader.IsDBNull(9) ? null : reader.GetString(9);
                        existing.Pack = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.MarketedBy = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.Rate = reader.IsDBNull(12) ? null : reader.GetDecimal(12);
                        existing.DiscountPercent = reader.IsDBNull(13) ? null : reader.GetDecimal(13);
                        existing.CgstPercent = reader.IsDBNull(14) ? null : reader.GetDecimal(14);
                        existing.SgstPercent = reader.IsDBNull(15) ? null : reader.GetDecimal(15);
                        existing.IgstPercent = reader.IsDBNull(16) ? null : reader.GetDecimal(16);
                        existing.OwnerUserId = reader.IsDBNull(17) ? null : reader.GetInt32(17);
                        existing.CreatedAt = reader.GetDateTime(18);
                        existing.UpdatedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19);
                        restoredCount++;
                    }
                }
                await db.SaveChangesAsync();

                // 4. Pull Invoices
                await using (var cmd = new NpgsqlCommand($"SELECT id, invoice_number, invoice_date, due_date, customer_id, status, subtotal, tax_amount, discount_amount, customer_name_snapshot, customer_phone_snapshot, customer_address_snapshot, customer_gst_snapshot, total, total_paid, back_dues, private_notes, owner_user_id, created_at, updated_at FROM invoices{OwnerFilter()};", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Invoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id);
                        if (existing == null)
                        {
                            existing = new Invoice { Id = id };
                            db.Invoices.Add(existing);
                        }
                        existing.InvoiceNumber = reader.GetString(1);
                        existing.InvoiceDate = reader.GetDateTime(2);
                        existing.DueDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                        existing.CustomerId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                        existing.Status = (InvoiceStatus)reader.GetInt32(5);
                        existing.Subtotal = reader.GetDecimal(6);
                        existing.TaxAmount = reader.GetDecimal(7);
                        existing.DiscountAmount = reader.GetDecimal(8);
                        existing.CustomerNameSnapshot = reader.IsDBNull(9) ? null : reader.GetString(9);
                        existing.CustomerPhoneSnapshot = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.CustomerAddressSnapshot = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.CustomerGstSnapshot = reader.IsDBNull(12) ? null : reader.GetString(12);
                        existing.Total = reader.GetDecimal(13);
                        existing.TotalPaid = reader.GetDecimal(14);
                        existing.BackDues = reader.GetDecimal(15);
                        existing.PrivateNotes = reader.IsDBNull(16) ? null : reader.GetString(16);
                        existing.OwnerUserId = reader.IsDBNull(17) ? null : reader.GetInt32(17);
                        existing.CreatedAt = reader.GetDateTime(18);
                        existing.UpdatedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19);
                        restoredCount++;
                    }
                }
                await db.SaveChangesAsync();

                // 5. Pull Expenses
                await using (var cmd = new NpgsqlCommand($"SELECT id, description, amount, category, date, note, owner_user_id, created_at FROM expenses{OwnerFilter()};", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        var existing = await db.Expenses.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
                        if (existing == null)
                        {
                            existing = new Expense { Id = id };
                            db.Expenses.Add(existing);
                        }
                        existing.Description = reader.GetString(1);
                        existing.Amount = reader.GetDecimal(2);
                        existing.Category = reader.GetString(3);
                        existing.Date = reader.GetDateTime(4);
                        existing.Note = reader.IsDBNull(5) ? null : reader.GetString(5);
                        existing.OwnerUserId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
                        existing.CreatedAt = reader.GetDateTime(7);
                        restoredCount++;
                    }
                }
                await db.SaveChangesAsync();

                return (true, $"Cloud restore complete: {restoredCount} records restored from online database.");
            }
            catch (Exception ex)
            {
                return (false, $"Cloud restore failed: {ex.Message}");
            }
        }
    }
}
