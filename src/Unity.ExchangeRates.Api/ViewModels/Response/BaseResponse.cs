using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Unity.ExchangeRates.Api.ViewModels.Response
{
    public class BaseResponse
    {
        public string AppId { get; set; } = "unity-exchange-rates";
        public string Status { get; set; }
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        public string TraceId { get; set; } = Activity.Current?.Id;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorCode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMsg { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Data { get; set; }
    }
}
