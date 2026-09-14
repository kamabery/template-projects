# PostgreSQL Flexible Server - the deployed counterpart of the `pg` container the AppHost starts.
#
# Public endpoint with a firewall, per the network decision recorded in IaC/README.md. No VNet, no
# private endpoint, no private DNS zone.

resource "random_password" "admin" {
  length           = 32
  special          = true
  override_special = "-_"
}

resource "azurerm_postgresql_flexible_server" "main" {
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

resource "azurerm_postgresql_flexible_server_database" "appdb" {
  name      = "appdb"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# A separate database, never the appdb one. Keeping the two schemas apart is what lets either
# be dropped without touching the other - the same rule the AppHost follows in development.
resource "azurerm_postgresql_flexible_server_database" "eventstore" {
  count = var.include_events ? 1 : 0

  name      = "eventstore"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# The container apps have no fixed outbound address without a VNet, so this is the rule that lets
# them in. It admits any Azure resource in any tenant, which is the honest cost of the
# public-endpoint posture; the credential in Key Vault is what actually gates access.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# For running `dotnet ef database update` from a laptop or a CI runner, neither of which is covered
# by the rule above.
resource "azurerm_postgresql_flexible_server_firewall_rule" "clients" {
  for_each = { for r in var.allowed_ip_ranges : r.name => r }

  name             = each.value.name
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = each.value.start
  end_ip_address   = each.value.end
}

locals {
  base_connection = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Port=5432;Username=${azurerm_postgresql_flexible_server.main.administrator_login};Password=${random_password.admin.result};SSL Mode=Require;Trust Server Certificate=true"
}

resource "azurerm_key_vault_secret" "appdb" {
  name         = "appdb-connection"
  key_vault_id = var.key_vault_id
  value        = "${local.base_connection};Database=${azurerm_postgresql_flexible_server_database.appdb.name}"
}

resource "azurerm_key_vault_secret" "eventstore" {
  count = var.include_events ? 1 : 0

  name         = "eventstore-connection"
  key_vault_id = var.key_vault_id
  value        = "${local.base_connection};Database=${azurerm_postgresql_flexible_server_database.eventstore[0].name}"
}
