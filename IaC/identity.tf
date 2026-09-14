# One user-assigned identity for every workload, rather than a system-assigned one per app.
#
# User-assigned because the role assignments below have to exist before the container app starts -
# a system-assigned identity does not exist until its app does, which makes the registry pull on
# the very first revision a race. This identity is created first, granted everything, and then
# handed to each app.

resource "azurerm_user_assigned_identity" "app" {
  name                = "id-${local.base_name}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}
