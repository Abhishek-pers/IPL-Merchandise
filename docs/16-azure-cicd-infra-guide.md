# 16. Azure Infrastructure and CI/CD Guide (and the Road to Production)

This guide explains, with diagrams, **what is built in Azure, how it is created, how code reaches it, and what is used when a request arrives**, then lists every gap between today's dev environment and a production setup.

Everything here is taken from the code: [infra/terraform/](../infra/terraform/), [.github/workflows/](../.github/workflows/), [scripts/deploy.ps1](../scripts/deploy.ps1) and the API composition root. Related: [13 runbook](13-azure-deployment-runbook.md), [14 Q&A walkthrough](14-deployment-walkthrough.md).

**Contents**

1. [The environment at a glance](#1-the-environment-at-a-glance)
2. [Who can do what (identities and roles)](#2-who-can-do-what-identities-and-roles)
3. [How the infrastructure is created (Terraform)](#3-how-the-infrastructure-is-created-terraform)
4. [How code reaches Azure (CI/CD)](#4-how-code-reaches-azure-cicd)
5. [What is picked up from where when the API starts](#5-what-is-picked-up-from-where-when-the-api-starts)
6. [What happens when a request comes in](#6-what-happens-when-a-request-comes-in)
7. [Dev vs prod today](#7-dev-vs-prod-today)
8. [Gaps to production](#8-gaps-to-production)
9. [Target production architecture](#9-target-production-architecture)
10. [Short answers for the panel](#10-short-answers-for-the-panel)

---

## 1. The environment at a glance

One resource group per environment, Central India. All names follow `<type>-iplstore-<env>` from [main.tf](../infra/terraform/main.tf#L10-L19).

```mermaid
flowchart LR
    User([Shopper's browser])

    subgraph RG["rg-iplstore-dev  (Central India)"]
        direction LR
        SWA["Static Web App<br/>swa-iplstore-dev<br/>React SPA, global CDN"]

        subgraph CAE["Container Apps environment  cae-iplstore-dev"]
            API["Container App<br/>ca-iplstore-dev-api<br/>.NET 8 API, port 8080"]
            JOB["Container Apps Job<br/>caj-iplstore-dev-migrate<br/>--migrate-only"]
        end

        ACR[("Container Registry<br/>acriplstoredev")]
        KV[("Key Vault<br/>kv-iplstoredev")]
        ID{{"Managed identity<br/>id-iplstore-dev-api"}}
        PG[("PostgreSQL Flexible Server 16<br/>psql-iplstore-dev  B1ms<br/>database iplstore")]
        LOG[("Log Analytics<br/>log-iplstore-dev")]
        APPI["Application Insights<br/>appi-iplstore-dev"]
    end

    subgraph STATE["rg-iplstore-tfstate"]
        TFS[("Storage stiplstoretfstate<br/>container tfstate")]
    end

    User -->|"1. HTML / JS"| SWA
    User -->|"2. REST /api/v1  (HTTPS, CORS)"| API
    API -->|"SQL over TLS  (Npgsql)"| PG
    JOB -->|"migrations"| PG
    API -. "pull image as" .-> ID
    API -. "read secret as" .-> ID
    ID -->|AcrPull| ACR
    ID -->|"Key Vault Secrets User"| KV
    CAE -->|"container logs"| LOG
    APPI --> LOG
```

| Resource | Terraform | Why this service |
|---|---|---|
| Static Web App | [frontend.tf](../infra/terraform/frontend.tf) | Static files on a global CDN with free TLS; no server for a SPA. Free tier in dev, Standard in prod. |
| Container Apps environment | [compute.tf:23-36](../infra/terraform/compute.tf#L23-L36) | Serverless containers (Consumption profile), logs wired to Log Analytics. |
| Container App (API) | [compute.tf:50-149](../infra/terraform/compute.tf#L50-L149) | Revisions (deploy new, test, then switch), HTTP autoscaling, health probes. No cluster to run (vs AKS). |
| Container Apps Job (migrations) | [compute.tf:153-209](../infra/terraform/compute.tf#L153-L209) | Same image, runs once with `--migrate-only` before the new API revision gets traffic. |
| Container Registry | [compute.tf:8-21](../infra/terraform/compute.tf#L8-L21) | Private images, `admin_enabled = false`: pulled only by identity. |
| PostgreSQL Flexible Server | [database.tf](../infra/terraform/database.tf) | Same engine as local dev; triggers, trigram search and row locks the app relies on. |
| Key Vault | [secrets.tf](../infra/terraform/secrets.tf) | Holds the DB connection string; RBAC mode; apps read it by reference. |
| User-assigned managed identity | [main.tf:28-33](../infra/terraform/main.tf#L28-L33) | One identity for API and job: pulls images, reads secrets. No stored credentials. |
| Log Analytics + App Insights | [main.tf:36-54](../infra/terraform/main.tf#L36-L54) | Container logs today; App Insights provisioned for telemetry (see gaps). |
| Terraform state storage | [scripts/bootstrap-tfstate.ps1](../scripts/bootstrap-tfstate.ps1) | Created once, outside Terraform (Terraform can't create the place its own state lives). |

---

## 2. Who can do what (identities and roles)

Three kinds of principals touch Azure. None of them uses a stored Azure secret.

```mermaid
flowchart LR
    subgraph PEOPLE["People"]
        DEV["You (Entra user)<br/>az login"]
    end
    subgraph PIPELINE["GitHub Actions"]
        GH["Workflow on main<br/>environment: dev"]
        OIDC["Entra app<br/>github-iplstore-deploy<br/>(federated credential, no secret)"]
        GH -->|"OIDC token<br/>repo + environment"| OIDC
    end
    subgraph WORKLOAD["Workload"]
        MI["Managed identity<br/>id-iplstore-dev-api"]
    end

    RG[["rg-iplstore-dev<br/>(all app resources)"]]
    KV[("Key Vault")]
    ACR[("Container Registry")]
    PG[("PostgreSQL")]
    TFS[("Terraform state")]

    DEV -->|"runs terraform apply"| RG
    DEV -->|"Key Vault Secrets Officer<br/>(Terraform writes secrets)"| KV
    DEV -->|"Entra admin of the server"| PG
    DEV -->|"Storage Blob Data Contributor<br/>(Entra auth, shared keys off)"| TFS
    OIDC -->|"Contributor<br/>(this RG only)"| RG
    MI -->|"AcrPull"| ACR
    MI -->|"Key Vault Secrets User<br/>(read only)"| KV
    MI -.->|"connects with the ipladmin<br/>PASSWORD from Key Vault (gap)"| PG
```

| Principal | Used by | Roles | Where defined |
|---|---|---|---|
| You (Entra user) | `terraform apply`, portal | Owner/Contributor on the subscription; Key Vault Secrets Officer; PostgreSQL Entra admin | [secrets.tf:17-21](../infra/terraform/secrets.tf#L17-L21), [database.tf:54-62](../infra/terraform/database.tf#L54-L62), `local.auto.tfvars` (git-ignored) |
| `github-iplstore-deploy` | CD workflow | Contributor on `rg-iplstore-dev` only | Created once in Entra; federated credentials for `repo:Abhishek-pers/IPL-Merchandise:environment:dev` |
| `id-iplstore-dev-api` | API container and migration job | AcrPull, Key Vault Secrets User | [compute.tf:17-21](../infra/terraform/compute.tf#L17-L21), [secrets.tf:24-28](../infra/terraform/secrets.tf#L24-L28) |

**How the pipeline signs in without a secret (OIDC):** GitHub issues a short-lived token that says "this is repo X, environment dev". Entra trusts that exact subject (the federated credential) and returns an Azure token for the app registration. The workflow only stores three **non-secret** IDs as repository variables: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` ([cd.yml:53-58](../.github/workflows/cd.yml#L53-L58)).

---

## 3. How the infrastructure is created (Terraform)

Infrastructure and application are deployed by **different tools at different times**. Terraform owns the *shape* (resources, settings, secrets, identity, scaling); the pipeline owns the *image* that runs in it.

### 3.1 First-time creation, in order

```mermaid
sequenceDiagram
    autonumber
    actor You
    participant Boot as bootstrap-tfstate.ps1
    participant State as Storage stiplstoretfstate
    participant TF as terraform (local)
    participant Azure as Azure Resource Manager

    You->>Boot: run once
    Boot->>State: create RG, storage account, versioned "tfstate" container (Entra auth only)
    You->>TF: terraform init -backend-config=environments/dev.backend.hcl
    TF->>State: connect, lock state with a blob lease
    You->>TF: terraform plan / apply -var-file=environments/dev.tfvars
    TF->>Azure: RG, identity, Log Analytics, App Insights
    TF->>Azure: ACR + AcrPull for the identity
    TF->>Azure: PostgreSQL server, database, pg_trgm allow-list, firewall, Entra admins
    TF->>Azure: Key Vault, roles, secret db-connection-primary (built from server FQDN + random password)
    TF->>Azure: Static Web App
    TF->>Azure: Container Apps env, API app and migration job with a PLACEHOLDER image
    TF->>State: write new state (versioned blob)
    Note over You,Azure: The real API image arrives later from CI/CD (section 4)
```

### 3.2 Resource dependency graph (what Terraform builds first)

```mermaid
flowchart TB
    RG["azurerm_resource_group.main"]
    ID["azurerm_user_assigned_identity.api"]
    LOG["azurerm_log_analytics_workspace.main"]
    APPI["azurerm_application_insights.main"]
    ACR["azurerm_container_registry.main"]
    PULL["role: AcrPull -> identity"]
    PWD["random_password.postgres_admin"]
    PG["azurerm_postgresql_flexible_server.main"]
    DB["..._database.store  (iplstore)"]
    EXT["..._configuration  azure.extensions = PG_TRGM"]
    FW["..._firewall_rule  allow-azure-services"]
    ADM["..._active_directory_administrator  (people)"]
    KV["azurerm_key_vault.main"]
    OFF["role: KV Secrets Officer -> deployer"]
    RD["role: KV Secrets User -> identity"]
    SEC["azurerm_key_vault_secret.db_primary"]
    SWA["azurerm_static_web_app.web"]
    CAE["azurerm_container_app_environment.main"]
    APP["azurerm_container_app.api"]
    JOB["azurerm_container_app_job.migrator"]

    RG --> ID & LOG & ACR & PG & KV & SWA
    LOG --> APPI
    LOG --> CAE
    ACR --> PULL
    ID --> PULL & RD
    PWD --> PG
    PG --> DB & EXT & FW & ADM
    KV --> OFF & RD
    PG --> SEC
    DB --> SEC
    OFF --> SEC
    CAE --> APP & JOB
    SEC --> APP & JOB
    PULL --> APP & JOB
    RD --> APP & JOB
    SWA -->|"CORS origin"| APP
    APPI -->|"connection string env"| APP
```

### 3.3 Terraform vs pipeline: who owns what

```mermaid
flowchart LR
    subgraph TFOWN["Terraform owns the SHAPE"]
        T1["env vars, secrets references"]
        T2["identity, registry login"]
        T3["probes, CPU/memory, min/max replicas, scale rule"]
        T4["ingress, revision mode Multiple"]
    end
    subgraph PIPEOWN["Pipeline owns the IMAGE and traffic"]
        P1["az containerapp update --image ...:commit"]
        P2["az containerapp job update --image"]
        P3["revision activate / deactivate"]
    end
    TFOWN --- RULE{{"lifecycle.ignore_changes =<br/>[container image, traffic_weight]<br/>compute.tf:144-146, 204-206"}}
    PIPEOWN --- RULE
```

Without `ignore_changes`, the next `terraform apply` would put the placeholder image back and undo the last deployment.

**Configuration differences live only in tfvars** — the code is the same for every environment:

| Setting | [dev.tfvars](../infra/terraform/environments/dev.tfvars) | [prod.tfvars](../infra/terraform/environments/prod.tfvars) |
|---|---|---|
| PostgreSQL SKU / storage | B_Standard_B1ms / 32 GB | GP_Standard_D4ds_v5 / 128 GB |
| Zone-redundant HA standby | off | **on** (automatic failover) |
| Read replica | off | **on** (catalogue reads) |
| Backups | 7 days, local | 35 days, **geo-redundant** |
| API replicas | 1 – 1 | 2 – 20 |
| API CPU / memory | 0.5 / 1 GiB | 1 / 2 GiB |
| Key Vault purge protection, SWA tier, ACR tier | off, Free, Basic | on, Standard, Standard (computed in the .tf files) |
| `ASPNETCORE_ENVIRONMENT`, Swagger, demo seed | Staging, on, on | Production, off, off ([compute.tf:40-47](../infra/terraform/compute.tf#L40-L47)) |

---

## 4. How code reaches Azure (CI/CD)

### 4.1 Push to `main`, end to end

```mermaid
flowchart TB
    PUSH(["git push to main"]) --> CD["cd.yml"]
    PR(["push to a feature branch / PR"]) --> CI0["ci.yml only<br/>(no deploy)"]

    subgraph GATE["Job 1 - CI gate  (cd.yml calls ci.yml)"]
        BE["Backend: restore, build,<br/>unit tests, integration tests<br/>(Testcontainers PostgreSQL)"]
        FE["Frontend: npm ci, typecheck,<br/>vitest, vite build"]
        DK["Docker images: build only,<br/>no push (proves Dockerfiles work)"]
        BE --> DK
        FE --> DK
    end

    subgraph DEPLOY["Job 2 - Deploy to dev  (environment: dev)"]
        LOGIN["azure/login: OIDC,<br/>no stored secret"]
        S2["deploy.ps1 step 2: dotnet publish /t:PublishContainer<br/>-> push iplstore-api:commit to ACR"]
        S3["step 3: point migration job at the image,<br/>start it, wait for Succeeded"]
        S4["step 4: az containerapp update --image<br/>-> NEW revision r{commit}-{time}"]
        S5["step 5: smoke test /health/ready and<br/>/api/v1/products (up to 5 min)"]
        S5b["deactivate old revisions"]
        S6["step 6: vite build with VITE_API_BASE_URL<br/>= API URL, upload dist/ to Static Web Apps"]
        LOGIN --> S2 --> S3 --> S4 --> S5 --> S5b --> S6
    end

    CD --> GATE --> DEPLOY
    S3 -. "fails" .-> STOP1(["stop: API NOT updated"])
    S5 -. "fails" .-> STOP2(["stop: old revision still active,<br/>roll back command printed"])
```

Code: [cd.yml](../.github/workflows/cd.yml), [ci.yml](../.github/workflows/ci.yml), [deploy.ps1](../scripts/deploy.ps1). The same script is used for manual deploys, so a laptop deploy and a pipeline deploy cannot drift apart. `concurrency: deploy-dev` stops two deployments overlapping.

### 4.2 The same deploy as a sequence (who talks to whom)

```mermaid
sequenceDiagram
    autonumber
    participant GH as GitHub runner
    participant Entra as Microsoft Entra ID
    participant ACR as Container Registry
    participant Job as Migration job
    participant App as Container App (API)
    participant KV as Key Vault
    participant PG as PostgreSQL
    participant SWA as Static Web Apps

    GH->>Entra: OIDC token (repo, environment dev)
    Entra-->>GH: Azure access token (Contributor on the RG)
    GH->>ACR: short-lived ACR token, then push iplstore-api:<commit>
    GH->>Job: update image, start execution
    Job->>ACR: pull image (managed identity, AcrPull)
    Job->>KV: read db-connection-primary (managed identity)
    Job->>PG: apply pending SQL migrations, record in schema_migrations
    Job-->>GH: Succeeded
    GH->>App: update image -> new revision
    App->>ACR: pull image (managed identity)
    App->>KV: resolve secret reference (managed identity)
    App->>App: start, probes /health/live and /health/ready
    GH->>App: smoke test, then deactivate old revisions
    GH->>SWA: upload built SPA (deployment token read via az)
```

### 4.3 Why migrations run before the new revision

Migrations must be **additive** (expand/contract): add a column with a default, never rename or drop in the same release. Then the **old** revision still works on the **new** schema while the new revision starts, and a failed migration stops the deploy before any traffic moves.

---

## 5. What is picked up from where when the API starts

```mermaid
flowchart TB
    START(["Container App starts a replica<br/>of revision r{commit}"]) --> PULL
    PULL["Pull image acriplstoredev.azurecr.io/iplstore-api:{commit}<br/>using the managed identity (AcrPull)"] --> SECRET
    SECRET["Resolve secret 'db-primary' -> Key Vault db-connection-primary<br/>(versionless id, managed identity, Secrets User)"] --> ENV
    ENV["Inject environment variables:<br/>Database__ConnectionString (from the secret)<br/>Database__ApplyMigrationsOnStartup=false<br/>Cors__AllowedOrigins__0=https://{swa host}<br/>ASPNETCORE_ENVIRONMENT=Staging, Swagger__Enabled, ...<br/>(compute.tf:38-48, 111-122)"] --> RUN
    RUN["dotnet IplStore.Api.dll  ->  Program.cs"] --> CFG
    CFG["Configuration layers, later wins:<br/>appsettings.json (localhost defaults)<br/>appsettings.Staging.json (if any)<br/>ENVIRONMENT VARIABLES  <- Azure values win"] --> COMP
    COMP["CompositionRoot.AddIplStore:<br/>bind + validate options (fail fast)<br/>AddApplication, AddInfrastructure<br/>register DbContexts with Npgsql + retry"] --> MIG
    MIG{"ApplyMigrationsOnStartup?"} -->|"false in Azure<br/>(the job did it)"| PIPE
    PIPE["UseIplStorePipeline:<br/>exception handler, CORS, rate limiter,<br/>controllers, /health endpoints"] --> PROBES
    PROBES["Probes: /health/live (process up)<br/>/health/ready (database reachable)"] --> TRAFFIC(["Replica receives traffic"])
```

Key point: **the image is the same in every environment**. Everything environment-specific (database, CORS origin, Swagger, seed data) arrives as environment variables from Terraform, and secrets arrive through Key Vault references.

---

## 6. What happens when a request comes in

### 6.1 Page load and one API call

```mermaid
sequenceDiagram
    autonumber
    actor B as Browser
    participant SWA as Static Web Apps (CDN)
    participant ING as Container Apps ingress (TLS)
    participant API as API replica (.NET)
    participant DI as DI container (per request scope)
    participant PG as PostgreSQL (TLS, port 5432)

    B->>SWA: GET https://<swa host>/
    SWA-->>B: index.html + JS bundle (API URL baked in at build: VITE_API_BASE_URL)
    B->>ING: GET https://ca-iplstore-dev-api.../api/v1/cart  + X-Customer-Id
    Note over B,ING: Cross-origin call: preflight OPTIONS answered by the API's CORS policy<br/>(allowed origin = the SWA host only)
    ING->>API: forward to a healthy replica of the active revision (port 8080)
    API->>API: middleware: exception handler, CORS, rate limiter, routing
    API->>DI: build CartController -> CartService -> CartQueries, PricingPolicySelector ...
    DI->>PG: StoreDbContext opens a pooled Npgsql connection<br/>(Database__ConnectionString, SSL Mode=Require)
    PG-->>DI: rows (firewall allows Azure services)
    DI-->>API: CartDto priced by IPricingPolicy
    API-->>B: 200 JSON
    Note over API,PG: Scoped objects (DbContext, repositories, services) are disposed at the end of the request.<br/>Singletons (pricing strategies, payment gateway) live per replica process.
```

### 6.2 The same path as one picture (what is picked from where)

```mermaid
flowchart LR
    B([Browser]) -->|"static files"| SWA["Static Web Apps"]
    B -->|"HTTPS /api/v1/..."| ING["Container Apps ingress<br/>TLS, external, port 8080"]
    ING --> REV["Active revision<br/>(100% traffic)"]
    REV --> R1["Replica"]
    R1 --> MW["Middleware<br/>CORS (Cors__AllowedOrigins__0)<br/>rate limit (RateLimiting:*)<br/>identity: X-Customer-Id (demo)"]
    MW --> CTRL["Controller -> Application service"]
    CTRL --> CTX["StoreDbContext / ReadOnlyStoreDbContext"]
    CTX -->|"connection string:<br/>env var <- Key Vault <- Terraform"| PG[("psql-iplstore-dev<br/>database iplstore")]
    R1 -. "stdout logs" .-> LOG[("Log Analytics")]
```

Note: the browser calls the API **directly** on its Container Apps URL, so CORS is required. A single domain with `/api` routed by Azure Front Door would remove CORS (see gaps).

---

## 7. Dev vs prod today

The prod tfvars and backend config exist, but **no prod environment has been created and no pipeline deploys to prod**. Everything that runs is dev.

| | Dev (live) | Prod (designed, not deployed) |
|---|---|---|
| Resource group / state file | `rg-iplstore-dev` / `iplstore-dev.tfstate` | `rg-iplstore-prod` / `iplstore-prod.tfstate` |
| Deployed by | `cd.yml` on every push to `main` | nothing yet |
| Terraform applied by | a person, locally | nothing yet |

---

## 8. Gaps to production

Grouped by area. **P1** = must fix before real customers, **P2** = should fix before go-live, **P3** = improve after.

### 8.1 Delivery (CI/CD)

| Gap | Today | Production target | How | Priority |
|---|---|---|---|---|
| Single environment | `main` deploys straight to dev | dev → staging → prod | `cd.yml` jobs per environment, each with its own GitHub environment and federated credential | P1 |
| No approval gate | every merge to `main` is live | manual approval before prod | GitHub environment **required reviewers** on `prod` | P1 |
| Build per environment | image built inside the deploy job | **build once, promote** the same image digest | Build + push in one job, pass the digest; prod deploy only re-tags/updates | P1 |
| No branch protection | direct pushes to `main` allowed | PR + green CI + review required | GitHub branch protection rules | P1 |
| Terraform applied by hand | `terraform apply` on a laptop | infra changes reviewed and applied by a pipeline | `terraform.yml`: `plan` on PR (posted as a comment), `apply` on merge with approval; separate identity per environment | P2 |
| No progressive rollout | old revision deactivated right after the smoke test | canary: 10% → watch metrics → 100% | `az containerapp ingress traffic set` with weights; keep the previous revision for instant rollback | P2 |
| Smoke test only | `/health/ready` + one product call | E2E tests against staging | Playwright suite in the staging job | P2 |
| Supply chain | no scanning | dependency, image and IaC scanning, SBOM | Dependabot, `dotnet list package --vulnerable`, `npm audit`, Trivy/Defender for container images, Checkov/tfsec, signed images | P2 |
| SPA token | SWA deployment token read with `az` at deploy time | same, or SWA GitHub integration | fine as is; rotate the token if leaked | P3 |

### 8.2 Security and identity

| Gap | Today | Production target | How | Priority |
|---|---|---|---|---|
| No real authentication | `X-Customer-Id` header picks the shopper | Entra External ID / Azure AD B2C sign-in, JWT on every call | `AddAuthentication().AddJwtBearer(...)`; replace `HeaderCurrentCustomerAccessor` with a claims-based accessor (already behind `ICurrentCustomerAccessor`) | P1 |
| Database password | API logs in as `ipladmin` with a password from Key Vault | passwordless, least privilege | Entra auth for `id-iplstore-dev-api`, a DML-only role, token via `UsePeriodicPasswordProvider`, password auth off (diagram in [14 §3](14-deployment-walkthrough.md)) | P1 |
| Admin used by the app | migrations and API share `ipladmin` | separate identities: migrator (DDL) vs API (DML) | second managed identity for the job | P1 |
| Public network | PostgreSQL public endpoint + "allow Azure services" (any Azure tenant) | private networking | VNet-integrated Container Apps environment, PostgreSQL private access / private endpoint, Key Vault and ACR private endpoints, public access off | P1 |
| No WAF / edge | API exposed directly | Azure Front Door Premium + WAF, single domain, `/api` routed to the API (no CORS) | Front Door with origin groups for SWA and Container Apps; restrict Container Apps ingress to Front Door | P1 |
| Custom domain and TLS | default `*.azurestaticapps.net` / `*.azurecontainerapps.io` | `store.<company>.com` with managed certificates | Front Door custom domain | P2 |
| Secret hygiene | connection string never expires | rotation, expiry and alerts | Key Vault expiry + Event Grid near-expiry event; becomes moot with passwordless DB | P2 |
| Broad deploy rights | deploy app is Contributor on the whole RG | least privilege per task | custom role or scoped roles (Container Apps contributor, AcrPush, SWA contributor) | P2 |
| Defender for Cloud | off | on for containers, Key Vault, databases, App Service | enable plans; fix recommendations | P2 |

### 8.3 Reliability and data

| Gap | Today (dev) | Production target | How | Priority |
|---|---|---|---|---|
| One replica | `min = max = 1` | `min 2` across zones | prod tfvars already say 2–20; use a **zone-redundant** Container Apps environment (needs VNet) | P1 |
| No HA database | single zone | zone-redundant HA standby | already in prod tfvars | P1 |
| Backups | 7 days, local | 35 days, geo-redundant, **restore tested** | already in prod tfvars; add a quarterly restore drill | P1 |
| In-memory payment state | `FakePaymentGateway` (per replica, lost on restart) | real provider + persisted payment attempts + webhooks | `IPaymentGateway` adapter for Razorpay/Stripe, `payments` table, signed webhook endpoint, outbox | P1 |
| Region failure | single region | documented DR: geo-restore or a warm secondary | geo-redundant backups + IaC to rebuild in Central/South India; RTO/RPO agreed | P2 |
| Expand/contract discipline | by convention | enforced | migration review checklist; CI test that applies migrations to the previous schema and runs the old API's tests | P3 |

### 8.4 Observability and operations

| Gap | Today | Production target | How | Priority |
|---|---|---|---|---|
| No telemetry | App Insights exists, `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, but no SDK sends data | traces, metrics, dependencies, exceptions | add `Azure.Monitor.OpenTelemetry.AspNetCore` and `UseAzureMonitor()`; Npgsql tracing | P1 |
| No alerts | none | alerts on 5xx rate, p95 latency, failed migrations, DB CPU/storage/connections, replica restarts | Azure Monitor alert rules + action group, in Terraform | P1 |
| No dashboards / SLOs | none | availability and latency SLOs with error budgets | Workbook or Grafana; synthetic availability test | P2 |
| Cost control | none | budgets and alerts per RG | `azurerm_consumption_budget_resource_group` | P2 |
| Runbooks | [13](13-azure-deployment-runbook.md) covers deploy and teardown | incident runbooks (rollback, failover, restore, key rotation) | write and rehearse | P3 |

---

## 9. Target production architecture

```mermaid
flowchart LR
    User([Shopper]) --> FD["Azure Front Door Premium<br/>WAF, custom domain, TLS"]
    FD -->|"/*"| SWA["Static Web Apps (Standard)<br/>private origin"]
    FD -->|"/api/*  (no CORS)"| ING

    subgraph VNET["Virtual network"]
        subgraph CAE["Container Apps env (zone-redundant, internal)"]
            ING["Ingress (internal)"] --> API["API: 2-20 replicas<br/>across zones"]
            JOB["Migration job<br/>(DDL identity)"]
        end
        PE1["Private endpoint<br/>Key Vault"]
        PE2["Private endpoint<br/>ACR"]
        PGP[("PostgreSQL GP D4ds_v5<br/>private access, zone-redundant HA")]
        PGR[("Read replica<br/>catalogue reads")]
    end

    ENTRA["Microsoft Entra ID<br/>(shoppers: External ID / B2C,<br/>app: managed identity tokens)"]
    APPI["App Insights + Log Analytics<br/>alerts, dashboards"]
    PAY["Payment provider<br/>(webhooks -> API)"]

    API -->|"Entra token, DML role"| PGP
    API -->|"catalogue reads"| PGR
    JOB -->|"Entra token, DDL role"| PGP
    API --> PE1
    API --> PE2
    API -. "JWT validation" .-> ENTRA
    API -. "OpenTelemetry" .-> APPI
    API <--> PAY
```

```mermaid
flowchart LR
    PR(["Pull request"]) --> CI["CI: build, unit + integration,<br/>scans, terraform plan comment"]
    CI --> MERGE(["Merge to main<br/>(protected)"])
    MERGE --> BUILD["Build ONCE<br/>push image by digest, SBOM, sign"]
    BUILD --> STG["Deploy staging<br/>migrate -> revision -> E2E tests"]
    STG --> APPROVE{{"Required reviewers<br/>(GitHub environment prod)"}}
    APPROVE --> PROD["Deploy prod: same digest<br/>migrate -> canary 10% -> metrics OK -> 100%"]
    PROD -. "alert / bad metrics" .-> RB(["Rollback: traffic back to<br/>previous revision"])
```

---

## 10. Short answers for the panel

> **"How is the infrastructure created?"** Terraform, one root module, one file per concern, remote state in an Azure Storage account with Entra-only access and blob-lease locking. Environment differences are only tfvars; the code is identical for dev and prod.

> **"What happens when you push to main?"** `cd.yml` runs the full CI as a gate, then signs in to Azure with OIDC (no secret), and runs the same `deploy.ps1` I use by hand: build and push the image to ACR tagged with the commit, run the migration job, roll out a new revision, smoke-test it, retire the old revision, then build and upload the SPA.

> **"When a request comes in, what is used from where?"** The browser gets the SPA from Static Web Apps and calls the API's Container Apps URL directly. Ingress sends it to a healthy replica of the active revision. That replica started from the commit-tagged image, pulled with the managed identity, and its connection string is an environment variable resolved from Key Vault by the same identity. DI builds the request's objects; `StoreDbContext` opens a pooled TLS connection to PostgreSQL.

> **"Is it production-ready?"** Not yet, and I can say exactly why: one environment with no approval gate, a demo identity header instead of real auth, a password-based admin database login, public network endpoints without a WAF, no telemetry or alerts, and an in-memory payment gateway. Prod tfvars already cover HA, replicas, backups and scale; the P1 list in section 8 is what I would do first.
