# terraform init -backend-config=environments/prod.backend.hcl
resource_group_name  = "rg-iplstore-tfstate"
storage_account_name = "stiplstoretfstate"
container_name       = "tfstate"
key                  = "iplstore-prod.tfstate"
use_oidc             = true # CI signs in with GitHub OIDC. Locally: -backend-config="use_oidc=false" -backend-config="use_cli=true"
use_azuread_auth     = true # state is read/written with Entra ID + RBAC, never storage account keys
