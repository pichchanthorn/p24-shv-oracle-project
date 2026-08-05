using System.ComponentModel.DataAnnotations;

namespace backend.Models.Request
{
    public class RegisterRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool RequiresTwoFactor { get; set; }
        public string? AccessToken { get; set; }
        public string? ChallengeToken { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }

    public class TwoFactorSetupResponse
    {
        public string Secret { get; set; } = string.Empty;
        public string OtpAuthUri { get; set; } = string.Empty;
        public string QrCodeDataUrl { get; set; } = string.Empty;
    }

    public class TwoFactorEnableRequest
    {
        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string TwoFactorCode { get; set; } = string.Empty;
    }

    public class TwoFactorDisableRequest
    {
        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string TwoFactorCode { get; set; } = string.Empty;
    }

    public class TwoFactorVerifyLoginRequest
    {
        [Required]
        public string ChallengeToken { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string TwoFactorCode { get; set; } = string.Empty;
    }
}