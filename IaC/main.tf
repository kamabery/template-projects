data "azurerm_client_config" "current" {}

# Registry and key vault names are globally unique. A short random suffix is cheaper than making
# the caller invent one, and it is held in state so it is stable across applies.
resource "random_string" "suffix" {
  length  = 5
  special = false
  upper   = false
}

resource "azurerm_resource_group" "main" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags
}
