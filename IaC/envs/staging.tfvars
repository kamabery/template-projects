project_name = "template"
environment  = "staging"
location     = "eastus2"

api_min_replicas = 0
api_max_replicas = 2

# Set to true when the event-sourcing slice was scaffolded. It is what creates the eventstore
# database beside appdb.
include_events = false

# Only the Blazor WebAssembly front end needs this, and it can only be filled in after the first
# apply has created the Static Web App. Take the value from `terraform output blazor_web_url`.
cors_allowed_origins = []
