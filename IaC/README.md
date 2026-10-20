# IaC

Terraform for Azure. Everything the application needs to run outside `aspire run`: the API and the
optional events service on **Azure Container Apps**, the front ends on **Azure Static Web Apps**,
the data store that matches the EF provider this project was scaffolded with, and - with the events
slice - **Azure Service Bus** plus an **Azure Functions** consumer that scales with the queue.

Terraform loads every `*.tf` in this directory, which is why the add-ons need no merge step: the
React front end is `frontend.react.tf`, the events slice is `containerapp.events.tf`,
`servicebus.tf` and `functions.tf`, and a scaffold simply has the files for the pieces it has.

## What gets created

```
resource group
├── log analytics workspace + application insights     observability.tf
├── user-assigned managed identity                     identity.tf
├── container registry                     (AcrPull)   registry.tf
├── key vault                  (Secrets User/Officer)  keyvault.tf
├── container apps environment                         containerapps.tf
│   ├── container app: api           external ingress  containerapp.api.tf
│   └── container app: events        internal ingress  containerapp.events.tf   (events only)
├── database                                           database.tf -> modules/database-*
│   └── appdb, and eventstore when include_events
├── service bus namespace + widget-completed queue       servicebus.tf            (events only)
├── function app: consumer   Flex Consumption + storage functions.tf             (events only)
├── static web app: react           Standard + linked  frontend.react.tf        (react only)
└── static web app: blazor          Free, cross-origin frontend.blazor-web.tf   (blazor web only)
```

## Decisions this encodes

| | |
| --- | --- |
| **Front end to API** | A scaffold has one front end. React's Static Web App **links** the API container app as a backend, so the browser calls a same-origin `/api` — the same shape Vite's proxy gives in development. Blazor WebAssembly instead calls the API's own address cross-origin, which is also what it does in development, and is why it is the one client that needs CORS. Each deployed front end therefore reaches the API the same way it does locally. |
| **Network** | Public endpoints with firewall rules. No VNet, no private endpoints, no private DNS. Cheap, fast to apply, and reachable from a laptop — which is what a scaffold should be. Moving to private networking is a real change, not a flag. |
| **Identity** | One user-assigned managed identity, granted `AcrPull`, `Key Vault Secrets User`, `Azure Service Bus Data Owner` and, on the Functions storage account, `Storage Blob Data Owner`. Database credentials are generated here, stored in Key Vault, and referenced by the container apps by URI. No credential is an app setting. |
| **State** | HCP Terraform. `backend.tf` is an empty `cloud {}` block; the organization, project and workspace come from `TF_CLOUD_ORGANIZATION`, `TF_CLOUD_PROJECT` and `TF_WORKSPACE`, so nothing about who runs it is baked into a scaffold. Runs execute in HCP with dynamic Azure credentials. A gitignored `backend_override.tf` with `backend "local" {}` gives local state for experiments. |
| **Environments** | `envs/dev.tfvars` and `envs/staging.tfvars`, plus `envs/ephemeral.tfvars` for a factory job's preview (`environment = j<NNN>`, scale to zero, tagged `ephemeral`). One HCP workspace per environment, `<repository>-<environment>`. **No production**: `variables.tf` refuses `prod`, and nothing here deploys one. |
| **Deploying** | `.github/workflows/deploy.yml`, dispatch only. Merging to `main` deploys nothing; dev and staging are ready and wait for a person to run them. |
| **Messaging** | Deployed, the events service publishes `WidgetCompletedV1` to Service Bus on every event store (`events_transport` defaults to `azureservicebus`), through its own outbox. The **only** consumer is an Azure Function on **Flex Consumption**: target-based scaling divides the queue length by `maxConcurrentCalls` in the project's `host.json`, up to `functions_max_instances`, and scales to zero when the queue is empty. The events service keeps `min_replicas = 1` for its projection daemon; the consumer does not inherit that. It reaches its storage and the namespace with the same managed identity, so there is no connection string anywhere. |

## Apply it

Normally the deploy workflow does this. By hand:

```bash
az login                                   # for the image build and schema steps below
terraform login                            # HCP Terraform

export TF_CLOUD_ORGANIZATION=<org>
export TF_CLOUD_PROJECT=<repository>
export TF_WORKSPACE=<repository>-dev

terraform init
terraform apply -var-file=envs/dev.tfvars
```

`terraform test -var-file=envs/ephemeral.tfvars` (after `terraform init -backend=false`) checks
the ephemeral contract offline, with every provider mocked.

The first apply runs a public placeholder image, because nothing has been pushed to the registry
yet. Then build and push, and apply again with a tag:

```bash
ACR=$(terraform output -raw acr_name)
az acr build -r "$ACR" -t <name>-api:v1 -f src/<name>.api/Dockerfile .

terraform apply -var-file=envs/dev.tfvars -var="image_tag=v1"
```

### Schema

**Nothing migrates the database for you.** `InitializeDatabaseAsync` applies migrations in
Development only, deliberately — a deployment must not apply an unreviewed schema change, and
concurrent replicas would race. Run it from your pipeline or by hand:

```bash
KV=$(terraform output -raw key_vault_name)
CONN=$(az keyvault secret show --vault-name "$KV" --name appdb-connection --query value -o tsv)

dotnet ef database update \
  --project src/<name>.api.migrations.<provider> \
  --startup-project src/<name>.api \
  --connection "$CONN"
```

Your own address is not covered by the database's `AllowAzureServices` rule. Add it to
`db_allowed_ip_ranges` in the tfvars first, or the command hangs and then times out.

### Front ends

```bash
npm --prefix src/ui/react run build
swa deploy src/ui/react/dist --deployment-token "$(terraform output -raw react_api_key)" --env production
```

## Things that will bite you

- **Do not point a container app probe at `/health`.** `MapDefaultEndpoints()` maps `/health` and
  `/alive` in Development only, and these containers run as Production. The probes here use
  `/openapi/v1.json`, which `Program.cs` maps unconditionally. A `/health` probe fails every
  revision and the dashboard blames the image.
- **The Blazor WebAssembly front end needs a second apply.** `containerapp.api.tf` is part of the
  base scaffold and cannot reference a resource declared in an add-on file, so the Static Web App's
  hostname reaches `Cors:AllowedOrigins` only after it exists. Apply, copy `blazor_web_url` into
  `cors_allowed_origins`, apply again. Skip it and the API logs healthy 200s while every request
  fails in the browser — the failure `WarnOnMissingCorsOrigins` exists to warn about.
- **`ApiBaseAddress` is a static file.** The WebAssembly host reads
  `wwwroot/appsettings.json` in the browser, and it has `http://localhost:5058` baked in. Rewrite it
  to `api_url` before uploading; the deploy workflow does.
- **Choosing SQLite is choosing one replica.** SQLite has a single writer, so the module reports
  `single_writer = true` and both container apps pin `max_replicas = 1`. The file lives on an Azure
  Files share; a container's own filesystem is wiped on every revision.
- **The Functions consumer deploys as a zip, not an image.** Flex Consumption runs code from the
  `deployment` blob container in its own storage account, so it has no image in the registry. The
  workflow publishes it after the apply that creates the app:
  `az functionapp deployment source config-zip -g <rg> -n $(terraform output -raw function_app_name) --src <zip>`.
- **Flex Consumption is not offered in every region.** If the `FC1` service plan fails to create,
  check `az functionapp list-flexconsumption-locations` before anything else.
- **Never add a Service Bus listener to the events service.** The Function is the queue's only
  consumer; a second one competes with it, and each quietly receives about half of the messages.
- **`AllowAzureServices` admits any Azure resource, in any tenant.** That is the cost of having no
  VNet. The generated credential in Key Vault is what actually gates access.
- **Never use HCL's string-rendering function here** - the one that reads a file and interpolates
  variables into it. These files come from a `dotnet new` scaffold whose source name is a bare word
  that appears in that function's own name, so substitution would corrupt every call - and would
  corrupt this sentence too if it named it. Use `file()` with `replace()`, or a heredoc.
