# The Aspire dashboard has no Azure equivalent, and this is the closest thing: the services already
# emit OpenTelemetry through AddServiceDefaults, and Application Insights ingests it unchanged.
# The services export it themselves: servicedefaults turns on the Azure Monitor exporter when
# APPLICATIONINSIGHTS_CONNECTION_STRING is set, so every container app and the Function app that
# runs template code is given it (tests/platform*.tftest.hcl hold that). The Container Apps
# environment forwards nothing - no OpenTelemetry agent is configured on it, here or on the
# factory's shared platform - so a service without the connection string sends no telemetry at all.
# The browser's spans and logs arrive through the API's /api/otel relay, under the API's name.

resource "azurerm_log_analytics_workspace" "main" {
  count = local.on_platform ? 0 : 1

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
  workspace_id        = local.log_analytics_workspace_id
  application_type    = "web"
  tags                = local.tags
}
