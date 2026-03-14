using Unity.ExchangeRates.Domain.Models;

namespace Unity.ExchangeRates.Repository
{
    public interface IExchangeRateRepository
    {
        Task<List<Currency>> GetActiveCurrenciesAsync(CancellationToken cancellationToken);
        Task<DateTime?> GetLatestRateDateAsync(CancellationToken cancellationToken);
        Task<ExchangeRateHistory?> GetRateByLatestSessionAsync(string currencyCode, DateTime rateDate, CancellationToken cancellationToken);
        Task<List<ExchangeRateHistory>> GetAllRatesByLatestSessionAsync(DateTime rateDate, CancellationToken cancellationToken);
        Task<bool> SessionExistsAsync(DateTime rateDate, string session, CancellationToken cancellationToken);
        Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken cancellationToken);
    }
}
