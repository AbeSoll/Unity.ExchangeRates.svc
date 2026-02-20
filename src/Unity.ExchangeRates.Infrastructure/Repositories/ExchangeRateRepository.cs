using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Infrastructure.Data;
using Unity.ExchangeRates.Repository;

namespace Unity.ExchangeRates.Infrastructure.Repositories
{
    public class ExchangeRateRepository : IExchangeRateRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ExchangeRateRepository> _logger;

        public ExchangeRateRepository(AppDbContext context, ILogger<ExchangeRateRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Currency>> GetActiveCurrenciesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: GetActiveCurrenciesAsync called");
            var list = await _context.Currencies.ToListAsync(cancellationToken);
            _logger.LogInformation("Repository: GetActiveCurrenciesAsync returned {Count} currencies", list.Count);
            return list;
        }

        public async Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: AddRateHistoryAsync for CurrencyCode={CurrencyCode}, RateDate={RateDate}",
                history.CurrencyCode, history.RateDate);
            await _context.ExchangeRateHistories.AddAsync(history, cancellationToken);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: SaveChangesAsync called");
            var count = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Repository: SaveChangesAsync persisted {Count} changes", count);
            return count;
        }
    }
}
