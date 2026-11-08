# PostgreSQL Flexible Server - the deployed counterpart of the `pg` container the AppHost starts.
#
# Public endpoint with a firewall, per the network decision recorded in IaC/README.md. No VNet, no
# private endpoint, no private DNS zone.
#
# A preview on the factory's platform has no server here. It gets databases on the shared server,
# whose names carry its id, and a login of its own - a role this configuration cannot create, because
# HCP's runners sit outside Azure and the firewall keeps them out. deploy.yml creates it from the
# GitHub runner, which opens the firewall for the schema step anyway. See "On the platform" in
# IaC/README.md.

locals {
  shared = var.platform != null

  server_id   = local.shared ? var.platform.postgres_server_id : azurerm_postgresql_flexible_server.main[0].id
  server_name = local.shared ? var.platform.postgres_server_name : azurerm_postgresql_flexible_server.main[0].name
  server_fqdn = local.shared ? var.platform.postgres_server_fqdn : azurerm_postgresql_flexible_server.main[0].fqdn

  # Postgres identifiers, so no hyphens.
  database_prefix = replace(var.shared_prefix, "-", "_")
  role_name       = local.shared ? "preview_${trimsuffix(local.database_prefix, "_")}" : null
  login           = local.shared ? local.role_name : "pgadmin"
  password        = local.shared ? random_password.role[0].result : random_password.admin.result
}

resource "random_password" "admin" {
  length           = 32
  special          = true
  override_special = "-_"
}

# Only a preview on the platform uses it; the workflow reads it back out of the connection string to
# create the role.
resource "random_password" "role" {
  count = local.shared ? 1 : 0

  length           = 32
  special          = true
  override_special = "-_"
}

resource "azurerm_postgresql_flexible_server" "main" {
  count = local.shared ? 0 : 1

  name                = "psql-${var.name_prefix}-${var.environment}-${var.suffix}"
  resource_group_name = var.resource_group_name
  location            = var.location

  version                = "16"
  administrator_login    = "pgadmin"
  administrator_password = random_password.admin.result

  # Burstable, the cheapest tier that exists. A scaffold should cost pennies until someone decides
  # otherwise; this is the line to change when they do.
  sku_name   = "B_Standard_B1ms"
  storage_mb = 32768

  public_network_access_enabled = true
  geo_redundant_backup_enabled  = false
  backup_retention_days         = 7

  tags = var.tags

  lifecycle {
    # Changing the zone forces a rebuild of the server, and Azure assigns one if none is given -
    # so without this every apply after the first one wants to destroy the database.
    ignore_changes = [zone, high_availability[0].standby_availability_zone]
  }
}

moved {
  from = azurerm_postgresql_flexible_server.main
  to   = azurerm_postgresql_flexible_server.main[0]
}

resource "azurerm_postgresql_flexible_server_database" "appdb" {
  name      = "${local.database_prefix}appdb"
  server_id = local.server_id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# A separate database, never the appdb one. Keeping the two schemas apart is what lets either
# be dropped without touching the other - the same rule the AppHost follows in development.
resource "azurerm_postgresql_flexible_server_database" "eventstore" {
  count = var.include_events ? 1 : 0

  name      = "${local.database_prefix}eventstore"
  server_id = local.server_id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# The container apps have no fixed outbound address without a VNet, so this is the rule that lets
# them in. It admits any Azure resource in any tenant, which is the honest cost of the
# public-endpoint posture; the credential in Key Vault is what actually gates access. The platform
# has the same rule on its own server.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  count = local.shared ? 0 : 1

  name             = "AllowAzureServices"
  server_id        = local.server_id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

moved {
  from = azurerm_postgresql_flexible_server_firewall_rule.azure_services
  to   = azurerm_postgresql_flexible_server_firewall_rule.azure_services[0]
}

# For running `dotnet ef database update` from a laptop or a CI runner, neither of which is covered
# by the rule above.
resource "azurerm_postgresql_flexible_server_firewall_rule" "clients" {
  for_each = local.shared ? {} : { for r in var.allowed_ip_ranges : r.name => r }

  name             = each.value.name
  server_id        = local.server_id
  start_ip_address = each.value.start
  end_ip_address   = each.value.end
}

locals {
  base_connection = "Host=${local.server_fqdn};Port=5432;Username=${local.login};Password=${local.password};SSL Mode=Require;Trust Server Certificate=true"
}

resource "azurerm_key_vault_secret" "appdb" {
  name         = "${var.shared_prefix}appdb-connection"
  key_vault_id = var.key_vault_id
  value        = "${local.base_connection};Database=${azurerm_postgresql_flexible_server_database.appdb.name}"
}

resource "azurerm_key_vault_secret" "eventstore" {
  count = var.include_events ? 1 : 0

  name         = "${var.shared_prefix}eventstore-connection"
  key_vault_id = var.key_vault_id
  value        = "${local.base_connection};Database=${azurerm_postgresql_flexible_server_database.eventstore[0].name}"
}

# On the platform the vault is shared, so the app is granted its own secrets and nothing else.
resource "azurerm_role_assignment" "appdb_secret" {
  count = local.shared ? 1 : 0

  scope                = azurerm_key_vault_secret.appdb.resource_versionless_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.secret_reader_principal_id
}

resource "azurerm_role_assignment" "eventstore_secret" {
  count = local.shared && var.include_events ? 1 : 0

  scope                = azurerm_key_vault_secret.eventstore[0].resource_versionless_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.secret_reader_principal_id
}
