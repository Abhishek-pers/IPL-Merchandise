# 19. Design Decisions and the Alternatives We Rejected

Every major choice in this project, why it fits **this** store (one checkout workflow, a small team, a demo-sized budget, correctness first), the alternatives, when each alternative would be the better choice, and what would make us change our mind.

The short form of several of these is in the ADRs: [docs/adr/](adr/). This document is the long form, side by side.

**The constraints that drive almost every decision below**

1. **Correctness of money and stock first:** no overselling, no duplicate orders, the price shown is the price charged.
2. **One workflow that must be atomic:** checkout changes cart, stock and orders together.
3. **Small team, short timeline:** minimise things to operate.
4. **Change requests will arrive live:** changes must land in one place and be testable fast.
5. **Low cost in dev, a clear path to production.**

**Contents**

| # | Area | Decision |
|---|---|---|
| 1 | Architecture style | Layered monolith |
| 2 | Code structure | Clean Architecture (ports and adapters) |
| 3 | Backend framework | ASP.NET Core 8 |
| 4 | Hosting the API | Azure Container Apps |
| 5 | Hosting the UI | Azure Static Web Apps |
| 6 | Database | PostgreSQL Flexible Server |
| 7 | Schema management | SQL-first migrations, own migrator |
| 8 | Data access | EF Core + targeted SQL |
| 9 | Reads vs writes | CQRS-lite with a trigger-maintained read model |
| 10 | Concurrency | Row lock per cart + atomic stock update |
| 11 | Duplicate requests | Idempotency keys |
| 12 | Pricing | Strategy with a selector + Template Method |
| 13 | Database migrations at deploy | Separate Container Apps Job, before the rollout |
| 14 | Infrastructure as code | Terraform |
| 15 | CI/CD | GitHub Actions + OIDC, one deploy script |
| 16 | Building the image | .NET SDK container build |
| 17 | Secrets | Key Vault references + managed identity |
| 18 | Releases | Revisions (blue/green-ready) |
| 19 | Frontend stack | React + TypeScript + Vite |
| 20 | Frontend data access | Small fetch wrapper, no data library |
| 21 | Integration tests | Testcontainers with real PostgreSQL |
| 22 | Payment and identity | Ports with demo adapters |
| 23 | Region | Central India |

---

## 1. Architecture style: layered monolith

**Decision.** One deployable API, layered inside (Domain, Application, Infrastructure, Api), grouped by feature folders (Catalog, Carts, Orders).

**Why it fits.** Checkout must reserve stock, create the order and empty the cart **in one ACID transaction**. In one process with one database that is a local transaction: simple, fast and provably correct. A small team can build, test, deploy and debug one thing.

| Alternative | Better when | Why not here |
|---|---|---|
| **Microservices** (cart, catalogue, orders, payments) | many teams need independent releases; parts scale very differently; different tech per part | checkout would become a distributed transaction (sagas, outbox, compensations, eventual consistency) to solve a problem we don't have; far more to operate |
| **Modular monolith** (one project per module, enforced boundaries, schema per module) | the codebase or team grows and boundaries must be enforced | a good next step, but today `CheckoutService` legitimately uses cart, catalogue and order data in one transaction; strict modules add ceremony now |
| **Serverless functions** | spiky, event-driven, independent operations | cold starts, a transaction spanning several functions, harder local testing |

**Trade-off accepted.** Everything scales together; module boundaries are a convention (feature folders), not enforced.

**What would change our mind.** Several teams, very different scaling (e.g. catalogue at 100× checkout), or a part needing its own release cadence → modular monolith first, then extract the part with the clearest boundary (e.g. catalogue search) behind events.

---

## 2. Code structure: Clean Architecture (ports and adapters)

**Decision.** Domain has no dependencies; Application defines use cases and **interfaces** (ports); Infrastructure implements them (EF Core, SQL, payment); Api is HTTP + composition root. Project references enforce the direction.

**Why it fits.** Change requests are expected live. Business rules live in one place and are unit-tested in milliseconds with fakes; technology details can change without touching use cases.

| Alternative | Better when | Why not here |
|---|---|---|
| **Classic N-tier / controllers using `DbContext` directly** | small CRUD apps | rules would spread across controllers; locking and idempotency logic would be hard to test without a database |
| **Vertical slices (one folder per request, often MediatR)** | many independent endpoints with little shared logic | checkout, cart and payment share rules and transactions; slices would duplicate them or reintroduce shared services |
| **Transaction script** | very simple logic | invariants (order totals, cart limits) need a home that every path goes through |

**Trade-off accepted.** More files and interfaces. Mitigated by feature folders and one type per file.

---

## 3. Backend framework: ASP.NET Core 8

**Decision.** .NET 8 (LTS), C# 12, EF Core 8, Npgsql.

**Why it fits.** Long-term support, high performance, first-class DI, options validation, problem details, rate limiting and health checks built in, excellent PostgreSQL driver, and the SDK can build container images without Docker.

| Alternative | Better when | Why not here |
|---|---|---|
| Node.js (Express / NestJS) | the team is JavaScript-only; very I/O-light services | weaker typing at the data layer; we wanted strong typing end to end in the backend |
| Java (Spring Boot) | an existing JVM estate | heavier start-up and footprint for a single small API |

---

## 4. Hosting the API: Azure Container Apps

**Decision.** API on Container Apps (Consumption profile, `Multiple` revision mode); migrations as a Container Apps **Job** in the same environment.

**Why it fits.** We get containers, HTTP autoscaling (KEDA), health probes, **revisions with traffic splitting**, one-off **jobs** and managed identity, **without running a Kubernetes cluster**. One small API does not justify cluster operations.

| | **Container Apps** (chosen) | **AKS** | **App Service** |
|---|---|---|---|
| What you operate | the app | the cluster: node pools, upgrades, ingress controller, add-ons | the app + an App Service plan |
| Deploy unit | container image | container image + Kubernetes manifests / Helm | code or container |
| Zero-downtime release | revisions, % traffic split, instant rollback | rolling updates; canary needs extra tooling (Flagger, service mesh) | deployment slots + swap (slot count depends on tier) |
| One-off jobs (migrations) | **Container Apps Jobs** | Kubernetes Jobs | WebJobs / separate app |
| Scale | KEDA rules, can scale to zero | anything, you configure it | plan-level autoscale, no scale to zero |
| Cost at our size | pay per use (dev runs 1 replica) | at least the node VMs, always on | the plan, always on |
| Best when | a few containerised services, small team | many services, custom networking, service mesh, operators, GPUs, full control | traditional web apps, no containers needed, rich App Service features |

**Why not AKS:** a cluster is a platform to run and upgrade; its strengths (many services, mesh, custom controllers) are things we don't use. **Why not App Service:** a strong option and nearly equivalent for one API; we preferred revisions with traffic weights, Jobs for migrations in the same environment, KEDA scaling and a container-native model that matches how we test.

**Trade-off accepted.** Less low-level control than AKS (no sidecars of our own, limited networking customisation in Consumption).

**What would change our mind.** Many services with complex service-to-service traffic, or needs such as custom operators → AKS.

---

## 5. Hosting the UI: Azure Static Web Apps

**Decision.** The Vite build output (`dist/`) is uploaded to Static Web Apps.

**Why it fits.** A SPA is static files. SWA gives a global CDN, free TLS, a free tier in dev, and no server to patch.

| Alternative | Better when | Why not here |
|---|---|---|
| Blob Storage static website + Front Door / CDN | you already run Front Door | more pieces to wire for the same result (this is part of the production target, see [16 §9](16-azure-cicd-infra-guide.md#9-target-production-architecture)) |
| App Service | server-side rendering | no SSR needed |
| nginx container (we have a Dockerfile for it) | everything must run as containers | a server to run for static files |

**Trade-off accepted.** The browser calls the API on a different domain, so CORS is needed. Front Door with `/api` routing removes that in production.

---

## 6. Database: PostgreSQL Flexible Server

**Decision.** PostgreSQL 16 on Azure Database for PostgreSQL Flexible Server (B1ms in dev; General Purpose, zone-redundant HA, read replica and geo backups in the prod tfvars).

**Why it fits.** The workload is relational and transactional. We use PostgreSQL features directly: `INSERT ... ON CONFLICT`, `SELECT ... FOR UPDATE`, conditional `UPDATE` for stock, triggers for the read model, **`pg_trgm`** trigram indexes for search, advisory locks for the migrator, `xmin` as a free concurrency token. Local development runs the same engine and version.

| Alternative | Better when | Why not here |
|---|---|---|
| **Azure SQL Database** | a SQL Server estate and tooling; features like temporal tables, Hyperscale | equally capable for transactions; we chose PostgreSQL for trigram search, `ON CONFLICT`, open-source local parity and no licence considerations. A fair alternative, not a wrong one |
| **Cosmos DB** | global distribution, huge write scale, document data, single-partition access patterns | multi-document ACID only within one logical partition; checkout spans cart, stock and orders; relational queries and constraints would move into code |
| **MySQL Flexible Server** | MySQL estate | weaker fit for our use of triggers, partial/trigram indexes and `ON CONFLICT`-style SQL |

**Trade-off accepted.** Scaling writes beyond one primary needs work (replicas help reads only). The path is documented in [docs/06](06-distributed-database.md) (replicas, then partitioning or Citus).

---

## 7. Schema management: SQL-first migrations with our own migrator

**Decision.** Versioned `.sql` files (`V001...`) are the source of truth, embedded in the API and applied by a small migrator: in order, one transaction each, SHA-256 checksums, PostgreSQL advisory lock. EF Core maps to existing tables and never generates DDL ([ADR-0002](adr/0002-postgresql-sql-first-migrations.md)).

**Why it fits.** The schema uses features EF migrations model poorly (triggers, trigram and partial indexes, CHECK constraints). Reviewable SQL, safe concurrent start-up, and one image that can run `--migrate-only`.

| Alternative | Better when | Why not here |
|---|---|---|
| EF Core migrations | schema is plain tables/indexes | we would hand-write raw SQL inside migrations anyway, and get C# model snapshots to keep in sync |
| DbUp / Flyway | you want a maintained tool | a fine choice; our migrator is ~150 lines with no extra dependency and does exactly what we need |

**Trade-off accepted.** Model/schema drift is caught by integration tests, not the compiler.

---

## 8. Data access: EF Core with targeted SQL

**Decision.** EF Core for mapping, change tracking and LINQ queries; a few precise SQL statements where semantics matter (`ON CONFLICT`, `FOR UPDATE`, single-statement stock update via `ExecuteUpdate`).

| Alternative | Better when | Why not here |
|---|---|---|
| Dapper only | hot read paths, SQL-centric teams | we would hand-write change tracking and mapping for aggregates (cart lines, order items) |
| EF Core only (no raw SQL) | simple CRUD | locking and atomic conditional updates need exact SQL semantics |

---

## 9. Reads vs writes: CQRS-lite with a trigger-maintained read model

**Decision.** Normalised write tables; a flat `product_catalog` table for browsing and search, refreshed by triggers **in the same transaction**; repositories for writes and query classes for reads; catalogue reads through a read-only, replica-capable context ([ADR-0003](adr/0003-trigger-maintained-read-model.md)).

**Why it fits.** Browsing is most of the traffic and needs product + franchise + category per row. Pre-joining once on write is cheaper than joining on every read, and same-transaction triggers mean no stale data on the primary.

| Alternative | Better when | Why not here |
|---|---|---|
| Plain joins on every read | low traffic | the hottest path would do a 3-way join plus search every time |
| SQL view | readability only | still joins every time |
| Materialised view | data can be minutes old | stale until refreshed; stock shown would lag |
| Projection maintained in application code | the database must stay logic-free | every writer must remember to update it; a missed path silently corrupts the read model |
| Change data capture → search engine (e.g. Azure AI Search) | full-text relevance, facets, very large catalogues | more infrastructure, eventual consistency; it is the documented next step |
| Full CQRS (separate read database, events) | very different read/write scale | eventual consistency and messaging for no current benefit |

**Trade-off accepted.** Logic lives in the database (documented, covered by integration tests); every product write does a little extra work.

---

## 10. Concurrency: row lock per cart and atomic stock update

**Decision.** READ COMMITTED; every cart or checkout transaction locks **that customer's cart row** (`SELECT ... FOR UPDATE`); stock is reserved with one conditional `UPDATE ... WHERE stock >= q`, rows touched in ascending product id; constraints as backstops; whole-transaction retry on transient errors ([ADR-0004](adr/0004-concurrency-strategy.md)).

| Alternative | Better when | Why not here |
|---|---|---|
| Optimistic concurrency everywhere (version checks, retry on conflict) | low contention | a shopper double-clicking or retrying would see conflicts; we would still need atomic stock |
| SERIALIZABLE isolation | complex invariants across many rows | more aborts and retries for invariants we can guarantee with targeted locks |
| Queue / single writer per product (or Redis counter) | flash sales on one product | extra infrastructure; documented for hot-product drops |
| Read stock, check in code, then write | never for stock | race condition: two checkouts both see enough stock → oversell |

**Trade-off accepted.** Requests for the same customer, or the same product row, are serialised (by design).

---

## 11. Duplicate requests: idempotency keys

**Decision.** `POST /orders` requires an `Idempotency-Key`; `(customer_id, idempotency_key)` is UNIQUE; the lookup runs after the cart lock; a repeat returns the original order (200 + `Idempotent-Replayed`). Add-to-cart uses an `idempotency_keys` table ([ADR-0005](adr/0005-idempotent-checkout.md)).

| Alternative | Better when | Why not here |
|---|---|---|
| Disable the button / client-side dedupe only | never sufficient alone | does not survive network retries or lost responses |
| Natural uniqueness (e.g. one open order per cart) | strict one-at-a-time business rules | does not cover "same request sent twice" and blocks legitimate repeat purchases |
| Exactly-once via messaging | event-driven systems | no broker in this design |

**Why it matters beyond retries.** Because checkout is idempotent, the **server** can also retry a transaction after an ambiguous commit failure without risk.

---

## 12. Pricing: Strategy with a selector, Template Method inside

**Decision.** Cart and checkout depend on one `IPricingPolicy` (`PricingPolicySelector`), which picks the highest-priority registered `IPricingStrategy` that applies. `StandardPricingPolicy` is the only strategy today; GST and shipping are overridable steps.

| Alternative | Better when | Why not here |
|---|---|---|
| `if/else` pricing in each service | one rule forever | cart and checkout could disagree; every new rule edits existing code |
| Register several `IPricingPolicy` and cast at call sites | never | couples callers to classes; cart and checkout could choose differently |
| Keyed services + resolver | the choice comes from data (a contract's price list) | for rules about the basket, the strategy deciding `AppliesTo` is simpler |
| Rules engine | business users edit rules at runtime | heavy for one rule set |
| Composed step strategies (`ITaxPolicy`, `IDiscountRule`, ...) | several rules of one kind combine (stacked offers) | not needed yet; documented as the next step |

**Deliberately not built:** discounts, promo codes, campaigns. The brief does not ask for them; the design shows where they would go.

---

## 13. Database migrations at deploy: a separate job, before the rollout

**Decision.** The pipeline runs the Container Apps Job (`--migrate-only`) and waits for success **before** creating the new API revision. In Azure the API itself does not migrate on start-up.

| Alternative | Better when | Why not here |
|---|---|---|
| Migrate on API start-up | single instance, local dev (we do this locally) | several replicas starting together (the advisory lock handles it, but a failed migration would crash-loop the API) |
| A migration step directly in the pipeline runner | the runner can reach the database | the database is reachable from Azure, not from GitHub's runners; a job runs inside the environment with the app's identity |

**Rule that makes it safe.** Migrations are additive (expand/contract), so the old revision keeps working on the new schema during the switch.

---

## 14. Infrastructure as code: Terraform

**Decision.** Terraform with the `azurerm` provider, one root module, one file per concern, remote state in Azure Storage (Entra auth, versioning, blob-lease locking), environment differences only in tfvars.

| | **Terraform** (chosen) | **Bicep** | **ARM JSON** | **Pulumi** | **Portal / CLI scripts** |
|---|---|---|---|---|---|
| State | explicit state file (remote, locked) | none (Azure is the state) | none | state (managed or self-hosted) | none |
| Plan / preview | `terraform plan` | `what-if` | `what-if` | `preview` | no |
| Scope | any provider (Azure, GitHub, random, ...) | Azure only | Azure only | any | Azure |
| New Azure features | provider release lag | day 0 | day 0 | provider lag | day 0 |
| Language | HCL | Bicep DSL | JSON | C#, TS, Python | — |

**Why Terraform here:** a mature plan/apply workflow, explicit state that can be reviewed and locked, one tool if we later manage GitHub settings or other providers, and broad team familiarity. **Bicep is an equally good Azure-only choice**: no state to manage and same-day support for new resources.

**Trade-off accepted.** We manage state (bootstrap script, locking, access control) and occasionally wait for provider support.

---

## 15. CI/CD: GitHub Actions with OIDC, one deploy script

**Decision.** `ci.yml` on every PR and feature branch; `cd.yml` on push to `main` runs CI as a gate, signs in to Azure with **OIDC federation** (no stored secret) and runs `scripts/deploy.ps1`, the same script used for manual deploys.

| Alternative | Better when | Why not here |
|---|---|---|
| Azure DevOps Pipelines | an organisation standardised on Azure DevOps | the code is on GitHub; Actions keeps code, PRs and pipelines together |
| Service principal with a client secret in GitHub | legacy setups | a long-lived secret that can leak and must be rotated; OIDC tokens are short-lived and bound to repo + environment |
| Logic only in YAML (no script) | simple deploys | the script lets the same deploy run from a laptop and the pipeline, so they cannot drift |

---

## 16. Building the image: .NET SDK container build

**Decision.** `dotnet publish /t:PublishContainer` builds and pushes the API image to ACR with a short-lived token. CI still runs `docker build` on the Dockerfiles as a check.

| Alternative | Better when | Why not here |
|---|---|---|
| `docker build` + `docker push` | non-.NET images, custom OS packages | needs Docker on the build machine; not available on this laptop |
| ACR Tasks (build in Azure) | no build agent | **blocked on the free-trial subscription**; would be the choice otherwise |

---

## 17. Secrets: Key Vault references with a managed identity

**Decision.** The connection string is generated by Terraform, stored in Key Vault, and referenced by the Container App and the job by **versionless ID**, read with a user-assigned managed identity that has only *Key Vault Secrets User* (and *AcrPull* for images).

| Alternative | Better when | Why not here |
|---|---|---|
| Secret value directly in app settings | quick prototypes | visible to anyone with read access to the app; no central rotation or audit |
| GitHub secret passed at deploy | — | the secret would pass through the pipeline and logs risk |
| **Passwordless DB auth (Entra token)** | **always, in production** | not done yet; it is the top security gap ([14 §3](14-deployment-walkthrough.md)) |

---

## 18. Releases: revisions, blue/green-ready

**Decision.** `revision_mode = Multiple`. Each deploy creates a new revision, smoke-tests it, then deactivates old ones. Traffic weights can be split for canaries; rollback is a traffic switch.

| Alternative | Better when | Why not here |
|---|---|---|
| Single revision mode | simplest possible | no side-by-side old/new, no instant rollback |
| Canary 10% → 100% with metrics | production | needs telemetry and alerts first (a P1 gap); the mechanism is already in place |

---

## 19. Frontend stack: React + TypeScript + Vite

**Decision.** React 18 SPA in TypeScript, built and served in development by Vite, routing with React Router, state with React context and hooks.

**Why it fits.** A shop UI with a handful of pages; fast dev loop (hot reload), a dev proxy to the API (no CORS locally), small production bundles, strict typing of the API contract ([types.ts](../frontend/src/api/types.ts)).

| Alternative | Better when | Why not here |
|---|---|---|
| Next.js | SEO-critical pages, server-side rendering, edge rendering | needs a Node server or a specialised host; SSR is not required for this brief |
| Angular | large enterprise apps with an Angular estate | heavier framework for a few pages |
| Create React App | — | deprecated; slower builds than Vite |
| Redux / Zustand for state | large shared client state | the only shared state is the shopper and the cart badge |

---

## 20. Frontend data access: a small fetch wrapper

**Decision.** `httpClient.ts` (~150 lines) adds headers, maps problem details to `ApiError`, and retries **only** what is safe to repeat (GET/PUT/DELETE, or POST with an idempotency key), with full-jitter backoff and `Retry-After`. Pages call `storeApi.ts`, never `fetch`.

| Alternative | Better when | Why not here |
|---|---|---|
| axios | interceptors across many APIs | `fetch` covers what we need; no dependency |
| TanStack Query (React Query) | caching, background refetch, many shared queries | worth adding as the UI grows; the retry rule tied to idempotency keys would still live in our client |

---

## 21. Integration tests: Testcontainers with real PostgreSQL

**Decision.** Integration tests start a real PostgreSQL 16 in Docker and run the real migrations, then exercise the API, including concurrency tests.

| Alternative | Better when | Why not here |
|---|---|---|
| EF Core in-memory provider | never for this app | no SQL, no locks, no triggers, no constraints: it would pass tests that fail in production |
| SQLite | simple schemas | different locking, no `FOR UPDATE`, no trigram, different SQL |
| A shared test database | — | tests interfere with each other; drift from production version |

**Trade-off accepted.** Needs Docker (available on CI runners; not on this laptop, so integration tests run in CI).

---

## 22. Payment and identity: ports with demo adapters

**Decision.** `IPaymentGateway` with an in-memory `FakePaymentGateway`; `ICurrentCustomerAccessor` reading `X-Customer-Id`. Both are behind interfaces so the real implementation is a new adapter registered in DI.

**Why.** The brief is about the store's design, not integrating a payment provider or an identity platform. The ports show exactly where they plug in; the demo adapters are stated as gaps, not hidden.

**Production replacements.** Razorpay or Stripe adapter + persisted payment attempts + signed webhooks; Entra External ID / Azure AD B2C with JWT bearer authentication.

---

## 23. Region: Central India

**Decision.** All resources in Central India (the Static Web App control plane is in East Asia; content is served from the global CDN).

**Why.** IPL fans are mostly in India: lowest latency, and data stays in-country. South India is the natural pair region for disaster recovery.

---

## How to talk about decisions in the interview

For any choice, use the same four beats:

1. **The constraint:** "Checkout must be atomic" / "small team" / "correctness first".
2. **The choice and the one-line reason.**
3. **The best alternative and when it would win:** shows you know it is a trade-off, not a preference.
4. **What would change your mind:** shows the design can evolve.

> Example: "I chose a layered monolith because checkout has to reserve stock, create the order and clear the cart in one ACID transaction, and one deployable makes that a local transaction. Microservices would win with several teams or very different scaling, but they would turn checkout into a saga. If the team grew, I'd move to a modular monolith first and extract catalogue search behind events."
