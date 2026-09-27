# =============================================================================
# IPL Store - Azure infrastructure (one root module, one file per concern).
#   main.tf        resource group, naming, identity, observability
#   database.tf    PostgreSQL Flexible Server (+ HA standby, + read replica)
#   compute.tf     Container Registry, Container Apps (API) + migration job
#   frontend.tf    Static Web App (React SPA)
#   secrets.tf     Key Vault + secrets
# =============================================================================

locals {
  name = "${var.project}-${var.environment}"
  # Globally-unique names can't contain dashes (ACR, Key Vault limits).
  compact = replace(local.name, "-", "")
  tags = merge(var.tags, {
    project     = var.project
    environment = var.environment
    managed_by  = "terraform"
  })
}

data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = local.tags
}

# Workload identity used by the API and the migration job: pulls images, reads secrets.
# No passwords/keys for Azure resources live in app config.
resource "azurerm_user_assigned_identity" "api" {
  name                = "id-${local.name}-api"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

# ---------------- observability
resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = var.environment == "prod" ? 90 : 30
  tags                = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}
