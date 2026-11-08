# State lives in HCP Terraform.
#
# The block is empty on purpose: nothing about who runs this is written into a scaffold. HCP reads
# the rest from the environment, which the deploy workflow sets and a developer sets by hand:
#
#   TF_CLOUD_ORGANIZATION       the HCP organization
#   TF_CLOUD_PROJECT            one HCP project per repository, named after it
#   TF_WORKSPACE                <repository>-<environment>, e.g. shelf-dev, shelf-j012
#   TF_TOKEN_app_terraform_io   or `terraform login`
#
# Runs execute in HCP, which holds the Azure credentials as dynamic provider credentials on the
# project's variable set. Neither a laptop nor the workflow needs an Azure secret for Terraform.
#
# To experiment with local state instead, create backend_override.tf (gitignored) containing
#
#   terraform {
#     backend "local" {}
#   }
#
# and run `terraform init`. Override files replace this block entirely.

terraform {
  cloud {}
}
