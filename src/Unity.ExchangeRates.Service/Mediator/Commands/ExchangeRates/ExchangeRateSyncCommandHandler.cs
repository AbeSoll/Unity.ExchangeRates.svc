using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
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
        /// <summary>
        /// Currency used to probe BNM API availability for a given date and session.
        /// USD is the most universally published rate by BNM.
        /// </summary>
        private const string ReferenceCurrency = "usd";

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
                var session = !string.IsNullOrEmpty(request.session) ? request.session : _settings.DefaultSession;
                var dateStr = inputDate.ToString("yyyy-MM-dd");

                // Step 1: Idempotency — skip if this session has already been synced
                if (await _unitOfWork.ExchangeRates.SessionExistsAsync(inputDate, session, cancellationToken))
                {
                    _logger.LogDebug("ExchangeRateSyncCommandHandler: Session {Session} for {Date} already synced. Skipping.",
                        session, dateStr);
                    return new BaseResult()
                    {
                        data = $"Session {session} for {dateStr} has already been synced."
                    };
                }

                // Step 2: Probe BNM with USD — if no data, mirror BNM's response exactly
                if (!await IsRateAvailableAsync(inputDate, session, cancellationToken))
                {
                    _logger.LogDebug("ExchangeRateSyncCommandHandler: BNM has no rates for {Date} session={Session}.",
                        dateStr, session);
                    return Result.Fail(new NotFoundError()
                    {
                        errorCode = "00404",
                        errorMsg = "No records found."
                    });
                }

                // Step 3: Sync all active currencies for this date and session
                _logger.LogInformation("ExchangeRateSyncCommandHandler: Starting sync for date={Date}, session={Session}",
                    dateStr, session);

                var currencies = await _unitOfWork.ExchangeRates.GetActiveCurrenciesAsync(cancellationToken);
                _logger.LogDebug("ExchangeRateSyncCommandHandler: Loaded {Count} active currencies", currencies.Count);

                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                var syncedCount = 0;
                foreach (var curr in currencies)
                {
                    var path = _settings.Endpoints["ExchangeRate"];
                    var url = $"{path}/{curr.CurrencyCode}/date/{dateStr}?session={session}&quote=rm";

                    _logger.LogDebug("ExchangeRateSyncCommandHandler: Calling BNM API for currency={Currency}, date={Date}, session={Session}",
                        curr.CurrencyCode, dateStr, session);

                    var response = await _httpClient.GetAsync(url, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("ExchangeRateSyncCommandHandler: Failed to fetch {Currency} for {Date} session={Session}. BNM returned {StatusCode}",
                            curr.CurrencyCode, dateStr, session, response.StatusCode);
                        continue;
                    }

                    var bnmData = await response.Content.ReadFromJsonAsync<BnmApiResponse>(cancellationToken: cancellationToken);

                    if (bnmData?.Data?.Rate is null)
                    {
                        _logger.LogWarning("ExchangeRateSyncCommandHandler: BNM returned empty data for {Currency} on {Date} session={Session}",
                            curr.CurrencyCode, dateStr, session);
                        continue;
                    }

                    var rateData = bnmData.Data;
                    var history = new ExchangeRateHistory
                    {
                        Id = 0,
                        CurrencyId = curr.Id,
                        CurrencyCode = curr.CurrencyCode,
                        RateDate = inputDate,
                        Session = session,
                        EffectiveDate = inputDate,
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

                _logger.LogInformation("ExchangeRateSyncCommandHandler: Completed. Synced {Synced}/{Total} currencies for {Date} session={Session}",
                    syncedCount, currencies.Count, dateStr, session);

                return new BaseResult()
                {
                    data = $"Synced {syncedCount} of {currencies.Count} currencies for {dateStr} (session={session})"
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "ExchangeRateSyncCommandHandler failed for date={Date}, session={Session}. Transaction rolled back.",
                    request.date, request.session);
                return Result.Fail(new GeneralError()
                {
                    errorCode = "00500",
                    errorMsg = "An unexpected error occurred while syncing exchange rates."
                });
            }
        }

        /// <summary>
        /// Probes BNM API with a single reference currency (USD) to check if rates exist for the given date and session.
        /// Returns false for holidays/weekends (404). Throws on server errors (5xx after Polly exhaustion).
        /// </summary>
        private async Task<bool> IsRateAvailableAsync(DateTime date, string session, CancellationToken cancellationToken)
        {
            var path = _settings.Endpoints["ExchangeRate"];
            var dateStr = date.ToString("yyyy-MM-dd");
            var url = $"{path}/{ReferenceCurrency}/date/{dateStr}?session={session}&quote=rm";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();

            var bnmData = await response.Content.ReadFromJsonAsync<BnmApiResponse>(cancellationToken: cancellationToken);
            return bnmData?.Data?.Rate is not null;
        }
    }
}
