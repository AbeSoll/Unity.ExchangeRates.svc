using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Unity.ExchangeRates.Api.ViewModels.Request
{
    [ValidateNever]
    public class ExchangeRateSyncRequest
    {
        public string appId { get; set; }
        public string date { get; set; }
    }
}
