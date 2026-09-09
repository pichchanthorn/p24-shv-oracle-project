using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Oracle.EntityFrameworkCore.Infrastructure;
using backend.Data;
using backend.Models.Data;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
var connectionString = builder.Configuration.GetConnectionString("OracleDb");
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "P24AuthenticationApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "P24AngularClient";

// ---- Services ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Oracle EF Core DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseOracle(
        connectionString,
        oracleOptions => oracleOptions.UseOracleSQLCompatibility(
            OracleSQLCompatibility.DatabaseVersion21)));

// Password hasher
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// Custom services
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<TwoFactorService>();
builder.Services.AddScoped<IUserService, UserService>();

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // Security boundary: a 2fa-challenge token must never be usable as a normal
    // access token. This is enforced here, at the authentication pipeline level,
    // so every [Authorize] endpoint is protected regardless of the token's
    // signature/issuer/audience/expiry all being otherwise valid.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var tokenType = context.Principal?.FindFirst("token_type")?.Value;
            if (tokenType != "access")
            {
                context.Fail("Token type is not valid for this endpoint.");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// CORS for Angular dev client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ---- Middleware pipeline ----
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AngularClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }