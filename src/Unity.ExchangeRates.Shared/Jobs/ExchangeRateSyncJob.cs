using Mediator;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.Service.Common.Errors;
using Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates;

namespace Unity.ExchangeRates.Shared.Jobs
{
    public class ExchangeRateSyncJob : IExchangeRateSyncJob
    {
        private readonly ISender _mediator;
        private readonly ILogger<ExchangeRateSyncJob> _logger;

        public ExchangeRateSyncJob(ISender mediator, ILogger<ExchangeRateSyncJob> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task SyncSessionAsync(string session, int dateOffset = 0, CancellationToken cancellationToken = default)
        {
            try
            {
                var rateDate = DateTime.Now.Date.AddDays(dateOffset).ToString("yyyy-MM-dd");

                _logger.LogInformation("Hangfire SyncSession: Starting sync. Session={Session}, RateDate={RateDate}, DateOffset={DateOffset}",
                    session, rateDate, dateOffset);

                var command = new ExchangeRateSyncCommand { date = rateDate, session = session };
                var result = await _mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Hangfire SyncSession: Sync succeeded for {RateDate} session={Session}.", rateDate, session);
                    return;
                }

                // 404 = BNM has no rates for this session (public holiday) — expected, not an error
                if (result.Errors.Any(e => e is NotFoundError))
                {
                    _logger.LogInformation("Hangfire SyncSession: No BNM rates available for {RateDate} session={Session}.", rateDate, session);
                    return;
                }

                // Actual failure (500)
                _logger.LogError("Hangfire SyncSession: Sync failed for {RateDate} session={Session}. Errors={Errors}",
                    rateDate, session, string.Join("; ", result.Errors.Select(e => e.Message)));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Hangfire SyncSession: Job crashed unexpectedly for session={Session}", session);
            }
        }
    }
}
