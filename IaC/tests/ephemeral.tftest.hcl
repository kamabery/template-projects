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

# Azure adds a "Failure Anomalies" alert rule to Application Insights minutes after the apply, outside
# the state, and azurerm refuses to delete a resource group that still holds anything (canary-8).
run "preview_destroy_takes_what_azure_added" {
  command = plan

  assert {
    condition     = local.delete_unmanaged_resources == true
    error_message = "A j<NNN> preview's destroy must delete the resource group with what Azure added to it."
  }
}

run "dev_keeps_the_nested_resource_guard" {
  command = plan

  variables {
    environment = "dev"
  }

  assert {
    condition     = local.delete_unmanaged_resources == false
    error_message = "dev and staging keep azurerm's refusal to delete a resource group that is not empty."
  }
}

run "production_is_refused" {
  command = plan

  variables {
    environment = "prod"
  }

  expect_failures = [terraform_data.no_production]
}

# The workflow's schema step reads the connection string as the GitHub Actions identity, whose
# subscription Contributor role does not reach a vault's data plane (canary-4: ForbiddenByRbac).
run "deployer_reads_the_secrets" {
  command = plan

  variables {
    deployer_object_id = "11111111-1111-1111-1111-111111111111"
  }

  assert {
    condition     = one(azurerm_role_assignment.kv_secrets_deployer[*].principal_id) == "11111111-1111-1111-1111-111111111111"
    error_message = "The deployer identity must get Key Vault Secrets User on the environment's own vault."
  }

  assert {
    condition     = one(azurerm_role_assignment.kv_secrets_deployer[*].role_definition_name) == "Key Vault Secrets User"
    error_message = "The deployer reads secrets; it never writes them."
  }
}

run "no_deployer_no_grant" {
  command = plan

  assert {
    condition     = length(azurerm_role_assignment.kv_secrets_deployer) == 0
    error_message = "Without deployer_object_id (a developer's own apply) there is nobody to grant."
  }
}
