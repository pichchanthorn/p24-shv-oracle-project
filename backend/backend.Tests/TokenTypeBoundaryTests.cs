using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Models.Request;
using Xunit;

namespace backend.Tests
{
    // Verifies the highest-priority security requirement: a 2fa-challenge
    // token must never be usable as a normal access token against
    // [Authorize]-protected endpoints. This exercises the real Program
    // pipeline (JwtBearerEvents.OnTokenValidated in Program.cs), not just
    // the manual check inside VerifyTwoFactorLoginAsync.
    public class TokenTypeBoundaryTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public TokenTypeBoundaryTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string username, string password)> RegisterAndEnableTwoFactorAsync(HttpClient client)
        {
            var username = $"user_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "Password123!";

            var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = password
            });
            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = username,
                Password = password
            });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginBody?.AccessToken);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

            var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
            Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
            var setupBody = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();

            var code = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(setupBody!.Secret)).ComputeTotp();

            var enableResponse = await client.PostAsJsonAsync("/api/auth/2fa/enable", new TwoFactorEnableRequest
            {
                TwoFactorCode = code
            });
            Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);

            client.DefaultRequestHeaders.Authorization = null;
            return (username, password);
        }

        [Fact]
        public async Task ProtectedEndpoint_With2faChallengeToken_IsRejected()
        {
            var client = _factory.CreateClient();
            var (username, password) = await RegisterAndEnableTwoFactorAsync(client);

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = username,
                Password = password
            });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.True(loginBody!.RequiresTwoFactor);
            Assert.NotNull(loginBody.ChallengeToken);
            Assert.Null(loginBody.AccessToken);

            // The critical assertion: a 2fa-challenge token must be rejected
            // by a normal [Authorize] endpoint, not just by the 2FA verify endpoint.
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginBody.ChallengeToken);

            var meResponse = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);

            var setupResponse = await client.PostAsync("/api/auth/2fa/setup", null);
            Assert.Equal(HttpStatusCode.Unauthorized, setupResponse.StatusCode);
        }

        [Fact]
        public async Task ProtectedEndpoint_WithAccessToken_IsAllowed()
        {
            var client = _factory.CreateClient();
            var (username, password) = await RegisterAndEnableTwoFactorAsync(client);

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = username,
                Password = password
            });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            var verifyResponse = await client.PostAsJsonAsync("/api/auth/2fa/verify-login", new TwoFactorVerifyLoginRequest
            {
                ChallengeToken = loginBody!.ChallengeToken!,
                TwoFactorCode = "000000"
            });
            // Wrong code is fine here; we only need a real access token below.
            Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
        }

        [Fact]
        public async Task ProtectedEndpoint_WithoutToken_Returns401()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ProtectedEndpoint_WithValidAccessToken_Returns200()
        {
            var client = _factory.CreateClient();
            var username = $"user_{Guid.NewGuid():N}".Substring(0, 20);
            const string password = "Password123!";

            await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = password
            });

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = username,
                Password = password
            });
            var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.False(loginBody!.RequiresTwoFactor);
            Assert.NotNull(loginBody.AccessToken);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);

            var meResponse = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        }
    }
}
