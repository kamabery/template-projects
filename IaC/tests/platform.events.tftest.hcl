# Offline, every provider mocked. Ships with the events add-on only, because a test file that names a
# resource Terraform cannot find fails the whole run, in every scaffold without it.
#
#   terraform init -backend=false
#   terraform test -var-file=envs/ephemeral.tfvars

mock_provider "azurerm" {
  mock_data "azurerm_client_config" {
    defaults = {
      tenant_id = "00000000-0000-0000-0000-000000000000"
      object_id = "00000000-0000-0000-0000-000000000000"
    }
  }
}

mock_provider "azapi" {}

# Only the auth add-on uses these two; mocking a provider a scaffold never configures is harmless.
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
  project_name = "shelf"
  environment  = "j012"
  location     = "centralus"

  include_events = true

  # The auth add-on refuses to plan without its External tenant. A scaffold without auth does not
  # declare these.
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

run "the_events_app_cannot_collide_on_the_platform" {
  command = plan

  assert {
    condition     = azurerm_container_app.events.name == "ca-shelf-j012-events"
    error_message = "Container app names are unique within an environment, so a preview's carry its id."
  }
}
# Every service that runs template code exports through servicedefaults, so every one needs the
# connection string - not only the API.
run "the_events_services_get_the_application_insights_connection_string" {
  command = plan

  # A known value, so the Function app's setting can be compared at plan time.
  override_resource {
    target          = azurerm_application_insights.main
    override_during = plan
    values = {
      connection_string = "InstrumentationKey=00000000-0000-0000-0000-000000000000"
    }
  }

  assert {
    condition     = contains([for env in azurerm_container_app.events.template[0].container[0].env : env.name], "APPLICATIONINSIGHTS_CONNECTION_STRING")
    error_message = "The events service exports to Application Insights only when APPLICATIONINSIGHTS_CONNECTION_STRING is set."
  }
  assert {
    condition     = azurerm_function_app_flex_consumption.consumer.site_config[0].application_insights_connection_string == azurerm_application_insights.main.connection_string
    error_message = "The Function app must report to the same Application Insights as the services it serves."
  }
}
