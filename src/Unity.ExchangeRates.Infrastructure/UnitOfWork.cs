using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.Infrastructure.Data;
using Unity.ExchangeRates.Repository;

namespace Unity.ExchangeRates.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private IDbContextTransaction? _transaction;

        public IExchangeRateRepository ExchangeRates { get; }

        public UnitOfWork(
            AppDbContext context,
            IExchangeRateRepository exchangeRates,
            ILogger<UnitOfWork> logger)
        {
            _context = context;
            ExchangeRates = exchangeRates;
            _logger = logger;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("UnitOfWork: SaveChangesAsync called");
            var count = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("UnitOfWork: SaveChangesAsync persisted {Count} changes", count);
            return count;
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            _logger.LogDebug("UnitOfWork: Transaction started");
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            if (_transaction is null) return;
            await _transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("UnitOfWork: Transaction committed");
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_transaction is null) return;
            await _transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning("UnitOfWork: Transaction rolled back");
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}