# Offline: every provider is mocked.
#
#   terraform init -backend=false
#   terraform test -var-file=envs/ephemeral.tfvars
#
# Every environment gets an Application Insights Workbook (observability.tf), the Aspire dashboard's
# nearest twin in Azure, and publishes what the factory's telemetry check needs to find its proof:
# the App Insights app id to query and the page whose load makes a traceable read.

mock_provider "azurerm" {
  mock_data "azurerm_client_config" {
    defaults = {
      tenant_id = "00000000-0000-0000-0000-000000000000"
      object_id = "00000000-0000-0000-0000-000000000000"
    }
  }
}

mock_provider "azapi" {}

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

run "every_environment_gets_a_telemetry_workbook" {
  command = plan

  assert {
    condition     = azurerm_application_insights_workbook.main.source_id == lower(azurerm_application_insights.main.id)
    error_message = "A preview on the platform needs a Workbook over its own Application Insights; the portal matches source_id lowercased."
  }
  assert {
    condition     = azurerm_application_insights_workbook.main.resource_group_name == "rg-shelf-j012" && azurerm_application_insights_workbook.main.display_name == "shelf j012 telemetry"
    error_message = "The Workbook sits beside its Application Insights and is named for its environment."
  }
}

run "a_full_preview_gets_a_telemetry_workbook" {
  command = plan

  variables {
    platform = null
  }

  assert {
    condition     = azurerm_application_insights_workbook.main.source_id == lower(azurerm_application_insights.main.id)
    error_message = "A preview with a stack of its own needs a Workbook too."
  }
}

run "dev_gets_a_telemetry_workbook" {
  command = plan

  variables {
    environment = "dev"
  }

  assert {
    condition     = azurerm_application_insights_workbook.main.source_id == lower(azurerm_application_insights.main.id) && azurerm_application_insights_workbook.main.display_name == "shelf dev telemetry"
    error_message = "dev and staging need a Workbook too."
  }
}

# The name is a UUID the portal requires. It is pinned here as a literal: a template change that moved
# it would make every environment's next apply replace its Workbook.
run "the_workbook_name_is_stable" {
  command = plan

  assert {
    condition     = azurerm_application_insights_workbook.main.name == "a3c4d2d5-1b43-5d0a-8a37-2f5cf1bb8d4b"
    error_message = "The Workbook's name must be known at plan and the same on every apply."
  }
}

run "another_environment_gets_another_workbook_name" {
  command = plan

  variables {
    environment = "dev"
  }

  assert {
    condition     = azurerm_application_insights_workbook.main.name != "a3c4d2d5-1b43-5d0a-8a37-2f5cf1bb8d4b"
    error_message = "Each environment's Workbook needs a name of its own."
  }
}

run "the_workbook_has_its_four_sections" {
  command = plan

  assert {
    condition = anytrue([
      for item in jsondecode(azurerm_application_insights_workbook.main.data_json).items :
      item.type == 9 && anytrue([for p in item.content.parameters : p.name == "TimeRange" && p.type == 4])
    ])
    error_message = "The Workbook needs a TimeRange parameter for its queries to share."
  }
  assert {
    condition = length(setsubtract(
      ["transactions", "failures", "dependencies", "browser-logs"],
      [for item in jsondecode(azurerm_application_insights_workbook.main.data_json).items : item.name if item.type == 3]
    )) == 0
    error_message = "The Workbook needs its four query sections: transactions, failures, dependencies and browser-logs."
  }
  assert {
    condition = anytrue([
      for item in jsondecode(azurerm_application_insights_workbook.main.data_json).items :
      item.name == "transactions" && strcontains(item.content.query, "operation_Id") && strcontains(item.content.query, "relay.source")
    ])
    error_message = "The transactions query must group by operation_Id and find the relayed browser spans by relay.source."
  }
}

run "the_proof_outputs_are_set" {
  command = plan

  assert {
    condition     = output.application_insights_app_id != null && output.application_insights_name == "appi-shelf-j012" && output.telemetry_workbook_url != null
    error_message = "The factory's telemetry check needs the app id to query and the Workbook to link."
  }
  assert {
    condition     = startswith(output.trace_page_path, "/")
    error_message = "trace_page_path is a path the check appends to the frontend URL."
  }
}
