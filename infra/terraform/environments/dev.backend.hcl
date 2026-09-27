# terraform init -backend-config=environments/dev.backend.hcl
resource_group_name  = "rg-iplstore-tfstate"
storage_account_name = "stiplstoretfstate"
container_name       = "tfstate"
key                  = "iplstore-dev.tfstate"
use_oidc             = true
