# PostgreSQL Flexible Server 16.
#   dev : Burstable SKU, single zone.
#   prod: General Purpose, zone-redundant HA (sync standby in another AZ, automatic failover),
#         geo-redundant backups, optional async read replica for catalogue reads.
# See docs/06-distributed-database.md for the scaling path (replicas -> Citus sharding).

resource "random_password" "postgres_admin" {
  length  = 32
  special = false
}

resource "azurerm_postgresql_flexible_server" "main" {
  name                          = "psql-${local.name}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "16"
  sku_name                      = var.postgres_sku
  storage_mb                    = var.postgres_storage_mb
  auto_grow_enabled             = true
  backup_retention_days         = var.postgres_backup_retention_days
  geo_redundant_backup_enabled  = var.postgres_geo_redundant_backup
  administrator_login           = "ipladmin"
  administrator_password        = random_password.postgres_admin.result
  public_network_access_enabled = true # roadmap: VNet integration + private endpoint (docs/07)
  zone                          = "1"

  # People sign in with their Entra ID account (no shared password). Password auth stays on
  # only because the API still uses the ipladmin connection string; next step is moving the
  # API to its managed identity and turning password auth off.
  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = true
    tenant_id                     = data.azurerm_client_config.current.tenant_id
  }

  dynamic "high_availability" {
    for_each = var.postgres_high_availability ? [1] : []
    content {
      mode                      = "ZoneRedundant"
      standby_availability_zone = "2"
    }
  }

  tags = local.tags

  lifecycle {
    # Azure may move the primary to the standby zone after a failover; don't fight it.
    ignore_changes = [zone, high_availability[0].standby_availability_zone]
  }
}

# Entra ID administrators of the server (people or groups). Supplied per environment through
# a git-ignored *.auto.tfvars file so personal identities are not committed.
resource "azurerm_postgresql_flexible_server_active_directory_administrator" "admins" {
  for_each            = var.postgres_entra_admins
  server_name         = azurerm_postgresql_flexible_server.main.name
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  object_id           = each.key
  principal_name      = each.value.principal_name
  principal_type      = each.value.principal_type
}

resource "azurerm_postgresql_flexible_server_database" "store" {
  name      = "iplstore"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# pg_trgm powers the catalogue search index (database/migrations/V001). Extensions must be
# allow-listed on Azure before CREATE EXTENSION works.
resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "PG_TRGM"
}

# Allow Azure-hosted services (Container Apps) to connect. Tighten with private networking in prod.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_postgresql_flexible_server" "replica" {
  count               = var.postgres_read_replica ? 1 : 0
  name                = "psql-${local.name}-replica"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  create_mode         = "Replica"
  source_server_id    = azurerm_postgresql_flexible_server.main.id
  version             = "16"
  sku_name            = var.postgres_sku
  storage_mb          = var.postgres_storage_mb
  zone                = "3"
  tags                = local.tags

  lifecycle {
    ignore_changes = [zone]
  }
}

locals {
  postgres_connection_string = join(";", [
    "Host=${azurerm_postgresql_flexible_server.main.fqdn}",
    "Port=5432",
    "Database=${azurerm_postgresql_flexible_server_database.store.name}",
    "Username=${azurerm_postgresql_flexible_server.main.administrator_login}",
    "Password=${random_password.postgres_admin.result}",
    "SSL Mode=Require",
    "Maximum Pool Size=50",
  ])

  postgres_replica_connection_string = var.postgres_read_replica ? join(";", [
    "Host=${azurerm_postgresql_flexible_server.replica[0].fqdn}",
    "Port=5432",
    "Database=${azurerm_postgresql_flexible_server_database.store.name}",
    "Username=${azurerm_postgresql_flexible_server.main.administrator_login}",
    "Password=${random_password.postgres_admin.result}",
    "SSL Mode=Require",
    "Maximum Pool Size=50",
  ]) : ""
}
