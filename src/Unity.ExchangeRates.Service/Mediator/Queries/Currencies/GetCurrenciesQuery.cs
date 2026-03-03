using FluentResults;
using Mediator;
using Unity.ExchangeRates.Service.Models.Results;

namespace Unity.ExchangeRates.Service.Mediator.Queries.Currencies
{
    public class GetCurrenciesQuery : IRequest<Result<BaseResult>>
    {
    }
}
