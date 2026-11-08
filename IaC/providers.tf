# HCP sets this itself when dynamic credentials are on. Null outside HCP: a developer's own
# `az login`, or an offline `terraform test`.
#
# The factory's variable set carries a second, tagged configuration (EXTERNAL, for the auth add-on),
# and with more than one configuration HCP no longer configures the providers for you: each one must
# say use_oidc and read its token and client id from these paths. Without that, azurerm falls back to
# the Azure CLI, which HCP's runners do not have ("exec: az: executable file not found in $PATH").
variable "tfc_azure_dynamic_credentials" {
  description = "Set by HCP Terraform. Do not set by hand."
  type = object({
    default = object({ client_id_file_path = string, oidc_token_file_path = string })
    aliases = map(object({ client_id_file_path = string, oidc_token_file_path = string }))
  })
  default = null
}

locals {
  hcp_credentials = try(var.tfc_azure_dynamic_credentials.default, null)

  # Azure adds a "Failure Anomalies" alert rule to Application Insights minutes after it is
  # created, outside the state, and azurerm will not delete a resource group that still holds
  # anything - so every preview that lived that long failed its destroy (canary-8). A j<NNN> preview
  # is disposable by definition; dev and staging keep the guard.
  delete_unmanaged_resources = can(regex("^j[0-9]+$", var.environment))
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = !local.delete_unmanaged_resources
    }
  }

  # azurerm 4.x requires a subscription. Leave the variable null and it falls back to
  # ARM_SUBSCRIPTION_ID, or to whatever `az account show` reports - which is what a developer
  # running this by hand will want.
  subscription_id = var.subscription_id

  use_oidc             = local.hcp_credentials != null
  use_cli              = local.hcp_credentials == null
  client_id_file_path  = try(local.hcp_credentials.client_id_file_path, null)
  oidc_token_file_path = try(local.hcp_credentials.oidc_token_file_path, null)
}

provider "azapi" {
  use_oidc             = local.hcp_credentials != null
  use_cli              = local.hcp_credentials == null
  client_id_file_path  = try(local.hcp_credentials.client_id_file_path, null)
  oidc_token_file_path = try(local.hcp_credentials.oidc_token_file_path, null)
}
