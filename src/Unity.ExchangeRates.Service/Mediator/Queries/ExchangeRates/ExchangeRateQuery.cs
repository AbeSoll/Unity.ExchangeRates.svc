using FluentResults;
using Mediator;
using Unity.ExchangeRates.Service.Models.Results;

namespace Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates
{
    public class ExchangeRateQuery : IRequest<Result<BaseResult>>
    {
        public string? appId { get; set; }
        public string? currency { get; set; }
        public string? date { get; set; }
    }
}
