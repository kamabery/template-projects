# Offline: every provider is mocked.
#
#   terraform init -backend=false
#   terraform test -var-file=envs/ephemeral.tfvars
#
# A j<NNN> preview on the factory's shared platform: the Container Apps environment, Log Analytics,
# Key Vault and the database server are the platform's, because they are what makes a preview slow
# to create and destroy. Anything else keeps a stack of its own.

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

run "a_preview_uses_the_platform" {
  command = plan

  assert {
    condition     = length(azurerm_container_app_environment.main) == 0
    error_message = "A preview on the platform must not create a Container Apps environment: it is ~8 min to create and ~25 to destroy."
  }
  assert {
    condition     = azurerm_container_app.api.container_app_environment_id == var.platform.environment_id
    error_message = "The API must run in the platform's environment."
  }
  assert {
    condition     = length(azurerm_log_analytics_workspace.main) == 0 && azurerm_application_insights.main.workspace_id == var.platform.log_analytics_workspace_id
    error_message = "A preview's Application Insights must log to the platform's workspace."
  }
  assert {
    condition     = length(azurerm_key_vault.main) == 0 && output.key_vault_name == "kv-factory-abc"
    error_message = "A preview must use the platform's vault."
  }
  assert {
    condition     = length(azurerm_role_assignment.kv_secrets_user) == 0 && length(azurerm_role_assignment.kv_secrets_officer) == 0 && length(azurerm_role_assignment.kv_secrets_deployer) == 0
    error_message = "No vault-wide role per preview: it would let every preview read every other's secrets."
  }
  assert {
    # Only a Postgres scaffold uses the platform's server; SQLite and SQL Server scaffolds have none of these outputs.
    condition     = output.database_provider != "postgres" || (output.database_server_name == "psql-factory-abc" && output.database_server_resource_group == "rg-factory-platform" && output.database_server_fqdn == "psql-factory-abc.postgres.database.azure.com")
    error_message = "A preview must use the platform's Postgres server, and the workflow needs to find it."
  }
}

run "names_cannot_collide_on_the_platform" {
  command = plan

  assert {
    condition     = azurerm_container_app.api.name == "ca-shelf-j012-api"
    error_message = "Container app names are unique within an environment, so a preview's carry its id."
  }
  assert {
    condition     = output.appdb_connection_secret_name == "shelf-j012-appdb-connection"
    error_message = "Secret names are unique within a vault, so a preview's carry its id."
  }
  assert {
    condition     = output.database_provider != "postgres" || output.database_names == "shelf_j012_appdb shelf_j012_eventstore"
    error_message = "Database names are unique within a server, so a preview's carry its id."
  }
  assert {
    condition     = output.database_provider != "postgres" || output.database_role == "preview_shelf_j012"
    error_message = "Each preview gets its own login, so none can reach another's database."
  }
}

run "dev_keeps_its_own_stack" {
  command = plan

  variables {
    environment = "dev"
  }

  assert {
    condition     = length(azurerm_container_app_environment.main) == 1 && length(azurerm_log_analytics_workspace.main) == 1 && length(azurerm_key_vault.main) == 1
    error_message = "The platform is for previews: dev and staging keep their own stack."
  }
  assert {
    condition     = azurerm_container_app.api.name == "ca-shelf-api" && output.appdb_connection_secret_name == "appdb-connection"
    error_message = "A stack of its own keeps its names."
  }
  assert {
    condition     = output.database_role == null
    error_message = "A stack of its own uses its server's admin login."
  }
}

run "no_platform_is_a_full_stack" {
  command = plan

  variables {
    platform = null
  }

  assert {
    condition     = length(azurerm_container_app_environment.main) == 1 && length(azurerm_key_vault.main) == 1 && azurerm_container_app.api.name == "ca-shelf-api"
    error_message = "A subscription with no platform still deploys previews."
  }
  assert {
    condition     = length(azurerm_role_assignment.kv_secrets_user) == 1 && length(azurerm_role_assignment.kv_secrets_officer) == 1
    error_message = "A vault of its own grants its readers and writers."
  }
}

run "use_platform_false_is_a_full_stack" {
  command = plan

  # The full canary: the only thing that still proves a from-scratch create.
  variables {
    use_platform = false
  }

  assert {
    condition     = length(azurerm_container_app_environment.main) == 1 && length(azurerm_key_vault.main) == 1
    error_message = "use_platform = false must build everything."
  }
}

run "another_region_is_a_full_stack" {
  command = plan

  # A container app must be in its environment's region.
  variables {
    location = "westus2"
  }

  assert {
    condition     = length(azurerm_container_app_environment.main) == 1
    error_message = "A preview in another region cannot use the platform's environment."
  }
}

# The API exports its traces, metrics and logs to Application Insights itself (servicedefaults'
# UseAzureMonitor), and only when it is given the connection string. Nothing in the environment
# forwards telemetry for it.
run "the_api_gets_the_application_insights_connection_string" {
  command = plan

  assert {
    condition     = contains([for env in azurerm_container_app.api.template[0].container[0].env : env.name], "APPLICATIONINSIGHTS_CONNECTION_STRING")
    error_message = "The API exports to Application Insights only when APPLICATIONINSIGHTS_CONNECTION_STRING is set; without it the environment is blind."
  }
}
