variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "name_prefix" { type = string }
variable "short_name" { type = string }
variable "environment" { type = string }
variable "suffix" { type = string }
variable "key_vault_id" { type = string }
variable "container_app_environment_id" { type = string }
variable "container_app_environment_name" { type = string }
variable "include_events" { type = bool }
variable "tags" { type = map(string) }

variable "allowed_ip_ranges" {
  type = list(object({
    name  = string
    start = string
    end   = string
  }))
  default = []
}

variable "shared_prefix" {
  description = "Empty, or what makes this environment's names unique on the factory's shared platform."
  type        = string
  default     = ""
}

variable "secret_reader_principal_id" {
  description = "On the platform, the app's identity: it is granted each secret here, never the shared vault."
  type        = string
}

variable "platform" {
  description = "The factory's shared platform when this environment runs on it, else null. Only the Postgres module has a server to share; the others take it so database.tf can pass one set of arguments."
  type        = any
  default     = null
}
