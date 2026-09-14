# Azure Service Bus for the events slice.
#
# Provisioned on every events scaffold, and authenticated with the same managed identity - no SAS
# key, no connection string, nothing to rotate.
#
# BE HONEST ABOUT WHAT THIS DOES TODAY. Only the Polecat (SQL Server) events host has an
# Events:Transport = "azureservicebus" branch in its Program.cs. On a Marten or Fisher scaffold this
# namespace is provisioned, role-granted and then idle: those hosts publish through Wolverine's
# durable local queues, which are backed by their own event store's outbox tables and need no
# broker at all. It is here so the seam exists and the RBAC is already right; wiring a host to it
# is a code change in that host, not a change here.

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
