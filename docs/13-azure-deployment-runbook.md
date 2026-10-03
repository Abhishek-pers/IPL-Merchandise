# 13 · Azure deployment runbook

How the IPL Store is hosted on Azure, how it was first deployed, and how every later
release ships. Live dev environment: resource group `rg-iplstore-dev` (Central India).

## What runs where

| Piece | Azure resource | Created by |
|---|---|---|
| React SPA | Static Web Apps `swa-iplstore-dev` (global CDN) | Terraform |
| API + business logic | Container Apps `ca-iplstore-dev-api` (1 replica in dev, 2-20 in prod) | Terraform (shape), `deploy.ps1` (image) |
| Schema migrations | Container Apps Job `caj-iplstore-dev-migrate` (same image, `--migrate-only`) | Terraform, run by `deploy.ps1` |
| Database | PostgreSQL Flexible Server `psql-iplstore-dev`, database `iplstore` | Terraform |
| Images | Container Registry `acriplstoredev` | Terraform |
| Secrets | Key Vault `kv-iplstoredev` (DB connection string) | Terraform |
| Workload identity | `id-iplstore-dev-api` (AcrPull + Key Vault Secrets User) | Terraform |
| Monitoring | Application Insights + Log Analytics | Terraform |
| Terraform state | Storage account `stiplstoretfstate` in `rg-iplstore-tfstate` | `scripts/bootstrap-tfstate.ps1` |

## Three layers, three tools

1. **State storage** (once per subscription): `scripts/bootstrap-tfstate.ps1`.
   Terraform cannot create the place it keeps its own state. Entra auth only, no keys, blob versioning on.
2. **Infrastructure** (when `infra/**` changes): Terraform.
   ```powershell
   cd infra/terraform
   $env:ARM_SUBSCRIPTION_ID = az account show --query id -o tsv
   terraform init -backend-config=environments/dev.backend.hcl -backend-config="use_oidc=false" -backend-config="use_cli=true"
   terraform plan  -var-file=environments/dev.tfvars -out dev.plan
   terraform apply dev.plan
   ```
3. **Application** (every release): `scripts/deploy.ps1`, locally or from `.github/workflows/cd.yml`.
   Terraform owns the shape of the Container App; the deploy owns the image (`ignore_changes = [image]`).

## What `deploy.ps1` does, in order

1. Unit tests.
2. Builds the API image with the .NET SDK (`dotnet publish /t:PublishContainer`) and pushes it to ACR,
   tagged with the git commit. No Docker needed, which also works on subscriptions where ACR Tasks is blocked.
3. Points the migration job at the new image and runs it. **If migrations fail, the API is not touched.**
4. Creates a new API revision with the new image.
5. Smoke test: `/health/ready` (checks the database) and `GET /api/v1/products`. Then deactivates old revisions.
6. Builds the SPA with `VITE_API_BASE_URL` set to the API's URL and uploads it to Static Web Apps.

Rollback: `az containerapp revision activate` the previous revision and
`az containerapp ingress traffic set --revision-weight <previous>=100`. This is safe because migrations only
add (expand first, contract in a later release).

## CI/CD

- `ci.yml`: every PR and feature-branch push. Build, unit and integration tests (Testcontainers Postgres), frontend typecheck/tests/build, Docker builds.
- `cd.yml`: push to `main`, or "Run workflow" for any branch. Runs CI as a gate, signs in to Azure with **GitHub OIDC**
  (Entra app `github-iplstore-deploy`, federated credential for the `dev` environment, Contributor on `rg-iplstore-dev` only),
  then runs `deploy.ps1`. No Azure secret is stored in GitHub.

One-time GitHub setup: repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

## How the first deploy was done

1. `bootstrap-tfstate.ps1` created the state storage.
2. `terraform apply -target=azurerm_container_registry.main` created the registry first, so an image could be pushed
   before the Container App existed.
3. The API image was built and pushed with the .NET SDK.
4. `terraform apply` with `api_image=<that image>` created the rest (17 resources, Postgres took about 5 minutes).
5. The migration job created the schema and seeded the demo catalogue; the SPA was built and uploaded.

## Known limits (dev)

- `FakePaymentGateway` keeps approvals in memory per replica, so dev runs exactly one replica.
  Production fix: a `payments` table (unique `order_id`) and a real provider called with the order id as idempotency key.
- Postgres uses a password stored in Key Vault and a public endpoint limited to Azure services. Production fix:
  Entra authentication with the managed identity, and private networking.

## Tear down

```powershell
az group delete -n rg-iplstore-dev --yes
az keyvault purge -n kv-iplstoredev     # Key Vault names stay reserved for 7 days otherwise
```
