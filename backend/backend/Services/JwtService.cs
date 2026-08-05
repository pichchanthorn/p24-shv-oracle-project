using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using backend.Models.Data;

namespace backend.Services
{
    public class JwtService
    {
        private readonly string _key;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _accessTokenExpirationMinutes;
        private readonly int _twoFactorChallengeExpirationMinutes;

        public JwtService(IConfiguration configuration)
        {
            _key = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            _issuer = configuration["Jwt:Issuer"] ?? "P24AuthenticationApi";
            _audience = configuration["Jwt:Audience"] ?? "P24AngularClient";
            _accessTokenExpirationMinutes =
                int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMin)
                    ? accessMin : 15;
            _twoFactorChallengeExpirationMinutes =
                int.TryParse(configuration["Jwt:TwoFactorChallengeExpirationMinutes"], out var challengeMin)
                    ? challengeMin : 5;
        }

        private SigningCredentials GetSigningCredentials()
        {
            var keyBytes = Encoding.UTF8.GetBytes(_key);
            var securityKey = new SymmetricSecurityKey(keyBytes);
            return new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        }

        public (string token, DateTimeOffset expiresAtUtc) GenerateAccessToken(AppUser user, string amr)
        {
            var expires = DateTimeOffset.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("token_type", "access"),
                new Claim("amr", amr),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: expires.UtcDateTime,
                signingCredentials: GetSigningCredentials());

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        public (string token, DateTimeOffset expiresAtUtc) GenerateTwoFactorChallengeToken(AppUser user)
        {
            var expires = DateTimeOffset.UtcNow.AddMinutes(_twoFactorChallengeExpirationMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("token_type", "2fa-challenge"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: expires.UtcDateTime,
                signingCredentials: GetSigningCredentials());

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        public ClaimsPrincipal? ValidateToken(string token, string expectedTokenType)
        {
            var handler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.UTF8.GetBytes(_key);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            try
            {
                var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

                var tokenTypeClaim = principal.FindFirst("token_type")?.Value;
                if (tokenTypeClaim != expectedTokenType)
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}