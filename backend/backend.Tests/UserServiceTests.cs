using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using backend.Data;
using backend.Models.Data;
using backend.Models.Request;
using backend.Services;
using Xunit;

namespace backend.Tests
{
    public class UserServiceTests
    {
        private static (UserService service, AppDbContext db) CreateService()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new AppDbContext(options);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "Test-Only-Signing-Key-That-Is-At-Least-32-Bytes-Long!",
                    ["Security:MaxFailedLoginAttempts"] = "5",
                    ["Security:LockoutMinutes"] = "15"
                })
                .Build();

            var jwtService = new JwtService(configuration);
            var twoFactorService = new TwoFactorService();
            var passwordHasher = new PasswordHasher<AppUser>();

            var service = new UserService(db, passwordHasher, jwtService, twoFactorService, configuration);
            return (service, db);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
        {
            var (service, _) = CreateService();
            await service.RegisterAsync(new RegisterRequest { Username = "alice", Email = "alice@example.com", Password = "Password123!" }, null);

            var result = await service.LoginAsync(new LoginRequest { Username = "alice", Password = "WrongPassword!" }, null);

            Assert.Equal(UserServiceStatus.InvalidCredentials, result.Status);
        }

        [Fact]
        public async Task Login_WithFiveFailedAttempts_LocksAccount()
        {
            var (service, _) = CreateService();
            await service.RegisterAsync(new RegisterRequest { Username = "bob", Email = "bob@example.com", Password = "Password123!" }, null);

            for (var i = 0; i < 5; i++)
            {
                await service.LoginAsync(new LoginRequest { Username = "bob", Password = "WrongPassword!" }, null);
            }

            var result = await service.LoginAsync(new LoginRequest { Username = "bob", Password = "Password123!" }, null);

            Assert.Equal(UserServiceStatus.AccountLocked, result.Status);
        }

        [Fact]
        public async Task Login_WithCorrectPasswordAndNoTwoFactor_ReturnsAccessToken()
        {
            var (service, _) = CreateService();
            await service.RegisterAsync(new RegisterRequest { Username = "carol", Email = "carol@example.com", Password = "Password123!" }, null);

            var result = await service.LoginAsync(new LoginRequest { Username = "carol", Password = "Password123!" }, null);

            Assert.Equal(UserServiceStatus.Ok, result.Status);
            Assert.False(result.Value!.RequiresTwoFactor);
            Assert.NotNull(result.Value.AccessToken);
            Assert.Null(result.Value.ChallengeToken);
        }

        [Fact]
        public async Task EnableTwoFactor_WithInvalidCode_IsRejected()
        {
            var (service, _) = CreateService();
            await service.RegisterAsync(new RegisterRequest { Username = "dave", Email = "dave@example.com", Password = "Password123!" }, null);
            var login = await service.LoginAsync(new LoginRequest { Username = "dave", Password = "Password123!" }, null);
            var userId = GetUserIdFromAccessToken(login.Value!.AccessToken!);

            await service.StartTwoFactorSetupAsync(userId, null);
            var result = await service.EnableTwoFactorAsync(userId, new TwoFactorEnableRequest { TwoFactorCode = "000000" }, null);

            Assert.Equal(UserServiceStatus.ValidationFailed, result.Status);
        }

        [Fact]
        public async Task EnableTwoFactor_WithValidCode_Succeeds()
        {
            var (service, db) = CreateService();
            await service.RegisterAsync(new RegisterRequest { Username = "erin", Email = "erin@example.com", Password = "Password123!" }, null);
            var login = await service.LoginAsync(new LoginRequest { Username = "erin", Password = "Password123!" }, null);
            var userId = GetUserIdFromAccessToken(login.Value!.AccessToken!);

            var setup = await service.StartTwoFactorSetupAsync(userId, null);
            var code = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret)).ComputeTotp();

            var result = await service.EnableTwoFactorAsync(userId, new TwoFactorEnableRequest { TwoFactorCode = code }, null);

            Assert.Equal(UserServiceStatus.Ok, result.Status);
            var user = await db.AppUsers.FindAsync(userId);
            Assert.True(user!.IsTwoFactorEnabled);
        }

        [Fact]
        public async Task Login_WithTwoFactorEnabled_ReturnsChallengeTokenNotAccessToken()
        {
            var (service, _) = CreateService();
            await service.RegisterAsync(new RegisterRequest { Username = "frank", Email = "frank@example.com", Password = "Password123!" }, null);
            var login = await service.LoginAsync(new LoginRequest { Username = "frank", Password = "Password123!" }, null);
            var userId = GetUserIdFromAccessToken(login.Value!.AccessToken!);
            var setup = await service.StartTwoFactorSetupAsync(userId, null);
            var code = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(setup.Value!.Secret)).ComputeTotp();
            await service.EnableTwoFactorAsync(userId, new TwoFactorEnableRequest { TwoFactorCode = code }, null);

            var secondLogin = await service.LoginAsync(new LoginRequest { Username = "frank", Password = "Password123!" }, null);

            Assert.True(secondLogin.Value!.RequiresTwoFactor);
            Assert.Null(secondLogin.Value.AccessToken);
            Assert.NotNull(secondLogin.Value.ChallengeToken);
        }

        private static int GetUserIdFromAccessToken(string token)
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            return int.Parse(jwt.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        }
    }
}
