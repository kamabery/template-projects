# The data store, as one module with a uniform interface.
#
# There is a directory per provider under modules/, and exactly one of them ships in a scaffold -
# the other two are pruned at instantiation, the same way the unused migrations projects are. Every
# one of them exposes the same outputs, so containerapp.api.tf reads only module.database.* and
# does not know or care which provider it got.
#
# The source line below is the single thing that differs between the three, and it is rewritten by
# the dbProvider scaffolding parameter. Changing the data store later means changing this line and
# the EF provider in the API together - a code change, not a configuration change.

module "database" {
  source = "./modules/database-postgres"

  resource_group_name            = azurerm_resource_group.main.name
  location                       = azurerm_resource_group.main.location
  name_prefix                    = local.base_name
  short_name                     = local.short_name
  environment                    = var.environment
  suffix                         = local.suffix
  key_vault_id                   = local.key_vault_id
  container_app_environment_id   = local.container_app_environment_id
  container_app_environment_name = local.container_app_environment_name
  shared_prefix                  = local.shared_prefix
  secret_reader_principal_id     = azurerm_user_assigned_identity.app.principal_id
  platform                       = local.on_platform ? var.platform : null
  include_events                 = var.include_events
  allowed_ip_ranges              = var.db_allowed_ip_ranges
  tags                           = local.tags

  # Terraform has no way to know that writing a secret needs the role assignment to have landed
  # first. Without this the first apply races and fails with a 403 from Key Vault.
  depends_on = [azurerm_role_assignment.kv_secrets_officer]
}
