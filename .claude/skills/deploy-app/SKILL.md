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
2. On success it prints the URLs. Smoke them yourself: the front end loads, and
   `<api_url>/openapi/v1.json` returns 200. With the auth add-on, `<api_url>/auth/config` says
   `enabled: true` and names the External tenant's authority.
3. On failure, read the log (`factory.ps1 deploy-log <id>`): the GitHub job log and the HCP
   plan/apply log. Find the first real error, fix it in the clone, commit, and deploy again. The
   caller counts attempts and stops you; do not try to reset its count.

### Symptom → cause

| Symptom | Cause | Fix |
| --- | --- | --- |
| Every container app revision fails its probe | a probe on `/health`, which is Development-only | probe `/openapi/v1.json` |
| Plan stops at `terraform_data.no_production` | `environment = prod` reached Terraform | never deploy prod; fix the input |
| Plan stops at `terraform_data.entra_external_configured` | the HCP variable set lacks the External tenant settings | a finding for the human - you cannot set them |
| `403` writing a Key Vault secret on first apply | the role assignment had not landed | `depends_on` the `kv_secrets_officer` assignment |
| Image pull fails on the first apply | no image pushed yet | the two-phase `-target` apply in `deploy.yml` builds first; keep that ordering |
| Blazor page loads, every API call fails, API logs 200s | CORS origin missing | the workflow's second apply passes `cors_allowed_origins`; keep it |
| `dotnet ef database update` hangs | the runner's IP is not in the database firewall | the workflow opens and closes it; keep both steps |
| Flex Consumption plan `FC1` fails to create | the region does not offer it | pick a region from `az functionapp list-flexconsumption-locations` (a finding) |
| `Error: No valid credential sources` in HCP | the workspace is not in the project that has the dynamic-credential variable set | a finding: the caller creates workspaces |
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
