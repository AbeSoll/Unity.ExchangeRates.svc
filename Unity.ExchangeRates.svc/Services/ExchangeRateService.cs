using System.Net.Http.Json;

namespace Unity.ExchangeRates.svc.Services
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ExchangeRateService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;

            // Fix: Pulling the base URL from appsettings.json
            // If the config is missing, it uses the official public API as fallback
            _baseUrl = config["BnmApiConfig:BaseUrl"] ?? "https://api.bnm.gov.my/public/exchange-rate";

            // Mandatory BNM header for authentication/format
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.BNM.API.v1+json");
        }

        public async Task<object> GetRatesFromBnmAsync()
        {
            // URL: https://api.bnm.gov.my/public/exchange-rate
            var response = await _httpClient.GetAsync(_baseUrl);

            // This checks if the call succeeded (200 OK)
            response.EnsureSuccessStatusCode();

            // Returns the full JSON (data + meta)
            return await response.Content.ReadFromJsonAsync<object>() ?? new { };
        }
    }
}