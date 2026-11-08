variable "project_name" {
  description = "Short name for this project. Every resource name is derived from it. Lowercase letters and digits survive; everything else is stripped, because container registries and storage accounts allow nothing else."
  type        = string
}

variable "environment" {
  description = "Environment discriminator - dev, staging, or j<NNN> for one factory job's ephemeral preview. Part of every resource name, so keep it short."
  type        = string
  default     = "dev"

  validation {
    condition     = can(regex("^[a-z0-9]{1,8}$", var.environment))
    error_message = "environment must be 1-8 lowercase letters or digits."
  }
}

variable "location" {
  description = "Azure region. Static Web Apps are not available in every region; eastus2, westus2, centralus, westeurope and eastasia are the safe ones."
  type        = string
  default     = "centralus"
}

variable "subscription_id" {
  description = "Target subscription. Null falls back to ARM_SUBSCRIPTION_ID or the current az login."
  type        = string
  default     = null
}

variable "image_tag" {
  description = "Tag of the container images to run, normally the commit SHA the pipeline pushed. Null runs a public placeholder image instead, so the very first apply succeeds before any image exists - see locals.tf."
  type        = string
  default     = null
}

variable "api_min_replicas" {
  description = "Minimum API replicas. 0 scales to zero when idle, which is cheap and adds a cold start to the first request after a quiet period."
  type        = number
  default     = 0
}

variable "api_max_replicas" {
  description = "Maximum API replicas. Ignored on the SQLite branch, which is pinned to 1 - see containerapp.api.tf."
  type        = number
  default     = 3
}

variable "include_events" {
  description = "Whether the event-sourcing slice was scaffolded. Adds the eventstore database to the data store. Set this to true when the aspire-events add-on is present - its wiring file says so."
  type        = bool
  default     = false
}

variable "cors_allowed_origins" {
  description = "Origins written into the API's Cors:AllowedOrigins. Needed only by the Blazor WebAssembly front end, which is genuinely cross-origin; the React front end reaches the API same-origin through the Static Web App's linked backend and needs no entry here. Populated on a second apply - see frontend.blazor-web.tf."
  type        = list(string)
  default     = []
}

variable "deployer_object_id" {
  description = "Object id of the identity the deploy workflow signs in to Azure as (the factory's GitHub Actions app). It gets Key Vault Secrets User on this environment's vault, so the schema step can read the connection string. Set on the HCP variable set by setup-azure.ps1; null for a developer's own apply."
  type        = string
  default     = null
}

variable "platform" {
  description = "The factory's shared platform, which j<NNN> previews run on (see local.on_platform). Set on the HCP variable set by the factory's setup-platform.ps1; null means every environment has a stack of its own."
  type = object({
    location                   = string
    environment_id             = string
    environment_name           = string
    environment_domain         = string
    log_analytics_workspace_id = string
    key_vault_id               = string
    key_vault_name             = string
    postgres_server_id         = string
    postgres_server_name       = string
    postgres_server_fqdn       = string
    postgres_resource_group    = string
  })
  default = null
}

variable "use_platform" {
  description = "False builds the whole stack for a preview too, which is what the factory's full canary does to prove a from-scratch create."
  type        = bool
  default     = true
}

variable "db_allowed_ip_ranges" {
  description = "Extra client IPs allowed through the database firewall, as start/end pairs. The database already allows Azure services, which covers the container apps; this is for running `dotnet ef database update` from a laptop or a CI runner."
  type = list(object({
    name  = string
    start = string
    end   = string
  }))
  default = []
}

variable "log_retention_days" {
  description = "Log Analytics retention. 30 is the minimum billed rate."
  type        = number
  default     = 30
}

variable "tags" {
  description = "Extra tags merged onto every resource."
  type        = map(string)
  default     = {}
}
