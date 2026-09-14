output "resource_group_name" {
  value = azurerm_resource_group.state.name
}

output "storage_account_name" {
  description = "Paste this into the backend block in ../backend.tf."
  value       = azurerm_storage_account.state.name
}

output "container_name" {
  value = azurerm_storage_container.state.name
}
