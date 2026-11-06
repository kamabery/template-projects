resource "azurerm_container_app_environment" "main" {
  count = local.on_platform ? 0 : 1

  name                       = local.own_environment_name
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = local.log_analytics_workspace_id
  tags                       = local.tags
}
