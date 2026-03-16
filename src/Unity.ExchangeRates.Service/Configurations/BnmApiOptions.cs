namespace Unity.ExchangeRates.Service.Configurations
{
    public class BnmApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string AcceptHeader { get; set; } = "application/vnd.BNM.API.v1+json";
        public Dictionary<string, string> Endpoints { get; set; } = [];
        public string DefaultSession { get; set; } = "1700";
    }
}
