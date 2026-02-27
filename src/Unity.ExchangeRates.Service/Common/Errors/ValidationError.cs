using FluentResults;
using System.Diagnostics;
using static Unity.ExchangeRates.Service.Models.Constants.CommonConstants;

namespace Unity.ExchangeRates.Service.Common.Errors
{
    public class ValidationError : IError
    {
        public List<IError> Reasons => new List<IError>();

        public string? Message { get; private set; }

        public Dictionary<string, object> Metadata { get; private set; } = new Dictionary<string, object>();

        public string status { get; set; } = StandardFormat.FailedStatus;
        public string timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        public string traceId { get; set; } = Activity.Current?.Id;
        public string errorCode { get; set; } = string.Empty;
        public string errorMsg { get; set; } = ResponseMessage.VALIDATOR_ERROR;
        public object? data { get; set; }

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
