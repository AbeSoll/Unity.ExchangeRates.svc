using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Unity.ExchangeRates.svc.Models;
using Microsoft.Extensions.Logging;

namespace Unity.ExchangeRates.svc.Services
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(HttpClient httpClient, ILogger<ExchangeRateService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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