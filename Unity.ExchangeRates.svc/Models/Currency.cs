using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unity.ExchangeRates.svc.Models
{
    // Maps to the "Currency" table in the database
    [Table("Currency")]
    public class Currency : BaseEntity<string>
    {
        // Remove the Id declaration so we do NOT hide the required base member.
        // The base `Id` will be mapped via Fluent API in AppDbContext.

        // Full name of the currency (e.g., "US Dollar", "Japanese Yen")
        [StringLength(100)]
        public required string CurrencyName { get; set; }

        // The unit base for the currency (e.g., 1 for USD, 100 for JPY)
        public int UnitBase { get; set; }
    }
}