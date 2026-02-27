using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Unity.ExchangeRates.Api.ViewModels.Request
{
    [ValidateNever]
    public class ExchangeRateRequest
    {
        public string currency { get; set; }
        public string date { get; set; }
    }
}
