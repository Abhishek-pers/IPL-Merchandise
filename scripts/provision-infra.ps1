# -----------------------------------------------------------------------------
# Creates or updates the Azure INFRASTRUCTURE for an environment with Terraform.
# Code releases are a separate step: scripts/deploy.ps1 (or .github/workflows/cd.yml).
#
#   1. Terraform state storage          (scripts/bootstrap-tfstate.ps1, idempotent; skip with -SkipBootstrap)
#   2. terraform init                   (remote state in Azure Storage, signed in with your az login)
#   3. FIRST RUN ONLY:
#        a. create the container registry alone (-target), so an image can be pushed
#        b. build + push a first API image with the .NET SDK (no Docker)
#   4. terraform plan -> review -> apply (asks before applying unless -AutoApprove)
#   5. print the outputs (URLs, names); optionally run deploy.ps1 (-Deploy)
#
#   ./scripts/provision-infra.ps1                     # dev: plan, confirm, apply
#   ./scripts/provision-infra.ps1 -PlanOnly           # show what would change, apply nothing
#   ./scripts/provision-infra.ps1 -Deploy             # apply, then run the first release
#   ./scripts/provision-infra.ps1 -Environment prod
#
# Safe to re-run: on an existing environment it only plans and applies the differences.
# Terraform ignores the API image after creation, so this never rolls back a release.
# Prerequisites: az login, Terraform >= 1.6, .NET 8 SDK (first run only), Node 18+ (-Deploy only).
# -----------------------------------------------------------------------------
param(
    [ValidateSet("dev", "prod")]
    [string]$Environment = "dev",
    [string]$Project = "iplstore",
    [switch]$SkipBootstrap,
    [switch]$PlanOnly,
    [switch]$AutoApprove,
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$tf = Join-Path $repo "infra/terraform"
$varFile = "environments/$Environment.tfvars"
$backendFile = "environments/$Environment.backend.hcl"
$planFile = "$Environment.plan"

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

function Invoke-Native {
    # Runs a native command and stops the script if it fails.
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $Command" }
}

function Invoke-Terraform {
    terraform "-chdir=$tf" @args
    if ($LASTEXITCODE -ne 0) { throw "terraform $($args -join ' ') failed (exit $LASTEXITCODE)" }
}

# Resource names follow the Terraform naming convention (infra/terraform/main.tf).
$name = "$Project-$Environment"
$compact = $name -replace "-", ""
$acrName = "acr$compact"

# ------------------------------------------------------------------ 0. prerequisites
Step "Checking tools and Azure sign-in"
foreach ($tool in @("az", "terraform")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "'$tool' is not installed or not on PATH." }
}
$subscription = az account show --query "{id:id, name:name}" -o json --only-show-errors | ConvertFrom-Json
if (-not $subscription) { throw "Not signed in to Azure. Run 'az login' first." }
$env:ARM_SUBSCRIPTION_ID = $subscription.id
Write-Host "   subscription: $($subscription.name) ($($subscription.id))"
Write-Host "   environment:  $Environment  (vars: infra/terraform/$varFile)"

# ------------------------------------------------------------------ 1. state storage
if (-not $SkipBootstrap) {
    Step "Terraform state storage (idempotent)"
    & "$PSScriptRoot/bootstrap-tfstate.ps1"
}

# ------------------------------------------------------------------ 2. init
Step "terraform init (remote state, Entra auth with your az login)"
Invoke-Terraform init -input=false -reconfigure `
    "-backend-config=$backendFile" "-backend-config=use_oidc=false" "-backend-config=use_cli=true"

# ------------------------------------------------------------------ 3. first run: registry + first image
$state = terraform "-chdir=$tf" state list 2>$null
$isFirstRun = -not ($state -match '^azurerm_container_app\.api$')
$extraVars = @()

if ($isFirstRun -and -not $PlanOnly) {
    Step "First run: create the container registry on its own"
    # The Container App needs an image at creation time, and an image needs a registry to live in.
    $targetArgs = @("apply", "-input=false", "-var-file=$varFile", "-target=azurerm_container_registry.main")
    if ($AutoApprove) { $targetArgs += "-auto-approve" }
    Invoke-Terraform @targetArgs

    $acrServer = (az acr show -n $acrName --query loginServer -o tsv --only-show-errors).Trim()
    $commit = (git -C $repo rev-parse --short HEAD).Trim()
    $image = "$acrServer/$Project-api:initial-$commit"

    Step "First run: build and push $image (.NET SDK container build, no Docker)"
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
                -p:ContainerImageTag="initial-$commit" --nologo -v:minimal
        }
    }
    finally {
        Remove-Item Env:DOCKER_CONFIG
        Remove-Item $dockerConfig -Recurse -Force
    }
    $extraVars = @("-var=api_image=$image")
}
elseif ($isFirstRun) {
    Write-Host "   first run detected: the plan uses the placeholder image (no registry/image step with -PlanOnly)"
}

# ------------------------------------------------------------------ 4. plan + apply
Step "terraform plan"
Invoke-Terraform plan -input=false "-var-file=$varFile" @extraVars "-out=$planFile"

if ($PlanOnly) {
    Remove-Item (Join-Path $tf $planFile) -ErrorAction SilentlyContinue
    Write-Host "`n-PlanOnly: nothing was applied." -ForegroundColor Yellow
    return
}

if (-not $AutoApprove) {
    $answer = Read-Host "`nApply this plan to '$Environment'? Type 'yes' to continue"
    if ($answer -ne "yes") {
        Remove-Item (Join-Path $tf $planFile) -ErrorAction SilentlyContinue
        Write-Host "Cancelled. Nothing was applied." -ForegroundColor Yellow
        return
    }
}

Step "terraform apply"
try { Invoke-Terraform apply -input=false $planFile }
finally { Remove-Item (Join-Path $tf $planFile) -ErrorAction SilentlyContinue }

# ------------------------------------------------------------------ 5. outputs + optional release
Step "Outputs"
Invoke-Terraform output

if ($Deploy) {
    Step "First release: scripts/deploy.ps1 -Environment $Environment"
    & "$PSScriptRoot/deploy.ps1" -Environment $Environment
}
elseif ($isFirstRun) {
    Write-Host "`nInfrastructure is ready. Next: ./scripts/deploy.ps1 -Environment $Environment (migrations, API revision, SPA)." -ForegroundColor Green
}
