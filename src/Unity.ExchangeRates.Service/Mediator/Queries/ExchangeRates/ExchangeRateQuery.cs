using FluentResults;
using Mediator;
using Unity.ExchangeRates.Service.Models.Results;

namespace Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates
{
    public class ExchangeRateQuery : IRequest<Result<BaseResult>>
    {
        public string? Currency { get; set; }
        public string? Date { get; set; }
    }
}
