# 20. Session Notes: Panel Q&A on Config, CORS, Rate Limiting, Triggers and Migrations

Short answers worked out while preparing for the panel. Each points to the code that proves it.

## 1. Areas the panel is likely to dig into

| Area | Read first |
|---|---|
| Architecture (layered monolith, Clean Architecture) | [01 §1.2](01-architecture.md), [19 §1-2](19-design-decisions.md) |
| One request end to end | [18 §2](18-ui-to-database-layers.md), [17 §5-6](17-request-data-path.md) |
| Concurrency, idempotency, retry | [04 §4.1-4.4](04-concurrency-idempotency-retry.md) |
| Database design and read model | [03 §3.2-3.5](03-database-er.md), [11](11-denormalised-tables.md), [06 §6.4](06-distributed-database.md) |
| Patterns and SOLID | [01 §1.4-1.5](01-architecture.md), [12 §5-6](12-design-guide.md), [19 §12](19-design-decisions.md) |
| Live code changes | [08](08-change-playbook.md), [15](15-ui-layout-changes.md), [12 §9](12-design-guide.md) |
| Azure service choices | [19 §4-6, §14](19-design-decisions.md), [14 §7-8](14-deployment-walkthrough.md) |
| CI/CD, config, secrets, RBAC | [14 §2-6](14-deployment-walkthrough.md), [16 §2, §8, §10](16-azure-cicd-infra-guide.md) |

Not yet covered in other docs:

- **Service Bus vs Storage Queues vs Event Hubs.**
  - Storage Queues: simple and cheap, with few features.
  - Service Bus: business messages, with ordering, sessions, dead-lettering, transactions and topics. Use it for order events, with an outbox table.
  - Event Hubs: very high volume streams such as telemetry, read by many consumers.
- **RBAC vs ABAC.**
  - RBAC grants access by role, for example "Key Vault Secrets User".
  - ABAC adds conditions on attributes, for example "only blobs tagged `project=ipl`".
  - The app already applies an attribute rule: every cart and order query filters by the current customer.

## 2. CORS and rate limiting: where they live

All in [CompositionRoot.cs](../backend/src/IplStore.Api/Composition/CompositionRoot.cs). There's no custom middleware class; the code uses ASP.NET Core's built-in middleware.

- **Settings classes:** `CorsSettings` and `RateLimitingSettings` in [ApiSettings.cs](../backend/src/IplStore.Api/Composition/ApiSettings.cs), bound and validated at startup.
- **Registration:** `AddCors()`, `AddRateLimiter()` and `ConfigureRateLimiter`.
- **Rate-limit rules:**
  - A fixed window, counted separately per `X-Customer-Id` header, or per IP if the header is missing.
  - Over the limit, the API returns `429`.
  - The defaults are 100 requests per 10 seconds.
- **Pipeline order in `UseIplStorePipeline`:**
  1. `UseExceptionHandler`
  2. `UseCors`
  3. `UseRateLimiter`
  4. `MapControllers`

  CORS runs first so that preflight requests don't use up a caller's limit.
- **Gaps:**
  - Counters are kept per replica, in memory, so 3 replicas allow 3× the limit.
  - The counter key is a header the client controls.
  - A shared limit would need API Management or Front Door.

## 3. Local vs Azure configuration

Settings load in this order, and the last one wins:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables

In an environment variable name, `__` separates sections, and a number is an array index. `Cors__AllowedOrigins__0` is the same setting as `Cors:AllowedOrigins[0]`.

| | Local | Azure |
|---|---|---|
| Environment name | Development | Staging (`ASPNETCORE_ENVIRONMENT` from Terraform) |
| CORS origin | `http://localhost:5173` from `appsettings.json` | Static Web App URL from `Cors__AllowedOrigins__0`, set in [compute.tf:44](../infra/terraform/compute.tf) |
| Rate limit | 100 per 10 s | Same, because no env var overrides it |

- **Locally, CORS is not used.** Vite forwards `/api` to the API ([vite.config.ts](../frontend/vite.config.ts)), so the browser sees one origin.
- **In Azure, CORS is used.** `deploy.ps1` builds the SPA with `VITE_API_BASE_URL` pointing at the Container App, so the page and the API are on different origins.
- **`appsettings.Development.json`** only holds overrides (logging, seed data, Swagger). Keys it doesn't mention come from `appsettings.json`.
- **To fix:** the comment in `vite.config.ts` says cloud traffic goes through "Front Door / Static Web Apps". In fact, the browser calls the Container App directly.

## 4. The read model: triggers, not a materialised view

- **V001** creates the write tables only. It has no triggers.
- **V002** creates:
  - `product_catalog`, an ordinary table
  - `project_product(id)`, which joins products, franchises and categories and inserts or updates one catalogue row
  - triggers on `products`, `franchises` and `product_categories`

It isn't a `MATERIALIZED VIEW`. A materialised view needs `REFRESH` and rebuilds everything. The triggers update one row, inside the same transaction as the write.

**The projection runs on the write path.** For example:

1. `ProductRepository.TryReserveStockAsync` sends `UPDATE products ...`.
2. PostgreSQL fires `products_project`.
3. The trigger calls `project_product(NEW.id)`, which updates the catalogue row.
4. Both changes commit, or both roll back.

Reads only `SELECT` from `product_catalog`.

**`StoreDbContext` doesn't know about the trigger.** EF Core maps classes to existing tables (`CatalogItem` maps to `product_catalog`) and sends normal SQL. PostgreSQL runs the trigger itself.

**Pros in production:**
- The read table is always current on the primary.
- It can't be bypassed: every writer fires the trigger.
- Reads are one indexed `SELECT` with no joins.
- No queue or worker is needed.
- It works with read replicas.

**Cons in production:**
- Every stock change writes two rows.
- Hot products hold locks longer.
- The logic is hidden from C# developers.
- A schema change must also update the function.
- Large updates such as a franchise rename run in one long transaction.
- Citus limits triggers on distributed tables.
- It can't update an external search engine or cache.
- It ties the code to PostgreSQL.

**When to change approach:** move to an outbox table and a background projector, published through Service Bus or CDC. Do this when adding an external search engine, when stock updates get hot, or when sharding.

## 5. Migrations

- Scripts are embedded in the Infrastructure DLL ([IplStore.Infrastructure.csproj](../backend/src/IplStore.Infrastructure/IplStore.Infrastructure.csproj)) and applied by [SqlScriptDatabaseMigrator.cs](../backend/src/IplStore.Infrastructure/Persistence/Migrations/SqlScriptDatabaseMigrator.cs):
  - one instance at a time, using an advisory lock
  - in name order
  - each script in its own transaction
  - each recorded in `schema_migrations` with a SHA-256 checksum
- Each script runs **once per database, ever**, not once per deployment. In Azure, the `caj-iplstore-dev-migrate` job runs `--migrate-only` before the new revision gets traffic.
- **Editing an applied script stops the deployment.** The checksum no longer matches, the migrator throws, `deploy.ps1` stops, and the API isn't updated. A brand-new database would run the edited file, so environments would differ. That's why applied scripts are immutable.
- **Scripts are immutable, but the schema isn't.** Add `V005__...sql` with any SQL, for example:
  - `ALTER TABLE`
  - `CREATE OR REPLACE FUNCTION`
  - `DROP TRIGGER` / `CREATE TRIGGER`
  - a data fix or backfill, such as `SELECT project_product(id) FROM products;`

  Flyway, Liquibase and EF migrations all work this way.
- **Cautions on a live database:**
  - Set `SET lock_timeout`.
  - Keep each change compatible with the version still running: expand first, contract in a later release.
  - Batch large backfills.
  - There's no automatic undo: write a new script to reverse a change, and rely on point-in-time restore.
- **Known limitation:** each script runs in a transaction, so `CREATE INDEX CONCURRENTLY` can't be used yet.
