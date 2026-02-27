using Mediator;
using Microsoft.Extensions.Logging;
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

        public async Task SyncDailyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.Now;
                var yesterday = now.Date.AddDays(-1).ToString("yyyy-MM-dd");

                _logger.LogInformation("Hangfire SyncDaily: Starting sync. Now={Now}, TargetDate={TargetDate}", now, yesterday);

                var command = new ExchangeRateSyncCommand { date = yesterday };
                var result = await _mediator.Send(command, cancellationToken);

                if (result.IsFailed)
                    _logger.LogError("Hangfire SyncDaily: Sync failed for {TargetDate}. Errors={Errors}",
                        yesterday, string.Join("; ", result.Errors.Select(e => e.Message)));
                else
                    _logger.LogInformation("Hangfire SyncDaily: Sync succeeded for {TargetDate}.", yesterday);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Hangfire SyncDaily: Job crashed unexpectedly");
            }
        }
    }
}
