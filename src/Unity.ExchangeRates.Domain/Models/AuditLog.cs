using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unity.ExchangeRates.Domain.Models
{
    [Table("AuditLog")]
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        public string? TraceId { get; set; }

        [Required, StringLength(10)]
        public required string HttpMethod { get; set; }

        [Required, StringLength(500)]
        public required string Endpoint { get; set; }

        [StringLength(2000)]
        public string? QueryString { get; set; }

        public string? RequestHeaders { get; set; }

        public string? RequestBody { get; set; }

        public int ResponseStatusCode { get; set; }

        public string? ResponseBody { get; set; }

        [StringLength(50)]
        public string? ClientIpAddress { get; set; }

        //[StringLength(500)]
        //public string? UserAgent { get; set; }

        public long DurationMs { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
