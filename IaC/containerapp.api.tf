resource "azurerm_container_app" "api" {
  name                         = local.api_app_name
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

  # By URI, resolved at run time with the managed identity. The value never enters this
  # configuration and so never enters Terraform state as an app setting.
  secret {
    name                = "appdb-connection"
    key_vault_secret_id = module.database.appdb_connection_secret_id
    identity            = azurerm_user_assigned_identity.app.id
  }

  ingress {
    external_enabled           = true
    target_port                = local.api_port
    transport                  = "auto"
    allow_insecure_connections = false

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.api_min_replicas

    # SQLite has exactly one writer. The module says so rather than this file guessing, which is
    # what lets the same file serve all three providers.
    max_replicas = module.database.single_writer ? 1 : var.api_max_replicas

    dynamic "volume" {
      for_each = module.database.volumes

      content {
        name         = volume.value.name
        storage_name = volume.value.storage_name
        storage_type = volume.value.storage_type
      }
    }

    container {
      name   = "api"
      image  = local.api_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "Database__Provider"
        value = module.database.provider_name
      }

      env {
        name        = "ConnectionStrings__appdb"
        secret_name = "appdb-connection"
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.main.connection_string
      }

      # The seam for API-to-API calls. Both apps live in one Container Apps environment and resolve
      # each other over its internal domain; the name is computed from locals rather than from the
      # events resource, which is declared in an add-on file this scaffold may not have.
      env {
        name  = "Services__events"
        value = local.events_internal_url
      }

      # Only the Blazor WebAssembly front end needs these - it is the one client that is genuinely
      # cross-origin. Empty in a React-only or API-only scaffold, and empty on the first apply of a
      # Blazor one; see frontend.blazor-web.tf for why that takes a second apply.
      dynamic "env" {
        for_each = { for i, origin in var.cors_allowed_origins : i => origin }

        content {
          name  = "Cors__AllowedOrigins__${env.key}"
          value = env.value
        }
      }

      dynamic "volume_mounts" {
        for_each = module.database.volume_mounts

        content {
          name = volume_mounts.value.name
          path = volume_mounts.value.path
        }
      }

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
}
