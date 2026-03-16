using FluentResults;
using Mediator;
using Unity.ExchangeRates.Service.Models.Results;

namespace Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates
{
    public class ExchangeRateSyncCommand : IRequest<Result<BaseResult>>
    {
        public string? Date { get; set; }
        public string? Session { get; set; }
    }
}
