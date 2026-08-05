using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models.Data
{
    [Table("AUTH_AUDIT_LOGS")]
    public class AuthAuditLog
    {
        [Key]
        public int Id { get; set; }

        public int? UserId { get; set; }

        [MaxLength(100)]
        public string? Username { get; set; }

        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        public bool Success { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        public string? Details { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}