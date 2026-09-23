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
                optionsBuilder.UseSqlite($"Data Source={dbPath}")
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

                // _currentUserId == null  → not signed in, see all
                var notSignedIn = Expression.Equal(currentUserIdExpr, Expression.Constant(null, typeof(int?)));
                // e.OwnerUserId == null   → legacy / shared row
                var noOwner = Expression.Equal(ownerProp, Expression.Constant(null, typeof(int?)));
                // e.OwnerUserId == _currentUserId
                var isOwner = Expression.Equal(ownerProp, currentUserIdExpr);

                var body = Expression.OrElse(notSignedIn, Expression.OrElse(noOwner, isOwner));
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, param));
            }
        }

        private void StampOwners()
        {
            var user = AuthService.CurrentUser;
            if (user == null) return;
            foreach (var entry in ChangeTracker.Entries<IOwnedEntity>())
                if (entry.State == EntityState.Added && entry.Entity.OwnerUserId == null)
                    entry.Entity.OwnerUserId = user.Id;
        }

        public override int SaveChanges()
        {
            StampOwners();
            GenerateSkus();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            StampOwners();
            GenerateSkus();
            return base.SaveChangesAsync(cancellationToken);
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
