using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models.Data
{
    [Table("APP_USERS")]
    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsTwoFactorEnabled { get; set; } = false;

        public string? TwoFactorSecret { get; set; }

        public int FailedLoginCount { get; set; } = 0;

        public DateTime? LockoutEndUtc { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}