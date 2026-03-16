using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.Service.Models.Results;
using Unity.ExchangeRates.Repository;

namespace Unity.ExchangeRates.Service.Mediator.Queries.Currencies
{
    public class GetCurrenciesQueryHandler : IRequestHandler<GetCurrenciesQuery, Result<BaseResult>>
    {
        private readonly IExchangeRateRepository _repository;
        private readonly ILogger<GetCurrenciesQueryHandler> _logger;

        public GetCurrenciesQueryHandler(
            IExchangeRateRepository repository,
            ILogger<GetCurrenciesQueryHandler> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async ValueTask<Result<BaseResult>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("GetCurrenciesQueryHandler: Fetching all active currencies");

                var currencies = await _repository.GetActiveCurrenciesAsync(cancellationToken);

                _logger.LogDebug("GetCurrenciesQueryHandler: Retrieved {Count} currencies", currencies.Count);

                var result = currencies.Select(c => new
                {
                    currencyCode = c.CurrencyCode,
                    currencyName = c.CurrencyName
                }).ToList();

                return new BaseResult()
                {
                    Data = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCurrenciesQueryHandler: Failed to retrieve currencies");
                return Result.Fail(new Common.Errors.GeneralError()
                {
                    ErrorCode = "00500",
                    ErrorMsg = "An unexpected error occurred while retrieving currencies."
                });
            }
        }
    }
}
