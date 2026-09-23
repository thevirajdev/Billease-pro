using System;
using System.IO;
using BillingSuite.App.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BillingSuite.App.Data
{
    // Provides AppDbContext for 'dotnet ef' at design time
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var dbPath = Services.AppPaths.DatabaseFile;
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            return new AppDbContext();
        }
    }
}
