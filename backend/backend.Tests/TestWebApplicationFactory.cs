using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using backend.Data;

namespace backend.Tests
{
    // Boots the real Program pipeline (real JWT bearer configuration, real
    // middleware order) but swaps the Oracle-backed DbContext for an
    // isolated in-memory database, so tests never require a live Oracle
    // instance while still exercising the actual authentication boundary.
    public class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const string TestJwtKey = "Test-Only-Signing-Key-That-Is-At-Least-32-Bytes-Long!";
        public readonly string DatabaseName = Guid.NewGuid().ToString();

        // Program.cs reads Jwt:Key/Issuer/Audience into local variables at the
        // top of the file (before the host is built), so overriding them via
        // WebApplicationFactory's ConfigureAppConfiguration hook is too late —
        // it only affects IConfiguration as seen by services resolved through
        // DI later (like JwtService), causing a signing-key mismatch between
        // the token issuer and the bearer validation parameters. Environment
        // variables are read at builder-creation time, early enough for both
        // to agree, so we set them before the host is constructed instead.
        public TestWebApplicationFactory()
        {
            Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);
            Environment.SetEnvironmentVariable("Jwt__Issuer", "P24AuthenticationApi");
            Environment.SetEnvironmentVariable("Jwt__Audience", "P24AngularClient");
            Environment.SetEnvironmentVariable("Jwt__AccessTokenExpirationMinutes", "15");
            Environment.SetEnvironmentVariable("Jwt__TwoFactorChallengeExpirationMinutes", "5");
            Environment.SetEnvironmentVariable("Security__MaxFailedLoginAttempts", "5");
            Environment.SetEnvironmentVariable("Security__LockoutMinutes", "15");
            Environment.SetEnvironmentVariable("ConnectionStrings__OracleDb", "unused-in-tests");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove every registration Program.cs made for the
                // Oracle-backed AppDbContext (options, options factory, and
                // the context registration itself) before adding the
                // in-memory provider — EF Core refuses to run with two
                // providers registered in the same service collection.
                var descriptorsToRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>) ||
                    (d.ServiceType.FullName?.Contains("Oracle") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("Oracle") ?? false)).ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(DatabaseName));
            });
        }
    }
}
