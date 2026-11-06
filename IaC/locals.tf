locals {
  # Azure name rules differ per service and none of them accept everything a project name might
  # contain. Strip to lowercase alphanumerics once, here, and derive every name from that.
  base_name  = lower(replace(var.project_name, "/[^A-Za-z0-9]/", ""))
  short_name = substr(local.base_name, 0, 14)
  suffix     = random_string.suffix.result

  resource_group_name = "rg-${local.base_name}-${var.environment}"

  # 5-50 chars, alphanumeric only - no hyphens.
  registry_name = substr("cr${local.short_name}${var.environment}${local.suffix}", 0, 50)

  # 3-24 chars, alphanumerics and hyphens, must start with a letter.
  key_vault_name = substr("kv-${local.short_name}-${var.environment}-${local.suffix}", 0, 24)

  container_app_environment_name = "cae-${local.base_name}-${var.environment}"
  api_app_name                   = "ca-${local.base_name}-api"
  events_app_name                = "ca-${local.base_name}-events"

  # Container Apps in one environment resolve each other on the environment's own internal domain,
  # and that name is derived from the app name rather than from the events resource. That matters:
  # the events app is declared in an add-on file (containerapp.events.tf) that a scaffold without
  # the events slice does not have, so containerapp.api.tf cannot reference it. Computing the URL
  # keeps the seam
  # open in every scaffold and costs nothing in the ones without an events service - the API simply
  # never calls it.
  events_internal_url = "https://${local.events_app_name}.internal.${azurerm_container_app_environment.main.default_domain}"

  # The first apply happens before any image has been built, so there is nothing in the registry to
  # pull. Rather than make that a documented failure, run a public placeholder until a tag is
  # supplied. The deploy workflow always supplies one.
  placeholder_image = "mcr.microsoft.com/k8se/quickstart:latest"
  use_placeholder   = var.image_tag == null

  api_image = local.use_placeholder ? local.placeholder_image : "${azurerm_container_registry.main.login_server}/${local.base_name}-api:${var.image_tag}"
  api_port  = local.use_placeholder ? 80 : 8080

  # NOT /health. It includes the database check, so a probe on it restarts a healthy container
  # whenever the database blips (and older scaffolds map it in Development only).
  # /openapi/v1.json is mapped unconditionally in Program.cs.
  api_probe_path = local.use_placeholder ? "/" : "/openapi/v1.json"

  # Declared here rather than in containerapp.events.tf so that the events add-on file is nothing
  # but resources. Unused - and harmless - in a scaffold with no events slice.
  events_image = local.use_placeholder ? local.placeholder_image : "${azurerm_container_registry.main.login_server}/${local.base_name}-events:${var.image_tag}"

  tags = merge({
    project     = var.project_name
    environment = var.environment
    managed_by  = "terraform"
  }, var.tags)
}
