# IPL Franchise Store

This is an e-commerce site for official IPL franchise merchandise: jerseys, caps, flags, autographed photos and accessories.
It is built as a senior-level reference solution: Clean Architecture, SOLID, safe under concurrent load, and fully tested.

| Stack | |
|---|---|
| API | ASP.NET Core 8 Web API (controllers, ProblemDetails, rate limiting, health checks, Swagger) |
| ORM | EF Core 8 + Npgsql. A plain SQL migrator applies the versioned scripts |
| Database | PostgreSQL 16 (ACID). Normalised write model + **denormalised read model** |
| Client | React 18 + TypeScript + Vite (kept deliberately plain) |
| Tests | xUnit + FluentAssertions (unit tests), WebApplicationFactory + Testcontainers (integration tests), Vitest |
| Delivery | Docker, docker-compose, GitHub Actions (CI, app CD and infra CD as separate pipelines), Terraform on Azure |

---

## 1. Run it locally

**Prerequisites:** .NET 8 SDK, Node 18+ and Docker Desktop. Docker is optional if you already have PostgreSQL.

```powershell
# Option A - IDE-friendly (3 terminals)
./scripts/dev.ps1 db     # PostgreSQL 16 in Docker on :5432
./scripts/dev.ps1 api    # API on http://localhost:5080  (Swagger: /swagger). Migrates + seeds automatically
./scripts/dev.ps1 web    # React on http://localhost:5173 (proxies /api to :5080)

# Option B - everything in containers
docker compose --profile full up -d --build   # web http://localhost:3000, API http://localhost:5080/swagger

# Tests
./scripts/dev.ps1 test   # backend unit + integration (Docker) + frontend
# or double-click scripts\verify-local.cmd -> writes verify-log.txt
node scripts/demo-concurrency.mjs   # live proof of exactly-once checkout + no overselling (API running, no Docker needed)
```

**Using your own PostgreSQL instead of Docker:** edit `Database:ConnectionString` in
`backend/src/IplStore.Api/appsettings.json` (or set the env var `Database__ConnectionString`).
The API creates the schema and the 60 demo products on first start.

**Demo shoppers:** the header's *Shopping as* dropdown switches between two seeded customers.
Every request carries an `X-Customer-Id` header, which stands in for authentication (see the [roadmap](docs/07-deployment-roadmap.md)).

---

## 2. Requirements → where they live

| # | Requirement | UI | API | Core logic |
|---|---|---|---|---|
| 1 | Product list with prices | `pages/ProductListPage.tsx` | `GET /api/v1/products` | `CatalogService`, `CatalogQueries` |
| 2 | Product details | `pages/ProductDetailsPage.tsx` | `GET /api/v1/products/{id}` | `CatalogQueries.GetDetailsAsync` |
| 3 | Search by name, type, franchise | filters on the list page | `?search=&franchise=&category=&minPrice=&sort=` | `ICatalogFilter` pipeline |
| 4 | Cart | `pages/CartPage.tsx` | `GET/POST/PUT/DELETE /api/v1/cart[/items]` | `Cart` aggregate, `CartService` |
| 5 | Order history | `pages/OrderPages.tsx` | `POST/GET /api/v1/orders[/{id}]` | `CheckoutService`, `OrderQueries` |

## 3. Non-functional guarantees

| Guarantee | How | Proof |
|---|---|---|
| **No duplicates** | UNIQUE constraints (1 cart per customer, 1 line per product, 1 order per idempotency key) + `INSERT … ON CONFLICT` + cart row lock | `ConcurrencyTests.Parallel_first_requests_create_exactly_one_cart_and_one_line` |
| **No race conditions / lost updates** | `SELECT … FOR UPDATE` on the cart serialises one customer's writes. Stock uses an atomic conditional `UPDATE`. `xmin` optimistic token on products | `…never_oversell_limited_stock` |
| **No overselling** | `UPDATE products SET stock = stock - q WHERE id = @id AND stock >= q` + `CHECK (stock >= 0)` | same |
| **Exactly-once checkout** | `Idempotency-Key` header + UNIQUE `(customer_id, idempotency_key)` | `…same_idempotency_key_sent_concurrently_creates_exactly_one_order` |
| **Retry on unexpected errors** | The whole transaction is re-run with backoff on transient DB errors (`ResilienceOptions`). The client retries only idempotent calls | `EfUnitOfWork`, `httpClient.test.ts` |
| **Thread safety** | Stateless scoped services, immutable options snapshots, singletons are stateless or thread-safe | code review / [docs/04](docs/04-concurrency-idempotency-retry.md) |

## 4. Repository map

```
├── README.md                     ← you are here
├── docs/                         ← design: architecture, UML, ER, concurrency, API, distribution, deployment, change playbook, ADRs
├── database/
│   ├── migrations/V00x__*.sql    ← schema = source of truth (immutable, checksummed, embedded in the API)
│   └── seed/S00x__*.sql          ← demo data (idempotent, dev/test only)
├── backend/
│   ├── IplStore.sln · Directory.Build.props · Directory.Packages.props (central package versions) · Dockerfile
│   ├── src/
│   │   ├── IplStore.Domain/          entities + business rules. Zero dependencies
│   │   ├── IplStore.Application/     use cases, ports (interfaces), options, pricing strategy
│   │   ├── IplStore.Infrastructure/  EF Core/Npgsql adapters, read model, filters, migrator
│   │   └── IplStore.Api/             controllers, error mapping, composition root (DI)
│   └── tests/
│       ├── IplStore.UnitTests/       domain + use cases with in-memory fakes (no DB)
│       └── IplStore.IntegrationTests/ real HTTP + real PostgreSQL, parallel-request race tests
├── frontend/                     ← React + TS SPA, retrying API client, Vitest tests
├── infra/terraform/              ← Azure: Container Apps, PostgreSQL Flexible (HA + replica), Key Vault, ACR, SWA
├── .github/workflows/            ← ci.yml · cd-app.yml · cd-infra.yml (+ reusable deploy/apply)
├── scripts/                      ← dev.ps1 / dev.sh / verify-local.cmd
└── docker-compose.yml
```

## 5. Documentation

| Doc | What's inside |
|---|---|
| [01 Architecture](docs/01-architecture.md) | Layers, request flow, patterns used, SOLID mapping, DI |
| [02 UML](docs/02-uml-class-diagram.md) | Class diagrams (domain, ports and adapters), sequence diagrams |
| [03 Database & ER](docs/03-database-er.md) | ER diagram, normalised vs denormalised tables, constraints, indexes |
| [04 Concurrency, idempotency, retry](docs/04-concurrency-idempotency-retry.md) | Every race condition we handle, with failure scenarios |
| [05 API reference](docs/05-api-reference.md) | Endpoints, headers, status and error codes |
| [06 Distributed database](docs/06-distributed-database.md) | HA, read replicas, sharding with Citus, partitioning |
| [07 Deployment & roadmap](docs/07-deployment-roadmap.md) | Azure resources, CI/CD, code vs infra deployment, phased roadmap |
| [08 Change playbook](docs/08-change-playbook.md) | **How to make common changes quickly and cleanly (for the live review)** |
| [09 Review guide](docs/09-review-guide.md) | Walkthrough order, trade-offs, likely questions |
| [10 Design document](docs/10-design-document.md) | **Everything in one place: code flow, sequence diagrams, DB, trade-offs and a demo playbook** |
| [ADRs](docs/adr) | Why each key decision was made |

## 6. Tunable parameters (no code changes)

Every knob is a property on an **options object** bound from `appsettings.json` (or env vars such as `Pricing__TaxRate=0.12`).
All options are validated at start-up, so the app fails fast on bad config. Pricing, cart and catalogue options are read through `IOptionsMonitor`, so edits apply without a restart.

| Section | Examples |
|---|---|
| `Pricing` | `TaxRate`, `FlatShippingFee`, `FreeShippingThreshold`, `Currency` |
| `Cart` | `MaxQuantityPerLine`, `MaxDistinctLines`, `ValidateStockOnAdd` |
| `Checkout` | `RequireIdempotencyKey`, `MaxIdempotencyKeyLength` |
| `Catalog` / `Paging` | `MaxSearchTerms`, `MaxFilterValues`, `DefaultPageSize`, `MaxPageSize` |
| `Resilience` | `MaxRetryCount`, `MaxRetryDelayMilliseconds`, `AdditionalTransientErrorCodes` |
| `Database` | `ConnectionString`, `ReadReplicaConnectionString`, `ApplyMigrationsOnStartup`, `SeedDemoData` |
| `RateLimiting` / `Cors` | `PermitLimit`, `WindowSeconds`, `AllowedOrigins` |
