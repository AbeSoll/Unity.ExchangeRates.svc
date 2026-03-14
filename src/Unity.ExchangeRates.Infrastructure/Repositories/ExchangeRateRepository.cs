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
            var list = await _context.Currencies.AsNoTracking().ToListAsync(cancellationToken);
            _logger.LogDebug("Repository: GetActiveCurrenciesAsync returned {Count} currencies", list.Count);
            return list;
        }

        public async Task<DateTime?> GetLatestRateDateAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: GetLatestRateDateAsync called");

            var latestDate = await _context.ExchangeRateHistories
                .MaxAsync(h => (DateTime?)h.RateDate, cancellationToken);

            _logger.LogDebug("Repository: GetLatestRateDateAsync returned {LatestDate}",
                latestDate?.ToString("yyyy-MM-dd") ?? "null");

            return latestDate;
        }

        public async Task<ExchangeRateHistory?> GetRateByLatestSessionAsync(string currencyCode, DateTime rateDate, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: GetRateByLatestSessionAsync for CurrencyCode={CurrencyCode}, RateDate={RateDate}",
                currencyCode, rateDate);

            // Pick the row from this currency's own latest session on that date
            var history = await _context.ExchangeRateHistories
                .AsNoTracking()
                .Include(h => h.Currency)
                .Where(h => h.CurrencyCode == currencyCode
                    && h.RateDate.Date == rateDate.Date)
                .OrderByDescending(h => h.Session)
                .FirstOrDefaultAsync(cancellationToken);

            if (history is not null)
                _logger.LogDebug("Repository: Found rate for {CurrencyCode} on RateDate={RateDate} session={Session}",
                    currencyCode, rateDate, history.Session);
            else
                _logger.LogDebug("Repository: No rate found for {CurrencyCode} on RateDate={RateDate}",
                    currencyCode, rateDate);

            return history;
        }

        public async Task<List<ExchangeRateHistory>> GetAllRatesByLatestSessionAsync(DateTime rateDate, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: GetAllRatesByLatestSessionAsync for RateDate={RateDate}", rateDate);

            // For each currency, pick the row from its own latest session on that date.
            // Generates: WHERE h.Session = (SELECT MAX(Session) FROM ExchangeRateHistory
            //            WHERE RateDate = @rateDate AND CurrencyCode = h.CurrencyCode)
            var histories = await _context.ExchangeRateHistories
                .AsNoTracking()
                .Include(h => h.Currency)
                .Where(h => h.RateDate.Date == rateDate.Date
                    && h.Session == _context.ExchangeRateHistories
                        .Where(inner => inner.RateDate.Date == rateDate.Date
                            && inner.CurrencyCode == h.CurrencyCode)
                        .Max(inner => inner.Session))
                .OrderBy(h => h.CurrencyCode)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Repository: GetAllRatesByLatestSessionAsync returned {Count} rates for RateDate={RateDate}",
                histories.Count, rateDate);

            return histories;
        }

        public async Task<bool> SessionExistsAsync(DateTime rateDate, string session, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: SessionExistsAsync for RateDate={RateDate}, Session={Session}", rateDate, session);
            return await _context.ExchangeRateHistories
                .AnyAsync(h => h.RateDate.Date == rateDate.Date && h.Session == session, cancellationToken);
        }

        public async Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Repository: AddRateHistoryAsync for CurrencyCode={CurrencyCode}, RateDate={RateDate}, Session={Session}",
                history.CurrencyCode, history.RateDate, history.Session);
            await _context.ExchangeRateHistories.AddAsync(history, cancellationToken);
        }
    }
}
