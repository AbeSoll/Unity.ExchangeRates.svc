namespace Unity.ExchangeRates.Domain.Exceptions
{
    [Serializable]
    public class ExchangeRatesDomainException : Exception
    {
        public string Code { get; private set; } = string.Empty;

        public ExchangeRatesDomainException() { }

        public ExchangeRatesDomainException(string message) : base(message) { }

        public ExchangeRatesDomainException(string code, string message) : base(message)
        {
            Code = code;
        }

        public ExchangeRatesDomainException(string message, Exception innerException) : base(message, innerException) { }

        public ExchangeRatesDomainException(string code, string message, Exception innerException) : base(message, innerException)
        {
            Code = code;
        }
    }
}
