# -----------------------------------------------------------------------------
# One-time bootstrap: the storage account that holds Terraform's state.
# Terraform cannot create the place it stores its own state, so this runs once,
# before the first `terraform init`. Safe to re-run (every command is idempotent).
#
#   ./scripts/bootstrap-tfstate.ps1                 # uses the signed-in az account
#   ./scripts/bootstrap-tfstate.ps1 -Location southindia
# -----------------------------------------------------------------------------
param(
    [string]$ResourceGroup = "rg-iplstore-tfstate",
    [string]$StorageAccount = "stiplstoretfstate",
    [string]$Container = "tfstate",
    [string]$Location = "centralindia"
)

$ErrorActionPreference = "Stop"

function Invoke-Az {
    az @args --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "az $($args -join ' ') failed" }
}

Write-Host "Resource group $ResourceGroup"
Invoke-Az group create -n $ResourceGroup -l $Location --tags project=iplstore purpose=tfstate -o none

Write-Host "Storage account $StorageAccount (no public blobs, TLS 1.2, no shared-key access)"
Invoke-Az storage account create -n $StorageAccount -g $ResourceGroup -l $Location `
    --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 `
    --allow-blob-public-access false --allow-shared-key-access false -o none

# Versioning + soft delete: a bad apply or an accidental delete of the state file can be undone.
Invoke-Az storage account blob-service-properties update -n $StorageAccount -g $ResourceGroup `
    --enable-versioning true --enable-delete-retention true --delete-retention-days 14 -o none

# Whoever runs Terraform reads/writes state with their Entra identity (no storage keys).
$me = az ad signed-in-user show --query id -o tsv --only-show-errors
$scope = az storage account show -n $StorageAccount -g $ResourceGroup --query id -o tsv --only-show-errors
Write-Host "Granting 'Storage Blob Data Contributor' on the state account to the signed-in user"
az role assignment create --assignee-object-id $me --assignee-principal-type User `
    --role "Storage Blob Data Contributor" --scope $scope -o none --only-show-errors 2>$null

Write-Host "Container $Container"
# Role assignments take up to a minute to apply; retry until the data-plane call succeeds.
for ($i = 1; $i -le 12; $i++) {
    az storage container create -n $Container --account-name $StorageAccount --auth-mode login -o none --only-show-errors 2>$null
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 10
}
if ($LASTEXITCODE -ne 0) { throw "Could not create container $Container (role assignment not active yet?). Re-run in a minute." }

Write-Host "Done. Next: cd infra/terraform; terraform init -backend-config=environments/dev.backend.hcl"
