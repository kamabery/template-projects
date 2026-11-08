# Offline, every provider mocked. Ships with the events add-on only.
#
#   terraform init -backend=false
#   terraform test -var-file=envs/ephemeral.tfvars
#
# The events service on the factory's shared platform: its container app shares an environment with
# every other preview's, so its name has to carry the preview's id.

mock_provider "azurerm" {
  mock_data "azurerm_client_config" {
    defaults = {
      tenant_id = "00000000-0000-0000-0000-000000000000"
      object_id = "00000000-0000-0000-0000-000000000000"
    }
  }
}

mock_provider "azapi" {}

# For a scaffold that also has the auth add-on.
mock_provider "azuread" {
  mock_data "azuread_service_principal" {
    defaults = {
      object_id = "00000000-0000-0000-0000-000000000000"
      oauth2_permission_scope_ids = {
        openid         = "00000000-0000-0000-0000-000000000001"
        profile        = "00000000-0000-0000-0000-000000000002"
        offline_access = "00000000-0000-0000-0000-000000000003"
      }
    }
  }
}

mock_provider "msgraph" {}

variables {
  project_name   = "shelf"
  environment    = "j012"
  location       = "centralus"
  include_events = true

  entra_external_tenant_id    = "00000000-0000-0000-0000-000000000000"
  entra_external_subdomain    = "shelfqa"
  entra_external_user_flow_id = "00000000-0000-0000-0000-000000000000"

  platform = {
    location                   = "centralus"
    environment_id             = "/subscriptions/s/resourceGroups/rg-factory-platform/providers/Microsoft.App/managedEnvironments/cae-factory-platform"
    environment_name           = "cae-factory-platform"
    environment_domain         = "calm.centralus.azurecontainerapps.io"
    log_analytics_workspace_id = "/subscriptions/s/resourceGroups/rg-factory-platform/providers/Microsoft.OperationalInsights/workspaces/log-factory-platform"
    key_vault_id               = "/subscriptions/s/resourceGroups/rg-factory-platform/providers/Microsoft.KeyVault/vaults/kv-factory-abc"
    key_vault_name             = "kv-factory-abc"
    postgres_server_id         = "/subscriptions/s/resourceGroups/rg-factory-platform/providers/Microsoft.DBforPostgreSQL/flexibleServers/psql-factory-abc"
    postgres_server_name       = "psql-factory-abc"
    postgres_server_fqdn       = "psql-factory-abc.postgres.database.azure.com"
    postgres_resource_group    = "rg-factory-platform"
  }
}

run "events_app_name_carries_the_preview_on_the_platform" {
  command = plan

  assert {
    condition     = azurerm_container_app.events.name == "ca-shelf-j012-events"
    error_message = "Container app names are unique within an environment, so a preview's carry its id."
  }
}
