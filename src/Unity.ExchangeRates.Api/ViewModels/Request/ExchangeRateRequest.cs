using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Unity.ExchangeRates.Api.ViewModels.Request
{
    [ValidateNever]
    public class ExchangeRateRequest
    {
        public string Currency { get; set; }
        public string Date { get; set; }
    }
}
