using Microsoft.Extensions.Configuration;
using backend.Models.Data;
using backend.Services;
using Xunit;

namespace backend.Tests
{
    public class JwtServiceTests
    {
        private static JwtService CreateService(int accessMinutes = 15, int challengeMinutes = 5)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "Test-Only-Signing-Key-That-Is-At-Least-32-Bytes-Long!",
                    ["Jwt:Issuer"] = "P24AuthenticationApi",
                    ["Jwt:Audience"] = "P24AngularClient",
                    ["Jwt:AccessTokenExpirationMinutes"] = accessMinutes.ToString(),
                    ["Jwt:TwoFactorChallengeExpirationMinutes"] = challengeMinutes.ToString()
                })
                .Build();

            return new JwtService(configuration);
        }

        private static AppUser TestUser() => new()
        {
            Id = 1,
            Username = "testuser",
            Email = "testuser@example.com"
        };

        [Fact]
        public void GenerateAccessToken_ValidatedAsAccess_Succeeds()
        {
            var service = CreateService();
            var (token, _) = service.GenerateAccessToken(TestUser(), "pwd");

            var principal = service.ValidateToken(token, "access");

            Assert.NotNull(principal);
        }

        [Fact]
        public void GenerateTwoFactorChallengeToken_ValidatedAsAccess_Fails()
        {
            var service = CreateService();
            var (token, _) = service.GenerateTwoFactorChallengeToken(TestUser());

            // The token is well-formed and signed correctly, but its
            // token_type claim is "2fa-challenge" — it must not validate
            // when the caller expects an "access" token.
            var principal = service.ValidateToken(token, "access");

            Assert.Null(principal);
        }

        [Fact]
        public void GenerateAccessToken_ValidatedAsChallenge_Fails()
        {
            var service = CreateService();
            var (token, _) = service.GenerateAccessToken(TestUser(), "pwd");

            var principal = service.ValidateToken(token, "2fa-challenge");

            Assert.Null(principal);
        }

        [Fact]
        public void ExpiredChallengeToken_IsRejected()
        {
            var service = CreateService(challengeMinutes: 0);
            var (token, _) = service.GenerateTwoFactorChallengeToken(TestUser());

            Thread.Sleep(TimeSpan.FromSeconds(31));

            var principal = service.ValidateToken(token, "2fa-challenge");

            Assert.Null(principal);
        }

        [Fact]
        public void ExpiredAccessToken_IsRejected()
        {
            var service = CreateService(accessMinutes: 0);
            var (token, _) = service.GenerateAccessToken(TestUser(), "pwd");

            Thread.Sleep(TimeSpan.FromSeconds(31));

            var principal = service.ValidateToken(token, "access");

            Assert.Null(principal);
        }
    }
}
