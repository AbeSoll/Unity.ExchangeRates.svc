using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Unity.ExchangeRates.Service.Models.Results
{
    public class BaseResult
    {
        public string appId { get; set; } = "unity-exchange-rates";
        public string status { get; set; } = "Success";
        public string timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        public string traceId { get; set; } = Activity.Current?.Id;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? errorCode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? errorMsg { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? data { get; set; }
    }
}
