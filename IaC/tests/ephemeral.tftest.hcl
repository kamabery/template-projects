# Offline: every provider is mocked, so this plans without Azure or HCP.
#
#   terraform init -backend=false
#   terraform test -var-file=envs/ephemeral.tfvars
#
# An ephemeral environment is one factory job's preview. Its name carries the job id, it is tagged
# so it can be found and swept, and it is sized to cost as little as it can while idle.

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

  # The live workspace carries every add-on at once, and the events one refuses to plan without
  # this. In a scaffold without events it only adds an eventstore database to a mock.
  include_events = true

  # Likewise the auth add-on, which refuses to plan without its External tenant. A scaffold
  # without auth does not declare these.
  entra_external_tenant_id    = "00000000-0000-0000-0000-000000000000"
  entra_external_subdomain    = "shelfqa"
  entra_external_user_flow_id = "00000000-0000-0000-0000-000000000000"
}

run "named_for_the_job" {
  command = plan

  assert {
    condition     = output.resource_group_name == "rg-shelf-j012"
    error_message = "An ephemeral resource group must carry the job id."
  }
}

run "tagged_ephemeral" {
  command = plan

  assert {
    condition     = lookup(azurerm_resource_group.main.tags, "ephemeral", "") == "true"
    error_message = "Ephemeral environments must be tagged ephemeral=true so a sweep can find them."
  }
}

run "scales_to_zero" {
  command = plan

  assert {
    condition     = var.api_min_replicas == 0 && var.api_max_replicas == 1
    error_message = "An ephemeral API scales to zero and never past one replica."
  }
}

run "production_is_refused" {
  command = plan

  variables {
    environment = "prod"
  }

  expect_failures = [terraform_data.no_production]
}
