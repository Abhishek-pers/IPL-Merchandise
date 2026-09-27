# React SPA on Azure Static Web Apps: global CDN, free TLS, PR preview environments.
# The app pipeline uploads the build with the deployment token (output below, stored as a
# GitHub secret). Roadmap: Azure Front Door (WAF + single domain routing /api -> Container Apps).

resource "azurerm_static_web_app" "web" {
  name                = "swa-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = "eastasia" # SWA control plane region; content is served globally
  sku_tier            = var.environment == "prod" ? "Standard" : "Free"
  sku_size            = var.environment == "prod" ? "Standard" : "Free"
  tags                = local.tags
}
