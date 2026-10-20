project_name = "template"
location     = "eastus2"

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
