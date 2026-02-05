using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unity.ExchangeRates.svc.Models
{
    [Table("ExchangeRateHistory")]
    public class ExchangeRateHistory : BaseEntity<int>
    {
        [Required, StringLength(10)]
        public required string CurrencyCode { get; set; }

        public DateTime RateDate { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? BuyingRate { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? SellingRate { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? MiddleRate { get; set; }

        public DateTime EffectiveDate { get; set; }

        // navigation back to Currency
        [ForeignKey(nameof(CurrencyCode))]
        public Currency? Currency { get; set; }
    }
}