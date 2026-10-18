# The widget-completed consumer, on Azure Functions Flex Consumption.
#
# Part of the events add-on, beside containerapp.events.tf and servicebus.tf. The events service
# publishes WidgetCompletedV1 to the Service Bus queue and does not listen on it; this Function is
# the queue's only consumer.
#
# Flex Consumption rather than a container app, because the two halves want opposite scaling. The
# events service holds a projection daemon and an outbox and so never drops below one replica, while
# the consumer should follow the queue: target-based scaling divides the queue length by
# maxConcurrentCalls in the project's host.json and runs that many instances, down to none when the
# queue is empty. It never touches the event store, so it scales freely even on the SQLite branch,
# where the events service is pinned to one replica.
#
# Code, not a container: Flex deploys a zip package from the blob container below, so this app has
# no image in the registry and the deploy workflow publishes it separately.
#
# Flex Consumption is not offered in every region. If an apply fails on the service plan, check the
# region against `az functionapp list-flexconsumption-locations` before anything else.

variable "functions_max_instances" {
  description = "Upper bound on Function instances. Flex Consumption scales out toward this as the widget-completed queue grows, and to zero when it is empty."
  type        = number
  default     = 40
}

variable "functions_instance_memory_mb" {
  description = "Memory per Function instance. 2048 is the Flex default; 512 is cheaper and enough for a handler that only logs."
  type        = number
  default     = 2048
}

# Holds the deployment package, and is the Functions host's own storage (AzureWebJobsStorage). Both
# are reached with the managed identity, so shared keys are switched off.
resource "azurerm_storage_account" "functions" {
  name                = substr("stfn${local.short_name}${var.environment}${local.suffix}", 0, 24)
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  shared_access_key_enabled       = false
  allow_nested_items_to_be_public = false

  tags = local.tags
}

resource "azurerm_storage_container" "functions_deployment" {
  name                  = "deployment"
  storage_account_id    = azurerm_storage_account.functions.id
  container_access_type = "private"
}

# Data Owner because the Functions host keeps leases and keys in blobs as well as reading the
# deployment package. The Service Bus side needs nothing new: the same identity already holds Data
# Owner on the namespace, from servicebus.tf.
resource "azurerm_role_assignment" "functions_storage_blob_owner" {
  scope                = azurerm_storage_account.functions.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

resource "azurerm_service_plan" "functions" {
  name                = "asp-${local.base_name}-${var.environment}-fn"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "FC1"
  tags                = local.tags
}

resource "azurerm_function_app_flex_consumption" "consumer" {
  name                = "func-${local.short_name}-${var.environment}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.functions.id
  tags                = local.tags

  runtime_name           = "dotnet-isolated"
  runtime_version        = "10.0"
  maximum_instance_count = var.functions_max_instances
  instance_memory_in_mb  = var.functions_instance_memory_mb

  storage_container_type            = "blobContainer"
  storage_container_endpoint        = "${azurerm_storage_account.functions.primary_blob_endpoint}${azurerm_storage_container.functions_deployment.name}"
  storage_authentication_type       = "UserAssignedIdentity"
  storage_user_assigned_identity_id = azurerm_user_assigned_identity.app.id

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app.id]
  }

  site_config {
    application_insights_connection_string = azurerm_application_insights.main.connection_string
  }

  app_settings = {
    # The identity-based form of the trigger's "servicebus" connection - the name is the trigger's
    # Connection property and the AppHost's resource name. No connection string exists.
    "servicebus__fullyQualifiedNamespace" = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
    "servicebus__credential"              = "managedidentity"
    "servicebus__clientId"                = azurerm_user_assigned_identity.app.client_id

    # The host's own storage, by identity for the same reason.
    "AzureWebJobsStorage__accountName" = azurerm_storage_account.functions.name
    "AzureWebJobsStorage__credential"  = "managedidentity"
    "AzureWebJobsStorage__clientId"    = azurerm_user_assigned_identity.app.client_id
  }

  # The identity must be able to read the package container before the first instance starts.
  depends_on = [azurerm_role_assignment.functions_storage_blob_owner]
}

output "function_app_name" {
  description = "Deploy the Functions package here. `az functionapp deployment source config-zip -g <resource group> -n <this> --src <zip>`"
  value       = azurerm_function_app_flex_consumption.consumer.name
}
