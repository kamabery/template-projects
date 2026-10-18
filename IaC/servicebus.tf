# Azure Service Bus for the events slice.
#
# Provisioned on every events scaffold, and authenticated with the same managed identity - no SAS
# key, no connection string, nothing to rotate.
#
# Used on every event store. All three events hosts publish WidgetCompletedV1 here when
# Events:Transport is "azureservicebus" - the deployed default, see containerapp.events.tf - and the
# Function in functions.tf is the queue's only consumer. The outbox in front of it is still the event
# store's own, so a message is not lost between the event append and the send.

resource "azurerm_servicebus_namespace" "main" {
  name                = "sb-${local.base_name}-${var.environment}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  # Standard, not Basic: Basic has no topics and no sessions, and Premium is an order of magnitude
  # more expensive than a scaffold should default to.
  sku = "Standard"

  tags = local.tags
}

# The same queue name the AppHost declares on the Service Bus emulator, so the local and deployed
# topologies match.
resource "azurerm_servicebus_queue" "order_approved" {
  name         = "widget-completed"
  namespace_id = azurerm_servicebus_namespace.main.id
}

# Data Owner rather than Sender + Receiver: Wolverine auto-provisions its own queues and dead-letter
# paths at startup, which the narrower roles cannot do. Drop to Sender/Receiver and remove
# AutoProvision from the events host together, or not at all.
resource "azurerm_role_assignment" "servicebus_data_owner" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
}

output "servicebus_namespace" {
  value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
}
