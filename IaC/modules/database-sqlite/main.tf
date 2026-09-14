# SQLite - a file, not a server, exactly as it is in development.
#
# The AppHost declares no resource at all on this branch, and the point of choosing it is that
# `aspire run` needs no Docker. Deployment cannot be quite that free: a container's own filesystem
# is wiped on every revision, so the file lives on an Azure Files share mounted into the API
# instead. That is the whole of the infrastructure this branch needs - no server, no firewall, no
# credential.
#
# The cost is real and is expressed in the single_writer output below: SQLite has one writer, so
# the API is pinned to one replica. Choosing SQLite is choosing a single-instance deployment.

resource "azurerm_storage_account" "main" {
  name                = substr("st${var.short_name}${var.environment}${var.suffix}", 0, 24)
  resource_group_name = var.resource_group_name
  location            = var.location

  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  # The Container Apps environment storage below mounts the share with an access key, so this
  # cannot be disabled.
  shared_access_key_enabled = true

  tags = var.tags
}

resource "azurerm_storage_share" "data" {
  name               = "data"
  storage_account_id = azurerm_storage_account.main.id
  quota              = 5
}

resource "azurerm_container_app_environment_storage" "data" {
  name                         = "data"
  container_app_environment_id = var.container_app_environment_id
  account_name                 = azurerm_storage_account.main.name
  share_name                   = azurerm_storage_share.data.name
  access_key                   = azurerm_storage_account.main.primary_access_key
  access_mode                  = "ReadWrite"
}

# Written to Key Vault like the server providers' connection strings, even though it holds no
# credential, so that containerapp.api.tf has one shape to handle rather than two.
resource "azurerm_key_vault_secret" "appdb" {
  name         = "appdb-connection"
  key_vault_id = var.key_vault_id
  value        = "Data Source=/data/app.db"
}

# A separate file on the same share, never the appdb one - the same separation the server
# providers get from a separate database.
resource "azurerm_key_vault_secret" "eventstore" {
  count = var.include_events ? 1 : 0

  name         = "eventstore-connection"
  key_vault_id = var.key_vault_id
  value        = "Data Source=/data/eventstore.db"
}
