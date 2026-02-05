using System.Text.Json.Serialization;

namespace Unity.ExchangeRates.svc.Models
{
    // Main structure JSON from BNM
    public class BnmApiResponse
    {
        [JsonPropertyName("data")]
        public BnmRateData? Data { get; set; }

        [JsonPropertyName("meta")]
        public BnmMeta? Meta { get; set; }
    }

    public class BnmRateData
    {
        [JsonPropertyName("currency_code")]
        public string? CurrencyCode { get; set; }

        [JsonPropertyName("unit")]
        public int Unit { get; set; }

        [JsonPropertyName("rate")]
        public RateDetails? Rate { get; set; }
    }

    public class RateDetails
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("buying_rate")]
        public decimal? BuyingRate { get; set; }

        [JsonPropertyName("selling_rate")]
        public decimal? SellingRate { get; set; }

        [JsonPropertyName("middle_rate")]
        public decimal? MiddleRate { get; set; }
    }

    public class BnmMeta
    {
        [JsonPropertyName("quote")]
        public string? Quote { get; set; }

        [JsonPropertyName("session")]
        public string? Session { get; set; }

        [JsonPropertyName("last_updated")]
        public string? LastUpdated { get; set; }

        [JsonPropertyName("total_result")]
        public int TotalResult { get; set; }
    }
}