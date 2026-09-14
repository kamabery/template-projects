# The Aspire dashboard has no Azure equivalent, and this is the closest thing: the services already
# emit OpenTelemetry through AddServiceDefaults, and Application Insights ingests it unchanged.
# Nothing in the application changes - the connection string arrives as an environment variable and
# the Azure Monitor exporter is not needed for traces to land, because the Container Apps
# environment forwards them.

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.base_name}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.base_name}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}
