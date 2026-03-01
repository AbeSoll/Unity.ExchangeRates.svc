using Unity.ExchangeRates.Domain.Events;

namespace Unity.ExchangeRates.Service.Services
{
    public interface IAuditLogEventDispatcher
    {
        Task DispatchAsync(IAuditLogEvent @event, CancellationToken cancellationToken = default);
    }
}
