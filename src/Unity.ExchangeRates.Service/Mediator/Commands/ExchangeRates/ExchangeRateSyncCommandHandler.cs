using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Service.Models.Results;
using Unity.ExchangeRates.Repository;
using Unity.ExchangeRates.Service.Common.Errors;
using Unity.ExchangeRates.Service.Configurations;

namespace Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates
{
    public class ExchangeRateSyncCommandHandler : IRequestHandler<ExchangeRateSyncCommand, Result<BaseResult>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly HttpClient _httpClient;
        private readonly BnmApiOptions _settings;
        private readonly ILogger<ExchangeRateSyncCommandHandler> _logger;

        public ExchangeRateSyncCommandHandler(
            IUnitOfWork unitOfWork,
            IHttpClientFactory httpClientFactory,
            IOptions<BnmApiOptions> settings,
            ILogger<ExchangeRateSyncCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _httpClient = httpClientFactory.CreateClient("BnmClient");
            _settings = settings.Value;
            _logger = logger;
        }

        public async ValueTask<Result<BaseResult>> Handle(ExchangeRateSyncCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var inputDate = DateTime.ParseExact(request.date!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                var targetDate = ResolveBusinessDate(inputDate);
                var targetDateStr = targetDate.ToString("yyyy-MM-dd");
                var session = !string.IsNullOrEmpty(request.session) ? request.session : _settings.DefaultSession;

                if (inputDate != targetDate)
                    _logger.LogInformation("ExchangeRateSyncCommandHandler: Input date {inputDate} falls on weekend, resolved to {targetDate}",
                        request.date, targetDateStr);

                _logger.LogInformation("ExchangeRateSyncCommandHandler: Starting sync for date={date}, session={session}", targetDateStr, session);

                var currencies = await _unitOfWork.ExchangeRates.GetActiveCurrenciesAsync(cancellationToken);
                _logger.LogDebug("ExchangeRateSyncCommandHandler: Loaded {Count} active currencies", currencies.Count);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                var syncedCount = 0;
                foreach (var curr in currencies)
                {
                    var path = _settings.Endpoints["ExchangeRate"];
                    var url = $"{path}/{curr.CurrencyCode}/date/{targetDateStr}?session={session}&quote=rm";

                    _logger.LogDebug("ExchangeRateSyncCommandHandler: Calling BNM API for currency={currency}, date={date}, url={url}",
                        curr.CurrencyCode, targetDateStr, url);

                    var response = await _httpClient.GetAsync(url, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("ExchangeRateSyncCommandHandler: Failed to fetch {currency} for {date}. BNM API returned {StatusCode}",
                            curr.CurrencyCode, targetDateStr, response.StatusCode);
                        continue;
                    }

                    var bnmData = await response.Content.ReadFromJsonAsync<BnmApiResponse>(cancellationToken: cancellationToken);

                    if (bnmData?.Data?.Rate is null)
                    {
                        _logger.LogError("ExchangeRateSyncCommandHandler: Failed to fetch {currency} for {date}. BNM returned empty data",
                            curr.CurrencyCode, targetDateStr);
                        continue;
                    }

                    var rateData = bnmData.Data;
                    var history = new ExchangeRateHistory
                    {
                        Id = 0,
                        CurrencyId = curr.Id,
                        CurrencyCode = curr.CurrencyCode,
                        RateDate = targetDate,
                        EffectiveDate = targetDate,
                        BuyingRate = rateData.Rate?.BuyingRate ?? 0,
                        SellingRate = rateData.Rate?.SellingRate ?? 0,
                        MiddleRate = rateData.Rate?.MiddleRate ?? 0,
                        CreatedOn = DateTime.Now,
                        CreatedBy = "System_Mediator"
                    };

                    await _unitOfWork.ExchangeRates.AddRateHistoryAsync(history, cancellationToken);
                    syncedCount++;
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("ExchangeRateSyncCommandHandler: Completed. Synced {synced}/{total} currencies for {date} session={session}",
                    syncedCount, currencies.Count, targetDateStr, session);

                return new BaseResult()
                {
                    data = $"Synced {syncedCount} of {currencies.Count} currencies for {targetDateStr} (session={session})"
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "ExchangeRateSyncCommandHandler failed for date={date}. Transaction rolled back.", request.date);
                return Result.Fail(new GeneralError() { errorCode = "00500", errorMsg = ex.Message });
            }
        }

        /// <summary>
        /// BNM only publishes rates on business days (Mon-Fri)
        /// If the date falls on Saturday or Sunday, resolve to the previous Friday
        /// </summary>
        private static DateTime ResolveBusinessDate(DateTime date)
        {
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                date = date.AddDays(-1);
            return date;
        }
    }
}
