using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Service.Models.Results;
using Unity.ExchangeRates.Repository;
using Unity.ExchangeRates.Service.Common.Errors;
using Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates;

namespace Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates
{
    public class ExchangeRateSyncCommandHandler : IRequestHandler<ExchangeRateSyncCommand, Result<BaseResult>>
    {
        private readonly IExchangeRateRepository _repository;
        private readonly ISender _mediator;
        private readonly ILogger<ExchangeRateSyncCommandHandler> _logger;

        public ExchangeRateSyncCommandHandler(
            IExchangeRateRepository repository,
            ISender mediator,
            ILogger<ExchangeRateSyncCommandHandler> logger)
        {
            _repository = repository;
            _mediator = mediator;
            _logger = logger;
        }

        public async ValueTask<Result<BaseResult>> Handle(ExchangeRateSyncCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("ExchangeRateSyncCommandHandler: Starting sync for date={date}", request.date);

                var currencies = await _repository.GetActiveCurrenciesAsync(cancellationToken);
                _logger.LogDebug("ExchangeRateSyncCommandHandler: Loaded {Count} active currencies", currencies.Count);

                var syncedCount = 0;
                foreach (var curr in currencies)
                {
                    var query = new ExchangeRateQuery { appId = request.appId, currency = curr.Id, date = request.date };
                    var result = await _mediator.Send(query, cancellationToken);

                    if (result.IsSuccess && result.ValueOrDefault?.data is BnmRateData rateData)
                    {
                        var history = new ExchangeRateHistory
                        {
                            Id = 0,
                            CurrencyCode = curr.Id,
                            RateDate = DateTime.Parse(request.date!),
                            EffectiveDate = DateTime.Parse(request.date!),
                            BuyingRate = rateData.Rate?.BuyingRate ?? 0,
                            SellingRate = rateData.Rate?.SellingRate ?? 0,
                            MiddleRate = rateData.Rate?.MiddleRate ?? 0,
                            CreatedOn = DateTime.Now,
                            CreatedBy = "System_Mediator"
                        };

                        await _repository.AddRateHistoryAsync(history, cancellationToken);
                        syncedCount++;
                    }
                    else if (result.IsFailed)
                    {
                        _logger.LogWarning("ExchangeRateSyncCommandHandler: Skip {currency} for {date}. Reason={errors}",
                            curr.Id, request.date, string.Join("; ", result.Errors.Select(e => (e as GeneralError)?.errorMsg ?? e.Message)));
                    }
                }

                await _repository.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("ExchangeRateSyncCommandHandler: Completed. Synced {synced}/{total} currencies for {date}",
                    syncedCount, currencies.Count, request.date);

                return new BaseResult()
                {
                    appId = request.appId,
                    data = $"Synced {syncedCount} of {currencies.Count} currencies for {request.date}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("ExchangeRateSyncCommandHandler response: " + JsonConvert.SerializeObject(ex));
                return Result.Fail(new GeneralError() { appId = request.appId, errorCode = "00500", errorMsg = ex.Message });
            }
        }
    }
}
