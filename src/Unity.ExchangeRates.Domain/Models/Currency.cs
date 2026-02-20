using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unity.ExchangeRates.Domain.Models
{
    [Table("Currency")]
    public class Currency : BaseEntity<string>
    {
        [Key]
        [Column("CurrencyCode")]
        [StringLength(10)]
        public override required string Id { get; set; }

        [NotMapped]
        public string CurrencyCode
        {
            get => Id;
            set => Id = value;
        }

        [Required]
        [StringLength(100)]
        public required string CurrencyName { get; set; }

        public int UnitBase { get; set; }
    }
}
