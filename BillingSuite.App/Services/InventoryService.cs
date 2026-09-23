using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using BillingSuite.App.Data;
using BillingSuite.App.Models;

namespace BillingSuite.App.Services
{
    public static class InventoryService
    {
        /// <summary>
        /// Scans all batches and moves stock for expired items to ExpiredStock.
        /// </summary>
        public static void SyncExpiredStock(AppDbContext db)
        {
            var today = DateTime.Today;
            var expiredBatches = db.ProductBatches
                .Where(b => b.Expiry != null && b.Expiry < today && (b.Stock ?? 0) > 0)
                .ToList();

            if (expiredBatches.Count > 0)
            {
                foreach (var b in expiredBatches)
                {
                    b.ExpiredStock = (b.ExpiredStock ?? 0) + (b.Stock ?? 0);
                    b.Stock = 0;
                    b.UpdatedAt = DateTime.UtcNow;
                    db.Entry(b).State = EntityState.Modified;
                }
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Syncs product-level metadata (MarketedBy, etc.) to all batches of the product.
        /// </summary>
        public static void SyncProductMetadata(AppDbContext db, int productId, string marketedBy)
        {
            var batches = db.ProductBatches.Where(b => b.ProductIdRef == productId).ToList();
            foreach (var b in batches)
            {
                b.MarketedBy = marketedBy;
                b.UpdatedAt = DateTime.UtcNow;
                db.Entry(b).State = EntityState.Modified;
            }
            
            var prod = db.Products.FirstOrDefault(p => p.Id == productId);
            if (prod != null)
            {
                prod.MarketedBy = marketedBy;
                prod.UpdatedAt = DateTime.UtcNow;
                db.Entry(prod).State = EntityState.Modified;
            }
            
            db.SaveChanges();
        }
    }
}
