using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Json;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Service.Models.Results;
using Unity.ExchangeRates.Service.Common.Errors;
using Unity.ExchangeRates.Service.Configurations;

namespace Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates
{
    public class ExchangeRateQueryHandler : IRequestHandler<ExchangeRateQuery, Result<BaseResult>>
    {
        private readonly HttpClient _httpClient;
        private readonly BnmApiOptions _settings;
        private readonly ILogger<ExchangeRateQueryHandler> _logger;

        public ExchangeRateQueryHandler(
            IHttpClientFactory httpClientFactory,
            IOptions<BnmApiOptions> settings,
            ILogger<ExchangeRateQueryHandler> logger)
        {
            _httpClient = httpClientFactory.CreateClient("BnmClient");
            _settings = settings.Value;
            _logger = logger;
        }

        public async ValueTask<Result<BaseResult>> Handle(ExchangeRateQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var path = _settings.Endpoints["ExchangeRate"];
                var url = $"{path}/{request.currency}/date/{request.date}?session=1700&quote=rm";

                _logger.LogDebug("ExchangeRateQueryHandler: Calling BNM API for currency={currency}, date={date}, url={url}",
                    request.currency, request.date, url);

                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ExchangeRateQueryHandler: BNM API returned {StatusCode} for currency={currency}, date={date}",
                        response.StatusCode, request.currency, request.date);

                    return Result.Fail(new GeneralError()
                    {
                        appId = request.appId,
                        errorCode = "00400",
                        errorMsg = $"BNM API returned {response.StatusCode}"
                    });
                }

                var bnmData = await response.Content.ReadFromJsonAsync<BnmApiResponse>(cancellationToken: cancellationToken);

                if (bnmData is null)
                {
                    _logger.LogWarning("ExchangeRateQueryHandler: BNM API returned empty data for currency={currency}, date={date}",
                        request.currency, request.date);

                    return Result.Fail(new NotFoundError()
                    {
                        appId = request.appId,
                        errorCode = "00404",
                        errorMsg = "No exchange rate data found for the given date."
                    });
                }

                _logger.LogInformation("ExchangeRateQueryHandler: Success for currency={currency}, date={date}", request.currency, request.date);

                return new BaseResult() { appId = request.appId, data = bnmData.Data };
            }
            catch (Exception ex)
            {
                _logger.LogError("ExchangeRateQueryHandler response: " + JsonConvert.SerializeObject(ex));
                return Result.Fail(new GeneralError() { appId = request.appId, errorCode = "00500", errorMsg = ex.Message });
            }
        }
    }
}
