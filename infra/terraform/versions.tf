terraform {
  required_version = ">= 1.6.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.10"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Remote state in Azure Storage (with blob-lease locking so two pipelines can't apply at once).
  # Values are supplied per environment:  terraform init -backend-config=environments/<env>.backend.hcl
  backend "azurerm" {}
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
  # Authenticated by the pipeline through OIDC (ARM_USE_OIDC=true), no stored secrets.
}
