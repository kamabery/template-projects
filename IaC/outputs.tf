output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "acr_login_server" {
  description = "Push images here. `az acr build -r <this, without the domain> ...`"
  value       = azurerm_container_registry.main.login_server
}

output "acr_name" {
  value = azurerm_container_registry.main.name
}

output "key_vault_name" {
  value = local.key_vault_output_name
}

output "api_url" {
  value = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "api_docs_url" {
  description = "Swagger UI. Not gated on Development, so it works here."
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}/api-docs"
}

output "appdb_connection_secret_name" {
  description = "Read it with `az keyvault secret show` to run `dotnet ef database update` against the deployed store."
  value       = module.database.appdb_connection_secret_name
}

output "container_app_environment_default_domain" {
  value = local.container_app_environment_domain
}

output "database_provider" {
  description = "postgres, sqlserver or sqlite. The deploy pipeline branches on it, because applying a schema differs per provider."
  value       = module.database.provider_name
}

output "database_server_name" {
  description = "Null on the SQLite branch."
  value       = module.database.server_name
}

output "database_server_resource_group" {
  description = "Where the server lives: the platform's group, on the platform. Null on the SQLite branch."
  value       = module.database.server_resource_group
}

output "database_server_fqdn" {
  description = "Null on the SQLite branch."
  value       = module.database.server_fqdn
}

output "database_role" {
  description = "The login a preview on the platform uses. The workflow creates it (HCP's runners cannot reach the server). Null with a server of its own."
  value       = module.database.role_name
}

output "database_names" {
  description = "The preview's databases, space separated, for the workflow to hand to database_role. Null with a server of its own."
  value       = module.database.database_names
}

output "database_storage_account_name" {
  description = "Null except on the SQLite branch."
  value       = module.database.storage_account_name
}

output "application_insights_app_id" {
  description = "What `az monitor app-insights query --app` takes."
  value       = azurerm_application_insights.main.app_id
}

output "application_insights_name" {
  value = azurerm_application_insights.main.name
}

output "telemetry_workbook_url" {
  description = "The environment's telemetry Workbook in the portal."
  value       = "https://portal.azure.com/#resource${azurerm_application_insights_workbook.main.id}/workbook"
}

output "trace_page_path" {
  description = "The resource page. Loading it makes the read the factory's telemetry check traces: page load, fetch, API request, database call."
  value       = "/widgets"
}
