using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unity.ExchangeRates.svc.Models
{
    // Maps to the "ExchangeRateHistory" table for storing daily rates
    [Table("ExchangeRateHistory")]
    public class ExchangeRateHistory : BaseEntity<int>
    {
        // Foreign Key link to the Currency table
        [Required]
        [StringLength(10)]
        public required string CurrencyCode { get; set; }

        [Column("RateDate")]
        public DateTime RateDate { get; set; }

        // Rate values with 4 decimal places precision
        [Column(TypeName = "decimal(18, 4)")]
        public decimal? BuyingRate { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? SellingRate { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? MiddleRate { get; set; }

        // The date the rate is effective for (usually yesterday's date if fetching at 12AM)
        public DateTime EffectiveDate { get; set; }
    }
}