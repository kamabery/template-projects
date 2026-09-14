# Creates the storage account that holds Terraform state for the root module next door.
#
# Its own root module with its own local state, because of the obvious chicken and egg: the thing
# that stores state cannot store its own. Run it once per subscription, commit nothing, and keep
# the handful of bytes of local state it produces - or throw them away and import if you ever need
# to change something here.
#
#   terraform init && terraform apply
#   terraform output -raw storage_account_name
#
# Then uncomment and fill in the backend block in ../backend.tf and run
# `terraform init -migrate-state` in the parent directory.

terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.14"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

resource "azurerm_resource_group" "state" {
  name     = var.resource_group_name
  location = var.location
}

resource "azurerm_storage_account" "state" {
  name                = "sttfstate${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.state.name
  location            = azurerm_resource_group.state.location

  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  # State contains generated passwords. Nothing about it should be reachable anonymously, and it
  # should be recoverable from a mistake.
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false

  blob_properties {
    versioning_enabled = true

    delete_retention_policy {
      days = 30
    }
  }
}

resource "azurerm_storage_container" "state" {
  name                  = "tfstate"
  storage_account_id    = azurerm_storage_account.state.id
  container_access_type = "private"
}

# shared_access_key_enabled is off above, so the backend authenticates as you rather than with an
# account key - which is why backend.tf sets use_azuread_auth = true.
resource "azurerm_role_assignment" "state_contributor" {
  scope                = azurerm_storage_account.state.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = data.azurerm_client_config.current.object_id
}

data "azurerm_client_config" "current" {}
