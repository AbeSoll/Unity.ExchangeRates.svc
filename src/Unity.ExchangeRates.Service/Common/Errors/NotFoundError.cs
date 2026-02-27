using FluentResults;
using System.Diagnostics;
using static Unity.ExchangeRates.Service.Models.Constants.CommonConstants;

namespace Unity.ExchangeRates.Service.Common.Errors
{
    public sealed class NotFoundError : IError
    {
        public List<IError> Reasons => new List<IError>();
        public Dictionary<string, object> Metadata => new Dictionary<string, object>();
        public string? Message { get; private set; }

        public string status { get; set; } = StandardFormat.FailedStatus;
        public string timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
        public string traceId { get; set; } = Activity.Current?.Id;
        public string errorCode { get; set; } = string.Empty;
        public string errorMsg { get; set; } = ResponseMessage.NOTFOUND_ERROR;
        public object? data { get; set; }

        public NotFoundError()
        {
        }
    }
}
