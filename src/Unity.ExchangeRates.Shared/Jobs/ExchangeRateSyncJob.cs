using Mediator;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
                var targetDate = GetPreviousBusinessDate(now).ToString("yyyy-MM-dd");

                _logger.LogInformation("Hangfire SyncDaily: Starting sync. Now={Now}, TargetDate={TargetDate}", now, targetDate);

                var command = new ExchangeRateSyncCommand { date = targetDate };
                var result = await _mediator.Send(command, cancellationToken);

                if (result.IsFailed)
                    _logger.LogError("Hangfire SyncDaily: Sync failed for {TargetDate}. Errors={Errors}",
                        targetDate, string.Join("; ", result.Errors.Select(e => e.Message)));
                else
                    _logger.LogInformation("Hangfire SyncDaily: Sync succeeded for {TargetDate}.", targetDate);
            }
            catch (Exception ex)
            {
                _logger.LogError("ExchangeRateSyncJob: " + JsonConvert.SerializeObject(ex));
            }
        }

        private static DateTime GetPreviousBusinessDate(DateTime now)
        {
            var date = now.Date.AddDays(-1);
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                date = date.AddDays(-1);
            return date;
        }
    }
}
