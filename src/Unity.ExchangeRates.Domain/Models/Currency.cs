using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unity.ExchangeRates.Domain.Models
{
    [Table("Currency")]
    public class Currency : BaseEntity<int>
    {
        [Required]
        [StringLength(10)]
        public required string CurrencyCode { get; set; }

        [Required]
        [StringLength(100)]
        public required string CurrencyName { get; set; }

        public int UnitBase { get; set; }
    }
}
