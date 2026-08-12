​
# P24 SHV Oracle Project

Authentication API backend built with ASP.NET Core, Entity Framework Core, and Oracle Database.

## Features

- User registration and password authentication (secure password hashing)
- JWT bearer access tokens
- Time-based one-time password (TOTP) two-factor authentication with QR code setup
- Login throttling through temporary account lockout (5 failed attempts → 15-minute lock)
- Authentication audit logging
- CORS support for Angular development clients

## Technology Stack

| Area | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 |
| ORM | Entity Framework Core 10 |
| Database | Oracle Database |
| Authentication | JWT Bearer |
| Password Hashing | ASP.NET Core `PasswordHasher<TUser>` |
| Two-Factor Auth | TOTP via `Otp.NET` |
| QR Code Generation | `QRCoder` |

## Project Structure

```
backend/
└── backend/
    ├── Controllers/
    │   └── AuthController.cs
    ├── Data/
    │   ├── AppDbContext.cs
    │   └── AppDbContextFactory.cs
    ├── Migrations/
    ├── Models/
    │   ├── Data/
    │   └── Request/
    ├── Services/
    │   ├── JwtService.cs
    │   └── TwoFactorService.cs
    ├── Program.cs
    └── appsettings.json
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- Oracle Database (Docker or standalone)
- `dotnet-ef` CLI tool

### Setup

```bash
cd backend/backend
dotnet restore
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OracleDb" "User Id=YOUR_USER;Password=YOUR_PASSWORD;Data Source=..."
dotnet user-secrets set "Jwt:Key" "your-secret-key-at-least-32-bytes"
dotnet ef database update
dotnet run --launch-profile https
```

## API Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Anonymous | Register a new user |
| POST | `/api/auth/login` | Anonymous | Log in with username/password |
| POST | `/api/auth/2fa/setup` | Bearer token | Start 2FA setup (returns QR code) |
| POST | `/api/auth/2fa/enable` | Bearer token | Confirm and enable 2FA |
| POST | `/api/auth/2fa/verify-login` | Challenge token | Complete 2FA login |
| POST | `/api/auth/2fa/disable` | Bearer token | Disable 2FA |

## Author

**Chan Thorn Pich**
