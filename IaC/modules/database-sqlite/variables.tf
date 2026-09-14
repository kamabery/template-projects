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
