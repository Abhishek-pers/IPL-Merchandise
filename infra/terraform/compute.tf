# API on Azure Container Apps (serverless containers, KEDA autoscaling, revisions for blue/green).
#
# INFRA vs CODE deployment are deliberately separate:
#   - Terraform owns the SHAPE (env vars, scaling, probes, secrets, identity).
#   - The app pipeline owns the IMAGE (az containerapp update --image ...).
#   `ignore_changes` on the image below is what keeps the two from fighting.

resource "azurerm_container_registry" "main" {
  name                = substr("acr${local.compact}", 0, 50)
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = var.environment == "prod" ? "Standard" : "Basic"
  admin_enabled       = false
  tags                = local.tags
}

resource "azurerm_role_assignment" "api_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
}

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.name}"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

locals {
  # Non-secret application settings (same keys as appsettings.json, "__" = section separator).
  api_settings = {
    ASPNETCORE_ENVIRONMENT                = var.environment == "prod" ? "Production" : "Staging"
    Database__ApplyMigrationsOnStartup    = "false" # the migration JOB does it, once, before rollout
    Database__SeedDemoData                = var.environment == "prod" ? "false" : "true"
    Cors__AllowedOrigins__0               = "https://${azurerm_static_web_app.web.default_host_name}"
    Swagger__Enabled                      = var.environment == "prod" ? "false" : "true"
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.main.connection_string
  }
}

resource "azurerm_container_app" "api" {
  name                         = "ca-${local.name}-api"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Multiple" # enables blue/green traffic splitting between revisions
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.api.id
  }

  secret {
    name                = "db-primary"
    key_vault_secret_id = azurerm_key_vault_secret.db_primary.versionless_id
    identity            = azurerm_user_assigned_identity.api.id
  }

  dynamic "secret" {
    for_each = var.postgres_read_replica ? [1] : []
    content {
      name                = "db-replica"
      key_vault_secret_id = azurerm_key_vault_secret.db_replica[0].versionless_id
      identity            = azurerm_user_assigned_identity.api.id
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.api_min_replicas
    max_replicas = var.api_max_replicas

    container {
      name   = "api"
      image  = var.api_image
      cpu    = var.api_cpu
      memory = var.api_memory

      dynamic "env" {
        for_each = local.api_settings
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name        = "Database__ConnectionString"
        secret_name = "db-primary"
      }

      dynamic "env" {
        for_each = var.postgres_read_replica ? [1] : []
        content {
          name        = "Database__ReadReplicaConnectionString"
          secret_name = "db-replica"
        }
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health/live"
      }

      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health/ready"
      }
    }

    # Scale out on concurrent HTTP requests per replica.
    http_scale_rule {
      name                = "http-concurrency"
      concurrent_requests = "50"
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image, ingress[0].traffic_weight]
  }

  depends_on = [azurerm_role_assignment.api_acr_pull, azurerm_role_assignment.api_kv_reader]
}

# Same image, run to completion with --migrate-only. The CD pipeline starts it BEFORE rolling
# out a new API revision (expand/contract migrations keep old + new revisions compatible).
resource "azurerm_container_app_job" "migrator" {
  name                         = "caj-${local.name}-migrate"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  container_app_environment_id = azurerm_container_app_environment.main.id
  replica_timeout_in_seconds   = 600
  replica_retry_limit          = 2
  tags                         = local.tags

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.api.id
  }

  secret {
    name                = "db-primary"
    key_vault_secret_id = azurerm_key_vault_secret.db_primary.versionless_id
    identity            = azurerm_user_assigned_identity.api.id
  }

  template {
    container {
      name   = "migrate"
      image  = var.api_image
      cpu    = 0.5
      memory = "1Gi"
      args   = ["--migrate-only"]

      env {
        name        = "Database__ConnectionString"
        secret_name = "db-primary"
      }

      env {
        name  = "Database__SeedDemoData"
        value = var.environment == "prod" ? "false" : "true"
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [azurerm_role_assignment.api_acr_pull, azurerm_role_assignment.api_kv_reader]
}
