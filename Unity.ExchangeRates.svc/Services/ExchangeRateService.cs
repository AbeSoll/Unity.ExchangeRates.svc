using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Unity.ExchangeRates.svc.Models;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.svc.Data; // Added to allow database access

namespace Unity.ExchangeRates.svc.Services
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ExchangeRateService> _logger;
        private readonly AppDbContext _dbContext; // Field to hold database connection

        // Updated constructor to inject AppDbContext
        public ExchangeRateService(HttpClient httpClient, ILogger<ExchangeRateService> logger, AppDbContext dbContext)
        {
            _httpClient = httpClient;
            _logger = logger;
            _dbContext = dbContext; // Initialize database context
            // Do NOT clear DefaultRequestHeaders here — configured when registering the client.
        }

        public async Task<JsonElement> GetRatesFromBnmAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Use base address (configured in Program.cs)
                var response = await _httpClient.GetAsync(string.Empty, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await SafeReadAsStringAsync(response).ConfigureAwait(false);
                    _logger.LogWarning("GetRatesFromBnmAsync failed. StatusCode: {StatusCode}, Body: {Body}", response.StatusCode, body);
                    response.EnsureSuccessStatusCode();
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken).ConfigureAwait(false);
                return json;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetRatesFromBnmAsync cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while fetching latest rates from BNM.");
                throw;
            }
        }

        public async Task<BnmApiResponse?> GetRateByDateAsync(string currency, string date, CancellationToken cancellationToken = default)
        {
            // Encode path segments to avoid malformed urls
            var encodedCurrency = HttpUtility.UrlEncode(currency.ToUpperInvariant());
            var encodedDate = HttpUtility.UrlEncode(date);

            // Build relative URI with query
            var relative = $"{encodedCurrency}/date/{encodedDate}?session=1700&quote=rm";

            try
            {
                var response = await _httpClient.GetAsync(relative, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Rate not found for {Currency} on {Date} (BNM returned 404).", currency, date);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await SafeReadAsStringAsync(response).ConfigureAwait(false);
                    _logger.LogWarning("GetRateByDateAsync unexpected status. Currency={Currency}, Date={Date}, Status={Status}, Body={Body}",
                        currency, date, response.StatusCode, body);
                    response.EnsureSuccessStatusCode();
                }

                var result = await response.Content.ReadFromJsonAsync<BnmApiResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

                // Optional: verify returned session to be safe (defensive)
                if (result?.Meta?.Session != null && result.Meta.Session != "1700")
                {
                    _logger.LogWarning("BNM returned unexpected session {Session} for {Currency} {Date}", result.Meta.Session, currency, date);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetRateByDateAsync cancelled for {Currency} {Date}.", currency, date);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching rate for {Currency} on {Date}.", currency, date);
                throw;
            }
        }

        public async Task<bool> SyncDailyRatesAsync(string date, CancellationToken cancellationToken = default)
        {
            // 1. Get all active currencies
            var activeCurrencies = _dbContext.Currencies
                .Where(c => !c.IsDeleted)
                .ToList();

            if (!activeCurrencies.Any())
            {
                _logger.LogError("No active currencies found in DB. Please seed the Currency table first.");
                return false;
            }

            foreach (var currency in activeCurrencies)
            {
                try
                {
                    // FIX 1: Guna 'currency.Id' (sebab dalam Model Currency, kita map Id -> CurrencyCode)
                    var bnmData = await GetRateByDateAsync(currency.Id, date, cancellationToken);

                    if (bnmData?.Data?.Rate != null)
                    {
                        var history = new ExchangeRateHistory
                        {
                            // FIX 2: Set Id = 0 (Sebab required, tapi EF akan ignore dan auto-generate nombor baru)
                            Id = 0,

                            // FIX 3: Guna 'currency.Id' sini juga
                            CurrencyCode = currency.Id,

                            RateDate = DateTime.Parse(date),
                            EffectiveDate = DateTime.Parse(date),

                            BuyingRate = bnmData.Data.Rate.BuyingRate,
                            SellingRate = bnmData.Data.Rate.SellingRate,
                            MiddleRate = bnmData.Data.Rate.MiddleRate,

                            // FIX 4: Mesti isi sebab BaseEntity mungkin guna keyword 'required' atau setting strict
                            CreatedOn = DateTime.Now,
                            CreatedBy = "System_Sync"
                        };

                        _dbContext.ExchangeRateHistories.Add(history);
                    }
                }
                catch (Exception ex)
                {
                    // FIX 5: Guna 'currency.Id' untuk log
                    _logger.LogError(ex, "Failed to sync rate for {Currency}", currency.Id);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }

        private static async Task<string> SafeReadAsStringAsync(HttpResponseMessage response)
        {
            try
            {
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}