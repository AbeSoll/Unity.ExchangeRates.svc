using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Unity.ExchangeRates.Domain.Models;
using Unity.ExchangeRates.Repository;

namespace Unity.ExchangeRates.Infrastructure.Repositories
{
    public class TextFileExchangeRateRepository : IExchangeRateRepository
    {
        private readonly string _dataDirectory;
        private readonly ILogger<TextFileExchangeRateRepository> _logger;
        private readonly List<ExchangeRateHistory> _pendingHistories = new();

        public TextFileExchangeRateRepository(IConfiguration configuration, ILogger<TextFileExchangeRateRepository> logger)
        {
            _dataDirectory = configuration.GetValue<string>("TextFileStorage:DataDirectory") ?? "Data";
            _logger = logger;
            Directory.CreateDirectory(_dataDirectory);
        }

        public Task<List<Currency>> GetActiveCurrenciesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("TextFileRepo: GetActiveCurrenciesAsync called");

            var filePath = Path.Combine(_dataDirectory, "currencies.txt");
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("TextFileRepo: currencies.txt not found at {FilePath}. Returning empty list.", filePath);
                return Task.FromResult(new List<Currency>());
            }

            var currencies = File.ReadAllLines(filePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line =>
                {
                    var parts = line.Split('|');
                    return new Currency
                    {
                        Id = parts[0].Trim(),
                        CurrencyName = parts[1].Trim(),
                        UnitBase = int.Parse(parts[2].Trim())
                    };
                })
                .ToList();

            _logger.LogInformation("TextFileRepo: GetActiveCurrenciesAsync returned {Count} currencies from {FilePath}", currencies.Count, filePath);
            return Task.FromResult(currencies);
        }

        public Task AddRateHistoryAsync(ExchangeRateHistory history, CancellationToken cancellationToken)
        {
            _pendingHistories.Add(history);
            _logger.LogDebug("TextFileRepo: Queued {CurrencyCode} for {RateDate}", history.CurrencyCode, history.RateDate);
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("TextFileRepo: SaveChangesAsync called with {Count} pending records", _pendingHistories.Count);

            foreach (var history in _pendingHistories)
            {
                var fileName = $"rates-{history.RateDate:yyyy-MM-dd}.txt";
                var filePath = Path.Combine(_dataDirectory, fileName);
                var line = string.Join("|",
                    history.CurrencyCode,
                    history.RateDate.ToString("yyyy-MM-dd"),
                    history.BuyingRate?.ToString("F4") ?? "",
                    history.SellingRate?.ToString("F4") ?? "",
                    history.MiddleRate?.ToString("F4") ?? "",
                    history.EffectiveDate.ToString("yyyy-MM-dd"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                File.AppendAllText(filePath, line + Environment.NewLine);
            }

            var count = _pendingHistories.Count;
            _logger.LogInformation("TextFileRepo: SaveChangesAsync persisted {Count} records to text files", count);
            _pendingHistories.Clear();
            return Task.FromResult(count);
        }
    }
}
