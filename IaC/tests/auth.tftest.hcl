# Offline, every provider mocked. Ships with the auth add-on only.
#
#   terraform init -backend=false
#   terraform test -var-file=envs/ephemeral.tfvars
#
# The Entra External ID side of sign-in: which issuer the API and front ends are pointed at, and
# the QA account a person signs in with on a preview.

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
  project_name   = "shelf"
  environment    = "j012"
  include_events = true

  entra_external_tenant_id    = "11111111-2222-3333-4444-555555555555"
  entra_external_subdomain    = "shelfqa"
  entra_external_user_flow_id = "66666666-7777-8888-9999-000000000000"
}

run "points_at_the_external_tenant" {
  command = plan

  assert {
    condition     = output.auth_authority == "https://shelfqa.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0"
    error_message = "The authority must be the External tenant's ciamlogin.com issuer."
  }
}

run "asks_for_the_scope_by_client_id" {
  command = plan

  # J-022: asked for as api://<client id>/access, sign-in depends on the identifier URI, and fails
  # with AADSTS500011 if anything clears it. By client id it needs no URI at all.
  override_resource {
    target          = azuread_application.spa
    override_during = plan
    values = {
      # A plan-time override mocks every other computed attribute too; the id must still parse as
      # /applications/{object id} for the resources that reference it.
      id        = "/applications/00000000-0000-0000-0000-0000000000aa"
      client_id = "00000000-0000-0000-0000-0000000000bb"
    }
  }

  assert {
    condition     = local.auth_scope == "openid profile offline_access 00000000-0000-0000-0000-0000000000bb/access"
    error_message = "Request the API scope as <client id>/access, not through the identifier URI."
  }
}

run "qa_user_is_named_for_the_environment" {
  command = plan

  assert {
    condition     = output.qa_user == "qa-j012@shelf.test"
    error_message = "Each environment gets its own QA user, named after it."
  }
}

run "qa_password_name_carries_the_preview_on_the_platform" {
  command = plan

  variables {
    location = "centralus"
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

  assert {
    condition     = azurerm_key_vault_secret.qa_password.name == "shelf-j012-qa-password"
    error_message = "Secret names are unique within a vault, so a preview's carry its id."
  }
}

run "refuses_without_the_external_tenant" {
  command = plan

  variables {
    entra_external_tenant_id = null
  }

  expect_failures = [terraform_data.entra_external_configured]
}
