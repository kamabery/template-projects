# The Blazor WebAssembly front end, on Azure Static Web Apps.
#
# Free tier and no linked backend. This host calls the API's own public address cross-origin, which
# is exactly what it does in development - it is the one client in the solution that needs CORS, and
# keeping the deployed shape identical to the development one means there is a single arrangement to
# reason about instead of two.
#
# A linked backend would work here too and would remove the second apply below, at the price of
# ApiBaseAddress becoming this app's own origin plus /api rather than the API's address - a
# different value from the one development uses, and one that cannot be verified without a
# subscription. If you make that change, change it in both places at once.
#
# TWO THINGS HAVE TO HAPPEN OUTSIDE THIS FILE, and both fail silently if they do not:
#
#  1. The API's Cors:AllowedOrigins must contain the hostname below. Terraform cannot do it in one
#     pass: containerapp.api.tf is part of the base scaffold and cannot reference a resource
#     declared in an add-on file. So apply once, put the value of the blazor_web_url output into
#     cors_allowed_origins in envs/<environment>.tfvars and apply again. The deploy workflow does
#     this automatically. Skip it and the page loads, the API logs healthy 200s, and every request
#     fails in the browser with a CORS error - the exact asymmetry WarnOnMissingCorsOrigins exists
#     to warn about.
#
#  2. wwwroot/appsettings.json in the WebAssembly host has ApiBaseAddress hard-coded to
#     http://localhost:5058. It is a static file fetched by the browser, so nothing here can inject
#     it - the deploy workflow rewrites it to the api_url output before uploading.

resource "azurerm_static_web_app" "blazor_web" {
  name                = "stapp-${local.base_name}-blazor-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  sku_tier = "Free"
  sku_size = "Free"

  tags = local.tags
}

output "blazor_web_url" {
  description = "Put this into cors_allowed_origins in envs/<environment>.tfvars and apply again."
  value       = "https://${azurerm_static_web_app.blazor_web.default_host_name}"
}

output "blazor_web_api_key" {
  description = "Deployment token for `swa deploy`."
  value       = azurerm_static_web_app.blazor_web.api_key
  sensitive   = true
}
