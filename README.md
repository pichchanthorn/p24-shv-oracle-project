# P24 SHV Oracle Project

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-21-DD0031?logo=angular&logoColor=white)
![Build](https://img.shields.io/badge/build-not_configured-lightgrey)

A full-stack authentication system built as a university course project: an ASP.NET Core Web API backend with JWT and TOTP-based two-factor authentication, backed by Oracle Database, and an Angular client.

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

## Frontend

The `frontend/` directory contains an Angular 21 single-page application that consumes the backend API.

| Area | Technology |
|---|---|
| Framework | Angular 21 (standalone components) |
| HTTP | Angular `HttpClient` with a bearer-token interceptor |
| Language | TypeScript 5.9 |
| Package Manager | npm |

### Pages / Routes

| Route | Component | Description |
|---|---|---|
| `/login` | `Login` | Username/password sign-in |
| `/register` | `Register` | New user registration |
| `/dashboard` | `Dashboard` | Authenticated landing page, shows 2FA status |
| `/two-factor-setup` | `TwoFactorSetup` | QR code enrollment for TOTP 2FA |
| `/two-factor-verify` | `TwoFactorVerify` | 2FA challenge during login |

`auth.interceptor.ts` attaches the JWT access token to outgoing requests, but only to requests whose URL targets the backend API base URL — it never attaches the token to third-party requests. The API base URL is currently hardcoded in `src/app/services/auth.ts` (`https://localhost:7195/api/auth`) for local development against the backend's `https` launch profile. The backend's CORS policy allows `http://localhost:4200`, the default `ng serve` origin.

The access token is persisted in `sessionStorage` (cleared on logout or when the tab closes) so a page refresh doesn't force a re-login. `authGuard` protects `/dashboard` and `/two-factor-setup`, redirecting unauthenticated users to `/login`; this is a UX convenience only — the backend independently authorizes every protected API call and is the actual security boundary. `/two-factor-verify` is intentionally left outside the guard, since a user there holds only a short-lived 2FA challenge token, not an access token.

## Project Structure

```
backend/
├── backend/
│   ├── Controllers/
│   │   └── AuthController.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── AppDbContextFactory.cs
│   ├── Migrations/
│   ├── Models/
│   │   ├── Data/
│   │   │   ├── AppUser.cs
│   │   │   ├── AuthAuditLog.cs
│   │   │   └── Student.cs
│   │   └── Request/
│   │       └── AuthDtos.cs
│   ├── Services/
│   │   ├── JwtService.cs
│   │   ├── TwoFactorService.cs
│   │   ├── IUserService.cs
│   │   └── UserService.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Dockerfile
│   ├── Program.cs
│   ├── appsettings.json
│   └── backend.csproj
└── backend.Tests/
    ├── JwtServiceTests.cs
    ├── UserServiceTests.cs
    ├── TokenTypeBoundaryTests.cs
    └── TestWebApplicationFactory.cs

frontend/
└── src/
    └── app/
        ├── pages/
        │   ├── dashboard/
        │   ├── login/
        │   ├── register/
        │   ├── two-factor-setup/
        │   └── two-factor-verify/
        ├── services/
        │   ├── auth.ts
        │   ├── auth.interceptor.ts
        │   └── auth.guard.ts
        ├── app.config.ts
        └── app.routes.ts

docs/
└── BACKEND_DEVELOPMENT_MANUAL.md
```

`AuthController` delegates all business logic (password verification, lockout, 2FA setup/enable/verify/disable, audit logging) to `UserService` behind the `IUserService` interface, keeping the controller focused on HTTP concerns (status codes, model binding) and making the security-critical logic unit-testable without spinning up ASP.NET Core.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Node.js and npm (for the Angular frontend)
- Oracle Database (Docker or standalone)
- `dotnet-ef` CLI tool

### Backend Setup

```bash
cd backend/backend
dotnet restore
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OracleDb" "User Id=YOUR_USER;Password=YOUR_PASSWORD;Data Source=..."
dotnet user-secrets set "Jwt:Key" "your-secret-key-at-least-32-bytes"
dotnet ef database update
dotnet run --launch-profile https
```

The API listens on `https://localhost:7195` (and `http://localhost:5123`) by default.

### Frontend Setup

```bash
cd frontend
npm install
ng serve
```

The app is served at `http://localhost:4200` and expects the backend to be running at `https://localhost:7195`.

## API Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Anonymous | Register a new user |
| POST | `/api/auth/login` | Anonymous | Log in with username/password |
| POST | `/api/auth/2fa/setup` | Bearer token | Start 2FA setup (returns QR code) |
| POST | `/api/auth/2fa/enable` | Bearer token | Confirm and enable 2FA |
| POST | `/api/auth/2fa/verify-login` | Challenge token | Complete 2FA login |
| POST | `/api/auth/2fa/disable` | Bearer token | Disable 2FA |
| GET | `/api/auth/me` | Bearer token | Get the current user's profile |

## Security Notes

- Passwords are never stored in plaintext — they are hashed using ASP.NET Core's `PasswordHasher<TUser>`.
- 2FA (TOTP) secrets are stored server-side and are never exposed to the client after initial setup.
- Every JWT carries a `token_type` claim (`access` or `2fa-challenge`). `Program.cs` enforces `token_type == access` for every `[Authorize]`-protected endpoint via a `JwtBearerEvents.OnTokenValidated` handler, at the authentication pipeline level rather than inside individual actions. A `2fa-challenge` token — even one that is otherwise validly signed, unexpired, and has the correct issuer/audience — is rejected by every normal protected endpoint; it is only ever accepted by `POST /api/auth/2fa/verify-login`, which validates it explicitly. This boundary is covered by an integration test in `backend.Tests/TokenTypeBoundaryTests.cs`.
- JWT signing keys and database connection strings must never be committed to source control. Use `dotnet user-secrets` for local development and environment variables (or a secrets manager) in other environments.
- `backend/backend/appsettings.json` is excluded from version control via `.gitignore`; configure secrets locally with `dotnet user-secrets` instead of editing that file directly.
- Dependencies are regularly checked for known vulnerabilities with `dotnet list package --vulnerable --include-transitive`.

## Testing

`backend/backend.Tests` is a focused xUnit project covering the security-critical paths:

```bash
cd backend
dotnet test backend.Tests/backend.Tests.csproj
```

- `TokenTypeBoundaryTests` — integration tests (via `WebApplicationFactory<Program>` against an in-memory database) proving a `2fa-challenge` token is rejected by `[Authorize]` endpoints while a real `access` token is accepted, and that requests without a token get `401`.
- `UserServiceTests` — unit tests for login (wrong password, 5-failed-attempt lockout), 2FA enable (invalid/valid TOTP code), and the challenge-vs-access-token response shape.
- `JwtServiceTests` — unit tests for token_type discrimination and expiration handling for both token kinds.

No Angular test additions were made in this phase; the existing project has no frontend test suite to extend.

## Known Limitations / Roadmap

This project is under active development for a university course. The following items are tracked and planned before the codebase would be considered production-ready:

- [ ] Encrypt TOTP secrets at rest (currently stored as readable text in the database)
- [ ] Add Angular unit/e2e tests for the login and 2FA flows
- [ ] Add API rate limiting in addition to per-user lockout
- [ ] Add centralized exception handling and consistent error responses
- [ ] Add health/readiness endpoints, including an Oracle dependency check
- [ ] Add client-side JWT expiry parsing so the auth guard can react to an expired token before the API call — the backend response is currently what triggers logout

## Author

**Chan Thorn Pich**
