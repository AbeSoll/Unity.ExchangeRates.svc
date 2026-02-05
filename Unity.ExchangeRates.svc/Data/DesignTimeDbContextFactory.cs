using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Unity.ExchangeRates.svc.Data
{
    // Provides EF tools a guaranteed way to create AppDbContext at design-time
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>();
            // Fallback connection used by migrations/PMC — change to your dev server if needed
            options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=UnityExchangeRatesDb;Trusted_Connection=True;TrustServerCertificate=True;");
            return new AppDbContext(options.Options);
        }
    }
}
