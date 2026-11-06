---
name: deploy-app
description: Make an AspireStarter scaffold deployable to Azure through HCP Terraform, and get one of its ephemeral previews (environment j<NNN>) applied - or diagnose why it was not. Drives `aspire-addon infra adopt` and `infra check`, which bring the pack-owned infra files forward and prove every local AppHost resource has its Azure twin; then deploys and reads logs through the deploy commands the caller provides (in the claude-factory, `factory.ps1 deploy <id>` and `deploy-log <id>`). Use for the factory's devops agent, or when someone asks to deploy, preview, retrofit IaC, or fix a failed Terraform/HCP/GitHub Actions deployment of a scaffold. Never deploys production.
---

# Deploy a scaffold (previews, never production)

The infrastructure is the templates' job, not yours. `aspire-addon infra adopt` writes the
pack-owned files; `aspire-addon infra check` says what is still missing. You write HCL or workflow
YAML by hand only for what the templates do not cover - and every such hand edit is a **template
gap**, which you record as a lesson so the templates absorb it and the next run does not need you.

Rules that do not bend:

- **No production.** Environments are `dev`, `staging` and `j<NNN>` previews. `IaC/guards.tf`
  refuses `prod`; never remove or weaken it, and never add a `prod.tfvars`.
- **Container Apps and Function Apps only.** No VMs, scale sets or AKS; `infra check` refuses them.
- **Registry-verified modules first** (`Azure/avm-res-*`) for anything new, then plain provider
  resources; never a module of your own.
- **No credentials.** You never run `terraform apply`, `az`, `gh` or the HCP API yourself, and never
  read `.env` or the environment. The caller's deploy command holds every secret and does the
  pushing. If something needs a credential you do not have, that is a finding, not a workaround.
- **Equivalence.** Local and Azure differ in configuration only. Every `builder.Add<Kind>(...)` in
  `.aspire/apphost.cs` has the twin `IaC/RESOURCE-MAP.md` names; application code never branches
  on where it runs.

---

## Mode 1 — Author (during the build)

From the scaffold's root:

```bash
aspire-addon infra adopt     # only if `infra check` reports missing pack-owned files
aspire-addon infra check --json
```

- No `.aspirestarter.json`: run `aspire-addon adopt` first (see the enhance-app skill).
- For each finding `infra check` still reports, fix it in `IaC/` or `.github/workflows/` - the
  smallest change that clears it - and note it under **Template gaps** (below). A finding that
  names `RESOURCE-MAP.md` means the map has no row for that resource: add the twin, and the gap.
- Then, offline:

```bash
cd IaC && terraform init -backend=false && terraform validate && terraform test -var-file=envs/ephemeral.tfvars
```

Commit locally. Do not push.

## Mode 2 — Operate and diagnose (the deploy stage)

1. Run the caller's deploy command (in the factory: `factory.ps1 deploy <id>`). It pushes the
   branch, prepares the HCP workspace and the GitHub `preview` environment, dispatches
   `.github/workflows/deploy.yml` with `environment = j<NNN>`, and waits.
2. On success it prints the URLs. Smoke them yourself: the front end loads, and the API answers
   where the browser calls it - `<react_url>/api/openapi/v1.json` with a React front end (the API's
   own address answers 401 there: it is the Static Web App's linked backend), else
   `<api_url>/openapi/v1.json`. With the auth add-on, `.../auth/config` says `enabled: true` and
   names the External tenant's authority.
3. On failure, read the log (`factory.ps1 deploy-log <id>`): the GitHub job log and the HCP
   plan/apply log. Find the first real error, fix it in the clone, commit, and deploy again. The
   caller counts attempts and stops you; do not try to reset its count.

### Symptom → cause

| Symptom | Cause | Fix |
| --- | --- | --- |
| Every container app revision fails its probe, or restarts when the database blips | a probe on `/health`, which includes the database check (and is Development-only in scaffolds older than 2026-10-24) | probe `/openapi/v1.json` |
| Plan stops at `terraform_data.no_production` | `environment = prod` reached Terraform | never deploy prod; fix the input |
| Plan stops at `terraform_data.entra_external_configured` | `IaC/envs/<environment>.tfvars` lacks the `entra_external_*` ids | a finding for the human - you do not have them |
| `403` writing a Key Vault secret on first apply | the role assignment had not landed | `depends_on` the `kv_secrets_officer` assignment |
| Image pull fails on the first apply | no image pushed yet | the two-phase `-target` apply in `deploy.yml` builds first; keep that ordering |
| Blazor page loads, every API call fails, API logs 200s | CORS origin missing | the workflow's second apply passes `cors_allowed_origins`; keep it |
| `dotnet ef database update` hangs | the runner's IP is not in the database firewall | the workflow opens and closes it; keep both steps |
| Flex Consumption plan `FC1` fails to create | the region does not offer it | pick a region from `az functionapp list-flexconsumption-locations` (a finding) |
| `Error: No valid credential sources` in HCP | the workspace is not in the project that has the dynamic-credential variable set | a finding: the caller creates workspaces |
| `could not configure AzureCli Authorizer ... exec: "az": executable file not found` in HCP | the variable set has more than one dynamic-credential configuration (the `EXTERNAL` one), so HCP does not configure the default providers itself | `providers.tf` wires `azurerm` and `azapi` to `var.tfc_azure_dynamic_credentials.default` with `use_oidc`; restore that if it was lost |
| `AADSTS700213: No matching federated identity record` on `azure/login` | the repo presents GitHub's immutable OIDC subject (`repo:<owner>@<id>/<name>@<id>:...`) and the credential has another | a finding: the factory registers the subject; never edit the workflow to change it |
| `polling after CreateOrUpdate ... 404 ResourceNotFound` (or `retrieving Key Vault ... 404`) on `azurerm_key_vault` right after the resource group was created | ARM's read-after-create is eventually consistent; the vault was created (check `az resource list -g <rg>`) | `deploy` again unchanged. Never rename the vault to get past it: the first one is orphaned outside the state and blocks the resource group's destroy |
| `SkuNotAvailable` creating the PostgreSQL Flexible Server | the subscription has no capacity for that SKU in that region | move `location` in `envs/ephemeral.tfvars` (a finding: the region is the human's choice for dev and staging) |
| `AADSTS700024: Client assertion is not within its valid time range` in a workflow step after a long apply | GitHub's OIDC token lives five minutes and az re-presents it for each new resource (Key Vault, Storage) | `deploy.yml` signs in again before the schema step; keep that `azure/login` |
| `ForbiddenByRbac` on `Microsoft.KeyVault/vaults/secrets/getSecret/action` in the schema step | the workflow's identity has subscription Contributor, which stops at the control plane | `kv_secrets_deployer` grants it Secrets User per vault from `deployer_object_id` (HCP variable set); if that is unset, a finding for the human |
| `NETSDK1004: Assets file ... project.assets.json not found` in the schema step | `dotnet ef` builds the migrations and API projects but never restores them, and a fresh runner has no `obj/` | the workflow runs `dotnet restore` on both first; keep it |
| `MissingSubscriptionRegistration: ... not registered to use namespace 'Microsoft.App'` | the subscription never registered that resource provider; azurerm registers only its core set | a finding for the human: re-run `setup-azure.ps1`, which registers every namespace the templates use |
| `<api_url>/...` answers 400 or 401 (`WWW-Authenticate: Bearer`, `x-ms-middleware-request-id`) with a React front end | the Static Web App links the API as its backend, which turns on the platform's auth for every other caller | expected: go through `<react_url>/api/...` |
| The React app loads but every `/api/...` call is a 404 from the API | the linked backend forwards `/api/*` with the prefix; Vite and YARP strip it | `Program.cs` keeps `app.UsePathBase("/api")` followed by `app.UseRouting()`; restore it if it was lost |
| The React app loads at `/` but opening or refreshing `/todos` (any client-side route) is the Static Web App's "404: Not found" | no `navigationFallback`: Vite serves `index.html` for every route locally, a Static Web App only with `public/staticwebapp.config.json` | `infra check` reports it; restore the template's `staticwebapp.config.json` (rewrite to `/index.html`, excluding `/api/*` and files) |
| Destroy fails: `deleting Resource Group ... the Resource Group still contains Resources` | Azure adds a `Failure Anomalies - appi-...` alert rule to Application Insights minutes after the apply, outside the state | `providers.tf` turns off `prevent_deletion_if_contains_resources` for `j<NNN>` only; restore that if it was lost. Never for dev or staging |
| `a resource with the ID ... already exists - to be managed via Terraform this resource needs to be imported` | an apply that was cut off created the resource in Azure, and it never reached the state (J-022) | a finding for the human, who imports or deletes the orphan. Never rename it: the first one is orphaned for good and blocks the destroy |
| Static Web App deploy rejects the token | the token output read before the app existed | read `react_api_key` / `blazor_web_api_key` after the apply |

`IaC/README.md` "Things that will bite you" is the longer list. A symptom in neither place that you
had to work out is itself a template gap.

---

## Template gaps → lessons

Write `deploy.md` (or the file the caller names) with a `## Template gaps` section. One entry per
hand edit or diagnosis the templates should have made unnecessary:

```markdown
- Symptom: <what failed, first real error line>
- Cause: <why>
- Fix applied here: <file and change>
- Belongs in: template-projects:<path> (e.g. IaC/containerapp.api.tf, templating/wiring/..., this skill's table)
```

An empty section is a good outcome - say "none". Never fold a gap silently into the project and
move on: the point of recording it is that the next scaffold gets it for free.
