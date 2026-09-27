# Key Vault holds the DB connection strings. Container Apps reference them by URI through the
# managed identity, so no secret ever appears in pipeline variables or app settings.

resource "azurerm_key_vault" "main" {
  name                       = substr("kv-${local.compact}", 0, 24)
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  enable_rbac_authorization  = true
  purge_protection_enabled   = var.environment == "prod"
  soft_delete_retention_days = 7
  tags                       = local.tags
}

# The identity running Terraform writes secrets...
resource "azurerm_role_assignment" "deployer_kv_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# ...the API identity can only read them.
resource "azurerm_role_assignment" "api_kv_reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
}

resource "azurerm_key_vault_secret" "db_primary" {
  name         = "db-connection-primary"
  value        = local.postgres_connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.deployer_kv_officer]
}

resource "azurerm_key_vault_secret" "db_replica" {
  count        = var.postgres_read_replica ? 1 : 0
  name         = "db-connection-replica"
  value        = local.postgres_replica_connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.deployer_kv_officer]
}
