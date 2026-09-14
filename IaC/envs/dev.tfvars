project_name = "template"
environment  = "dev"
location     = "eastus2"

# Cheap and slow-to-wake. Set api_min_replicas = 1 to remove the cold start.
api_min_replicas = 0
api_max_replicas = 2

# Set to true when the event-sourcing slice was scaffolded. It is what creates the eventstore
# database beside appdb.
include_events = false

# Only the Blazor WebAssembly front end needs this, and it can only be filled in after the first
# apply has created the Static Web App. Take the value from `terraform output blazor_web_url`.
cors_allowed_origins = []

# Your own IP, so `dotnet ef database update` can reach the database. The container apps are
# already covered by the AllowAzureServices rule and do not need an entry here.
# db_allowed_ip_ranges = [
#   { name = "office", start = "203.0.113.4", end = "203.0.113.4" },
# ]
