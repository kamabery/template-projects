output "provider_name" {
  description = "The value the API reads as Database:Provider. It must agree with the EF provider the scaffold was generated with."
  value       = "sqlserver"
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

# A server, not a file: nothing to mount and nothing that limits replica count.
output "volumes" {
  value = []
}

output "volume_mounts" {
  value = []
}

output "single_writer" {
  value = false
}

output "server_name" {
  description = "The database server. Null on the SQLite branch, which has no server - the deploy pipeline branches on provider_name."
  value       = azurerm_mssql_server.main.name
}

output "storage_account_name" {
  description = "Null: only the SQLite branch keeps its store on a file share."
  value       = null
}

output "role_name" {
  description = "Null: only a preview on the shared Postgres server has a login of its own."
  value       = null
}

output "database_names" {
  value = null
}

output "server_resource_group" {
  value = var.resource_group_name
}

output "server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}
