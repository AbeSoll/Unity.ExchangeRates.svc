using Mediator;

namespace Unity.ExchangeRates.Shared.Jobs
{
    public interface IExchangeRateSyncJob
    {
        Task SyncDailyAsync(CancellationToken cancellationToken = default);
    }
}
