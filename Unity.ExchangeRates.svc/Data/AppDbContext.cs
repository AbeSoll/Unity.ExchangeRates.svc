using Microsoft.EntityFrameworkCore;
using Unity.ExchangeRates.svc.Models;

namespace Unity.ExchangeRates.svc.Data
{
    // The main database context class responsible for interacting with the database
    public class AppDbContext : DbContext
    {
        // Constructor to pass configuration options (like connection string) to the base class
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Represents the "Currency" table in the database
        public DbSet<Currency> Currencies { get; set; } = null!;

        // Represents the "ExchangeRateHistory" table in the database
        public DbSet<ExchangeRateHistory> ExchangeRateHistories { get; set; } = null!;

        // Configures the model properties and relationships
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Map BaseEntity<string>.Id for Currency to the legacy column CurrencyCode
            modelBuilder.Entity<Currency>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id)
                 .HasColumnName("CurrencyCode")
                 .HasMaxLength(10)
                 .IsRequired();

                b.Property(e => e.CurrencyName)
                 .HasMaxLength(100)
                 .IsRequired();

                b.ToTable("Currency");
            });

            // Ensure RateDate column mapping for ExchangeRateHistory uses PascalCase "RateDate"
            modelBuilder.Entity<ExchangeRateHistory>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.RateDate)
                 .HasColumnName("RateDate")
                 .HasDefaultValueSql("GETDATE()");
                b.Property(e => e.BuyingRate).HasColumnType("decimal(18,4)");
                b.Property(e => e.SellingRate).HasColumnType("decimal(18,4)");
                b.Property(e => e.MiddleRate).HasColumnType("decimal(18,4)");
                b.ToTable("ExchangeRateHistory");
            });
        }
    }
}