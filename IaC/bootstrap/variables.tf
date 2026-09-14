variable "resource_group_name" {
  type    = string
  default = "rg-tfstate"
}

variable "location" {
  type    = string
  default = "eastus2"
}

variable "subscription_id" {
  type    = string
  default = null
}
