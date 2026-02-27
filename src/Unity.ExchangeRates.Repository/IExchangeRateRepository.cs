using Unity.ExchangeRates.Domain.Models;

namespace Unity.ExchangeRates.Repository
{
    public interface IExchangeRateRepository
    {
        Task<List<Currency>> GetActiveCurrenciesAsync(CancellationToken cancellationToken);
        Task<ExchangeRateHistory?> GetRateByCreatedDateAsync(string currencyCode, DateTime createdDate, CancellationToken cancellationToken);
        Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken cancellationToken);
    }
}
