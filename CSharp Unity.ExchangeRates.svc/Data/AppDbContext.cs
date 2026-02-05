using Microsoft.EntityFrameworkCore;
using Unity.ExchangeRates.svc.Models;

namespace Unity.ExchangeRates.svc.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Currency> Currencies { get; set; } = null!;
        public DbSet<ExchangeRateHistory> ExchangeRateHistories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Currency>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).HasColumnName("CurrencyCode").HasMaxLength(10).IsRequired();
                b.Property(e => e.CurrencyName).HasMaxLength(100).IsRequired();
                b.ToTable("Currency");

                // soft-delete global filter
                b.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<ExchangeRateHistory>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.RateDate).HasColumnName("rate_date").IsRequired();
                b.Property(e => e.BuyingRate).HasColumnType("decimal(18,4)");
                b.Property(e => e.SellingRate).HasColumnType("decimal(18,4)");
                b.Property(e => e.MiddleRate).HasColumnType("decimal(18,4)");
                b.ToTable("ExchangeRateHistory");

                // relationship & FK
                b.HasOne(e => e.Currency)
                 .WithMany(c => c.ExchangeRateHistories)
                 .HasForeignKey(e => e.CurrencyCode)
                 .HasPrincipalKey(c => c.Id)
                 .OnDelete(DeleteBehavior.Cascade);

                // ensure uniqueness per currency+rateDate to avoid duplicates
                b.HasIndex(e => new { e.CurrencyCode, e.RateDate }).IsUnique();

                // soft-delete global filter
                b.HasQueryFilter(e => !e.IsDeleted);
            });
        }

        // optional: set CreatedOn/ModifiedOn automatically
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<BaseEntity<object>>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Property(nameof(BaseEntity<object>.CreatedOn)).CurrentValue = now;
                }
                if (entry.State == EntityState.Modified)
                {
                    entry.Property(nameof(BaseEntity<object>.ModifiedOn)).CurrentValue = now;
                }
            }
        }
    }
}