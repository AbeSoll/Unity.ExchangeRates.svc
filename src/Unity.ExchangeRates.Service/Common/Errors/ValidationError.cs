using FluentResults;
using System.Diagnostics;
using static Unity.ExchangeRates.Service.Models.Constants.CommonConstants;

namespace Unity.ExchangeRates.Service.Common.Errors
{
    public class ValidationError : IError
    {
        public List<IError> Reasons => [];

        public string? Message { get; private set; }

        public Dictionary<string, object> Metadata { get; private set; } = [];

        public string Status { get; set; } = StandardFormat.FailedStatus;
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        public string TraceId { get; set; } = Activity.Current?.Id;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMsg { get; set; } = ResponseMessage.VALIDATOR_ERROR;
        public object? Data { get; set; }

        public ValidationError()
        {
        }

        public ValidationError WithMetadata(string name, object value)
        {
            Metadata.Add(name, value);
            return this;
        }
    }
}
