using Mediator;

namespace Unity.ExchangeRates.Shared.Jobs
{
    public interface IExchangeRateSyncJob
    {
        Task SyncSessionAsync(string session, int dateOffset = 0, CancellationToken cancellationToken = default);
    }
}
