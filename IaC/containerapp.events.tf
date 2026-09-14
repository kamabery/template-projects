variable "events_transport" {
  description = "How the events service publishes its integration event. \"local\" uses Wolverine's durable local queues, backed by the event store's own outbox tables, and needs no broker. \"azureservicebus\" uses the namespace in servicebus.tf - see the note there about which event stores can actually read this."
  type        = string
  default     = "local"

  validation {
    condition     = contains(["local", "azureservicebus"], var.events_transport)
    error_message = "events_transport must be \"local\" or \"azureservicebus\"."
  }
}

variable "events_max_replicas" {
  description = "Maximum events replicas. The projection daemon runs HotCold - one node holds the lease - so scaling out is safe. Ignored on the SQLite branch, which is pinned to 1."
  type        = number
  default     = 2
}

resource "azurerm_container_app" "events" {
  name                         = local.events_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.app.id
  }

  secret {
    name                = "eventstore-connection"
    key_vault_secret_id = module.database.eventstore_connection_secret_id
    identity            = azurerm_user_assigned_identity.app.id
  }

  # Internal, not external. Nothing outside the environment calls this service directly; the API
  # reaches it at local.events_internal_url. Flip external_enabled to true if the orders endpoints
  # should be public.
  ingress {
    external_enabled           = false
    target_port                = local.api_port
    transport                  = "auto"
    allow_insecure_connections = false

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    # Not zero. The projection daemon and the outbox both need a running process: scaling to zero
    # stops projections advancing and leaves committed-but-undispatched messages sitting until
    # something else wakes the service up.
    min_replicas = 1
    max_replicas = module.database.single_writer ? 1 : var.events_max_replicas

    dynamic "volume" {
      for_each = module.database.volumes

      content {
        name         = volume.value.name
        storage_name = volume.value.storage_name
        storage_type = volume.value.storage_type
      }
    }

    container {
      name   = "events"
      image  = local.events_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name        = "ConnectionStrings__eventstore"
        secret_name = "eventstore-connection"
      }

      env {
        name  = "Events__Transport"
        value = var.events_transport
      }

      # The seam for moving off the emulator. The service authenticates with the same managed
      # identity, so there is no connection string to carry.
      env {
        name  = "AzureServiceBus__FullyQualifiedNamespace"
        value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
      }

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app.client_id
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.main.connection_string
      }

      dynamic "volume_mounts" {
        for_each = module.database.volume_mounts

        content {
          name = volume_mounts.value.name
          path = volume_mounts.value.path
        }
      }

      # Not /health: MapDefaultEndpoints maps it in Development only and this runs as Production.
      # /openapi/v1.json is mapped unconditionally in the events host's Program.cs.
      liveness_probe {
        transport = "HTTP"
        port      = local.api_port
        path      = local.api_probe_path
      }

      readiness_probe {
        transport = "HTTP"
        port      = local.api_port
        path      = local.api_probe_path
      }
    }
  }

  lifecycle {
    precondition {
      condition     = var.include_events
      error_message = "Set include_events = true in envs/<environment>.tfvars. The event store database is created by the database module, and without that flag there is no eventstore connection secret for this app to reference."
    }
  }
}

output "events_internal_url" {
  description = "Where the API reaches this service. Internal to the Container Apps environment; not routable from outside it."
  value       = local.events_internal_url
}
