# terraform init -backend-config=environments/prod.backend.hcl
resource_group_name  = "rg-iplstore-tfstate"
storage_account_name = "stiplstoretfstate"
container_name       = "tfstate"
key                  = "iplstore-prod.tfstate"
use_oidc             = true
