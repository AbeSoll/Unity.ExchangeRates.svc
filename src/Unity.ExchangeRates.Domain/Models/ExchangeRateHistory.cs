using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unity.ExchangeRates.Domain.Models
{
    [Table("ExchangeRateHistory")]
    public class ExchangeRateHistory : BaseEntity<int>
    {
        [Required]
        public int CurrencyId { get; set; }

        [Required, StringLength(10)]
        public required string CurrencyCode { get; set; }

        public DateTime RateDate { get; set; }

        /// <summary>
        /// BNM session code (0900, 1130, 1200, 1700).
        /// </summary>
        [Required, StringLength(4)]
        public required string Session { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? BuyingRate { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? SellingRate { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal? MiddleRate { get; set; }

        public DateTime EffectiveDate { get; set; }

        [ForeignKey(nameof(CurrencyId))]
        public Currency? Currency { get; set; }
    }
}
