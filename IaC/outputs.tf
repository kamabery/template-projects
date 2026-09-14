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
  value = azurerm_key_vault.main.name
}

output "api_url" {
  value = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "api_docs_url" {
  description = "Swagger UI. Unlike /health it is not gated on Development, so it works here."
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}/api-docs"
}

output "appdb_connection_secret_name" {
  description = "Read it with `az keyvault secret show` to run `dotnet ef database update` against the deployed store."
  value       = module.database.appdb_connection_secret_name
}

output "container_app_environment_default_domain" {
  value = azurerm_container_app_environment.main.default_domain
}

output "database_provider" {
  description = "postgres, sqlserver or sqlite. The deploy pipeline branches on it, because applying a schema differs per provider."
  value       = module.database.provider_name
}

output "database_server_name" {
  description = "Null on the SQLite branch."
  value       = module.database.server_name
}

output "database_storage_account_name" {
  description = "Null except on the SQLite branch."
  value       = module.database.storage_account_name
}
