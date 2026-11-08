project_name = "template"
# eastus2 refused B_Standard_B1ms for the factory's subscription (SkuNotAvailable, J-019); centralus
# also offers Static Web Apps. dev and staging use it too.
location = "centralus"

# One factory job's preview. The workflow passes -var environment=j<NNN> (the job id), which names
# the resource group and the HCP workspace after it; nothing here names an environment.

# Idle previews cost nothing to keep the API warm: scale to zero, and never past one replica.
api_min_replicas = 0
api_max_replicas = 1

# Set to true when the event-sourcing slice was scaffolded, as in dev.tfvars.
include_events = false

log_retention_days = 30

# Tagged so a sweep can find a preview that outlived its job. HCP destroys the workspace on its
# auto-destroy timer as well; this is the belt to that pair of braces.
tags = {
  ephemeral = "true"
}
