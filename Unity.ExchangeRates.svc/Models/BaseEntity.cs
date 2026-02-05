namespace Unity.ExchangeRates.svc.Models
{
    public abstract class BaseEntity<TId>
    {
        public required TId Id { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedOn { get; set; }
        public required string CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; } = DateTime.MinValue;
        public string? ModifiedBy { get; set; }
    }
}