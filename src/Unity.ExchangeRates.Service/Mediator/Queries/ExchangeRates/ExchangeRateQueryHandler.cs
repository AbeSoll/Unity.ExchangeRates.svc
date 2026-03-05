using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Service.Models.Results;
using Unity.ExchangeRates.Service.Common.Errors;
using Unity.ExchangeRates.Repository;

namespace Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates
{
    public class ExchangeRateQueryHandler : IRequestHandler<ExchangeRateQuery, Result<BaseResult>>
    {
        private readonly IExchangeRateRepository _repository;
        private readonly ILogger<ExchangeRateQueryHandler> _logger;

        public ExchangeRateQueryHandler(
            IExchangeRateRepository repository,
            ILogger<ExchangeRateQueryHandler> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async ValueTask<Result<BaseResult>> Handle(ExchangeRateQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var createdDate = DateTime.ParseExact(request.date!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                // If no currency specified — return ALL rates for the date
                if (string.IsNullOrEmpty(request.currency))
                {
                    _logger.LogDebug("ExchangeRateQueryHandler: Fetching ALL rates for date={date}", request.date);

                    var histories = await _repository.GetAllRatesByDateAsync(createdDate, cancellationToken);

                    if (histories.Count == 0)
                    {
                        _logger.LogWarning("ExchangeRateQueryHandler: No rates found for date={date}", request.date);
                        return Result.Fail(new NotFoundError()
                        {
                            errorCode = "00404",
                            errorMsg = $"No exchange rate data found for {request.date}."
                        });
                    }

                    _logger.LogInformation("ExchangeRateQueryHandler: Retrieved {Count} rates for date={date}", histories.Count, request.date);

                    var allRatesResult = histories.Select(h => new
                    {
                        currencyCode = h.CurrencyCode,
                        unit = h.Currency?.UnitBase ?? 0,
                        rate = new
                        {
                            rateDate = h.RateDate.ToString("yyyy-MM-dd"),
                            effectiveDate = h.EffectiveDate.ToString("yyyy-MM-dd"),
                            buyingRate = h.BuyingRate,
                            sellingRate = h.SellingRate,
                            middleRate = h.MiddleRate
                        },
                        lastUpdatedAt = h.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss"),
                        source = "Bank Negara Malaysia (BNM) Open API"
                    }).ToList();

                    return new BaseResult() { data = allRatesResult };
                }

                // Single currency
                _logger.LogDebug("ExchangeRateQueryHandler: Querying DB for currency={currency}, date={date}",
                    request.currency, request.date);

                var history = await _repository.GetRateByCreatedDateAsync(request.currency, createdDate, cancellationToken);

                if (history is null)
                {
                    _logger.LogWarning("ExchangeRateQueryHandler: No rate found in DB for currency={currency}, date={date}",
                        request.currency, request.date);

                    return Result.Fail(new NotFoundError()
                    {
                        errorCode = "00404",
                        errorMsg = "No exchange rate data found for the given date."
                    });
                }

                _logger.LogInformation("ExchangeRateQueryHandler: Success for currency={currency}, date={date}", request.currency, request.date);

                var singleResult = new
                {
                    currencyCode = history.CurrencyCode,
                    unit = history.Currency?.UnitBase ?? 0,
                    rate = new
                    {
                        rateDate = history.RateDate.ToString("yyyy-MM-dd"),
                        effectiveDate = history.EffectiveDate.ToString("yyyy-MM-dd"),
                        buyingRate = history.BuyingRate,
                        sellingRate = history.SellingRate,
                        middleRate = history.MiddleRate
                    },
                    lastUpdatedAt = history.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss"),
                    source = "Bank Negara Malaysia (BNM) Open API"
                };

                return new BaseResult() { data = singleResult };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExchangeRateQueryHandler failed for currency={currency}, date={date}", request.currency, request.date);
                return Result.Fail(new GeneralError() { errorCode = "00500", errorMsg = "An unexpected error occurred while retrieving exchange rates." });
            }
        }
    }
}
