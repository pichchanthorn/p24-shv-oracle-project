using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend.Data;
using backend.Models.Data;
using backend.Models.Request;
using backend.Services;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly JwtService _jwtService;
        private readonly TwoFactorService _twoFactorService;

        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        public AuthController(
            AppDbContext db,
            IPasswordHasher<AppUser> passwordHasher,
            JwtService jwtService,
            TwoFactorService twoFactorService)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _twoFactorService = twoFactorService;
        }

        private string? GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString();

        private async Task LogAuditAsync(int? userId, string? username, string eventType, bool success, string? details = null)
        {
            _db.AuthAuditLogs.Add(new AuthAuditLog
            {
                UserId = userId,
                Username = username,
                EventType = eventType,
                Success = success,
                IpAddress = GetClientIp(),
                Details = details,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        // POST /api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var usernameLower = request.Username.ToLower();
            var emailLower = request.Email.ToLower();

            var exists = await _db.AppUsers.AnyAsync(u =>
                u.Username.ToLower() == usernameLower || u.Email.ToLower() == emailLower);

            if (exists)
            {
                return Conflict(new { message = "Username or email already exists." });
            }

            var user = new AppUser
            {
                Username = request.Username,
                Email = request.Email,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _db.AppUsers.Add(user);
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "REGISTER", true);

            return Created(string.Empty, new { message = "User created successfully." });
        }

        // POST /api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var usernameLower = request.Username.ToLower();
            var user = await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == usernameLower);

            if (user == null || !user.IsActive)
            {
                await LogAuditAsync(null, request.Username, "LOGIN", false, "User not found or inactive");
                return Unauthorized(new { message = "Invalid username or password." });
            }

            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
            {
                await LogAuditAsync(user.Id, user.Username, "LOGIN", false, "Account locked");
                return StatusCode(423, new { message = "Account is temporarily locked. Try again later." });
            }

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (verifyResult == PasswordVerificationResult.Failed)
            {
                user.FailedLoginCount += 1;

                if (user.FailedLoginCount >= MaxFailedAttempts)
                {
                    user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                }

                user.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await LogAuditAsync(user.Id, user.Username, "LOGIN", false, "Invalid password");
                return Unauthorized(new { message = "Invalid username or password." });
            }

            // Password valid - regenerate hash if needed
            if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            }

            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (user.IsTwoFactorEnabled)
            {
                await LogAuditAsync(user.Id, user.Username, "PASSWORD_VERIFIED_2FA_REQUIRED", true);

                var (challengeToken, challengeExpires) = _jwtService.GenerateTwoFactorChallengeToken(user);

                return Ok(new LoginResponse
                {
                    RequiresTwoFactor = true,
                    AccessToken = null,
                    ChallengeToken = challengeToken,
                    ExpiresAtUtc = challengeExpires
                });
            }

            await LogAuditAsync(user.Id, user.Username, "LOGIN", true);

            var (accessToken, accessExpires) = _jwtService.GenerateAccessToken(user, "pwd");

            return Ok(new LoginResponse
            {
                RequiresTwoFactor = false,
                AccessToken = accessToken,
                ChallengeToken = null,
                ExpiresAtUtc = accessExpires
            });
        }

        // POST /api/auth/2fa/setup
        [HttpPost("2fa/setup")]
        [Authorize]
        public async Task<IActionResult> SetupTwoFactor()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var user = await _db.AppUsers.FindAsync(userId.Value);
            if (user == null) return Unauthorized();

            var secret = _twoFactorService.GenerateSecret();
            var otpAuthUri = _twoFactorService.BuildOtpAuthUri(secret, user.Username);
            var qrCodeDataUrl = _twoFactorService.GenerateQrCodeDataUrl(otpAuthUri);

            user.TwoFactorSecret = secret;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "2FA_SETUP_STARTED", true);

            return Ok(new TwoFactorSetupResponse
            {
                Secret = secret,
                OtpAuthUri = otpAuthUri,
                QrCodeDataUrl = qrCodeDataUrl
            });
        }

        // POST /api/auth/2fa/enable
        [HttpPost("2fa/enable")]
        [Authorize]
        public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorEnableRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var user = await _db.AppUsers.FindAsync(userId.Value);
            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                return BadRequest(new { message = "2FA setup has not been started." });
            }

            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.TwoFactorCode);
            if (!isValid)
            {
                await LogAuditAsync(user.Id, user.Username, "2FA_ENABLED", false, "Invalid code");
                return BadRequest(new { message = "Invalid verification code." });
            }

            user.IsTwoFactorEnabled = true;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "2FA_ENABLED", true);

            return Ok(new { message = "Two-factor authentication enabled." });
        }

        // POST /api/auth/2fa/verify-login
        [HttpPost("2fa/verify-login")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactorLogin([FromBody] TwoFactorVerifyLoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var principal = _jwtService.ValidateToken(request.ChallengeToken, "2fa-challenge");
            if (principal == null)
            {
                return Unauthorized(new { message = "Invalid or expired challenge token." });
            }

            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid challenge token." });
            }

            var user = await _db.AppUsers.FindAsync(userId);
            if (user == null || !user.IsActive || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                return Unauthorized(new { message = "Unable to complete two-factor login." });
            }

            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.TwoFactorCode);
            if (!isValid)
            {
                await LogAuditAsync(user.Id, user.Username, "2FA_LOGIN", false, "Invalid code");
                return Unauthorized(new { message = "Invalid verification code." });
            }

            await LogAuditAsync(user.Id, user.Username, "2FA_LOGIN", true);

            var (accessToken, accessExpires) = _jwtService.GenerateAccessToken(user, "pwd+mfa");

            return Ok(new LoginResponse
            {
                RequiresTwoFactor = false,
                AccessToken = accessToken,
                ChallengeToken = null,
                ExpiresAtUtc = accessExpires
            });
        }

        // POST /api/auth/2fa/disable
        [HttpPost("2fa/disable")]
        [Authorize]
        public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorDisableRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var user = await _db.AppUsers.FindAsync(userId.Value);
            if (user == null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                return BadRequest(new { message = "Two-factor authentication is not enabled." });
            }

            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.TwoFactorCode);
            if (!isValid)
            {
                await LogAuditAsync(user.Id, user.Username, "2FA_DISABLED", false, "Invalid code");
                return BadRequest(new { message = "Invalid verification code." });
            }

            user.IsTwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "2FA_DISABLED", true);

            return Ok(new { message = "Two-factor authentication disabled." });
        }
    }
}