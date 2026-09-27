output "resource_group" {
  value = azurerm_resource_group.main.name
}

output "acr_login_server" {
  value = azurerm_container_registry.main.login_server
}

output "api_app_name" {
  value = azurerm_container_app.api.name
}

output "api_url" {
  value = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "migration_job_name" {
  value = azurerm_container_app_job.migrator.name
}

output "web_url" {
  value = "https://${azurerm_static_web_app.web.default_host_name}"
}

output "static_web_app_deployment_token" {
  value     = azurerm_static_web_app.web.api_key
  sensitive = true
}

output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.main.fqdn
}
