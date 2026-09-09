using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models.Data;
using backend.Models.Request;

namespace backend.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly JwtService _jwtService;
        private readonly TwoFactorService _twoFactorService;
        private readonly int _maxFailedLoginAttempts;
        private readonly TimeSpan _lockoutDuration;

        public UserService(
            AppDbContext db,
            IPasswordHasher<AppUser> passwordHasher,
            JwtService jwtService,
            TwoFactorService twoFactorService,
            IConfiguration configuration)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _twoFactorService = twoFactorService;

            _maxFailedLoginAttempts =
                int.TryParse(configuration["Security:MaxFailedLoginAttempts"], out var maxAttempts)
                    ? maxAttempts : 5;
            _lockoutDuration = TimeSpan.FromMinutes(
                int.TryParse(configuration["Security:LockoutMinutes"], out var lockoutMinutes)
                    ? lockoutMinutes : 15);
        }

        private async Task LogAuditAsync(int? userId, string? username, string eventType, bool success, string? ipAddress, string? details = null)
        {
            _db.AuthAuditLogs.Add(new AuthAuditLog
            {
                UserId = userId,
                Username = username,
                EventType = eventType,
                Success = success,
                IpAddress = ipAddress,
                Details = details,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task<UserServiceResult<object>> RegisterAsync(RegisterRequest request, string? ipAddress)
        {
            var usernameLower = request.Username.ToLower();
            var emailLower = request.Email.ToLower();

            var exists = await _db.AppUsers.AnyAsync(u =>
                u.Username.ToLower() == usernameLower || u.Email.ToLower() == emailLower);

            if (exists)
            {
                return UserServiceResult<object>.Fail(UserServiceStatus.Conflict, "Username or email already exists.");
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

            await LogAuditAsync(user.Id, user.Username, "REGISTER", true, ipAddress);

            return UserServiceResult<object>.Ok(new { message = "User created successfully." });
        }

        public async Task<UserServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress)
        {
            var usernameLower = request.Username.ToLower();
            var user = await _db.AppUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == usernameLower);

            if (user == null || !user.IsActive)
            {
                await LogAuditAsync(null, request.Username, "LOGIN", false, ipAddress, "User not found or inactive");
                return UserServiceResult<LoginResponse>.Fail(UserServiceStatus.InvalidCredentials, "Invalid username or password.");
            }

            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
            {
                await LogAuditAsync(user.Id, user.Username, "LOGIN", false, ipAddress, "Account locked");
                return UserServiceResult<LoginResponse>.Fail(UserServiceStatus.AccountLocked, "Account is temporarily locked. Try again later.");
            }

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (verifyResult == PasswordVerificationResult.Failed)
            {
                user.FailedLoginCount += 1;

                if (user.FailedLoginCount >= _maxFailedLoginAttempts)
                {
                    user.LockoutEndUtc = DateTime.UtcNow.Add(_lockoutDuration);
                }

                user.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await LogAuditAsync(user.Id, user.Username, "LOGIN", false, ipAddress, "Invalid password");
                return UserServiceResult<LoginResponse>.Fail(UserServiceStatus.InvalidCredentials, "Invalid username or password.");
            }

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
                await LogAuditAsync(user.Id, user.Username, "PASSWORD_VERIFIED_2FA_REQUIRED", true, ipAddress);

                var (challengeToken, challengeExpires) = _jwtService.GenerateTwoFactorChallengeToken(user);

                return UserServiceResult<LoginResponse>.Ok(new LoginResponse
                {
                    RequiresTwoFactor = true,
                    AccessToken = null,
                    ChallengeToken = challengeToken,
                    ExpiresAtUtc = challengeExpires
                });
            }

            await LogAuditAsync(user.Id, user.Username, "LOGIN", true, ipAddress);

            var (accessToken, accessExpires) = _jwtService.GenerateAccessToken(user, "pwd");

            return UserServiceResult<LoginResponse>.Ok(new LoginResponse
            {
                RequiresTwoFactor = false,
                AccessToken = accessToken,
                ChallengeToken = null,
                ExpiresAtUtc = accessExpires
            });
        }

        public async Task<UserServiceResult<TwoFactorSetupResponse>> StartTwoFactorSetupAsync(int userId, string? ipAddress)
        {
            var user = await _db.AppUsers.FindAsync(userId);
            if (user == null)
            {
                return UserServiceResult<TwoFactorSetupResponse>.Fail(UserServiceStatus.NotFound, "User not found.");
            }

            if (user.IsTwoFactorEnabled)
            {
                return UserServiceResult<TwoFactorSetupResponse>.Fail(
                    UserServiceStatus.ValidationFailed,
                    "Two-factor authentication is already enabled. Disable it first to set up again.");
            }

            var secret = _twoFactorService.GenerateSecret();
            var otpAuthUri = _twoFactorService.BuildOtpAuthUri(secret, user.Username);
            var qrCodeDataUrl = _twoFactorService.GenerateQrCodeDataUrl(otpAuthUri);

            user.TwoFactorSecret = secret;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "2FA_SETUP_STARTED", true, ipAddress);

            return UserServiceResult<TwoFactorSetupResponse>.Ok(new TwoFactorSetupResponse
            {
                Secret = secret,
                OtpAuthUri = otpAuthUri,
                QrCodeDataUrl = qrCodeDataUrl
            });
        }

        public async Task<UserServiceResult<object>> EnableTwoFactorAsync(int userId, TwoFactorEnableRequest request, string? ipAddress)
        {
            var user = await _db.AppUsers.FindAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                return UserServiceResult<object>.Fail(UserServiceStatus.ValidationFailed, "2FA setup has not been started.");
            }

            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.TwoFactorCode);
            if (!isValid)
            {
                await LogAuditAsync(user.Id, user.Username, "2FA_ENABLED", false, ipAddress, "Invalid code");
                return UserServiceResult<object>.Fail(UserServiceStatus.ValidationFailed, "Invalid verification code.");
            }

            user.IsTwoFactorEnabled = true;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "2FA_ENABLED", true, ipAddress);

            return UserServiceResult<object>.Ok(new { message = "Two-factor authentication enabled." });
        }

        public async Task<UserServiceResult<LoginResponse>> VerifyTwoFactorLoginAsync(TwoFactorVerifyLoginRequest request, string? ipAddress)
        {
            var principal = _jwtService.ValidateToken(request.ChallengeToken, "2fa-challenge");
            if (principal == null)
            {
                return UserServiceResult<LoginResponse>.Fail(UserServiceStatus.InvalidCredentials, "Invalid or expired challenge token.");
            }

            var idClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId))
            {
                return UserServiceResult<LoginResponse>.Fail(UserServiceStatus.InvalidCredentials, "Invalid challenge token.");
            }

            var user = await _db.AppUsers.FindAsync(userId);
            if (user == null || !user.IsActive || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                return UserServiceResult<LoginResponse>.Fail(UserServiceStatus.InvalidCredentials, "Unable to complete two-factor login.");
            }

            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.TwoFactorCode);
            if (!isValid)
            {
                await LogAuditAsync(user.Id, user.Username, "2FA_LOGIN", false, ipAddress, "Invalid code");
                return UserServiceResult<LoginResponse>.Fail(UserServiceStatus.InvalidCredentials, "Invalid verification code.");
            }

            await LogAuditAsync(user.Id, user.Username, "2FA_LOGIN", true, ipAddress);

            var (accessToken, accessExpires) = _jwtService.GenerateAccessToken(user, "pwd+mfa");

            return UserServiceResult<LoginResponse>.Ok(new LoginResponse
            {
                RequiresTwoFactor = false,
                AccessToken = accessToken,
                ChallengeToken = null,
                ExpiresAtUtc = accessExpires
            });
        }

        public async Task<UserServiceResult<object>> DisableTwoFactorAsync(int userId, TwoFactorDisableRequest request, string? ipAddress)
        {
            var user = await _db.AppUsers.FindAsync(userId);
            if (user == null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                return UserServiceResult<object>.Fail(UserServiceStatus.ValidationFailed, "Two-factor authentication is not enabled.");
            }

            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.TwoFactorCode);
            if (!isValid)
            {
                await LogAuditAsync(user.Id, user.Username, "2FA_DISABLED", false, ipAddress, "Invalid code");
                return UserServiceResult<object>.Fail(UserServiceStatus.ValidationFailed, "Invalid verification code.");
            }

            user.IsTwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAuditAsync(user.Id, user.Username, "2FA_DISABLED", true, ipAddress);

            return UserServiceResult<object>.Ok(new { message = "Two-factor authentication disabled." });
        }

        public async Task<UserServiceResult<UserProfileResponse>> GetProfileAsync(int userId)
        {
            var user = await _db.AppUsers.FindAsync(userId);
            if (user == null)
            {
                return UserServiceResult<UserProfileResponse>.Fail(UserServiceStatus.NotFound, "User not found.");
            }

            return UserServiceResult<UserProfileResponse>.Ok(new UserProfileResponse
            {
                Username = user.Username,
                Email = user.Email,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled
            });
        }
    }
}
