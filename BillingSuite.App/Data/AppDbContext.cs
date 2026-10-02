using BillingSuite.App.Models;
using BillingSuite.App.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BillingSuite.App.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        // New purchasing/suppliers
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Purchase> Purchases => Set<Purchase>();
        public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
        public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
        public DbSet<RecycleBinItem> RecycleBin => Set<RecycleBinItem>();
        public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();
        public DbSet<DamagedItem> DamagedItems => Set<DamagedItem>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();
        // Phase 7: local account(s). Multiple accounts may exist on one device.
        public DbSet<AppUser> Users => Set<AppUser>();


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = Services.AppPaths.DatabaseFile;
                optionsBuilder.UseSqlite($"Data Source={dbPath};Default Timeout=30;")
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            }
        }

        private static bool _batchesEnsured = false;
        private static bool _suppliersEnsured = false;
        private static bool _recycleEnsured = false;

        // Phase 8: captured once per DbContext lifetime (contexts are short-lived / scoped).
        // Using an instance field is the EF Core-recommended way to make query filters
        // re-evaluate on each new context rather than being baked-in at model-build time.
        private readonly int? _currentUserId;

        public AppDbContext()
        {
            _currentUserId = AuthService.CurrentUser?.Id;
            // Database initialization is now handled centrally in DbInitializer
            // to ensure Migrations are applied correctly on startup.
        }

        public AppDbContext(int? currentUserId)
        {
            _currentUserId = currentUserId;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().HasIndex(p => p.Barcode).IsUnique(false);
            modelBuilder.Entity<ProductBatch>()
                .HasOne(pb => pb.Product)
                .WithMany()
                .HasForeignKey(pb => pb.ProductIdRef)
                .OnDelete(DeleteBehavior.Cascade);
            // Map recycle bin entity to table 'RecycleBin' (SQLite ensure step creates this exact table name)
            modelBuilder.Entity<RecycleBinItem>().ToTable("RecycleBin");
            modelBuilder.Entity<ProductBatch>()
                .HasIndex(pb => new { pb.ProductIdRef, pb.BatchNumber })
                .IsUnique(false);

            modelBuilder.Entity<InvoiceItem>()
                .HasOne(ii => ii.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseItem>()
                .HasOne(pi => pi.Purchase)
                .WithMany(p => p.Items)
                .HasForeignKey(pi => pi.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DamagedItem>()
                .HasOne(di => di.Product)
                .WithMany()
                .HasForeignKey(di => di.ProductIdRef)
                .OnDelete(DeleteBehavior.Cascade);

            // Phase 8: per-user isolation.
            // Filter: show row if (no user signed in) OR (row has no owner) OR (row belongs to current user).
            // The filter references _currentUserId (instance field) via Expression.Field so EF Core
            // does NOT bake the value in at model-build time — it reads the field from each context instance.
            var ctxConst = Expression.Constant(this);
            var userIdField = typeof(AppDbContext).GetField("_currentUserId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var currentUserIdExpr = Expression.Field(ctxConst, userIdField); // int?

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(IOwnedEntity).IsAssignableFrom(entityType.ClrType)) continue;

                var param = Expression.Parameter(entityType.ClrType, "e");
                var ownerProp = Expression.Property(param, nameof(IOwnedEntity.OwnerUserId)); // int?

                // When a user is signed in (_currentUserId != null):
                // STRICT ISOLATION: show ONLY rows belonging to the current user (e.OwnerUserId == _currentUserId).
                // When NO user is signed in (_currentUserId == null):
                // Show legacy/unowned rows (e.OwnerUserId == null).
                var signedIn = Expression.NotEqual(currentUserIdExpr, Expression.Constant(null, typeof(int?)));
                var isOwner = Expression.Equal(ownerProp, currentUserIdExpr);
                var noOwner = Expression.Equal(ownerProp, Expression.Constant(null, typeof(int?)));

                var body = Expression.Condition(signedIn, isOwner, noOwner);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, param));
            }

            // Universal resilient DateTime converter for SQLite:
            // Prevents "String '' was not recognized as a valid DateTime" when reading legacy or empty date strings.
            var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, string>(
                v => v.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF"),
                v => ParseSafeDate(v)
            );

            var nullableDateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, string?>(
                v => v.HasValue ? v.Value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF") : null,
                v => ParseSafeNullableDate(v)
            );

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }

        private static DateTime ParseSafeDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DateTime.UtcNow;
            return DateTime.TryParse(s, out var dt) ? dt : DateTime.UtcNow;
        }

        private static DateTime? ParseSafeNullableDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParse(s, out var dt) ? dt : null;
        }

        private void StampOwners()
        {
            var userId = _currentUserId ?? AuthService.CurrentUser?.Id;
            if (userId == null) return;
            foreach (var entry in ChangeTracker.Entries<IOwnedEntity>())
                if (entry.State == EntityState.Added && entry.Entity.OwnerUserId == null)
                    entry.Entity.OwnerUserId = userId.Value;
        }

        private void StampTimestamps()
        {
            if (SyncService._isPulling) return;
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
                {
                    var prop = entry.Metadata.FindProperty("UpdatedAt");
                    if (prop != null)
                    {
                        entry.Property("UpdatedAt").CurrentValue = now;
                    }
                }
            }
        }

        public override int SaveChanges()
        {
            StampOwners();
            StampTimestamps();
            GenerateSkus();
            var res = base.SaveChanges();
            if (res > 0 && !SyncService._isPulling)
            {
                try { SyncService.QueueInstantSync(); } catch { }
            }
            return res;
        }

        public override async Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            StampOwners();
            StampTimestamps();
            GenerateSkus();
            var res = await base.SaveChangesAsync(cancellationToken);
            if (res > 0 && !SyncService._isPulling)
            {
                try { SyncService.QueueInstantSync(); } catch { }
            }
            return res;
        }

        private void GenerateSkus()
        {
            var newProducts = ChangeTracker.Entries<Product>()
                .Where(e => e.State == EntityState.Added && string.IsNullOrWhiteSpace(e.Entity.Barcode))
                .ToList();

            if (newProducts.Any())
            {
                // Find current max numeric SKU
                long maxSku = 10000;
                var allBarcodes = Products.Select(p => p.Barcode).ToList();
                foreach (var bc in allBarcodes)
                {
                    if (long.TryParse(bc, out long val))
                    {
                        if (val > maxSku) maxSku = val;
                    }
                }

                foreach (var entry in newProducts)
                {
                    maxSku++;
                    entry.Entity.Barcode = maxSku.ToString();
                }
            }
        }
    }
}
