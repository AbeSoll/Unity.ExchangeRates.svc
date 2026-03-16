using FluentResults;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using static Unity.ExchangeRates.Service.Models.Constants.CommonConstants;

namespace Unity.ExchangeRates.Service.Common.Errors
{
    public sealed class GeneralError : IError
    {
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public List<IError> Reasons => [];

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<string, object> Metadata => [];

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? Message { get; private set; }

        public string Status { get; set; } = StandardFormat.FailedStatus;
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        public string TraceId { get; set; } = Activity.Current?.Id;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMsg { get; set; } = ResponseMessage.GENERAL_ERROR;

        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Newtonsoft.Json.JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? Data { get; set; }

        public GeneralError()
        {
        }
    }
}
