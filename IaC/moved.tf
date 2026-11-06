# The platform made these resources optional, so what dev and staging already have moves in their
# state instead of being destroyed and rebuilt.
moved {
  from = azurerm_container_app_environment.main
  to   = azurerm_container_app_environment.main[0]
}

moved {
  from = azurerm_log_analytics_workspace.main
  to   = azurerm_log_analytics_workspace.main[0]
}

moved {
  from = azurerm_key_vault.main
  to   = azurerm_key_vault.main[0]
}

moved {
  from = azurerm_role_assignment.kv_secrets_user
  to   = azurerm_role_assignment.kv_secrets_user[0]
}

moved {
  from = azurerm_role_assignment.kv_secrets_officer
  to   = azurerm_role_assignment.kv_secrets_officer[0]
}
