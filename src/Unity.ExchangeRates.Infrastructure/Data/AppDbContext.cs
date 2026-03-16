using Microsoft.EntityFrameworkCore;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Infrastructure.Interceptors;

namespace Unity.ExchangeRates.Infrastructure.Data
{
    public class AppDbContext(
        EntitySaveChangeInterceptor interceptor,
        DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        private readonly EntitySaveChangeInterceptor _interceptor = interceptor;

        public DbSet<Currency> Currencies { get; set; } = null!;
        public DbSet<ExchangeRateHistory> ExchangeRateHistories { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Currency>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id)
                 .HasColumnName("CurrencyId")
                 .ValueGeneratedOnAdd();
                b.Property(e => e.CurrencyCode)
                 .HasMaxLength(10)
                 .IsRequired();
                b.HasIndex(e => e.CurrencyCode)
                 .IsUnique();
                b.Property(e => e.CurrencyName)
                 .HasMaxLength(100)
                 .IsRequired();
                b.ToTable("Currency");
            });

            modelBuilder.Entity<ExchangeRateHistory>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.RateDate)
                 .HasColumnName("RateDate")
                 .HasDefaultValueSql("GETDATE()");
                b.Property(e => e.Session)
                 .HasMaxLength(4)
                 .IsRequired();
                b.Property(e => e.BuyingRate).HasColumnType("decimal(18,4)");
                b.Property(e => e.SellingRate).HasColumnType("decimal(18,4)");
                b.Property(e => e.MiddleRate).HasColumnType("decimal(18,4)");
                b.HasOne(e => e.Currency)
                 .WithMany()
                 .HasForeignKey(e => e.CurrencyId)
                 .OnDelete(DeleteBehavior.Restrict);
                b.HasIndex(e => new { e.RateDate, e.Session, e.CurrencyCode })
                 .IsUnique()
                 .HasDatabaseName("IX_ExchangeRateHistory_RateDate_Session_CurrencyCode");
                b.ToTable("ExchangeRateHistory");
            });

            modelBuilder.Entity<AuditLog>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.CreatedOn).HasDefaultValueSql("GETDATE()");
                b.HasIndex(e => e.TraceId);
                b.HasIndex(e => e.CreatedOn);
                b.HasIndex(e => e.ResponseStatusCode);
                b.ToTable("AuditLog");
            });
        }
    }
}
