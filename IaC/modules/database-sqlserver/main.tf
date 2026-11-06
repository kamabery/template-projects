# Azure SQL Database - the deployed counterpart of the `sql` container the AppHost starts.
#
# The AppHost pins mssql/server:2025-latest in development because Polecat stores events in SQL
# Server 2025's native json column type. Azure SQL Database has that type generally available, so
# the deployed path needs no equivalent pin - which is the reason Polecat is the SQL Server event
# store rather than something that would have needed one.
#
# Public endpoint with a firewall, per the network decision recorded in IaC/README.md.

resource "random_password" "admin" {
  length           = 32
  special          = true
  override_special = "-_"
}

resource "azurerm_mssql_server" "main" {
  name                = "sql-${var.name_prefix}-${var.environment}-${var.suffix}"
  resource_group_name = var.resource_group_name
  location            = var.location

  version                       = "12.0"
  administrator_login           = "sqladmin"
  administrator_login_password  = random_password.admin.result
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  tags = var.tags
}

resource "azurerm_mssql_database" "appdb" {
  name      = "appdb"
  server_id = azurerm_mssql_server.main.id

  # The cheapest tier that is not the 2 GB Basic one. This is the line to change when a scaffold
  # becomes a real workload.
  sku_name    = "S0"
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb = 32

  tags = var.tags
}

# A second database on the same server, never the appdb one - exactly what the AppHost does in
# development. Keeping the two schemas apart is what lets either be dropped without touching the
# other.
resource "azurerm_mssql_database" "eventstore" {
  count = var.include_events ? 1 : 0

  name        = "eventstore"
  server_id   = azurerm_mssql_server.main.id
  sku_name    = "S0"
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb = 32

  tags = var.tags
}

# 0.0.0.0-0.0.0.0 is Azure SQL's "allow Azure services" rule, not a literal address. The container
# apps have no fixed outbound address without a VNet, so this is what lets them in; it admits any
# Azure resource, which is the honest cost of the public-endpoint posture.
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_firewall_rule" "clients" {
  for_each = { for r in var.allowed_ip_ranges : r.name => r }

  name             = each.value.name
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = each.value.start
  end_ip_address   = each.value.end
}

locals {
  base_connection = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;User ID=${azurerm_mssql_server.main.administrator_login};Password=${random_password.admin.result};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30"
}

resource "azurerm_key_vault_secret" "appdb" {
  name         = "${var.shared_prefix}appdb-connection"
  key_vault_id = var.key_vault_id
  value        = "${local.base_connection};Initial Catalog=${azurerm_mssql_database.appdb.name}"
}

resource "azurerm_key_vault_secret" "eventstore" {
  count = var.include_events ? 1 : 0

  name         = "${var.shared_prefix}eventstore-connection"
  key_vault_id = var.key_vault_id
  value        = "${local.base_connection};Initial Catalog=${azurerm_mssql_database.eventstore[0].name}"
}

# On the platform the vault is shared, so the app is granted its own secrets and nothing else.
resource "azurerm_role_assignment" "appdb_secret" {
  count = var.shared_prefix == "" ? 0 : 1

  scope                = azurerm_key_vault_secret.appdb.resource_versionless_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.secret_reader_principal_id
}

resource "azurerm_role_assignment" "eventstore_secret" {
  count = var.shared_prefix != "" && var.include_events ? 1 : 0

  scope                = azurerm_key_vault_secret.eventstore[0].resource_versionless_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.secret_reader_principal_id
}
