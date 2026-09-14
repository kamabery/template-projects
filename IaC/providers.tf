provider "azurerm" {
  features {}

  # azurerm 4.x requires a subscription. Leave the variable null and it falls back to
  # ARM_SUBSCRIPTION_ID, or to whatever `az account show` reports - which is what a developer
  # running this by hand will want.
  subscription_id = var.subscription_id
}

provider "azapi" {}
