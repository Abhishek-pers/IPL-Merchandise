# -----------------------------------------------------------------------------
# Deploys the APPLICATION (API + database schema + web) to an environment that
# Terraform has already created. Same steps, same order as .github/workflows/cd.yml:
#
#   1. unit tests                      (skip with -SkipTests)
#   2. build API image, push to ACR    (.NET SDK container build: no Docker needed)
#   3. run the migration job           (once, BEFORE the new API version gets traffic)
#   4. roll out a new API revision     (Container Apps, Multiple revision mode)
#   5. smoke test the API              (/health/ready, products) -> then retire old revisions
#   6. build the SPA with the API URL baked in, upload to Static Web Apps
#
#   ./scripts/deploy.ps1                       # dev, image tag = git commit
#   ./scripts/deploy.ps1 -Environment prod -SkipTests
#
# Prerequisites: az login, .NET 8 SDK, Node 18+. Infrastructure: infra/terraform.
# -----------------------------------------------------------------------------
param(
    [ValidateSet("dev", "prod")]
    [string]$Environment = "dev",
    [string]$Project = "iplstore",
    [string]$Tag,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

function Invoke-Native {
    # Runs a native command and stops the script if it fails.
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $Command" }
}

# Resource names follow the Terraform naming convention (infra/terraform/main.tf).
$name = "$Project-$Environment"
$compact = $name -replace "-", ""
$rg = "rg-$name"
$acrName = "acr$compact"
$apiApp = "ca-$name-api"
$migrateJob = "caj-$name-migrate"
$swa = "swa-$name"

if (-not $Tag) {
    $Tag = (git -C $repo rev-parse --short HEAD).Trim()
    if (git -C $repo status --porcelain) { $Tag = "$Tag-dirty-$(Get-Date -Format yyyyMMddHHmm)" }
}

Step "Target: $rg (subscription: $(az account show --query name -o tsv)) image tag: $Tag"
Invoke-Native { az group show -n $rg -o none --only-show-errors }

# ------------------------------------------------------------------ 1. tests
if (-not $SkipTests) {
    Step "Unit tests"
    Invoke-Native { dotnet test "$repo/backend/tests/IplStore.UnitTests" -c Release --nologo -v:quiet }
}

# ------------------------------------------------------------------ 2. image
$acrServer = (az acr show -n $acrName --query loginServer -o tsv --only-show-errors).Trim()
$image = "$acrServer/$Project-api:$Tag"
Step "Build and push $image"

# Short-lived ACR token in a throwaway Docker config: nothing is written to ~/.docker.
$token = az acr login -n $acrName --expose-token --query accessToken -o tsv --only-show-errors
$dockerConfig = Join-Path ([IO.Path]::GetTempPath()) "acr-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $dockerConfig | Out-Null
$auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("00000000-0000-0000-0000-000000000000:$token"))
[IO.File]::WriteAllText("$dockerConfig/config.json", (@{ auths = @{ $acrServer = @{ auth = $auth } } } | ConvertTo-Json -Depth 5))
$env:DOCKER_CONFIG = $dockerConfig
try {
    Invoke-Native {
        dotnet publish "$repo/backend/src/IplStore.Api/IplStore.Api.csproj" -c Release --os linux --arch x64 `
            /t:PublishContainer -p:ContainerRegistry=$acrServer -p:ContainerRepository="$Project-api" `
            -p:ContainerImageTag=$Tag --nologo -v:minimal
    }
}
finally {
    Remove-Item Env:DOCKER_CONFIG
    Remove-Item $dockerConfig -Recurse -Force
}

# ------------------------------------------------------------------ 3. migrate
Step "Run database migrations ($migrateJob)"
Invoke-Native { az containerapp job update -n $migrateJob -g $rg --image $image -o none --only-show-errors }
$execution = (az containerapp job start -n $migrateJob -g $rg --query name -o tsv --only-show-errors).Trim()
do {
    Start-Sleep -Seconds 10
    $status = (az containerapp job execution show -n $migrateJob -g $rg --job-execution-name $execution `
        --query properties.status -o tsv --only-show-errors).Trim()
    Write-Host "   migration $execution : $status"
} while ($status -in @("Running", "Processing", ""))
if ($status -ne "Succeeded") {
    throw "Migration $execution ended as '$status'. The API was NOT updated. Logs: az containerapp job logs show -n $migrateJob -g $rg --execution $execution"
}

# ------------------------------------------------------------------ 4. API revision
Step "Roll out API revision with $image"
# Revision suffix: lowercase letters/digits, must start with a letter (a commit sha may start with a digit).
# A timestamp keeps it unique when the same commit is deployed twice.
$tagPart = ($Tag.ToLower() -replace "[^a-z0-9]", "")
if ($tagPart.Length -gt 10) { $tagPart = $tagPart.Substring(0, 10) }
$suffix = "r$tagPart-$(Get-Date -Format MMddHHmm)"
Invoke-Native { az containerapp update -n $apiApp -g $rg --image $image --revision-suffix $suffix -o none --only-show-errors }
$newRevision = "$apiApp--$suffix"
$apiUrl = "https://" + (az containerapp show -n $apiApp -g $rg --query properties.configuration.ingress.fqdn -o tsv --only-show-errors).Trim()

# ------------------------------------------------------------------ 5. smoke test API
Step "Smoke test $apiUrl"
$healthy = $false
for ($i = 1; $i -le 30 -and -not $healthy; $i++) {
    try {
        $ready = Invoke-WebRequest "$apiUrl/health/ready" -UseBasicParsing -TimeoutSec 10
        $products = Invoke-WebRequest "$apiUrl/api/v1/products?pageSize=1" -UseBasicParsing -TimeoutSec 10
        $healthy = ($ready.StatusCode -eq 200 -and $products.StatusCode -eq 200)
    }
    catch { Start-Sleep -Seconds 10 }
}
if (-not $healthy) {
    throw "API did not become healthy. Roll back: az containerapp ingress traffic set -n $apiApp -g $rg --revision-weight <previous-revision>=100"
}
Write-Host "   /health/ready and /api/v1/products return 200"

# Multiple-revision mode keeps old revisions running; retire them once the new one is healthy.
$oldRevisions = az containerapp revision list -n $apiApp -g $rg `
    --query "[?properties.active && name!='$newRevision'].name" -o tsv --only-show-errors
foreach ($revision in $oldRevisions) {
    if ($revision) {
        Write-Host "   deactivating old revision $revision"
        az containerapp revision deactivate -n $apiApp -g $rg --revision $revision -o none --only-show-errors
    }
}

# ------------------------------------------------------------------ 6. web
Step "Build and deploy the web app"
Push-Location "$repo/frontend"
try {
    if (-not (Test-Path node_modules)) { Invoke-Native { npm ci --no-audit --no-fund } }
    $env:VITE_API_BASE_URL = "$apiUrl/api/v1"
    Invoke-Native { npm run build }
    Remove-Item Env:VITE_API_BASE_URL
    $swaToken = az staticwebapp secrets list -n $swa -g $rg --query properties.apiKey -o tsv --only-show-errors
    Invoke-Native { npx --yes @azure/static-web-apps-cli@2 deploy ./dist --deployment-token $swaToken --env production }
}
finally { Pop-Location }

$webUrl = "https://" + (az staticwebapp show -n $swa -g $rg --query defaultHostname -o tsv --only-show-errors).Trim()
Step "Deployed $Tag"
Write-Host "   Web : $webUrl"
Write-Host "   API : $apiUrl   (revision $newRevision)"
