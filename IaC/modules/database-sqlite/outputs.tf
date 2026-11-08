output "provider_name" {
  description = "The value the API reads as Database:Provider. It must agree with the EF provider the scaffold was generated with."
  value       = "sqlite"
}

output "appdb_connection_secret_id" {
  value = azurerm_key_vault_secret.appdb.versionless_id
}

output "appdb_connection_secret_name" {
  value = azurerm_key_vault_secret.appdb.name
}

output "eventstore_connection_secret_id" {
  value = var.include_events ? azurerm_key_vault_secret.eventstore[0].versionless_id : null
}

output "eventstore_connection_secret_name" {
  value = var.include_events ? azurerm_key_vault_secret.eventstore[0].name : null
}

output "volumes" {
  value = [{
    name         = "data"
    storage_name = azurerm_container_app_environment_storage.data.name
    storage_type = "AzureFile"
  }]
}

output "volume_mounts" {
  value = [{
    name = "data"
    path = "/data"
  }]
}

# SQLite has exactly one writer, so the API cannot scale out. containerapp.api.tf reads this rather
# than branching on the provider name itself.
output "single_writer" {
  value = true
}

output "server_name" {
  description = "Null: SQLite has no server. The storage account below is what the deploy pipeline needs instead."
  value       = null
}

output "storage_account_name" {
  description = "Where the SQLite file lives. The deploy pipeline seeds the schema by uploading a locally-migrated file to this account's `data` share."
  value       = azurerm_storage_account.main.name
}

output "role_name" {
  description = "Null: only a preview on the shared Postgres server has a login of its own."
  value       = null
}

output "database_names" {
  value = null
}

output "server_resource_group" {
  value = null
}

output "server_fqdn" {
  value = null
}
