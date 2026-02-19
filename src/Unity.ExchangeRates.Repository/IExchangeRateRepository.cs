using Unity.ExchangeRates.Domain.Models;

namespace Unity.ExchangeRates.Repository
{
    public interface IExchangeRateRepository
    {
        Task<List<Currency>> GetActiveCurrenciesAsync(CancellationToken cancellationToken);
        Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken cancellationToken);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
