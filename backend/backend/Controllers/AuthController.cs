using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend.Models.Request;
using backend.Services;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        private string? GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString();

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        private static IActionResult ToErrorResult(UserServiceStatus status, string message) => status switch
        {
            UserServiceStatus.Conflict => new ConflictObjectResult(new { message }),
            UserServiceStatus.AccountLocked => new ObjectResult(new { message }) { StatusCode = 423 },
            UserServiceStatus.InvalidCredentials => new UnauthorizedObjectResult(new { message }),
            UserServiceStatus.NotFound => new UnauthorizedObjectResult(new { message }),
            UserServiceStatus.ValidationFailed => new BadRequestObjectResult(new { message }),
            _ => new BadRequestObjectResult(new { message })
        };

        // POST /api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _userService.RegisterAsync(request, GetClientIp());
            if (result.Status != UserServiceStatus.Ok)
            {
                return ToErrorResult(result.Status, result.Message!);
            }

            return Created(string.Empty, result.Value);
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

            var result = await _userService.LoginAsync(request, GetClientIp());
            if (result.Status != UserServiceStatus.Ok)
            {
                return ToErrorResult(result.Status, result.Message!);
            }

            return Ok(result.Value);
        }

        // POST /api/auth/2fa/setup
        [HttpPost("2fa/setup")]
        [Authorize]
        public async Task<IActionResult> SetupTwoFactor()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _userService.StartTwoFactorSetupAsync(userId.Value, GetClientIp());
            if (result.Status != UserServiceStatus.Ok)
            {
                return ToErrorResult(result.Status, result.Message!);
            }

            return Ok(result.Value);
        }

        // POST /api/auth/2fa/enable
        [HttpPost("2fa/enable")]
        [Authorize]
        public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorEnableRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _userService.EnableTwoFactorAsync(userId.Value, request, GetClientIp());
            if (result.Status != UserServiceStatus.Ok)
            {
                return ToErrorResult(result.Status, result.Message!);
            }

            return Ok(result.Value);
        }

        // POST /api/auth/2fa/verify-login
        [HttpPost("2fa/verify-login")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactorLogin([FromBody] TwoFactorVerifyLoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.VerifyTwoFactorLoginAsync(request, GetClientIp());
            if (result.Status != UserServiceStatus.Ok)
            {
                return ToErrorResult(result.Status, result.Message!);
            }

            return Ok(result.Value);
        }

        // POST /api/auth/2fa/disable
        [HttpPost("2fa/disable")]
        [Authorize]
        public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorDisableRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _userService.DisableTwoFactorAsync(userId.Value, request, GetClientIp());
            if (result.Status != UserServiceStatus.Ok)
            {
                return ToErrorResult(result.Status, result.Message!);
            }

            return Ok(result.Value);
        }

        // GET /api/auth/me
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUserProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _userService.GetProfileAsync(userId.Value);
            if (result.Status != UserServiceStatus.Ok)
            {
                return ToErrorResult(result.Status, result.Message!);
            }

            return Ok(result.Value);
        }
    }
}
