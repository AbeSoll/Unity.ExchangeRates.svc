using Microsoft.AspNetCore.Mvc;
using Unity.ExchangeRates.svc.Services;

namespace Unity.ExchangeRates.svc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExchangeRateController : ControllerBase
    {
        private readonly IExchangeRateService _exchangeRateService;

        public ExchangeRateController(IExchangeRateService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        [HttpGet("bnm")]
        public async Task<IActionResult> GetBnmRates()
        {
            try
            {
                var rates = await _exchangeRateService.GetRatesFromBnmAsync();
                return Ok(rates);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching rates from BNM", error = ex.Message });
            }
        }
    }
}