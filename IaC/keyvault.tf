# Where database connection strings live. The container apps reference them by secret URI and
# resolve them with the managed identity, so no credential is ever written into a container app
# secret value - and therefore none is written into Terraform state as an app setting.
#
# The generated password itself IS in state, because Terraform generated it. That is the honest
# limit of this arrangement, and the reason state lives in HCP Terraform, encrypted at rest and
# readable only by the workspace's team (see backend.tf).

resource "azurerm_key_vault" "main" {
  name                = local.key_vault_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  # RBAC rather than access policies: the role assignments below are then the only place
  # permissions are expressed, instead of being split across two models.
  rbac_authorization_enabled = true

  purge_protection_enabled   = false
  soft_delete_retention_days = 7

  tags = local.tags
}

# The workloads read secrets.
resource "azurerm_role_assignment" "kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

# Whoever is running Terraform writes them. Without this the very first apply fails on the database
# module's secret with a 403, which reads as a Key Vault bug rather than a missing role.
resource "azurerm_role_assignment" "kv_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}
