# Provider and Terraform version pins.
#
# Pinned rather than floating for the same reason Directory.Packages.props pins every NuGet
# package: a scaffold has to plan the same way on a clean machine six months from now.

terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.14"
    }

    # Used only by the Static Web App linked-backend registration, which the azurerm provider does
    # not model. See frontend.react.tf.
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.2"
    }

    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}
