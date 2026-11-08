# Where Entra External ID may send a signed-in user back to: the React front end's root, which is
# where src/lib/auth.ts completes the sign-in. Shipped by the auth add-on only into a scaffold
# whose front end is React; auth.redirects.blazor-web.tf is the Blazor counterpart.

resource "azuread_application_redirect_uris" "react" {
  application_id = azuread_application.spa.id
  type           = "SPA"
  redirect_uris  = ["https://${azurerm_static_web_app.react.default_host_name}/"]
}
