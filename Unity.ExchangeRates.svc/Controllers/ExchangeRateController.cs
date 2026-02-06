using Microsoft.AspNetCore.Mvc;
using Unity.ExchangeRates.svc.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Unity.ExchangeRates.svc.Controllers
{
    [ApiController]
    [Route("api/exchangerates")]
    public class ExchangeRateController : ControllerBase
    {
        private readonly IExchangeRateService _service;
        private readonly ILogger<ExchangeRateController> _logger;

        public ExchangeRateController(IExchangeRateService service, ILogger<ExchangeRateController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // 1. Get Latest Rates (For all Currencies)
        // URL: GET /api/exchangerates
        [HttpGet]
        public async Task<IActionResult> GetLatestRates(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.GetRatesFromBnmAsync(cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(503, new { message = "Request cancelled or timed out" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest rates");
                return StatusCode(500, new { message = "Error fetching latest rates", error = ex.Message });
            }
        }

        // 2. Get Specific Currency & Date (For the future Scheduled Job)
        // URL: GET /api/exchangerates/usd/2026-02-04
        [HttpGet("{currency}/{date}")]
        public async Task<IActionResult> GetRateByDate(string currency, string date, CancellationToken cancellationToken)
        {
            try
            {
                // Strict date format: YYYY-MM-DD
                if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return BadRequest(new { message = "Invalid date format. Please use YYYY-MM-DD" });
                }

                var result = await _service.GetRateByDateAsync(currency, date, cancellationToken);

                if (result == null)
                {
                    return NotFound(new { message = $"No rate found for {currency.ToUpper()} on {date}. Usually because it's a holiday or weekend." });
                }

                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request cancelled for {Currency} {Date}", currency, date);
                return StatusCode(503, new { message = "Request cancelled or timed out" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching history rate for {Currency} {Date}", currency, date);
                return StatusCode(500, new { message = "Error fetching history rate", error = ex.Message });
            }
        }

        // 3. Sync Daily Rates Manually (Trigger Endpoint)
        // URL: POST /api/exchangerates/sync/2026-02-04
        // This endpoint forces the system to fetch rates for all active currencies and save them to the database.
        [HttpPost("sync/{date}")]
        public async Task<IActionResult> SyncRates(string date, CancellationToken cancellationToken)
        {
            try
            {
                // Validate date format strictly before processing
                if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return BadRequest(new { message = "Invalid date format. Use YYYY-MM-DD." });
                }

                _logger.LogInformation("Starting manual sync for date: {Date}", date);

                // Call the service method we just created to sync and save to DB
                var success = await _service.SyncDailyRatesAsync(date, cancellationToken);

                if (success)
                {
                    return Ok(new { message = $"Rates for {date} successfully synced to database." });
                }
                else
                {
                    // This happens if no active currencies are found in the DB (Seed issue)
                    return StatusCode(500, new { message = "Sync process failed or no currencies found. Check logs." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual sync for date {Date}", date);
                return StatusCode(500, new { message = "Error during manual sync", error = ex.Message });
            }
        }
    }
}