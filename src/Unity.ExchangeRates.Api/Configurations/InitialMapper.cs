using AutoMapper;
using Unity.ExchangeRates.Service.Mediator.Commands.ExchangeRates;
using Unity.ExchangeRates.Service.Mediator.Queries.ExchangeRates;
using Unity.ExchangeRates.Service.Models.Results;
using Unity.ExchangeRates.Api.ViewModels.Request;
using Unity.ExchangeRates.Api.ViewModels.Response;

namespace Unity.ExchangeRates.Api.Configurations
{
    internal class InitialMapper : Profile
    {
        public InitialMapper()
        {
            // Request ViewModels → Query/Command objects
            CreateMap<ExchangeRateRequest, ExchangeRateQuery>();
            CreateMap<ExchangeRateSyncRequest, ExchangeRateSyncCommand>();

            // Domain result → Response ViewModel
            CreateMap<BaseResult, BaseResponse>();
        }
    }
}
