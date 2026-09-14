# template-projects

A workspace of **`dotnet new` templates** for scaffolding new .NET 10 + Aspire
applications, plus a Claude Code skill that drives them.

The code under `base-project/` and `event-project/` is a working reference
implementation, and `IaC/` is the Terraform that deploys it to Azure.
`templating/` packages all of it into four templates; `packaging/` builds the
NuGet pack; `artifacts/` is the generated output.

## The templates

| shortName | Emits | Parameters |
| --- | --- | --- |
| `aspire-base` | Solution, Aspire AppHost, API, data + migrations, service defaults, API tests, the committed API contract | `--dbProvider postgres\|sqlserver\|sqlite`, `--resource` |
| `aspire-ui-react` | React + Vite + TanStack + Tailwind front end | `--resource` |
| `aspire-ui-blazor` | Blazor UI (Razor Class Library) + optional hosts and bUnit tests | `--web`, `--maui`, `--resource` |
| `aspire-events` | Wolverine event-sourcing slice | `--eventStore marten\|polecat\|fisher`, `--resource` |

**A scaffold arrives with your noun, not ours.** `--resource Product` names the entity, the
controller, the page and the tests, so there is no sample domain to delete first. It must be passed
to every template in a scaffold, exactly like `-n`. The plural is mechanically the singular plus
`s`, so an irregular one is a rename afterwards.

**The API is the contract.** `contracts/api.openapi.json` is committed and produced by the API's own
test suite; both front ends generate their types from it rather than declaring them by hand, and a
test fails when the committed document and the running API disagree. `docs/ADDING-A-RESOURCE.md` in
every scaffold is the end-to-end recipe.

Every one of them also carries its share of `IaC/`, so a scaffold's Terraform
matches the application it scaffolded. Terraform loads every `*.tf` in a
directory, so that composition needs no merge step.

The data store is baked in at scaffold time; only the chosen provider ships.
The event store follows it rather than being chosen separately — Marten on
PostgreSQL, Polecat on SQL Server 2025, Fisher on SQLite — so `--eventStore`
must match `--dbProvider`.

## Scaffolding with Claude (recommended)

Run the **`/scaffold-aspire-app`** skill in Claude Code. It interviews you on
four decisions — the first resource, the data store, event sourcing, and which
single front end — runs the templates, merges the new projects into the solution and
AppHost, generates the project's README and CLAUDE.md, and verifies it builds
and tests green.

The merge step is why the skill exists: `dotnet new` writes files but cannot
edit an existing `.slnx` or `apphost.cs`. Each add-on ships a
`.scaffold/*.wiring.md` describing the exact blocks to insert; the skill applies
them.

## Scaffolding by hand

Build and install the pack (needs PowerShell 7 on `PATH`):

```bash
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
# Uninstall first, and do NOT use --force. See "Reinstalling the pack" in templating/README.md.
dotnet new uninstall AspireStarter.Templates
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg
```

Then scaffold — `aspire-base` first, each add-on after, passing `-n <Name>` and
`--resource <Resource>` to every one:

```bash
dotnet new aspire-base     -n MyProject -o MyProject --dbProvider postgres --resource Product
dotnet new aspire-ui-react -n MyProject -o MyProject --resource Product
# ...then apply .scaffold/*.wiring.md and delete .scaffold/
```

## Running the reference implementation

```bash
aspire run          # API + both front ends + dashboard
```

## Deploying a scaffold

`IaC/` is Terraform for Azure: the API and the optional events service on Azure
Container Apps, the front ends on Azure Static Web Apps, and the managed store
that matches the chosen EF provider. A scaffold also gets Dockerfiles and a
GitHub Actions workflow that builds, applies the schema, provisions and
publishes. `IaC/README.md` has the walkthrough and the failure modes.

Nothing here is ever applied from this workspace — it is template source, not a
deployed application. `terraform validate` is the check that runs.

See `CLAUDE.md` for the full workspace layout, architecture notes, and the
rules that keep this repo template-ready.
