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

        public async Task<ExchangeRateHistory?> GetRateByCreatedDateAsync(string currencyCode, DateTime createdDate, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: GetRateByCreatedDateAsync for CurrencyCode={CurrencyCode}, CreatedDate={CreatedDate}",
                currencyCode, createdDate);
            var history = await _context.ExchangeRateHistories
                .FirstOrDefaultAsync(h => h.CurrencyCode == currencyCode && h.CreatedOn.Date == createdDate.Date, cancellationToken);
            if (history is not null)
                _logger.LogInformation("Repository: Found rate for {CurrencyCode} on CreatedDate={CreatedDate}", currencyCode, createdDate);
            else
                _logger.LogDebug("Repository: No rate found for {CurrencyCode} on CreatedDate={CreatedDate}", currencyCode, createdDate);
            return history;
        }

        public async Task<List<ExchangeRateHistory>> GetAllRatesByDateAsync(DateTime createdDate, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: GetAllRatesByDateAsync for CreatedDate={CreatedDate}", createdDate);
            var histories = await _context.ExchangeRateHistories
                .Where(h => h.CreatedOn.Date == createdDate.Date)
                .OrderBy(h => h.CurrencyCode)
                .ToListAsync(cancellationToken);
            _logger.LogInformation("Repository: GetAllRatesByDateAsync returned {Count} rates for {CreatedDate}", histories.Count, createdDate);
            return histories;
        }

        public async Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: AddRateHistoryAsync for CurrencyCode={CurrencyCode}, RateDate={RateDate}",
                history.CurrencyCode, history.RateDate);
            await _context.ExchangeRateHistories.AddAsync(history, cancellationToken);
        }
    }
}
