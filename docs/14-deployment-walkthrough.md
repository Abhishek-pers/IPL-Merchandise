# 14 · Deployment walkthrough (interview Q&A with code paths)

Each section: **the answer in a few lines**, then **the code path to follow** (click through in order),
then **what to show live**. Paths are relative to this file, so links work in VS Code and on GitHub.

Live environment: resource group `rg-iplstore-dev` (Central India) ·
web `https://mango-smoke-0e64dac00.1.azurestaticapps.net` ·
API `https://ca-iplstore-dev-api.calmwater-54a78600.centralindia.azurecontainerapps.io`

---

## 0. "How did you deploy it?" (tell it as a story)

**Answer.** Three layers. (1) **Infrastructure** with Terraform. (2) **Database and app**: image, migrations, API, website.
(3) **Repeatable and automated**: remote state, one deploy script, GitHub Actions running that script with OIDC.
Problems hit on the way: the trial subscription blocks ACR Tasks (so the .NET SDK builds the image), Terraform drift
on a workload profile, and an OIDC subject mismatch.

**Code path, in the order it happened:**
1. State storage, created once: [bootstrap-tfstate.ps1](../scripts/bootstrap-tfstate.ps1)
2. Backend points Terraform at that storage: [versions.tf:17](../infra/terraform/versions.tf#L17) + [dev.backend.hcl](../infra/terraform/environments/dev.backend.hcl)
3. First apply creates **only the registry** (so an image can exist before the app): [compute.tf:8](../infra/terraform/compute.tf#L8)
4. Image built and pushed with the .NET SDK, no Docker: [deploy.ps1:60-82](../scripts/deploy.ps1#L60-L82)
5. Full apply creates everything else. Start at [main.tf:23](../infra/terraform/main.tf#L23) and read section 1 below
6. Migration job creates tables and seed data: [deploy.ps1:84-96](../scripts/deploy.ps1#L84-L96)
7. Website built with the API URL and uploaded: [deploy.ps1:135-147](../scripts/deploy.ps1#L135-L147)
8. Automated: [cd.yml](../.github/workflows/cd.yml) runs the same script

**Show:** the GitHub Actions green CD run, then the resource group in the portal.

---

## 1. "What runs where in Azure?"

**Answer.** UI on Static Web Apps, API on Container Apps, data on PostgreSQL Flexible Server. Around them: the
Container Apps environment (required), Container Registry (images), managed identity (no passwords), Key Vault
(connection string), Log Analytics + App Insights (monitoring). One resource group per environment.

**Code path (one Terraform file per concern):**
1. Naming + resource group: [main.tf:10-29](../infra/terraform/main.tf#L10-L29)
2. API identity: [main.tf:31](../infra/terraform/main.tf#L31)
3. Monitoring: [main.tf:39](../infra/terraform/main.tf#L39) (Log Analytics), [main.tf:48](../infra/terraform/main.tf#L48) (App Insights)
4. Registry + pull permission: [compute.tf:8-21](../infra/terraform/compute.tf#L8-L21)
5. Container Apps environment, linked to Log Analytics: [compute.tf:23-36](../infra/terraform/compute.tf#L23-L36)
6. **The API**: [compute.tf:50](../infra/terraform/compute.tf#L50)
7. **Migration job**: [compute.tf:153](../infra/terraform/compute.tf#L153)
8. **Database**: [database.tf:12](../infra/terraform/database.tf#L12), database `iplstore` [database.tf:64](../infra/terraform/database.tf#L64)
9. Key Vault + secret: [secrets.tf:4](../infra/terraform/secrets.tf#L4), [secrets.tf:30](../infra/terraform/secrets.tf#L30)
10. **Website**: [frontend.tf:5](../infra/terraform/frontend.tf#L5)
11. Values printed after apply (URLs, names): [outputs.tf](../infra/terraform/outputs.tf)

**Show:** the portal resource list next to these files. Every resource maps to one `resource` block.

---

## 2. "Walk me through your CI/CD pipeline"

**Answer.** PR → CI (build, unit + integration tests, frontend tests, Docker build). Merge to main → CI as a gate →
Azure login via OIDC → `deploy.ps1`: image, migrations, new revision, smoke test, retire old revision, website.

**Code path:**
1. Trigger (push to main, or the Run workflow button): [cd.yml:14-17](../.github/workflows/cd.yml#L14-L17)
2. One deploy at a time: [cd.yml:24-26](../.github/workflows/cd.yml#L24-L26)
3. CI as a gate, reusing ci.yml: [cd.yml:29-31](../.github/workflows/cd.yml#L29-L31) → [ci.yml:12](../.github/workflows/ci.yml#L12) (`workflow_call`)
   - Backend build + unit + integration tests: [ci.yml:24-47](../.github/workflows/ci.yml#L24-L47)
   - Frontend typecheck, tests, build: [ci.yml:57-75](../.github/workflows/ci.yml#L57-L75)
   - Docker images build: [ci.yml:77-88](../.github/workflows/ci.yml#L77-L88)
4. Deploy job only if CI passed, in environment `dev`: [cd.yml:33-38](../.github/workflows/cd.yml#L33-L38)
5. Azure login (OIDC): [cd.yml:54-58](../.github/workflows/cd.yml#L54-L58)
6. Run the script: [cd.yml:67](../.github/workflows/cd.yml#L67) → [deploy.ps1](../scripts/deploy.ps1). Its numbered sections:
   - 1 tests [L54](../scripts/deploy.ps1#L54) · 2 image [L60](../scripts/deploy.ps1#L60) · 3 migrate [L84](../scripts/deploy.ps1#L84) ·
     4 revision [L98](../scripts/deploy.ps1#L98) · 5 smoke test [L109](../scripts/deploy.ps1#L109) · 6 web [L135](../scripts/deploy.ps1#L135)

**Show:** the green run in GitHub, then the Activity log in the portal (operations made by the pipeline identity `2dec98d2…`).

---

## 3. "How do you handle configuration and secrets per environment?"

**Answer.** Plain settings are environment variables on the Container App, set in Terraform per environment.
`__` in a name maps to a section in appsettings.json. Secrets live in Key Vault. The Container App reads the
secret with its managed identity and exposes it as an environment variable. Nothing secret is in code, the
pipeline or GitHub.

**Code path (follow one setting and one secret from end to end):**
1. Defaults for local runs: [appsettings.json:11-14](../backend/src/IplStore.Api/appsettings.json#L11-L14) (Database), [L50](../backend/src/IplStore.Api/appsettings.json#L50) (Pricing), [L57](../backend/src/IplStore.Api/appsettings.json#L57) (Cors)
2. Per-environment differences: [dev.tfvars](../infra/terraform/environments/dev.tfvars), [prod.tfvars](../infra/terraform/environments/prod.tfvars), declared in [variables.tf](../infra/terraform/variables.tf)
3. **Setting**: Terraform sets plain env vars on the app: [compute.tf:38-48](../infra/terraform/compute.tf#L38-L48)
   (e.g. `Cors__AllowedOrigins__0` = the website URL, `Database__ApplyMigrationsOnStartup` = false)
4. **Secret**: the connection string is written to Key Vault: [database.tf:105](../infra/terraform/database.tf#L105) (built) → [secrets.tf:30](../infra/terraform/secrets.tf#L30) (stored)
5. The app references the secret through its identity: [compute.tf:68-72](../infra/terraform/compute.tf#L68-L72)
6. …and exposes it as `Database__ConnectionString`: [compute.tf:111-114](../infra/terraform/compute.tf#L111-L114)
7. The code reads it as a typed option: [DatabaseOptions.cs:12](../backend/src/IplStore.Infrastructure/Options/DatabaseOptions.cs#L12) → used at [DependencyInjection.cs:69-77](../backend/src/IplStore.Infrastructure/DependencyInjection.cs#L69-L77)

**Show:** portal → `ca-iplstore-dev-api` → Settings → Secrets (`db-primary`, Key Vault reference) and Environment variables.

**Honest gap:** the connection string still contains the admin password. Next step: Entra authentication for the API's
identity, then password auth off. Entra sign-in for people is already on: [database.tf:30-34](../infra/terraform/database.tf#L30-L34), [database.tf:54](../infra/terraform/database.tf#L54).

---

## 4. "How does the pipeline authenticate to Azure?"

**Answer.** GitHub OIDC. GitHub issues a short-lived token for "this repo, environment dev". Entra trusts exactly that
subject (federated credential on the app `github-iplstore-deploy`) and exchanges it for an Azure token. No client
secret exists. The identity has Contributor on `rg-iplstore-dev` only.

**Code path:**
1. Permission to request the token: [cd.yml:21](../.github/workflows/cd.yml#L21) (`id-token: write`)
2. The job runs in environment `dev` (part of the token subject): [cd.yml:37-38](../.github/workflows/cd.yml#L37-L38)
3. Login with IDs from repository **variables** (not secrets): [cd.yml:54-58](../.github/workflows/cd.yml#L54-L58)
4. Terraform state is read the same way (Entra, no keys): [dev.backend.hcl:6-7](../infra/terraform/environments/dev.backend.hcl#L6-L7)

**Show:** Entra → App registrations → `github-iplstore-deploy` → Certificates & secrets → Federated credentials
(two subjects: `repo:Abhishek-pers/IPL-Merchandise:environment:dev` and the ID-based
`repo:Abhishek-pers@226307867/IPL-Merchandise@1390662889:environment:dev`).

**Story:** the first runs failed with `AADSTS700213`. GitHub now puts owner and repo IDs in the subject, so a deleted
and recreated repo with the same name can't take over the trust. I added the exact subject from the error.

---

## 5. "How do you deploy database changes safely?"

**Answer.** Versioned SQL files are compiled into the API. A Container Apps Job runs them **once, before** the new
revision. If they fail, the pipeline stops and the API isn't touched. Each script runs in a transaction and is
checksummed. Expand/contract keeps old and new code compatible during rollout.

**Code path:**
1. The scripts: [database/migrations](../database/migrations) (V001…V004, immutable once applied)
2. Compiled into the assembly: [IplStore.Infrastructure.csproj:17-22](../backend/src/IplStore.Infrastructure/IplStore.Infrastructure.csproj#L17-L22)
3. The migrator: [SqlScriptDatabaseMigrator.cs:28](../backend/src/IplStore.Infrastructure/Persistence/Migrations/SqlScriptDatabaseMigrator.cs#L28)
   - records applied scripts: [L37-L39](../backend/src/IplStore.Infrastructure/Persistence/Migrations/SqlScriptDatabaseMigrator.cs#L37-L39) (`schema_migrations` + checksum)
   - refuses edited scripts: [L103-L105](../backend/src/IplStore.Infrastructure/Persistence/Migrations/SqlScriptDatabaseMigrator.cs#L103-L105)
   - one transaction per script: [L114-L126](../backend/src/IplStore.Infrastructure/Persistence/Migrations/SqlScriptDatabaseMigrator.cs#L114-L126)
4. `--migrate-only`: migrate and exit: [Program.cs:17-28](../backend/src/IplStore.Api/Program.cs#L17-L28)
5. The job runs the API image with that flag: [compute.tf:153](../infra/terraform/compute.tf#L153), [compute.tf:190](../infra/terraform/compute.tf#L190)
6. The API itself never migrates in Azure: [compute.tf:42](../infra/terraform/compute.tf#L42)
7. The pipeline runs the job and **stops if it fails**: [deploy.ps1:84-96](../scripts/deploy.ps1#L84-L96)

**Show:** Log Analytics: `ContainerAppConsoleLogs_CL | where ContainerAppName_s == "caj-iplstore-dev-migrate"`.

**Why not migrate at startup?** Several replicas would race, and app availability would depend on schema changes.

---

## 6. "What if a deployment goes wrong? How do you roll back?"

**Answer.** The smoke test must pass before the old revision is retired. If it fails, the script stops with the
rollback command. Rolling back means sending traffic to the previous revision: seconds, no rebuild.
Expand/contract migrations keep the old code compatible with the database.

**Code path:**
1. Multiple revisions allowed (blue/green capable): [compute.tf:54](../infra/terraform/compute.tf#L54)
2. Each deploy creates a uniquely named revision: [deploy.ps1:98-107](../scripts/deploy.ps1#L98-L107)
3. Smoke test, and the rollback command if it fails: [deploy.ps1:109-123](../scripts/deploy.ps1#L109-L123)
4. Old revisions retired only after success: [deploy.ps1:125-133](../scripts/deploy.ps1#L125-L133)
5. Probes stop traffic to an unhealthy replica: [compute.tf:124-136](../infra/terraform/compute.tf#L124-L136) → endpoints [CompositionRoot.cs:75-76](../backend/src/IplStore.Api/Composition/CompositionRoot.cs#L75-L76)

**Commands:**
```powershell
az containerapp revision list -n ca-iplstore-dev-api -g rg-iplstore-dev -o table
az containerapp revision activate -n ca-iplstore-dev-api -g rg-iplstore-dev --revision <previous>
az containerapp ingress traffic set -n ca-iplstore-dev-api -g rg-iplstore-dev --revision-weight <previous>=100
```

**Honest gap:** the script retires the old revision immediately. In production keep it active at 0% for instant
rollback, and release by canary (10% → 100%) with alerts that trigger the rollback.

---

## 7. "Why Container Apps, not AKS or App Service?"

**Answer.** Container Apps is managed Kubernetes without running a cluster: revisions, jobs, KEDA scaling,
scale-to-zero, built-in ingress. AKS when you need full control (custom networking, operators, service mesh).
App Service also fits one web API (slots for blue/green). Container Apps was chosen for the migration job,
revisions and room to split services later.

**Code path (the features you'd lose or have to build elsewhere):**
1. Revisions (blue/green): [compute.tf:54](../infra/terraform/compute.tf#L54)
2. Jobs (migrations): [compute.tf:153](../infra/terraform/compute.tf#L153)
3. Autoscaling rule: [compute.tf:138](../infra/terraform/compute.tf#L138)
4. Ingress + TLS without a load balancer to manage: [compute.tf:83-90](../infra/terraform/compute.tf#L83-L90)
5. Image pull and secrets via managed identity: [compute.tf:62-72](../infra/terraform/compute.tf#L62-L72)

---

## 8. "How does it scale? What's the bottleneck?"

**Answer.** The API scales out on concurrent requests (prod 2-20 replicas, dev pinned to 1). Postgres scales up,
plus a read replica for catalogue reads in prod. The bottleneck is database connections: 20 replicas × pool 50 =
1,000 connections, so size the pool or use the PgBouncer built into Flexible Server.

**Code path:**
1. Replica limits: [compute.tf:94-95](../infra/terraform/compute.tf#L94-L95), values in [dev.tfvars:8-9](../infra/terraform/environments/dev.tfvars#L8-L9) / [prod.tfvars:8-9](../infra/terraform/environments/prod.tfvars#L8-L9)
2. Scale rule (50 concurrent requests per replica): [compute.tf:138](../infra/terraform/compute.tf#L138)
3. Database size and HA: [prod.tfvars:2-5](../infra/terraform/environments/prod.tfvars#L2-L5), HA block [database.tf:36](../infra/terraform/database.tf#L36)
4. Read replica: [database.tf:87-92](../infra/terraform/database.tf#L87-L92) → app uses it for catalogue reads [DependencyInjection.cs:28-29](../backend/src/IplStore.Infrastructure/DependencyInjection.cs#L28-L29), [L72-L74](../backend/src/IplStore.Infrastructure/DependencyInjection.cs#L72-L74)
5. Connection pool size: [appsettings.json:12](../backend/src/IplStore.Api/appsettings.json#L12) (`Maximum Pool Size=50`)
6. Why dev runs exactly 1 replica (in-memory fake payment gateway): [dev.tfvars:6-9](../infra/terraform/environments/dev.tfvars#L6-L9)

---

## 9. "What would you add for production?"

**Answer (lead with three):** dev → staging → prod with a manual approval; build once and promote the same image;
canary releases with automatic rollback. Then: a Terraform plan/apply pipeline, security scans (CodeQL, image scan,
Dependabot), E2E tests on staging, branch protection, passwordless database access.

**Code path (where each would go):**
1. Prod settings already exist: [prod.tfvars](../infra/terraform/environments/prod.tfvars), [prod.backend.hcl](../infra/terraform/environments/prod.backend.hcl)
2. Approval gate: add a `deploy-prod` job with `environment: prod` in [cd.yml](../.github/workflows/cd.yml) (protection rules in GitHub settings)
3. Build once: move the image build from [deploy.ps1:60](../scripts/deploy.ps1#L60) into CI, pass the tag to each environment
4. Canary: replace the 100% switch at [deploy.ps1:105](../scripts/deploy.ps1#L105) with `az containerapp ingress traffic set` weights
5. Terraform pipeline: a new `cd-infra.yml` running `terraform plan` on PRs touching `infra/**`

---

## 10. "How do you monitor it after deployment?"

**Answer.** Container logs go to Log Analytics: what the code logs (orders placed, payments, cancellations) and what
the platform does (image pulls, restarts, revisions). App Insights is provisioned and its connection string is
injected, but the API doesn't send telemetry yet. One package (`Azure.Monitor.OpenTelemetry.AspNetCore`) and one
line would add request traces, dependencies and failures.

**Code path:**
1. Environment linked to Log Analytics: [compute.tf:27](../infra/terraform/compute.tf#L27) → workspace [main.tf:39](../infra/terraform/main.tf#L39)
2. Business log lines you can search for:
   - order placed: [CheckoutService.cs:89](../backend/src/IplStore.Application/Orders/CheckoutService.cs#L89)
   - charged: [FakePaymentGateway.cs:40](../backend/src/IplStore.Infrastructure/Payments/FakePaymentGateway.cs#L40)
   - declined / paid / cancelled: [PaymentService.cs:74](../backend/src/IplStore.Application/Orders/PaymentService.cs#L74), [L100](../backend/src/IplStore.Application/Orders/PaymentService.cs#L100), [L125](../backend/src/IplStore.Application/Orders/PaymentService.cs#L125)
3. Log levels: [appsettings.json:3](../backend/src/IplStore.Api/appsettings.json#L3)
4. App Insights provisioned: [main.tf:48](../infra/terraform/main.tf#L48), connection string injected: [compute.tf:46](../infra/terraform/compute.tf#L46)
5. Error responses carry a trace id: [CompositionRoot.cs:101-103](../backend/src/IplStore.Api/Composition/CompositionRoot.cs#L101-L103)

**Show:** `log-iplstore-dev` → Logs → KQL mode → Last 24 hours:
```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "ca-iplstore-dev-api"
| project TimeGenerated, Log_s
| order by TimeGenerated desc
```

---

## 11. "What did you test locally vs in Azure?"

**Answer.** Everything is deployed and working on Azure: app, database, Key Vault, managed identity, logs, CI/CD.
Locally: Postgres in Docker, the same unit and integration tests. No Service Bus is used, so nothing was emulated.

**Code path:**
1. Local run: [scripts/dev.ps1](../scripts/dev.ps1), [docker-compose.yml](../docker-compose.yml)
2. Integration tests against a real Postgres container: [PostgresFixture.cs](../backend/tests/IplStore.IntegrationTests/Infrastructure/PostgresFixture.cs)
3. The same tests run in CI: [ci.yml:47](../.github/workflows/ci.yml#L47)
4. Azure proof: smoke test [deploy.ps1:109-123](../scripts/deploy.ps1#L109-L123), plus the green CD run

---

## 12. "What does it cost?"

**Answer.** Dev ≈ US$20-30/month, mostly the Postgres B1ms server. Container Apps (consumption) costs almost nothing
when idle, Static Web Apps is free, ACR Basic ≈ $5. Prod costs more: zone-redundant HA Postgres, a read replica, a
minimum of 2 replicas. Size from measured traffic.

**Code path (every cost driver is a variable):**
1. Postgres size: [dev.tfvars:2](../infra/terraform/environments/dev.tfvars#L2) vs [prod.tfvars:2](../infra/terraform/environments/prod.tfvars#L2)
2. HA + replica: [prod.tfvars:4-5](../infra/terraform/environments/prod.tfvars#L4-L5)
3. Replica counts: [dev.tfvars:8-9](../infra/terraform/environments/dev.tfvars#L8-L9) vs [prod.tfvars:8-9](../infra/terraform/environments/prod.tfvars#L8-L9)
4. Registry and Static Web Apps tiers switch on environment: [compute.tf:12](../infra/terraform/compute.tf#L12), [frontend.tf:5](../infra/terraform/frontend.tf#L5)
5. Log retention: [main.tf:39](../infra/terraform/main.tf#L39)

**Show:** portal → `rg-iplstore-dev` → Cost Management → Cost analysis.

---

## 13. "How do infrastructure and application deployment relate?"

**Answer.** Two lifecycles. Terraform owns the shape (settings, scaling, probes, secrets, identity) and runs when
infrastructure changes. The pipeline owns the image and runs on every merge. Terraform ignores the image, so the two
never overwrite each other. State lives in Azure Storage, Entra-only, versioned.

**Code path:**
1. The rule, stated at the top of the file: [compute.tf:1-6](../infra/terraform/compute.tf#L1-L6)
2. Terraform ignores the image (and traffic weights): [compute.tf:145](../infra/terraform/compute.tf#L145), [compute.tf:205](../infra/terraform/compute.tf#L205)
3. The pipeline sets the image: [deploy.ps1:86](../scripts/deploy.ps1#L86) (job), [deploy.ps1:105](../scripts/deploy.ps1#L105) (API)
4. Remote state: [versions.tf:17](../infra/terraform/versions.tf#L17), [dev.backend.hcl](../infra/terraform/environments/dev.backend.hcl), created by [bootstrap-tfstate.ps1:29-45](../scripts/bootstrap-tfstate.ps1#L29-L45)
5. Declared to stop Azure-added drift: [compute.tf:32-35](../infra/terraform/compute.tf#L32-L35) (Consumption workload profile)

---

## Supporting pieces you may be asked about

| Topic | Code |
|---|---|
| Website calls the API at a URL baked in at build time | [config.ts:8](../frontend/src/config.ts#L8) ← set by [deploy.ps1:140](../scripts/deploy.ps1#L140) |
| Refreshing `/cart` works on Static Web Apps | [staticwebapp.config.json](../frontend/public/staticwebapp.config.json) |
| API accepts only the website's origin (CORS) | [CompositionRoot.cs:64-65](../backend/src/IplStore.Api/Composition/CompositionRoot.cs#L64-L65) ← value from [compute.tf:44](../infra/terraform/compute.tf#L44) |
| Readiness check really touches the database | [DatabaseHealthCheck.cs:24-26](../backend/src/IplStore.Api/Health/DatabaseHealthCheck.cs#L24-L26) |
| Transient database errors are retried | [DependencyInjection.cs:81-84](../backend/src/IplStore.Infrastructure/DependencyInjection.cs#L81-L84), [EfUnitOfWork.cs:35](../backend/src/IplStore.Infrastructure/Persistence/EfUnitOfWork.cs#L35) |
| Rate limiting | [CompositionRoot.cs:70](../backend/src/IplStore.Api/Composition/CompositionRoot.cs#L70), [CompositionRoot.cs:128](../backend/src/IplStore.Api/Composition/CompositionRoot.cs#L128), settings [appsettings.json:61](../backend/src/IplStore.Api/appsettings.json#L61) |
| Runbook (commands, tear down) | [13-azure-deployment-runbook.md](13-azure-deployment-runbook.md) |
