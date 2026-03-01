using Unity.ExchangeRates.Domain.Events;
using Unity.ExchangeRates.Service.Services;
using Mediator;

namespace Unity.ExchangeRates.Shared.Services
{
    internal class AuditLogEventDispatcher : IAuditLogEventDispatcher
    {
        private readonly IMediator _mediator;

        public AuditLogEventDispatcher(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task DispatchAsync(IAuditLogEvent @event, CancellationToken cancellationToken = default)
        {
            await _mediator.Publish(@event, cancellationToken);
        }
    }
}
