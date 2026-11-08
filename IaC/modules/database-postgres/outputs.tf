output "provider_name" {
  description = "The value the API reads as Database:Provider. It must agree with the EF provider the scaffold was generated with."
  value       = "postgres"
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
  value       = local.server_name
}

output "server_resource_group" {
  value = local.shared ? var.platform.postgres_resource_group : var.resource_group_name
}

output "server_fqdn" {
  value = local.server_fqdn
}

output "role_name" {
  description = "The login a preview on the shared server uses, which deploy.yml creates. Null with a server of its own."
  value       = local.role_name
}

output "database_names" {
  description = "What role_name owns, space separated. Null with a server of its own."
  value       = local.shared ? join(" ", concat([azurerm_postgresql_flexible_server_database.appdb.name], var.include_events ? [azurerm_postgresql_flexible_server_database.eventstore[0].name] : [])) : null
}

output "storage_account_name" {
  description = "Null: only the SQLite branch keeps its store on a file share."
  value       = null
}
