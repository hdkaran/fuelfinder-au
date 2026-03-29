# FuelFinder AU — Claude Code Context

## What this app is

FuelFinder AU is a mobile-first crowdsourced petrol availability tracker built for Australia. During fuel shortage crises — when panic buying empties stations unpredictably — drivers need to know _before they drive_ which nearby stations have fuel, what types are available (Diesel, ULP, E10, Premium), and how long the queues are. The app lets any driver submit a quick report from the forecourt; those reports are aggregated in near real-time and surfaced to other drivers on a map and list view.

The design philosophy is: **launch fast, stay simple, stay reliable**. This means mobile-first (max-width 420px), no SignalR in MVP (RTK Query polling covers it), no Redux (RTK Query only), and Minimal API style on the backend. The stack was chosen to be deployable entirely on Azure from a single `az deployment group create` command and a GitHub Actions push.

## Tech stack

- **Frontend:** React 18 + TypeScript 5 (strict) + Vite + RTK Query + React Router v6 + CSS Modules
- **Backend:** .NET 8 Minimal API + EF Core 8 + Azure SQL + Azure Redis Cache
- **Infrastructure:** Azure App Service (B2 Linux, Docker container), Azure Static Web Apps (Free), Azure SQL (Basic 5 DTU), Azure Redis (C0 250 MB), Azure Key Vault (Standard), Azure CDN (Standard Microsoft), Azure App Insights + Log Analytics
- **CI/CD:** GitHub Actions — `ci.yml` (PR checks), `cd.yml` (deploy on push to master), `infra.yml` (Bicep deploy when infra/\*\* changes)
- **No SignalR in MVP** — use RTK Query polling + manual refresh button (see ADR 001)

## Hard rules — never break these

- TypeScript strict mode everywhere — `noImplicitAny`, `strictNullChecks`. Zero `any` types.
- **RTK Query for ALL server state** — no raw `fetch()`, no `axios`, no `useEffect` for data fetching.
- No Redux store or reducers outside of RTK Query's `createApi` slices.
- **Minimal API style on backend** — no `[ApiController]`, no controllers, endpoint handlers only.
- **Async all the way in .NET** — never use `.Result`, `.Wait()`, or blocking calls on async methods.
- **CSS Modules for component styles** — `*.module.css` files. No inline styles except for dynamic runtime values (e.g. `style={{ width: `${pct}%` }}`). No styled-components, no Tailwind.
- No SignalR in MVP — document the reasoning and point to ADR 001 if asked.
- All Azure secrets stored in Key Vault — never hardcoded in app settings or committed to git.

## Repository structure

```
fuelfinder-au/
├── src/
│   ├── FuelFinder.Api/                  ← .NET 8 Minimal API (Phase 1–2)
│   │   └── .gitkeep
│   ├── FuelFinder.Api.Tests/            ← xUnit integration tests (Phase 8)
│   │   └── .gitkeep
│   └── web/                             ← React 18 + Vite SPA (Phase 3–7)
│       └── .gitkeep
├── infra/
│   ├── master.bicep                       ← Orchestrates all Bicep modules
│   ├── parameters.json                  ← Deployment parameters
│   └── modules/
│       ├── appService.bicep             ← App Service Plan + App Service + ACR
│       ├── sql.bicep                    ← Azure SQL Server + Database
│       ├── redis.bicep                  ← Azure Redis Cache
│       ├── staticWebApp.bicep           ← Static Web Apps (frontend)
│       ├── keyVault.bicep               ← Key Vault + secrets + RBAC
│       ├── appInsights.bicep            ← App Insights + Log Analytics
│       └── cdn.bicep                    ← CDN Profile + Endpoint
├── .github/
│   └── workflows/
│       ├── ci.yml                       ← PR: lint, type-check, test, build
│       ├── cd.yml                       ← Push to master: build → migrate → deploy → smoke test
│       └── infra.yml                    ← Push to master (infra/**): Bicep deploy
├── docs/
│   └── adr/
│       └── 001-no-signalr-mvp.md        ← Architecture Decision Record
├── .gitignore
├── README.md
└── CLAUDE.md                            ← This file
```

## API endpoints

```
GET  /api/stations/nearby?lat=&lng=&radius=&fuelType=   → StationDto[]
GET  /api/stations/{id}                                  → StationDto
POST /api/reports                                        → 201 Created
GET  /api/reports/recent?stationId=                      → ReportDto[]
GET  /api/stats/summary                                  → StatsDto
GET  /health                                             → 200 OK (smoke test target)
```

## TypeScript types — source of truth

```typescript
type FuelType = "Diesel" | "ULP" | "E10" | "Premium";
type StationStatus = "available" | "low" | "out" | "unknown";
type ReportStatus = "available" | "low" | "out" | "queue";

interface StationDto {
  id: string;
  name: string;
  brand: string;
  address: string;
  suburb: string;
  state: string;
  latitude: number;
  longitude: number;
  distanceMetres: number;
  status: StationStatus;
  fuelAvailability: FuelAvailabilityDto[];
  reportCount: number;
  lastReportMinutesAgo: number | null;
}

interface FuelAvailabilityDto {
  fuelType: FuelType;
  available: boolean | null;
}

interface ReportPayload {
  stationId: string;
  status: ReportStatus;
  fuelTypes: { fuelType: FuelType; available: boolean }[];
  latitude: number;
  longitude: number;
}

interface StatsDto {
  totalReportsToday: number;
  stationsAffected: number;
  lastUpdated: string; // ISO 8601
}
```

## Design system

```
Font:            Outfit (Google Fonts) — weights 400, 600, 700, 800, 900
Amber (brand):   #f5a623
Dark:            #1a1a1a
Background:      #f5f4f0
Surface:         #ffffff
Green (available): color #22c55e / bg #f0fdf4 / text #15803d
Orange (low):    color #f97316 / bg #fff7ed / text #c2410c
Red (out):       color #ef4444 / bg #fef2f2 / text #b91c1c

Border radius:   buttons 16px | cards 18px | status pills 100px
Active state:    all buttons → transform: scale(0.97); transition: 0.1s

Layout:          Mobile-first. Max-width 420px centred on desktop.
                 Use CSS custom properties for all colour tokens (--color-amber, etc.)
```

## Infrastructure notes

- All Bicep resource names use `baseName` parameter (default: `fuelfinder`).
  - App Service: `fuelfinder-api`
  - ACR: `fuelfindercr` (alphanumeric, no hyphens)
  - SQL Server: `fuelfindersql`
  - Redis: `fuelfinder-redis`
  - Key Vault: `fuelfinder-kv`
  - App Insights: `fuelfinder-insights`
  - CDN: `fuelfinder-cdn`
- Key Vault uses RBAC (`enableRbacAuthorization: true`), not access policies.
- App Service uses `acrUseManagedIdentityCreds: true` — no ACR admin credentials needed.
- Key Vault references in App Service app settings use the `@Microsoft.KeyVault(VaultName=...;SecretName=...)` syntax — secrets are never stored in git.
- SQL Server firewall rule `AllowAzureServices` uses `0.0.0.0 → 0.0.0.0` (Azure magic range).

## GitHub Actions secrets required

| Secret                            | Purpose                                         |
| --------------------------------- | ----------------------------------------------- |
| `AZURE_CREDENTIALS`               | JSON from `az ad sp create-for-rbac --sdk-auth` |
| `AZURE_RESOURCE_GROUP`            | Azure resource group name                       |
| `ACR_NAME`                        | Container registry name (e.g. `fuelfindercr`)   |
| `DB_CONNECTION_STRING`            | SQL connection string for EF Core migrations    |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | SWA deployment token from Azure Portal          |
| `GOOGLE_MAPS_API_KEY`             | Google Maps JS API key                          |

## RTK Query polling intervals

```typescript
// Station list (nearby query) — 2 minute poll
useGetNearbyStationsQuery(params, { pollingInterval: 120_000 });

// Summary stats — 1 minute poll
useGetStatsSummaryQuery(undefined, { pollingInterval: 60_000 });

// Manual refresh — call refetch() from the Refresh button onClick
```

## How to resume a session

Paste this `CLAUDE.md` into a new Claude Code session and say:

> **"Start Phase N"** where N is the next phase to build.

### Phase roadmap

| Phase | Description                                                                                |
| ----- | ------------------------------------------------------------------------------------------ |
| **0** | Infrastructure scaffold (this session — complete)                                          |
| **1** | .NET 8 API scaffold: project setup, EF Core models, DbContext, migrations, health endpoint |
| **2** | API logic: nearby stations query, report submission, stats aggregation, Redis caching      |
| **3** | React scaffold: Vite project, RTK Query setup, Router, CSS custom properties, Outfit font  |
| **4** | Home screen + Nearby stations list UI                                                      |
| **5** | Report submission flow + Success screen                                                    |
| **6** | Live data wiring: connect frontend to real API, map view                                   |
| **7** | Polish: refresh button, loading states, error handling, PWA manifest                       |
| **8** | Tests: xUnit integration tests for API, Vitest for React components                        |
| **9** | Go live: custom domain, CDN, final smoke tests, production sign-off                        |
