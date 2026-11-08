# Things no tfvars file, workflow input or typo may plan.
#
# This file belongs to the pack: `aspire-addon infra adopt` overwrites it, so a scaffold made before
# a guard existed gets it without anyone editing its own variables.tf. That is why these are
# preconditions here rather than validations on the variables themselves.

# Production is out of scope: environments are dev, staging and j<NNN> previews.
resource "terraform_data" "no_production" {
  lifecycle {
    precondition {
      condition     = !contains(["prod", "production", "prd"], var.environment)
      error_message = "Production is not deployed from here."
    }
  }
}
