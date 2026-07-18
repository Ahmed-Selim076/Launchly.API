# Launchly — Multi-Tenant SaaS Store Builder

A Shopify-like platform where businesses sign up, choose their store type, pick a design template, and instantly get a live storefront with their own subdomain and a fully featured admin dashboard.

**Live Demo:** https://launchly-frontend.vercel.app

---

## What It Does

Any business owner can:
1. Sign up on the platform and pick a store type
2. Choose from 3 design templates (Minimal, Bold, Editorial)
3. Customize their store (logo, colors, content)
4. Manage everything from a dedicated admin dashboard
5. Share their storefront link with customers

The platform owner gets a Super Admin panel to manage all tenants, monitor analytics, and view audit logs across the entire platform.

---

## Store Types

**E-commerce** — Product catalog, cart, checkout, order management, discount categories, customer accounts.

**Booking** — Service listings, calendar-based appointment scheduling with conflict prevention, booking confirmation flow, appointment history.

**Restaurant** — Menu categories and items, delivery/pickup order flow, food cart, order status tracking.

Each store type comes with 3 fully built templates (Minimal / Bold / Editorial) — 9 complete storefronts total.

---

## Tech Stack

**Frontend:** Angular 18 · TypeScript · RxJS · Angular Router · Chart.js · date-fns · Lucide Angular · ngx-color-picker

**Backend:** ASP.NET Core 9 · C# · Entity Framework Core · PostgreSQL · JWT Auth · BCrypt · Cloudinary · FluentValidation · AutoMapper · Rate Limiting · Health Checks

**Architecture:** Clean Architecture (Application / Domain / Infrastructure layers) · Multi-tenancy with shared database and TenantId isolation · Global query filters enforced at the data-access layer

**Hosting:** Vercel (Frontend) · Railway (Backend + PostgreSQL)

---

## Key Features

- **Multi-tenancy** — Complete data isolation between tenants enforced at the database level via EF Core global query filters
- **9 live storefronts** — 3 store types × 3 templates, each fully functional
- **Tenant Admin Dashboard** — Products/services/menu management, order tracking, customer management, analytics, audit log, onboarding checklist, store settings and theming
- **Super Admin Panel** — Platform-wide tenant management, analytics, audit log
- **Dynamic theming** — Tenants customize colors and logo; changes reflect instantly on their storefront
- **Image uploads** — Cloudinary integration for product and store images
- **Role-based access** — Super Admin / Tenant Admin / Customer with server-enforced permissions
- **Google OAuth** — Available alongside email/password registration
- **Audit logging** — Every significant action is logged with user, timestamp, and details

---

## Architecture Highlights

### Multi-Tenancy
Every tenant-scoped table includes a `TenantId` foreign key. A custom middleware resolves the tenant from the incoming request and populates an `ITenantContext` service. EF Core global query filters automatically scope all queries to the current tenant — no manual `.Where(x => x.TenantId == ...)` needed in services.

### Clean Architecture
```
Launchly.API/
├── Application/          ← Services, DTOs, Validators (business logic)
│   ├── Auth/
│   ├── Products/
│   ├── Booking/
│   ├── Restaurant/
│   ├── Orders/
│   ├── Analytics/
│   ├── AuditLog/
│   └── SuperAdmin/
├── Controllers/          ← Admin/, Store/, SuperAdmin/
├── Core/                 ← Interfaces, domain models
├── Infrastructure/       ← EF Core, middleware, implementations
└── Migrations/
```

---

## Local Setup

### Backend
```bash
git clone https://github.com/Ahmed-Selim076/Launchly.API
cd Launchly.API
```

Create `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=launchly;Username=postgres;Password=yourpassword"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "Launchly",
    "Audience": "LaunchlyClient"
  },
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  }
}
```

```bash
dotnet ef database update
dotnet run
```

### Frontend
```bash
git clone https://github.com/Ahmed-Selim076/Launchly.Frontend
cd Launchly.Frontend
npm install
ng serve
```

---

## Environment Variables (Railway)

```
ConnectionStrings__DefaultConnection=
Jwt__Secret=
Jwt__Issuer=Launchly
Jwt__Audience=LaunchlyClient
Cloudinary__CloudName=
Cloudinary__ApiKey=
Cloudinary__ApiSecret=
FRONTEND_URL=https://launchly-frontend.vercel.app
```
