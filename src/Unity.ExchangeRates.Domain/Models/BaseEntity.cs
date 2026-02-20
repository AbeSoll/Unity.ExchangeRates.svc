using System.ComponentModel.DataAnnotations;

namespace Unity.ExchangeRates.Domain.Models
{
    public abstract class BaseEntity<TId>
    {
        [Key]
        public virtual required TId Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
