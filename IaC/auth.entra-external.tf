# Sign-in, in Azure: Entra External ID standing in for the Keycloak the AppHost runs locally.
#
# The API and both front ends do not know which provider they talk to. They read Auth:* - the API
# from its environment (the env blocks this add-on inserts into containerapp.api.tf), the front
# ends from the API's GET /auth/config. So the whole difference between local and Azure is here.
#
# Per environment (dev, staging, each j<NNN> preview) this creates, in the External tenant:
#   - one app registration: a public SPA client whose redirect URIs are the front end's, which also
#     exposes the "access" scope its own access tokens are issued for (aud = its client id)
#   - its link to the tenant's sign-in user flow
#   - one QA user (email + generated password), the password kept in this environment's Key Vault
# All of it is destroyed with the environment.
#
# Made once, by hand, and passed in through the HCP project's variable set (see RESOURCE-MAP.md):
#   - the External tenant itself, and a sign-in user flow in it (Terraform cannot create a tenant)
#   - an app registration there that HCP signs in as, with Application.ReadWrite.All,
#     User.ReadWrite.All and EventListener.ReadWrite.All, and a federated credential for HCP
#   - TFC_AZURE_PROVIDER_AUTH_EXTERNAL / TFC_AZURE_RUN_CLIENT_ID_EXTERNAL on the variable set, which
#     is what makes HCP hand this configuration the "EXTERNAL" credentials below

variable "entra_external_tenant_id" {
  description = "The Entra External ID tenant's id."
  type        = string
  default     = null
}

variable "entra_external_subdomain" {
  description = "The tenant's subdomain - the <name> in <name>.onmicrosoft.com and <name>.ciamlogin.com."
  type        = string
  default     = null
}

variable "entra_external_user_flow_id" {
  description = "Id of the sign-in user flow (an authenticationEventsFlow) each environment's app is added to."
  type        = string
  default     = null
}

# var.tfc_azure_dynamic_credentials is declared in providers.tf.
locals {
  external_credentials = try(var.tfc_azure_dynamic_credentials.aliases["EXTERNAL"], null)

  auth_authority = "https://${coalesce(var.entra_external_subdomain, "unset")}.ciamlogin.com/${coalesce(var.entra_external_tenant_id, "unset")}/v2.0"
  auth_client_id = azuread_application.spa.client_id
  auth_scope     = "openid profile offline_access api://${azuread_application.spa.client_id}/access"

  graph_app_id = "00000003-0000-0000-c000-000000000000"
}

provider "azuread" {
  tenant_id            = var.entra_external_tenant_id
  use_oidc             = true
  client_id_file_path  = try(local.external_credentials.client_id_file_path, null)
  oidc_token_file_path = try(local.external_credentials.oidc_token_file_path, null)
}

provider "msgraph" {
  tenant_id            = var.entra_external_tenant_id
  use_oidc             = true
  client_id_file_path  = try(local.external_credentials.client_id_file_path, null)
  oidc_token_file_path = try(local.external_credentials.oidc_token_file_path, null)
}

# The External tenant is required only for an apply; an offline test plans without it.
resource "terraform_data" "entra_external_configured" {
  lifecycle {
    precondition {
      condition     = var.entra_external_tenant_id != null && var.entra_external_subdomain != null && var.entra_external_user_flow_id != null
      error_message = "Set entra_external_tenant_id, entra_external_subdomain and entra_external_user_flow_id on the HCP project's variable set."
    }
  }
}

resource "random_uuid" "access_scope" {}

data "azuread_service_principal" "graph" {
  client_id = local.graph_app_id
}

# The SPA redirect URIs are not set here: they are the front end's, and which front end this
# scaffold has is not knowable from this file. auth.redirects.<front end>.tf owns them.
resource "azuread_application" "spa" {
  display_name     = "${local.base_name}-${var.environment}"
  sign_in_audience = "AzureADMyOrg"

  api {
    requested_access_token_version = 2

    oauth2_permission_scope {
      id                         = random_uuid.access_scope.result
      value                      = "access"
      type                       = "User"
      enabled                    = true
      admin_consent_display_name = "Call the ${local.base_name} API"
      admin_consent_description  = "Call the ${local.base_name} API as the signed-in user."
      user_consent_display_name  = "Call the ${local.base_name} API"
      user_consent_description   = "Call the ${local.base_name} API as you."
    }
  }

  required_resource_access {
    resource_app_id = local.graph_app_id

    dynamic "resource_access" {
      for_each = ["openid", "profile", "offline_access"]
      content {
        id   = data.azuread_service_principal.graph.oauth2_permission_scope_ids[resource_access.value]
        type = "Scope"
      }
    }
  }

  lifecycle {
    ignore_changes = [single_page_application]
  }

  depends_on = [terraform_data.entra_external_configured]
}

resource "azuread_application_identifier_uri" "spa" {
  application_id = azuread_application.spa.id
  identifier_uri = "api://${azuread_application.spa.client_id}"
}

resource "azuread_service_principal" "spa" {
  client_id = azuread_application.spa.client_id
}

# Consent granted here, once, so a QA user is never asked to consent to the app's own scope.
resource "azuread_service_principal_delegated_permission_grant" "graph" {
  service_principal_object_id          = azuread_service_principal.spa.object_id
  resource_service_principal_object_id = data.azuread_service_principal.graph.object_id
  claim_values                         = ["openid", "profile", "offline_access"]
}

resource "azuread_service_principal_delegated_permission_grant" "self" {
  service_principal_object_id          = azuread_service_principal.spa.object_id
  resource_service_principal_object_id = azuread_service_principal.spa.object_id
  claim_values                         = ["access"]
}

# Graph removes the app from the flow when the app is deleted, so there is nothing to undo here.
resource "msgraph_resource_action" "user_flow_link" {
  resource_url = "identity/authenticationEventsFlows/${coalesce(var.entra_external_user_flow_id, "unset")}/conditions/applications/includeApplications"
  method       = "POST"
  body = {
    "@odata.type" = "#microsoft.graph.authenticationConditionApplication"
    appId         = azuread_application.spa.client_id
  }
}

resource "random_password" "qa" {
  length  = 24
  special = true
}

# A local account that signs in with email and password through the user flow. azuread_user cannot
# express that identity, which is why this one resource talks to Graph directly.
resource "msgraph_resource" "qa_user" {
  url = "users"
  body = {
    accountEnabled   = true
    displayName      = "QA ${var.environment}"
    passwordPolicies = "DisablePasswordExpiration"
    passwordProfile = {
      forceChangePasswordNextSignIn = false
      password                      = random_password.qa.result
    }
    identities = [{
      signInType       = "emailAddress"
      issuer           = "${coalesce(var.entra_external_subdomain, "unset")}.onmicrosoft.com"
      issuerAssignedId = "qa-${var.environment}@${local.base_name}.test"
    }]
  }

  depends_on = [terraform_data.entra_external_configured]
}

resource "azurerm_key_vault_secret" "qa_password" {
  name         = "qa-password"
  value        = random_password.qa.result
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.kv_secrets_officer]
}

output "auth_authority" {
  value = local.auth_authority
}

output "qa_user" {
  description = "Sign in as this on the front end. The password is qa_password, and Key Vault's qa-password."
  value       = "qa-${var.environment}@${local.base_name}.test"
}

output "qa_password" {
  value     = random_password.qa.result
  sensitive = true
}
