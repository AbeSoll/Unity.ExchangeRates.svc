namespace Unity.ExchangeRates.Service.Common.Configuration
{
    public class ErrorCodes
    {
        public Dictionary<string, string> DataValidations { get; set; }
        public Dictionary<string, string> LogicValidations { get; set; }
        public Dictionary<string, string> IntegrationValidations { get; set; }
        public string SystemValidations { get; set; }
    }
}
