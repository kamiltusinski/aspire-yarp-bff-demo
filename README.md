# Aspire BFF Demo — YARP + React SPA

A minimal end-to-end demo of the **Backend-For-Frontend (BFF) / API-Gateway** pattern using:

| Component | Technology |
|-----------|-----------|
| BFF Gateway | ASP.NET Core 8 + [YARP reverse proxy](https://microsoft.github.io/reverse-proxy/) |
| Catalog API | ASP.NET Core 8 Minimal API |
| Orders API | ASP.NET Core 8 Minimal API |
| Frontend SPA | [Vite](https://vitejs.dev/) + React 18 + TypeScript |
| Orchestration | [.NET Aspire 9](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) |
| Observability | OpenTelemetry (traces, metrics, logs) via OTLP |

---

## Architecture

```
Browser
  │
  ▼  port 5200 (or Aspire-assigned)
BffGateway  ◄──── serves React SPA (wwwroot)
  │  ├─ /bff/*      BFF endpoints (login, logout, user, csrf)
  │  ├─ /api/catalog/**  ──► CatalogApi  (YARP proxy)
  │  └─ /api/orders/**   ──► OrdersApi   (YARP proxy)
  │
  ├── CatalogApi  (internal, port 5201)
  └── OrdersApi   (internal, port 5202)
```

The SPA is served from the **same origin** as the BFF, so no CORS complexity.  
YARP strips the `/api/catalog` or `/api/orders` prefix before forwarding.

---

## Prerequisites

| Tool | Minimum version |
|------|----------------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 (AppHost targets net9.0 — SDK 9+ recommended) |
| [Node.js](https://nodejs.org/) | 18 LTS |
| npm | 9+ |

---

## Running with .NET Aspire (recommended)

```bash
# 1. Build the React SPA once (also runs automatically via MSBuild target)
cd frontend
npm install
npm run build
cd ..

# 2. Start the Aspire AppHost — launches all services + Aspire Dashboard
dotnet run --project src/Aspire.AppHost

# OR just press F5 / Ctrl+F5 in Visual Studio / Rider
```

The Aspire Dashboard opens automatically and shows:
- Live resource status for all three services
- Structured logs, distributed traces and metrics
- Resource URLs and environment variables

### Service URLs (standalone / without Aspire)

You can also run each project independently:

```bash
dotnet run --project src/CatalogApi  # http://localhost:5201
dotnet run --project src/OrdersApi   # http://localhost:5202
dotnet run --project src/BffGateway  # http://localhost:5200
```

When running without Aspire, BffGateway reads service URLs from `appsettings.json`:

```json
"Services": {
  "CatalogApi": { "BaseUrl": "http://localhost:5201" },
  "OrdersApi":  { "BaseUrl": "http://localhost:5202" }
}
```

---

## Fake / Dev Authentication

This demo uses **cookie-based authentication with a fake sign-in endpoint** — no external
Identity Provider (IdP) is required.

| Endpoint | Description |
|----------|-------------|
| `GET /bff/login?username=<name>` | Signs in a demo user with the given name and sets a session cookie |
| `POST /bff/logout` | Clears the session cookie (requires `X-CSRF-TOKEN` header) |
| `GET /bff/user` | Returns current user info (401 if not signed in) |
| `GET /bff/csrf` | Returns an antiforgery token for use in POST requests |

### How it works

1. Browser visits `/bff/login?username=alice`.
2. BffGateway creates a `ClaimsPrincipal` with fake claims (name, email, role) and calls
   `HttpContext.SignInAsync` — no password or IdP involved.
3. ASP.NET Core sets an encrypted **session cookie** (`bff_session`).
4. Subsequent requests from the SPA carry the cookie automatically.
5. YARP forwards proxied requests to the internal APIs.  
   The APIs are **unauthenticated** in this demo (the gateway is the auth boundary).

> **Production note:** Replace the fake handler with a real OIDC flow
> (e.g. `Microsoft.Identity.Web`, `OpenIddict`, or `Duende.BFF`).

---

## Building the React SPA

The MSBuild target in `BffGateway.csproj` automatically builds the SPA and copies
`frontend/dist/` to `src/BffGateway/wwwroot/` during a `dotnet build`.

Manual workflow:

```bash
cd frontend
npm install      # once
npm run build    # outputs to frontend/dist/
```

For development with hot-reload (proxies API calls to BffGateway at `:5200`):

```bash
cd frontend
npm run dev      # Vite dev server on http://localhost:5173
```

---

## OpenTelemetry / Tracing

When running under Aspire the dashboard receives OTLP data automatically.

To send traces/metrics to an external collector, set the environment variable:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-collector:4317
```

All three services check for this variable at startup and enable the OTLP exporter
only when it is set.

---

## Project structure

```
aspire-yarp-bff-demo/
├── AspireYarpBffDemo.slnx          # Solution file
├── frontend/                       # React + Vite SPA
│   ├── src/
│   │   ├── App.tsx
│   │   └── main.tsx
│   ├── index.html
│   ├── package.json
│   └── vite.config.ts
└── src/
    ├── Aspire.AppHost/             # .NET Aspire orchestration host
    ├── BffGateway/                 # ASP.NET Core BFF + YARP gateway
    ├── CatalogApi/                 # Internal catalog service
    └── OrdersApi/                  # Internal orders service
```
