# Launchly

A multi-tenant SaaS platform that lets a business spin up its own storefront — e-commerce, restaurant ordering, or appointment booking — under its own subdomain, with a single shared codebase serving every tenant.

Built with **ASP.NET Core 10** and **PostgreSQL**, using EF Core's Global Query Filters to enforce tenant data isolation at the database layer rather than relying on developers to remember a `WHERE TenantId = ...` clause in every query.

## Why this project

Most "to-do list API" portfolio projects don't have to deal with the problem that actually makes SaaS backends hard: **making sure Tenant A can never see Tenant B's data**, no matter how many controllers and services touch the database. This project is built around solving that problem properly, with automated tests that prove it.

## Architecture

- **Multi-tenancy by subdomain.** A middleware resolves the tenant from the request's host (`tenant-name.launchly.app`), with a JWT-claim fallback for local development.
- **Tenant isolation via EF Core Global Query Filters.** Every tenant-scoped entity (`Product`, `Order`, `Appointment`, `MenuItem`, etc.) has a filter baked into the `DbContext` itself — so even a query a developer forgets to scope manually still can't leak across tenants.
- **Three store types, one codebase.** A tenant can run an e-commerce store, a restaurant ordering system, or an appointment-booking business — `StoreType` drives which feature set is active.
- **Role-based access**: `SuperAdmin` (platform owner), `TenantAdmin` (business owner), `Customer` (shopper) — enforced via ASP.NET Core authorization policies, not manual `if` checks scattered through controllers.
- **JWT auth with refresh-token rotation**, Google OAuth login, email verification, and password reset — refresh tokens are hashed before storage, and changing a password revokes all of a user's existing sessions.
- **Optimistic concurrency on stock.** Two customers buying the last unit of the same product can't both succeed — `Product` stock updates are protected by a concurrency token (PostgreSQL `xmin`), with bounded retry against fresh data when a write collides.
- **Cloudinary direct-upload signing.** Images never pass through the API server — the backend just issues a short-lived signed upload request, scoped to the tenant's own folder.

## Security practices

- **FluentValidation on every request DTO** — including auth endpoints (password strength, email format, reserved-subdomain checks) that are easy to forget and easy to exploit if skipped.
- **No secrets committed.** Local config lives in `appsettings.Development.json`, which is gitignored; `appsettings.Development.json.example` documents the expected shape with placeholder values.
- **IDOR-checked cross-resource writes** — creating an order or appointment for a customer ID always re-validates that the customer actually belongs to the requesting tenant, not just that the ID exists somewhere in the system.
- **Structured logging via Serilog**, with a global exception handler that returns generic error messages in production (no stack traces, no internal details leaked to API responses).
- **Rate limiting** on the API surface via `AspNetCoreRateLimit`.

## Tested

```
Test summary: total: 15, succeeded: 15
```

The test suite specifically targets tenant isolation — seeding two tenants with colliding data and asserting that one tenant's service layer can never read, update, or delete the other's records. These are the tests that matter most in a multi-tenant system: a failure here is a data breach, not just a bug.

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10 (.NET 10) |
| Database | PostgreSQL via Npgsql + EF Core 9 |
| Auth | JWT Bearer + refresh tokens, Google OAuth |
| Validation | FluentValidation |
| File storage | Cloudinary (direct signed upload) |
| Email | SendGrid |
| Logging | Serilog (console + file sinks) |
| Testing | xUnit, NSubstitute, FluentAssertions, EF Core InMemory |

## Project structure

```
Launchly.API/
├── Application/        # Feature-organized business logic (Auth, Orders, Booking, Store, ...)
│   └── <Feature>/
│       ├── <Feature>Service.cs
│       ├── DTOs/
│       └── Validators/
├── Controllers/         # Thin controllers: Admin/, Store/, SuperAdmin/
├── Core/
│   ├── Entities/        # EF Core entities
│   ├── Enums/
│   └── Interfaces/
├── Infrastructure/
│   ├── Data/            # AppDbContext, Global Query Filters, seeds
│   ├── Middleware/       # Tenant resolution, global exception handling
│   └── Services/        # Token, Cloudinary, AuditLog, etc.
└── Migrations/

Launchly.Tests/
└── TenantIsolationTests.cs   # The tests that matter most
```

## Running locally

```bash
# 1. Copy the example config and fill in your local Postgres password
cp Launchly.API/appsettings.Development.json.example Launchly.API/appsettings.Development.json

# 2. Apply migrations
cd Launchly.API
dotnet ef database update

# 3. Run
dotnet run
```

```bash
# Run the test suite
cd Launchly.Tests
dotnet test
```

## What's not in scope

This is a portfolio project, not a production deployment. It does not include load testing, a CI/CD pipeline, external monitoring, or a third-party penetration test — all of which a real production SaaS launch would need before handling live customer data.
