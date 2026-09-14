# The React front end, on Azure Static Web Apps.
#
# Standard rather than Free, because Free cannot link a backend - and the linked backend is the
# whole point here. It puts the API on the same origin as the SPA at /api, which is exactly what
# Vite's proxy does in development and what PublishAsStaticWebsite("/api", api) does on an Aspire
# publish. Same-origin means no CORS entry, no absolute API address baked into the bundle, and
# traceparent flowing from browser spans into the API's.
#
# A scaffold has one front end, never two, so this file and frontend.blazor-web.tf never coexist -
# which matters, because a Container App can be the linked backend of exactly one Static Web App.

resource "azurerm_static_web_app" "react" {
  name                = "stapp-${local.base_name}-react-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  sku_tier = "Standard"
  sku_size = "Standard"

  tags = local.tags
}

# azapi rather than azurerm: the azurerm provider models a Static Web App's Function App
# registration but not its linked backends, so this is written against the ARM resource directly.
resource "azapi_resource" "react_linked_backend" {
  type      = "Microsoft.Web/staticSites/linkedBackends@2023-12-01"
  name      = "api"
  parent_id = azurerm_static_web_app.react.id

  body = {
    properties = {
      backendResourceId = azurerm_container_app.api.id
      region            = azurerm_resource_group.main.location
    }
  }
}

output "react_url" {
  value = "https://${azurerm_static_web_app.react.default_host_name}"
}

output "react_api_key" {
  description = "Deployment token for `swa deploy`. The pipeline reads it from here rather than from a secret someone pasted."
  value       = azurerm_static_web_app.react.api_key
  sensitive   = true
}
