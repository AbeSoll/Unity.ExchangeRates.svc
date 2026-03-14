using Asp.Versioning;
using AutoMapper;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates;
using Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates;
using Unity.ExchangeRates.Service.Mediator.Queries.Currencies;
using Unity.ExchangeRates.Service.Models.Results;
using Unity.ExchangeRates.Api.Controllers.Base;
using Unity.ExchangeRates.Api.ViewModels.Request;
using Unity.ExchangeRates.Api.ViewModels.Response;

namespace Unity.ExchangeRates.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/exchange-rates")]
    [Produces("application/json")]
    public class ExchangeRateController : BaseApiController
    {
        private readonly IMapper _mapper;
        private readonly ISender _mediator;
        private readonly ILogger<ExchangeRateController> _logger;

        public ExchangeRateController(IMapper mapper, ISender mediator, ILogger<ExchangeRateController> logger)
        {
            _mapper = mapper;
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRate([FromQuery] string? currency, [FromQuery] string? date)
        {
            _logger.LogDebug("GetRate request received: currency={currency}, date={date}", currency ?? "ALL", date ?? "latest");
            var request = new ExchangeRateRequest { currency = currency, date = date };
            var query = _mapper.Map<ExchangeRateQuery>(request);
            var result = await _mediator.Send(query);
            return ApiResponse<BaseResponse, BaseResult>(_mapper.Map<BaseResponse>(result.ValueOrDefault), result);
        }

        [HttpGet("currencies")]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCurrencies()
        {
            _logger.LogDebug("GetCurrencies request received");
            var query = new GetCurrenciesQuery();
            var result = await _mediator.Send(query);
            return ApiResponse<BaseResult>(result);
        }

        [HttpPost("sync")]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(void), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Sync([FromBody] ExchangeRateSyncRequest syncRequest)
        {
            _logger.LogDebug("Sync request received: date={date}, session={session}", syncRequest.date, syncRequest.session);
            var command = _mapper.Map<ExchangeRateSyncCommand>(syncRequest);
            var result = await _mediator.Send(command);
            return ApiResponse<BaseResponse, BaseResult>(_mapper.Map<BaseResponse>(result.ValueOrDefault), result);
        }
    }
}
