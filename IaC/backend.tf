# State.
#
# Local by default, so `terraform init` in a freshly scaffolded project needs nothing at all. That
# is right for the first ten minutes and wrong the moment a second person applies: local state
# cannot lock, and two concurrent applies will corrupt it.
#
# Before anyone else touches this, run the bootstrap module once to create the state account:
#
#   cd bootstrap
#   terraform init && terraform apply
#   terraform output -raw storage_account_name
#
# then uncomment the block below, fill in that name, and run:
#
#   terraform init -migrate-state
#
# Keep terraform.tfstate out of git either way - .gitignore already does.

# terraform {
#   backend "azurerm" {
#     resource_group_name  = "rg-tfstate"
#     storage_account_name = "REPLACE_ME"
#     container_name       = "tfstate"
#     key                  = "dev.tfstate"
#     use_azuread_auth     = true
#   }
# }
