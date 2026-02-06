using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace Unity.ExchangeRates.svc.Models
{
    [Table("Currency")]
    public class Currency : BaseEntity<string>
    {
        [StringLength(100)]
        public required string CurrencyName { get; set; }

        public int UnitBase { get; set; }

        // navigation: 1 Currency -> many ExchangeRateHistory
        public ICollection<ExchangeRateHistory> ExchangeRateHistories { get; set; } = new List<ExchangeRateHistory>();
    }
}