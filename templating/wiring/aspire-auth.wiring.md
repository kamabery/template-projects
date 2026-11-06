# Wiring: sign-in (Keycloak locally, Entra External ID in Azure)

`dotnet new` can write files but cannot merge into the AppHost or into `IaC/containerapp.api.tf`.
These are the edits that finish the job. Apply them exactly, then delete the `.scaffold/` directory.

The API and both front ends already speak OIDC: the API turns it on when `Auth:Authority` is set
(`Startup/AuthConfigurator.cs`), and the front ends ask `GET /auth/config` whether to sign in. This
add-on only supplies an identity provider and sets `Auth:*` - Keycloak under the AppHost, Entra
External ID in Azure.

---

## 1. `.aspire/apphost.cs` - package directive

```csharp wire file=.aspire/apphost.cs at=<<addon-packages>> pos=above
#:package Aspire.Hosting.Keycloak@13.5.1-preview.1.26420.4
```

## 2. `.aspire/apphost.cs` - Keycloak, and the API pointed at it

```csharp wire file=.aspire/apphost.cs at=<<addon-resources>> pos=below
// Keycloak, standing in for the Entra External ID tenant a deployed environment signs in with.
// The realm in ./keycloak has a public client "spa" (code + PKCE, any localhost redirect) whose
// access tokens carry aud "spa", and one user, qa / qa. The API only ever sees Auth:*, so it does
// not know which provider it has - see AuthConfigurator. Port 8080 is pinned because the issuer in
// a token is the URL the browser used, and the API must be told that same URL.
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithRealmImport("./keycloak");

api.WithReference(keycloak)
    .WaitFor(keycloak)
    .WithEnvironment("Auth__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/app"))
    .WithEnvironment("Auth__Audience", "spa")
    .WithEnvironment("Auth__ClientId", "spa")
    .WithEnvironment("Auth__Scope", "openid profile email");
```

## 3. `IaC/containerapp.api.tf` - the same settings, from Entra External ID

Inside the API container, below the `<<addon-api-env>>` marker. The values come from
`IaC/auth.entra-external.tf`, which this add-on added.

```hcl wire file=IaC/containerapp.api.tf at=<<addon-api-env>> pos=below
      # Auth:* from auth.entra-external.tf - the API signs users in against Entra External ID.
      env {
        name  = "Auth__Authority"
        value = local.auth_authority
      }

      env {
        name  = "Auth__Audience"
        value = local.auth_client_id
      }

      env {
        name  = "Auth__ClientId"
        value = local.auth_client_id
      }

      env {
        name  = "Auth__Scope"
        value = local.auth_scope
      }

      dynamic "env" {
        for_each = var.auth_admin_email == null ? [] : [var.auth_admin_email]
        content {
          name  = "Auth__AdminEmail"
          value = env.value
        }
      }
```

---

## 4. The External tenant - once, by hand

Terraform cannot create an Entra External ID tenant. Before the first deploy, create the tenant, a
sign-in user flow in it, and an app registration HCP signs in as (see the header of
`IaC/auth.entra-external.tf`), then put `entra_external_tenant_id`, `entra_external_subdomain` and
`entra_external_user_flow_id` in `IaC/envs/{dev,staging,ephemeral}.tfvars`. A plan without them stops at
`terraform_data.entra_external_configured` with a message saying so.

---

## Verify

```bash
aspire run      # a keycloak resource appears; the front end's header offers "Sign in"
                # sign in as qa / qa; reads work signed out, writes need the sign-in
cd IaC && terraform init -backend=false && terraform test -var-file=envs/ephemeral.tfvars
```

## Notes for the generated docs

- Locally: Keycloak at https://localhost:8080, realm `app`, client `spa`, user `qa` / `qa`. The
  realm is imported from `.aspire/keycloak/app-realm.json` on every start; change users there.
- In Azure: each environment (dev, staging, every `j<NNN>` preview) gets its own app registration
  and its own QA user, `qa-<environment>@<project>.test`, whose password is the `qa_password`
  Terraform output and the `qa-password` Key Vault secret. All of it is destroyed with the
  environment.
- Reads are anonymous and every POST/PUT/PATCH/DELETE needs a signed-in user. Opt a write out with
  `[AllowAnonymous]`, as the Development harness does.
- The MAUI host does not sign in yet; it shows no sign-in control.
