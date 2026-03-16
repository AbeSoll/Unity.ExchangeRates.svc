using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;
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
                // Resolve date: explicit value or the latest available date in the database
                DateTime rateDate;
                if (string.IsNullOrEmpty(request.Date))
                {
                    var latestDate = await _repository.GetLatestRateDateAsync(cancellationToken);
                    if (latestDate is null)
                    {
                        _logger.LogDebug("ExchangeRateQueryHandler: No date provided and no data exists in database");
                        return Result.Fail(new NotFoundError()
                        {
                            ErrorCode = "00404",
                            ErrorMsg = "No records found."
                        });
                    }
                    rateDate = latestDate.Value;
                    _logger.LogDebug("ExchangeRateQueryHandler: No date provided, resolved to latest available: {date}",
                        rateDate.ToString("yyyy-MM-dd"));
                }
                else
                {
                    rateDate = DateTime.ParseExact(request.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }

                var dateStr = rateDate.ToString("yyyy-MM-dd");

                // If no currency specified — return ALL rates for the date (latest session per currency)
                if (string.IsNullOrEmpty(request.Currency))
                {
                    _logger.LogDebug("ExchangeRateQueryHandler: Fetching ALL rates for date={date}", dateStr);

                    var histories = await _repository.GetAllRatesByLatestSessionAsync(rateDate, cancellationToken);

                    if (histories.Count == 0)
                    {
                        _logger.LogDebug("ExchangeRateQueryHandler: No rates found for date={date}", dateStr);
                        return Result.Fail(new NotFoundError()
                        {
                            ErrorCode = "00404",
                            ErrorMsg = "No records found."
                        });
                    }

                    var distinctSessions = histories.Select(h => h.Session).Distinct().OrderByDescending(s => s);
                    _logger.LogDebug("ExchangeRateQueryHandler: Retrieved {Count} rates for date={date} across sessions=[{Sessions}]",
                        histories.Count, dateStr, string.Join(", ", distinctSessions));

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
                        session = h.Session,
                        lastUpdatedAt = h.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss"),
                        source = "Bank Negara Malaysia (BNM) Open API"
                    }).ToList();

                    return new BaseResult() { Data = allRatesResult };
                }

                // Single currency — return the latest session available for this specific currency
                _logger.LogDebug("ExchangeRateQueryHandler: Querying DB for currency={currency}, date={date}",
                    request.Currency, dateStr);

                var history = await _repository.GetRateByLatestSessionAsync(request.Currency, rateDate, cancellationToken);

                if (history is null)
                {
                    _logger.LogDebug("ExchangeRateQueryHandler: No rate found in DB for currency={currency}, date={date}",
                        request.Currency, dateStr);

                    return Result.Fail(new NotFoundError()
                    {
                        ErrorCode = "00404",
                        ErrorMsg = "No records found."
                    });
                }

                _logger.LogDebug("ExchangeRateQueryHandler: Success for currency={currency}, date={date}, session={session}",
                    request.Currency, dateStr, history.Session);

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
                    session = history.Session,
                    lastUpdatedAt = history.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss"),
                    source = "Bank Negara Malaysia (BNM) Open API"
                };

                return new BaseResult() { Data = singleResult };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExchangeRateQueryHandler failed for currency={currency}, date={date}", request.Currency, request.Date);
                return Result.Fail(new GeneralError() { ErrorCode = "00500", ErrorMsg = "An unexpected error occurred while retrieving exchange rates." });
            }
        }
    }
}
