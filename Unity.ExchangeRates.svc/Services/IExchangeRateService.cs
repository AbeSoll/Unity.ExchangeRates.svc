using Unity.ExchangeRates.svc.Models;
using System.Threading;

namespace Unity.ExchangeRates.svc.Services
{
    public interface IExchangeRateService
    {
        // Get latest rates (root endpoint). Use JsonElement for flexible typing.
        Task<System.Text.Json.JsonElement> GetRatesFromBnmAsync(CancellationToken cancellationToken = default);

        // Returns the BnmApiResponse model for structured data access
        Task<BnmApiResponse?> GetRateByDateAsync(string currency, string date, CancellationToken cancellationToken = default);

        // ADDING THIS: Method for sync dan save to DB
        Task<bool> SyncDailyRatesAsync(string date, CancellationToken cancellationToken = default);
    }
}
