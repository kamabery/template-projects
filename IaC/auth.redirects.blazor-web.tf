# Where Entra External ID may send a signed-in user back to: the Blazor WebAssembly host's
# RemoteAuthenticatorView callbacks (Pages/Authentication.razor). Shipped by the auth add-on only
# into a scaffold whose front end is the Blazor web host; auth.redirects.react.tf is the React
# counterpart.

resource "azuread_application_redirect_uris" "blazor_web" {
  application_id = azuread_application.spa.id
  type           = "SPA"
  redirect_uris = [
    "https://${azurerm_static_web_app.blazor_web.default_host_name}/authentication/login-callback",
    "https://${azurerm_static_web_app.blazor_web.default_host_name}/authentication/logout-callback",
  ]
}
