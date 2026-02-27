using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Unity.ExchangeRates.Api.ViewModels.Request
{
    [ValidateNever]
    public class ExchangeRateSyncRequest
    {
        public string date { get; set; }
        /// <summary>
        /// Optional. BNM session time (e.g. "0900", "1130", "1200", "1700"). 
        /// Defaults to appsettings value if not provided.
        /// </summary>
        public string? session { get; set; }
    }
}
