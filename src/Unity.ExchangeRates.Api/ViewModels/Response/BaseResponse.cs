using System.Diagnostics;

namespace Unity.ExchangeRates.Api.ViewModels.Response
{
    public class BaseResponse
    {
        public string appId { get; set; }
        public string status { get; set; }
        public string timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        public string traceId { get; set; } = Activity.Current?.Id;
        public string? errorCode { get; set; }
        public string? errorMsg { get; set; }
        public object? data { get; set; }
    }
}
