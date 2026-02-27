using FluentResults;
using Mediator;
using Unity.ExchangeRates.Service.Models.Results;

namespace Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates
{
    public class ExchangeRateSyncCommand : IRequest<Result<BaseResult>>
    {
        public string? date { get; set; }
        public string? session { get; set; }
    }
}
