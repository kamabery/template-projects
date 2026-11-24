# CLAUDE.md

**This workspace is template source, not an application.** The Aspire solution in
`base-project/` is destined to be packaged as `dotnet new` templates that agents invoke to
scaffold new projects. Every change you make propagates into every project someone scaffolds
from it. Treat edits accordingly: prefer the boring, portable option, and don't leave anything
here that only works on this machine, in this folder.

**Path convention for this document:** the workspace root holds `CLAUDE.md`, `.claude/`,
`base-project/`, `specs/`, and — since the solution and AppHost were lifted out of `base-project/` —
`template.slnx`, `aspire.config.json`, `nuget.config`, `.aspire/` and `IaC/`. Every path below is
relative to **`base-project/`** unless it is one of those workspace-root files, starts with `specs/`
or `.claude/`, or is called out as workspace-level. `base-project/` is still the directory intended
to be packed; the workspace root is not — see "Templating status" for what that now costs.

> **Tradeoff:** These guidelines bias toward caution and rigor over raw speed. For trivial tasks
> (typos, obvious one-liners), use judgment — not every change needs the full rigor.

---

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:

- State assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.

When your changes create orphans:

- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require
constant clarification.

---

## Project-Specific Guidelines

### Project summary

- **Name:** base-project (solution file `template.slnx`, at the workspace root)
- **Purpose:** a working reference implementation, packaged as a series of `dotnet new` templates
  that agents call to scaffold new projects. See "Templating" below and the
  `/boot-strap-app` skill.
- **Location:** the projects live in `base-project/`; the solution, `aspire.config.json` and the
  `.aspire/` AppHost sit one level up at the workspace root, so a second sibling project
  (`event-project/`) can join the same solution and AppHost — which it now does. The workspace is
  named `template-projects` (plural) and is a container, and `specs/` holds proposals that are not
  implemented in it. Anything at the workspace root is workspace-level and does **not** get packed
  into the template — which now includes the solution and the AppHost.
- **Stack:** .NET 10 + Aspire 13.5.1 AppHost and one ASP.NET Core API on the back, reading its
  data via EF Core 10 from a Postgres or SQL Server Express container, or from a local SQLite
  file with no container at all. **Two** front
  ends on top of it: React 19 + Vite 8 + the TanStack suite + Tailwind CSS v4, and a Blazor UI
  written once in a Razor Class Library and rendered by two hosts (MAUI Blazor Hybrid, Blazor
  WebAssembly). Both front ends serve the same three pages against the same API.
  An optional Wolverine event-sourcing slice sits beside all of it, on the Critter Stack event store
  that matches the data store: Marten on Postgres, Polecat on SQL Server 2025, Fisher on SQLite.
  Its integration event travels on Azure Service Bus once deployed, consumed by an Azure Function
  on Flex Consumption that scales with the queue.

### Workspace layout

```
CLAUDE.md                      this file; workspace-level, NOT part of the template
.claude/                       Claude Code settings + the /boot-strap-app and /enhance-app
                               skills; NOT packed
.claude-plugin/                marketplace.json: ships those two skills as the aspire-starter plugin
template.slnx                  the solution; its project paths are base-project/-prefixed
aspire.config.json             pointer to the AppHost; the CLI walks UP to find it
nuget.config                   mirrors base-project/nuget.config — the AppHost restores from here
.aspire/                       the file-based AppHost; see "Repo layout" below
IaC/                           Terraform for Azure; workspace-root but PACKED. See "IaC" below
base-project/                  the project source — everything below is relative to here
event-project/                 the optional Wolverine event-sourcing slice; see "Events" below
templating/                    stage.ps1 + one template.json per template + the wiring files
packaging/                     the template pack's .csproj; NOT in template.slnx, deliberately
tooling/                       the aspire-addon CLI + its tests (tooling.slnx); NOT in template.slnx
tooling/verify/                checks on the deploy path no suite can run, independent of the factory:
                               mutations.ps1 (no Azure, no Docker), postgres-roles.ps1 (Docker),
                               standalone.ps1 (a real deploy on its own HCP org, STANDALONE_HCP_ORG)
artifacts/                     generated: staged template trees and the .nupkgs. Not source.
specs/                         design proposals; see "specs/ — proposals not yet implemented"
```

`templating/`, `packaging/` and `artifacts/` are how this workspace becomes `dotnet new`
templates. See "Templating" below; nothing in them is packed into a scaffold.

### Repo layout

The first block is at the **workspace root**; everything from `base-project/` down is the project
source.

```
template.slnx                  the C# projects; src/ui/react and the AppHost are NOT projects
aspire.config.json             pointer to the AppHost; must sit at the root the CLI walks up to
nuget.config                   <clear/> + nuget.org, for the AppHost's own restore
.aspire/
  apphost.cs                   file-based Aspire AppHost; five resources
  apphost.run.json             the AppHost's launch profiles (http / https)
  appsettings.json             AppHost log levels + Database:Provider; read from here at run time
  appsettings.Development.json Development overrides for the above
  Directory.Packages.props     turns CPM OFF for this directory — see "Central package management"
IaC/                           Terraform/Azure. One .tf file per add-on, so nothing merges; the data
                               store is a module and only the chosen one ships. See "IaC" below
base-project/                  ← everything below is relative to here
Directory.Build.props          shared properties, incl. the Development configuration
Directory.Packages.props       central package versions + the three load-bearing test pins
nuget.config                   <clear/> + nuget.org; the copy that gets packed
README.md                      human-facing walkthrough; its paths are base-project-relative too
.gitignore                     stock `dotnet new gitignore` + a Terraform section; does NOT cover
                               the workspace root, which has its own copy of that section
.dockerignore                  for the Dockerfiles below; flattens onto the scaffold root
.github/workflows/deploy.yml   build -> migrate -> terraform apply -> publish the front ends
contracts/api.openapi.json     THE contract. Generated by the API test suite, committed, and read
                               by BOTH front ends' codegen. See "The contract" below
docs/ADDING-A-RESOURCE.md      the ordered recipe for adding a resource end to end. The document an
                               agent should be pointed at before it touches a screen
docs/DESIGN.md                 workspace-root file, staged into the UI templates - see "Design system"
src/
  template.api/                ASP.NET Core Web API (controllers + OpenAPI + Swagger UI + /harness/*)
                               and its Dockerfile — build context is the scaffold ROOT, not here
  template.data/               entity, AppDbContext, WidgetSeeder, IRepository<T>/EfRepository<T>
  template.api.migrations.postgres/   EF migrations, Postgres only (migration files and nothing else)
  template.api.migrations.sqlserver/  EF migrations, SQL Server only
  template.api.migrations.sqlite/     EF migrations, SQLite only
  template.servicedefaults/    stock Aspire shared project (OTel, health, resilience) +
                               SerilogExtensions.cs, which makes Serilog the logging front door
  ui/
    react/                     Vite + React front end, launched by the AppHost.
                               src/components/ui/ is the design system; src/routes/styleguide.tsx
                               is the living style guide over it
    blazor/
      Template.Blazor.Maui.Pages/   Sdk.Razor  ALL the Blazor UI + the MVVM framework +
                                    Telemetry/, the client RUM both hosts share, and
                                    Pages/StyleGuide.razor, the Blazor half of the style guide
      Template.Blazor.Maui/         Sdk.Razor + UseMaui   MAUI Blazor Hybrid host
      Template.Blazor.Maui.Web/     Sdk.BlazorWebAssembly Blazor WASM host
tests/
  template.api.tests/          xUnit + WebApplicationFactory; the API suite, in-memory SQLite
  Template.Blazor.Maui.Tests/  xUnit + bUnit; the Blazor suite. Separate assemblies on purpose:
                               one project referencing both template.api and the Pages RCL could
                               not be pruned when either side is left out of a scaffold.
```

`event-project/` is a sibling of `base-project/` rather than part of it, so these paths are
relative to the workspace root:

```
event-project/
  Directory.Build.props        its own; MSBuild stops at the nearest one, so nothing is inherited
  Directory.Packages.props     its own, for the same reason — but no nuget.config, those DO merge up
  README.md
  src/shared/                  LINKED source, not a project - no .csproj. Everything that is NOT
                               specific to an event store: Domain/ (the aggregate, its events and
                               commands, and WidgetDecisions - the decision logic every host shares),
                               Contracts/ (incl. WidgetCompletedV1Wire, the body reader),
                               Messaging/ServiceBusTransport.cs (the Events:Transport switch),
                               Projections/WidgetSummary.cs, Controllers/,
                               Abstractions/IWidgetSummaries.cs. See its README.md
  src/template.events/         Marten  / PostgreSQL         host
  src/template.events.polecat/ Polecat / SQL Server 2025    host
  src/template.events.fisher/  Fisher  / SQLite             host
  src/template.events*/        each host also carries a Dockerfile, written for the SCAFFOLD layout
                               (src/ at the root) rather than this one — see "IaC" below
  src/template.events.functions/  Azure Functions isolated worker: the widget-completed consumer.
                               Store-agnostic, so one project for all three hosts and never pruned.
                               Links shared/Contracts and shared/Handlers; no Dockerfile (Flex zip)
  tests/template.events.tests/ xUnit + Shouldly + AutoFixture; unit tests only, no Docker.
                               Links shared/Domain, Contracts and Messaging ONLY - never a host - so
                               it is store-agnostic
```

Three host projects and no shared one, and neither half of that is a stylistic choice.

**Why three projects.** Each event store declares its own `IDocumentSession`, its own
`SingleStreamProjection<,>` and its own `[AggregateHandler]`, and `JasperFx.Events` ships a source
generator that emits a type per projection - so a project referencing two stores fails to compile
with duplicate generated types (CS0433). Exactly one store package per project, which is the same
constraint that produced the three migrations projects on the base-project side.

**Why `shared/` is linked source rather than a fourth project.** That same generator emits an
aggregate's `Apply`/`Create` dispatcher for the *store's own closed generic* -
`Polecat.Projections.SingleStreamProjection<Widget, Guid>` and so on - and a generator only ever sees
the compilation it is referenced from. So the aggregate and the event store have to end up in the
same compilation. A shared assembly gives the generator `Widget` but no store, and then
`FetchForWriting<Widget>` throws `InvalidProjectionException` on the first command - after a clean
build and a green test run. Linking the source compiles one copy of `Widget` separately into each
host beside that host's store, which satisfies the generator without duplicating the code.

Each host is then five small files - `Program.cs`, two handlers, a projection and an
`IWidgetSummaries` implementation - all of them adapters over what is in `shared/`.

The AppHost is a **file-based app, not a project** — there is no `.csproj`, so there is no
generated `Projects` namespace and no `AddProject<T>`. `apphost.cs` declares its own SDK and
packages in `#:` directives at the top and reaches each project by path via `AddCSharpApp`. Paths
inside `apphost.cs` are relative to `.aspire/` — which now sits at the workspace root, so every
project path in it reads `../base-project/src/...`.

The two Blazor hosts contain only a router, an `_Imports.razor`, a host page and a composition
root. If you find yourself adding a `.razor` file to a host project, it belongs in `.Pages`.

### Commands

**The cwd is split now.** `aspire.config.json` and `template.slnx` are at the **workspace
root**, so anything naming them runs from there; the `dotnet` commands that take `src/` or `tests/`
paths still run from **`base-project/`**, and the events slice adds a third cwd,
**`event-project/`**. Each block below says which. `aspire run` itself is
happy anywhere, because the CLI walks *up* to `aspire.config.json`.

```bash
# --- from the workspace root ---

# Run everything (API + both front ends + dashboard). Also works from base-project/ or any
# directory below it, because the CLI walks up to aspire.config.json.
aspire run

# Same thing without the Aspire CLI
dotnet run .aspire/apphost.cs

# The MAUI host does not start automatically — see "Ports and resources" below
aspire resource maui start

# Switch the database provider for one run (default is postgres, in .aspire/appsettings.json)
$env:Database__Provider = "sqlserver"; aspire run

# Include the optional event-sourcing slice for one run (default is off, same file). Which event
# store runs follows Database:Provider - see "Events" below.
$env:Events__Enabled = "true"; aspire run

# On any branch, swap the durable local queues for Azure Service Bus and the Functions consumer -
# the shape a deployment runs. Starts the Service Bus emulator (two more containers, and it accepts
# a EULA) plus Azurite, and needs Azure Functions Core Tools (func) on PATH; default is "local".
$env:Events__Enabled = "true"; $env:Events__Transport = "azureservicebus"; aspire run

# Build the C# side
dotnet build template.slnx

# Build and install the dotnet new templates (needs PowerShell 7 on PATH)
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
# Uninstall first, and do NOT use --force. See "Reinstalling the pack" in templating/README.md.
dotnet new uninstall AspireStarter.Templates
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg

# The aspire-addon CLI: test, pack, install. Reinstall after a rebuild with `dotnet tool uninstall -g
# AspireStarter.Addon`, then this install again - NOT `dotnet tool update`, which sees the same 1.0.0
# and keeps the old build. The installed DLL's ProductVersion ends +<commit> it was built from.
# The plain add-source flag is refused under package source mapping.
dotnet test tooling/tooling.slnx
dotnet pack tooling/AspireStarter.Cli -c Release -o artifacts/nupkg
dotnet tool install -g AspireStarter.Addon --configfile tooling/install.nuget.config

# Terraform, from IaC/ at the workspace root. Validate only - nothing here is ever applied from
# this workspace, which is template source rather than a deployed application.
cd IaC
terraform init -backend=false && terraform validate && terraform fmt -check -recursive
# The data store is a module and only one is live at a time. To check the other two, edit the
# `source` line in database.tf and re-run init + validate. That line is the one thing the dbProvider
# scaffolding parameter rewrites.

# --- from base-project/ ---
cd base-project

# Add a migration - ALL THREE, or the providers drift. Needs dotnet-ef 10.x.
dotnet ef migrations add <Name> --project src/template.api.migrations.postgres  --startup-project src/template.api -- --provider postgres
dotnet ef migrations add <Name> --project src/template.api.migrations.sqlserver --startup-project src/template.api -- --provider sqlserver
dotnet ef migrations add <Name> --project src/template.api.migrations.sqlite    --startup-project src/template.api -- --provider sqlite

# Test the C# side
dotnet test tests/template.api.tests          # API suite; no Docker, in-memory SQLite
dotnet test tests/Template.Blazor.Maui.Tests  # Blazor suite; bUnit

# React, from src/ui/react
npm install
npm run dev         # Vite dev server, standalone (no AppHost)
npm run build       # runs `tsc -b` first, then vite build
npm run typecheck   # tsc -b only
npm run lint        # oxlint — NOT ESLint
npm run api:types   # regenerate src/lib/api.generated.ts from contracts/api.openapi.json.
                    # NOT part of `npm run build` - the output is committed

# Regenerate the contract itself, from base-project/. Do this BEFORE either codegen above.
UPDATE_CONTRACT=1 dotnet test tests/template.api.tests --filter "Category=ContractTest"

# Regenerate the Blazor Refit client, from src/ui/blazor/Template.Blazor.Maui.Pages
dotnet tool restore     # the manifest lives at src/ui/blazor/.config/, not at the root
dotnet refitter --settings-file .refitter

# --- from event-project/ ---
cd event-project

# Unit tests for the events slice. No Docker: the suite links src/shared/Domain and Contracts only,
# so nothing here touches an event store or Wolverine for real.
dotnet test tests/template.events.tests

# Standalone, without the AppHost. Each host needs whatever its appsettings.Development.json points
# at: a local Postgres for template.events, a local SQL Server 2025 for .polecat, nothing at all
# for .fisher.
cd src/template.events && dotnet run
```

`aspire run` needs the CLI on `PATH`, and `apphost.cs` pins `Aspire.AppHost.Sdk@13.5.1`, so an
older CLI will not match. Update with `dotnet tool update --global aspire.cli`. Bumping the pin
later is `aspire update`, which rewrites the `#:` directives rather than you editing them by hand.

### Ports and resources

| Resource | What | Port |
| --- | --- | --- |
| `pg` / `sql` | Postgres 18.3, or SQL Server **2025** Express (pinned `2025-latest`, for Polecat), per `Database:Provider` | assigned by the AppHost |
| — | `Database:Provider=sqlite` declares **no** resource: a local file, no container, no Docker | — |
| `api` | template.api | `5058` http (`7247` https, unused — see below); dashboard link opens `/api-docs` |
| `ui` | React / Vite | assigned by the AppHost via `PORT`; `5173` standalone |
| `blazor-web` | Blazor WebAssembly | `5280` http, `7115` https |
| `maui` | MAUI Blazor Hybrid | not a service; `WithExplicitStart` |
| `events-pg` | Postgres 18.3 event store, database `eventstore` — **postgres branch only**, when `Events:Enabled` | assigned by the AppHost |
| — | on the sqlserver branch the event store is the `eventstore` database on `sql` above — no extra resource | — |
| — | on the sqlite branch it is a local file — no resource, no Docker | — |
| `events` | the event-store host matching `Database:Provider` — only when `Events:Enabled` | `5090` http (`7290` https, unused); dashboard link opens `/api-docs` |
| `servicebus` | Azure Service Bus emulator + its companion SQL Server — only when `Events:Transport=azureservicebus` | `5672` AMQP (pinned), `5300` management |
| `functions` | `template.events.functions` under Functions Core Tools, plus its implicit Azurite host storage — only when `Events:Transport=azureservicebus` | assigned by the AppHost |
| — | Aspire dashboard | `15087` http, `17143` https |
| — | Dashboard OTLP | `19113` gRPC, `19114` http (https profile: `21222` / `21223`) |

Sources: each project's `Properties/launchSettings.json`, `.aspire/apphost.run.json` (mirrored in
the `profiles` block of `aspire.config.json`), `src/ui/react/vite.config.ts`.

The `events*`, `servicebus` and `functions` rows are absent from a default `aspire run`. They appear
only when `Events:Enabled` is true, which of the store rows appears follows `Database:Provider`, and
the last two need `Events:Transport=azureservicebus` as well — see "Events" below. `events` pins its `http` launch profile like `api` does, but for a
weaker reason: nothing hard-codes its address, so the pin only keeps the README's `curl` examples
and this table honest.

The `api` row's dashboard link is rewritten by `WithUrlForEndpoint("http", ...)` in `apphost.cs` so
it opens the Swagger UI page at `/api-docs` instead of the API root, which 404s — nothing is routed
at `/`. That form replaces the endpoint's URL rather than adding a second one, and the path is
relative, so it follows the port rather than pinning it a fourth time.

**The API's `http` launch profile is pinned in `apphost.cs`** (`LaunchProfileName = "http"`). That
is load-bearing twice over. Both Blazor hosts read a hard-coded `ApiBaseAddress`, so the port has
to be deterministic; and serving the WASM page over https while that address stays http is a
mixed-content block in the browser. Change the API's port and three files have to change with it:
`src/ui/blazor/Template.Blazor.Maui.Web/wwwroot/appsettings.json`,
`src/ui/blazor/Template.Blazor.Maui/appsettings.json` (two entries — the second is the Android
emulator's `10.0.2.2`), and this table. Because the profile is pinned, `API_HTTPS` is never set and
`vite.config.ts` falls through to `API_HTTP`.

`maui` is held back deliberately. It is a desktop client rather than a service, and it only builds
where its workload and target framework line up, so auto-starting it would break `aspire run` on
any box without the MAUI workload. `Template.Blazor.Maui.Pages` is not a resource at all — a Razor
Class Library has no entry point to launch.

### Architecture notes — the non-obvious wiring

- **`WithReference(api)`** on the React app injects the API's resolved endpoints as `API_HTTP` /
  `API_HTTPS`. `src/ui/react/vite.config.ts` reads those to target its `/api` proxy. The proxy is
  why the browser stays same-origin with the API and why `traceparent` flows from browser spans
  into the API's spans.
- **`WaitFor(api)`, not `WithReference`, on `blazor-web`.** `WithReference` injects
  service-discovery environment variables and neither Blazor host can read them: the WASM app's
  configuration is fetched by the browser from `wwwroot/appsettings.json`, and `MauiProgram` builds
  its configuration from an embedded JSON stream and nothing else. `WaitFor` still gates startup on
  the health check, which is the half of `WithReference` that buys something here.
- **`WithOtlpExporter(OtlpProtocol.HttpProtobuf)`** in `.aspire/apphost.cs`. HttpProtobuf is
  mandatory: browsers cannot speak the gRPC protocol the .NET services use for OTLP.
- **`envPrefix: ['VITE_', 'OTEL_']`** in `vite.config.ts`. Vite only forwards prefixed variables to
  the bundle, so this widening is what lets `src/ui/react/src/lib/telemetry.ts` read the AppHost's
  `OTEL_*` values from `import.meta.env`. Outside the AppHost no endpoint is set and RUM turns
  itself off — that is the intended standalone behavior, not a bug to fix.
- **`ui.PublishAsStaticWebsite("/api", api)`** puts YARP in front on publish, reverse-proxying
  `/api` to the API so production stays same-origin exactly as dev does. Still gated behind the
  `ASPIREJAVASCRIPT001` experimental warning.

### Persistence — the non-obvious wiring

The sample rows are read from a store, not generated. Four rules matter here.

**One value drives both halves.** `.aspire/appsettings.json` sets `Database:Provider` (`postgres` |
`sqlserver` | `sqlite`). `apphost.cs` reads it to decide which container to start — or, for
`sqlite`, to start none and inject a file path instead — and forwards it to the API as
`Database__Provider`; `Startup/PersistenceConfigurator.cs` reads it again to pick the EF provider.
Change it in one place only. Every branch is live code — all compile, all run, all are tested.

**`sqlite` is the odd one out**, deliberately: no container, no Docker, and no Aspire client
integration, so it also registers no EF health check. `aspire run` comes up anywhere. A scaffold
bakes one provider in and the others are not shipped.

**Why there are four new projects and not one folder.** EF discovers migrations by scanning a
single assembly, and two providers' migrations cannot share one: the SQL differs and the migration
ids collide. So there is a migrations project per provider (Microsoft's documented answer). Those
projects need the `DbContext` type, and `template.api` must reference *them* so EF's `Assembly.Load`
finds them at run time — which is a cycle unless the `DbContext` lives somewhere both can see.
Hence `template.data`. Do not try to collapse this back into `template.api/Data/`; it does not
build, and it does not run.

**`GetMigrations().Any()` in `InitializeDatabaseAsync` is load-bearing, not defensive.** Migrations
live in provider-specific assemblies, so a context configured for a provider that has none finds
none and builds the schema from the model instead. That is the branch the test suite's in-memory
SQLite override lands on, and it is the whole reason `dotnet test` still needs no Docker. Nothing in
the application knows it is under test.

**Seeding checks staleness, not emptiness.** Both containers use `WithDataVolume()`, so rows outlive
`aspire stop`. A "seed when empty" check would keep serving whatever dates were current when the
volume was created. `WidgetSeeder` rewrites the table when the newest row is no longer five days
out. Drop the volume to start genuinely clean.

Two consequences worth knowing before changing anything here:

- **The entity is the wire contract, and the wire contract is generated from.** `Widget` is both
  the EF entity and the response type, so its key and its `[NotMapped]` computed property are
  serialized and appear in the OpenAPI schema — and both front ends' types are generated from that
  schema. Changing a property here changes two generated files; see "The contract" below.
  `required` on a property is load-bearing rather than decorative: it reaches the schema as a
  required field, which is what keeps the generated TypeScript from making it optional.
- **`/widgets` does not change on Reload**, because the rows are stable. The README records what
  replaced that signal: the dashboard's trace view, or `/harness/latency`.

The test override lives in `tests/.../Infrastructure/ApiFactory.cs` and needs three things
that are each easy to get wrong: the connection string must arrive via `UseSetting` (the Aspire
integration refuses to register without one, and `ConfigureAppConfiguration` is applied too late);
`IDbContextOptionsConfiguration<AppDbContext>` must be removed, because since EF Core 9 *every*
registered one is applied and Aspire's would stack Npgsql onto the same options object; and the
replacement must be `AddDbContextPool`, because the Aspire integration leaves behind a singleton
`IDbContextPool` that a scoped `AddDbContext` cannot satisfy.

### The contract — the API is the source of truth

`base-project/contracts/api.openapi.json` is committed, and it is the **only** definition of this
API's wire shape. Both front ends generate from it; neither declares a DTO by hand.

```bash
# from base-project/ - rewrite the document from what the API actually serves
UPDATE_CONTRACT=1 dotnet test tests/template.api.tests --filter "Category=ContractTest"

cd src/ui/react && npm run api:types                              # -> src/lib/api.generated.ts
cd src/ui/blazor/Template.Blazor.Maui.Pages && dotnet refitter    # -> Providers/ApiClient.generated.cs
```

`docs/ADDING-A-RESOURCE.md` is the end-to-end recipe and is the document to point an agent at.
Five things here are not obvious:

- **The document is produced by a test, not by an MSBuild task.** `Harness/ContractTests.cs` uses
  the suite's existing `ApiFactory`, so it captures the document a real client would fetch with no
  package on `template.api`, no host execution during the build and no database. A build-time
  `Microsoft.Extensions.ApiDescription.Server` task would put all three at risk on a clean machine.
- **Neither codegen step runs during a build, and both outputs are committed.** That is what lets
  the React app build with no .NET SDK present and the Blazor library build with no Node — the same
  bargain `routeTree.gen.ts` already makes. `ContractTests` catches a stale *document*; nothing
  catches a stale *client* except the compiler, when someone uses the missing member.
- **`[ProducesResponseType<T>]` is what puts a schema in the document.** `Ok(new { ... })` produces
  a bare 200 and an untyped generated member.
  `ContractTests.ServedDocument_GivesEverySuccessResponseASchema` fails the build over it, which is
  why `HarnessController` declares a `HarnessState` record rather than returning anonymous types.
- **`NumericUnionTransformer` is load-bearing, not tidying.** The .NET generator describes an
  `int32` as `"type": ["integer", "string"]` — legal JSON Schema, and every downstream generator
  honours it, so `Widget.Id` would reach TypeScript as `number | string`. Emitting OpenAPI 3.0
  instead is *worse*: the downgrade drops the union and leaves the property with no type at all.
- **`servers` is stripped in `Normalize`.** It describes wherever the document was fetched from —
  `http://localhost/` under `WebApplicationFactory`, `:5058` under `aspire run`, a container app
  hostname once deployed — so committing any one of them makes the contract disagree with itself
  depending on who generated it. No client here reads it: React is hard-wired to the same-origin
  `/api` proxy, and Refit takes its base address at registration.

The paths both codegens use (`../../../contracts/…` from React, `../../../../contracts/…` from the
Blazor RCL) are **identical in this workspace and in a scaffold**, because staging flattens
`base-project/` onto the root uniformly. No rewrite in `stage.ps1` is needed, and none should be
added.

### CORS — three clients, three answers

`template.api` **does** configure CORS, in `src/template.api/Startup/CorsConfigurator.cs`, and it
is needed by exactly one of its three clients:

| Client | How it reaches the API | CORS? |
| --- | --- | --- |
| React / Vite | same-origin `/api` proxy | no |
| Blazor WebAssembly | browser `fetch` to an absolute origin — genuinely cross-origin | **yes** |
| MAUI | native `HttpClient`; no Origin header, no preflight | no |

The allowed origins live in `src/template.api/appsettings.json` under `Cors:AllowedOrigins` and
must match the WASM host's `launchSettings.json`. They are declared in the root appsettings rather
than only in the Development one on purpose: `WithOrigins([])` allows nothing, and a blocked
request still reaches the controller and still returns 200 — it is only missing the
`Access-Control-Allow-Origin` header, which the browser enforces on the client. That asymmetry is
expensive to diagnose, so `WarnOnMissingCorsOrigins` says it at startup instead.

### Logging — Serilog in front, OpenTelemetry out

`AddServiceDefaults()` makes Serilog the front door for logging in every service, wired in
`src/template.servicedefaults/SerilogExtensions.cs`. Two sinks: the console, and
`OpenTelemetryLogForwarder`, which hands every event to the OpenTelemetry logging provider. That
provider is the one way out, and its exporters decide where a record goes: the dashboard over OTLP
whenever the AppHost supplies `OTEL_EXPORTER_OTLP_ENDPOINT`, Application Insights through
`UseAzureMonitor()` whenever `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, both, or nowhere
(`specs/azure-monitor-telemetry.md`, L1-L7).

Things worth knowing before changing it:

- **Serilog does not read `Logging:LogLevel`.** Levels live under `Serilog:MinimumLevel` in each
  service's `appsettings.json`; the old sections were replaced rather than left to rot. A missing
  section is a no-op, not an error. The AppHost keeps its own `Logging` section — it is not a
  service and does not get Serilog. App code logs at Information; `Microsoft`, `System` and the
  events hosts' Marten / Polecat / Fisher / Wolverine at Warning, with `Microsoft.Hosting.Lifetime`
  kept at Information. EF Core's per-command SQL logs are dropped on purpose: the database
  dependency span already records each query.
- **Not `writeToProviders: true`.** That bridge passes the Serilog `LogEvent` itself as the log
  state, which the OpenTelemetry provider cannot read, so every property would be lost.
  `OpenTelemetryLogForwarder` passes the key/value list the provider expects instead: template
  properties and `LogContext` enrichments become attributes (`customDimensions` in Application
  Insights), nested values (`{@Order}`, sequences) become one JSON string each, and the record
  carries the current Activity's trace id.
- **`ConfigureSerilog` runs before `ConfigureOpenTelemetry`.** Its `ClearProviders()` removes the
  host's default Console, Debug and EventSource providers, which the forwarder would otherwise
  write every event to as well; the OpenTelemetry provider is registered after it and survives.
  `LoggingConfigurationTests` in the API suite asserts exactly one provider, and that a record
  reaches it with its properties, its trace id and the right levels.
- **The console keeps Warning and above in Production.** The Container Apps environment already
  copies console output into the Log Analytics workspace Application Insights writes to, so an
  Information line on both would be stored twice. `ConsoleLevelTests` covers it.
- **`UseSerilogRequestLogging` is deliberately absent.** `AddAspNetCoreInstrumentation` already
  records every request as a span, and `Microsoft.AspNetCore` is pinned at `Warning`.
- **Log through `[LoggerMessage]`, inside an entity scope, and `entity.id` is always the surrogate
  key.** CA2254 and CA1848 are build errors (`Directory.Build.props`; `*.tests` projects keep CA2254
  only). A controller gets `[EntityType(typeof(T))]` and `EntityScopeFilter` scopes every `{id}`
  action; elsewhere `logger.ForEntity(type, id, related...)` does. Every record also carries
  `trace_id`, `span_id`, `trace_flags`, `parent_span_id` and, when signed in, `enduser.id` (oid, else
  sub). A natural key such as an email never goes in a log, as `entity.id` or anywhere else
  (`specs/structured-logging.md`, D1-D18).

### Client RUM — the Blazor hosts

Both Blazor hosts export their own telemetry to the dashboard, the counterpart of
`src/ui/react/src/lib/telemetry.ts` on the React side. It lives in
`src/ui/blazor/Template.Blazor.Maui.Pages/Telemetry/` and both hosts call `AddSharedTelemetry` and
then `ActivateSharedTelemetry` — a third shared registration beside `AddSharedViewModels` and
`AddSharedProviders`, for the same reason. Page-view spans come from `MainLayout`, which is the one
component both hosts wrap every page in.

**What each host gets, and why they differ.** MAUI is an ordinary .NET process: traces, metrics and
logs. The WebAssembly host gets **traces and logs only** — see the WASM constraints below.

**The two hosts get their settings from different places, because their runtimes differ.** MAUI is
a process the AppHost starts, so `AddEnvironmentVariables()` in `MauiProgram` is enough. A browser
has no environment to read, and the WASM host's `wwwroot/appsettings.json` is a static file that
cannot carry the dashboard's per-run OTLP API key — so it asks the API instead, at
`GET /client-telemetry/config` (`ClientTelemetryController`, unconditional because `stage.ps1` would
strip a `//#if (includeBlazorWeb)` region out of every base scaffold). It hands out the OTLP settings
in Development only; outside it, it answers only when Application Insights is configured, and then
only with the relay's path (see "Azure — the browser relay" below).
That is also why `api` carries `WithOtlpExporter(OtlpProtocol.HttpProtobuf)` in `apphost.cs`: a
browser cannot speak gRPC, so the endpoint the API hands out has to be one it can post to.

`ASPIRE_DASHBOARD_CORS_ALLOWED_ORIGINS` is already `*` in both `.aspire/apphost.run.json` profiles,
so browser-origin OTLP needs no further dashboard configuration.

**Three WASM constraints, each found the hard way and each silent when broken:**

- **The exporters must take their `HttpClient` from the application's `IHttpClientFactory.`** The
  OTLP exporter's own default client cannot send from the browser, and it fails with no error the
  runtime surfaces — spans reach the exporter and no request is ever made. That is why the providers
  are registered as factories and started by `ActivateSharedTelemetry` rather than built inline. Logs
  never had the problem, because `AddLogging` composes into the application's own service collection
  where the factory already lives.
- **Spans must export one at a time** (`IsSingleThreadedRuntime` → `ExportProcessorType.Simple`).
  The batching processor drains on a dedicated thread that the browser creates without error and
  then never runs.
- **No `MeterProvider` is registered there at all.** Its reader collects on that same never-run
  thread, and neither `PeriodicTimer` nor `Task.Delay` continuations resume in that host, so there is
  no way to drive it from the page either. Registering it would be wiring that looks live and is not.

**Do not use `OpenTelemetry.Extensions.Hosting` here.** Its `AddOpenTelemetry()` starts the SDK from
an `IHostedService`, and a `WebAssemblyHost` never runs hosted services — the providers would be
configured and then never started, again with nothing to say so.

### Azure — the browser relay

In Azure there is no dashboard. The services export to Application Insights through the Azure
Monitor distro (`UseAzureMonitor()` in servicedefaults, on when `APPLICATIONINSIGHTS_CONNECTION_STRING`
is set), and the browser's telemetry goes through the API: `POST /api/otel/v1/traces` and
`/api/otel/v1/logs` (`OtlpRelayController`, `Telemetry/BrowserTelemetryRelay.cs`). Things worth
knowing before changing it (`specs/azure-monitor-telemetry.md`, Q2, R1-R6):

- **OTLP/JSON only, traces and logs only.** React uses the `-otlp-http` exporters on this path
  (`src/lib/telemetryTarget.ts`) and keeps `-proto` for the dashboard; the .NET OTLP exporter has no
  JSON mode, so Blazor uses the hand-written `OtlpJsonTraceExporter` / `OtlpJsonLogExporter`.
- **Spans are re-created, not forwarded.** Each becomes an Activity on the API's own source with
  the browser's trace id, times, kind, status and attributes and `relay.source = browser`; span ids
  are new (an Activity cannot be given one). Browser logs are written through `ILogger` under the
  `Browser` category, so Serilog's levels apply to them.
- **The guard is the security story**: no route without Application Insights; `Sec-Fetch-Site:
  same-origin` (not Origin against Host, which the Static Web App's linked backend breaks) or an
  Origin in `Cors:AllowedOrigins`; 64 KB; 120 requests a minute keyed on the first
  `X-Forwarded-For` entry. `OtlpRelayGuardTests` covers each refusal.
- **Discovery is at run time**, through `/client-telemetry/config`'s `relay` field, so a preview
  needs no build variable. The relay is excluded from the OpenAPI contract.
- **Postgres and SQL Server queries are dependency spans** through the Aspire EF integrations
  (`DatabaseSpanTests`); SQLite has none.
- **Each environment's Workbook** (`IaC/workbooks/telemetry.json`) is that telemetry on one page; its
  JSON must not mention the sourceName token or the resource name (`tests/telemetry.tftest.hcl`).

### The fault-injection endpoints

`template.api` exposes `/harness/latency`, `/harness/fail-next` and `/harness/reset`, backed by
`Services/HarnessFaultInjector.cs`. They exist so both front ends' loading and error states can be
reached without unplugging the network:

```bash
# Delay every /Widget response by 3s, to see the loading state.
curl -X POST http://localhost:5058/harness/latency -H "Content-Type: application/json" -d '{"milliseconds":3000}'

# Make the next 10 requests fail with a 500, then reload /widgets in either front end.
curl -X POST http://localhost:5058/harness/fail-next -H "Content-Type: application/json" -d '{"count":10}'

curl -X POST http://localhost:5058/harness/reset
```

**Use a `count` above 4, not 1.** The Refit clients are registered with
`AddStandardResilienceHandler()`, which retries 5xx a few times and then opens a circuit breaker.
One injected failure gets retried away and never reaches the UI; a large count exceeds the retry
budget. If repeated failures start returning instantly without hitting the server, the breaker is
open — wait it out or call `/harness/reset`.

`/harness/reset` clears faults only and deliberately leaves the database alone. Every `/harness/*`
action returns 404 outside Development. The injector itself is registered
unconditionally because it is inert until something arms it. `HarnessController` is gated by an
injected `IHostEnvironment` rather than by conditionally registering the controller, which keeps
`AddControllers`/`MapControllers` a single unconditional pair.

### Events — the optional Wolverine slice

`event-project/` is a working Wolverine event-sourcing slice that is **off by default**.
`.aspire/appsettings.json` carries `Events:Enabled` (default `false`); when it is true `apphost.cs`
declares the `events` service and whatever store it needs, and when it is false neither exists.
Override for one run with `$env:Events__Enabled = "true"`.

**The flag gates the AppHost and nothing else.** No code inside the events slice reads it, because
with the flag off there is no process running to gate. That is why this is a plain configuration
read and not `Microsoft.FeatureManagement` — which the spec's §6/§10 routing machinery would need,
and which is deliberately absent here (see below).

**The event store follows `Database:Provider`. It is not a second choice.**

| `Database:Provider` | Event store | Where it lives |
| --- | --- | --- |
| `postgres` | Marten | its own `events-pg` container |
| `sqlserver` | **Polecat** | the `eventstore` database on the **same `sql` container** |
| `sqlite` | **Fisher** | a local `eventstore.db` file — **no container, no Docker** |

The transport is the same on every row: durable local queues under `aspire run` by default, Azure
Service Bus once deployed. See "The transport" below.

All three are JasperFx, all three are MIT with no revenue threshold and no licence key, and all
three present the same API: `AddX(...).IntegrateWithWolverine().AddAsyncDaemon(...)`, plus
`[AggregateHandler]` over `FetchForWriting`. The handler code is therefore identical on every
branch, which is the whole reason this is one slice with three hosts rather than three slices. The
paid JasperFx product (CritterWatch, BSL-licensed) is an optional ops console, not a dependency.

**Why the branches differ in shape.** Marten keeps its own container because that is the proven path
and this arrangement should not regress it. Polecat is Marten's API *on SQL Server*, so there is no
engine mismatch to work around — a second database on the API's server is enough, and a second SQL
Server container plus the Service Bus emulator's own companion would be three engines on one
machine. Fisher declares nothing at all, which is what keeps the `sqlite` branch's promise that
`aspire run` needs no Docker.

**Polecat requires SQL Server 2025**, which is why `apphost.cs` pins `mssql/server:2025-latest` on
the `sqlserver` branch: it stores events and documents in the native `json` column type and
generates `JSON_OBJECT` and `GENERATE_SERIES`. Azure SQL Database has that type generally available,
so the deployed path needs nothing extra. Relaxing the tag back to 2022 breaks the event store at
startup while the API carries on working, which makes it look like an events bug.

**The transport: local queues under `aspire run`, Azure Service Bus deployed, on every store.**
`event-project/src/shared/Messaging/ServiceBusTransport.cs` is the one switch, and all three hosts
call it. `Events:Transport` is `local` by default in `.aspire/appsettings.json`: Wolverine's durable
local queues are backed by the event store's own outbox tables, so the whole outbox path — commit,
store, dispatch, acknowledge — is exercised with no broker, and `WidgetCompletedV1Handler` consumes
in-process. `azureservicebus` publishes `WidgetCompletedV1` to the `widget-completed` queue and does
**not** listen on it. The consumer is then `template.events.functions`, an Azure Function whose
`ServiceBusTrigger` calls the same handler class. That makes one handler with two hosts, not two
handlers, which is what keeps Functions from being a second programming model. The switch uses a
real namespace when `AzureServiceBus:FullyQualifiedNamespace` is set (Terraform sets it, together
with `AZURE_CLIENT_ID` for `DefaultAzureCredential`) and the emulator when it is not. Locally,
`azureservicebus` costs the emulator's two containers and Azurite, and needs Functions Core Tools.
On the `sqlite` branch it is the only thing that brings Docker in, which is why it is opt-in there.
Deployed, `events_transport` defaults to `azureservicebus`.

**Why a Function, not a Wolverine listener in a container app.** The two halves want opposite
scaling. The events service holds a projection daemon and an outbox, so it never drops below one
replica, and on SQLite it is pinned to exactly one. The consumer should follow the queue. Flex
Consumption's target-based scaling divides queue length by `maxConcurrentCalls` in the Function's
`host.json` and scales to zero when the queue is empty. The Function never touches an event store,
so it scales freely even on SQLite.

**The wire body is pinned by a test.** A Function is not a Wolverine endpoint, so it reads the
Service Bus body itself through `WidgetCompletedV1Wire.Read`. Wolverine writes **camelCase**; with
`JsonSerializerOptions.Default`, every field silently deserializes to its default value instead of
failing. `WidgetCompletedV1WireTests` writes the body through Wolverine's own `DefaultSerializer`,
so a Wolverine upgrade that changes it fails there, not in a deployed Function.

**MassTransit is deliberately not here.** v9 is source-available and requires a licence key at run
time, and its free tier is a discount you apply for — neither belongs in a template strangers run.
v8 is Apache-2.0 but reaches end of life at the end of 2026. Wolverine's own
`WolverineFx.AzureServiceBus` covers the same ground under MIT, so no second messaging framework is
needed. Rebus, DotNetCore.CAP and Brighter are all MIT and genuinely viable alternatives if one is
ever wanted; each would mean a second programming model for no capability gain.

**Cosmos DB is deliberately not here either.** There is no maintained, C#-idiomatic OSS
event-sourcing library for it — Equinox is F#-first, Eveneum is a storage mechanic with no
projections or daemon, and CosmoStore and SimpleEventStore are abandoned. `WolverineFx.CosmosDb` is
outbox and saga storage only. Polecat on Azure SQL is the Azure-native answer that keeps the
programming model identical.

Seven things that will bite you:

- **One event store per project, never two.** `JasperFx.Events` ships a source generator that emits
  an evolver type per projection, so a project referencing two stores fails to compile with
  duplicate generated types (CS0433) — an error a long way from its cause. This is why there are
  three host projects rather than conditional regions in one.
- **No handler may call `SaveChangesAsync()`.** `Policies.AutoApplyTransactions()` in `Program.cs`
  owns the transaction and commits it around each handler. Calling it yourself commits early
  and takes the outgoing messages out of the same unit of work — which is precisely the atomicity
  the outbox exists to provide.
- **`GET /widgets/{id}` is eventually consistent.** `WidgetSummaryProjection` is registered
  `ProjectionLifecycle.Async` and runs on the daemon, so a read straight after a write can
  return the old state or a 404. That is correct behaviour, not a race. `ProjectionLifecycle.Inline`
  is the one-line switch if a scaffold wants read-your-writes.
- **`Widget` is not a registered projection.** It is live-aggregated from its stream on every command
  via the `Apply` methods, which is what `FetchForWriting<Widget>` — the call `[AggregateHandler]`
  generates — uses. `WidgetSummary` is the registered one, and it is read-side only. Registering
  `Widget` too would be a second answer to the same question.
- **Domain events are not integration contracts.** `WidgetCompleted` stays inside the aggregate
  boundary; `WidgetCompletedV1` is the versioned public fact, carries a stable wire name via
  `[MessageIdentity]`, and is mapped explicitly in `WidgetDecisions.Complete`. A breaking change means
  a new type, not an edited one.
- **`shared/` is linked, not referenced, and that is what keeps MVC simple.** `WidgetsController`
  compiles into each host, so `AddControllers()` finds it by its normal entry-assembly scan and no
  `AddApplicationPart` is needed. Turn `shared/` into a class library and you need that call back —
  on top of the source-generator problem that made it linked source in the first place.
- **`WolverineFx.RuntimeCompilation` is not optional.** Wolverine 6 removed the Roslyn compiler from
  the core package, and its default `TypeLoadMode.Dynamic` compiles handler glue at startup, so
  without that reference the service throws `InvalidOperationException` before serving anything
  — at run time, not at build time, which is why it survives `dotnet build` and `dotnet test`
  unnoticed. Don't prune it as an unused dependency: nothing in the source names it.

Deliberately **not** implemented, so a future session does not go looking for it: the spec's §6/§10
migration machinery (`Wolverine.ExecutionMode`, `KillSwitch`, `CanaryPercentage`,
`IApplicationDispatcher`, legacy adapters — there is no legacy path here for them to route to);
any external broker (`Policies.UseDurableLocalQueues()` keeps messages in-process but still durable
on the Postgres outbox tables); `Microsoft.FeatureManagement`; and integration tests, which would
need a live Postgres and so would break the workspace's Docker-free `dotnet test`.

`event-project/README.md` has the `curl` walkthrough and the file-by-file map.

### IaC — Terraform and Azure

`IaC/` at the workspace root is a Terraform configuration for Azure. It is the deployment answer the
rest of this workspace did not have: the API and the optional events service on **Azure Container
Apps**, the front ends on **Azure Static Web Apps**, a managed store matching whichever EF
provider was scaffolded, and with the events slice **Azure Service Bus** plus an **Azure Functions**
consumer on Flex Consumption. `IaC/README.md` carries the operator-facing detail; this section is the
part a future session needs before editing anything.

**One `.tf` file per add-on, and no markers.** Terraform loads every `*.tf` in a directory, so an
add-on contributes by dropping a file in — `frontend.react.tf`, `frontend.blazor-web.tf`,
`containerapp.events.tf`, `servicebus.tf`, `functions.tf`. This is the one place `dotnet new`'s inability to merge
costs nothing, which is why there is no `<<addon-iac>>` marker and why the wiring files have almost
nothing to say about it. `stage.ps1` distributes the subsets.

**The data store is a module, and that is not a style choice.** `containerapp.api.tf` has to
reference whichever provider shipped, so three sibling `database.*.tf` files would either collide on
resource names here or leave a dangling reference in a scaffold. Every directory under
`IaC/modules/database-*` exposes the same outputs — `provider_name`,
`appdb_connection_secret_id`, `eventstore_connection_secret_id`, `volumes`, `volume_mounts`,
`single_writer`, `server_name`, `storage_account_name` — so `containerapp.api.tf` reads only
`module.database.*`. The single `source` line in `database.tf` is what `dbProvider` rewrites, through
a `dbProviderModule` switch symbol beside the three that already exist. **Add an output to one
module and you must add it to all three**, or the scaffold that picked a different provider stops
planning.

**No `//#if` regions in any `.tf` file, ever.** `.tf` is not a file type the scaffolding engine knows
how to conditionally process, and `Remove-FalseRegions` only resolves symbols that are statically
false. Everything conditional here is a pruned file or a switched `source` line.

**`azurerm_container_app` declares a block named `template`, which is this workspace's `sourceName`.**
Substitution would rename it and the configuration would stop parsing. So `containerapp.api.tf` and
`containerapp.events.tf` are `copyOnly` in their `template.json`, and **every other `.tf` file must
not contain that bare word at all** — `Assert-TerraformSubstitutionSafe` in `stage.ps1` fails the
build if one does. The same trap makes HCL's `templatefile()` function unusable here.

Seven things that will bite you:

- **A container app probe must not target `/health`.** `MapDefaultEndpoints()` maps `/health` and
  `/alive` in Development only and these containers run as Production, so the probes use
  `/openapi/v1.json`, which both `Program.cs` files map unconditionally. A `/health` probe fails
  every revision and looks like a broken image.
- **Nothing applies migrations.** `InitializeDatabaseAsync` returns early outside Development by
  design. `.github/workflows/deploy.yml` runs `dotnet ef database update` instead — and on the
  sqlite branch, where there is no server to connect to, it builds the file locally and uploads it
  to the Azure Files share once.
- **The Blazor WebAssembly origin needs a second apply.** `containerapp.api.tf` is base-template
  content and cannot reference a resource declared in an add-on file, so the Static Web App's
  hostname reaches `Cors:AllowedOrigins` only on a second pass. The workflow does it; by hand it is
  one line in `envs/<env>.tfvars`. Skipped, the API logs healthy 200s while the browser blocks every
  request.
- **The events service's address is computed, not referenced**, for the same reason — `locals.tf`
  builds it from the app name and the environment's default domain, so the seam exists in every
  scaffold including the ones with no events slice.
- **Each front end reaches the API the way it does locally.** React's Static Web App links the API
  container app and goes same-origin; Blazor WebAssembly goes cross-origin and needs CORS, exactly as
  the three-clients-three-answers table above describes. A scaffold only ever has one of them, which
  is also what keeps the "one linked backend per container app" limit from ever mattering.
- **The Functions consumer is code, not an image.** `functions.tf` runs it on Flex Consumption from
  a `deployment` blob container in its own storage account, reached by the shared managed identity
  with shared keys disabled. The workflow publishes it as a zip *after* the apply that creates the
  app, and excludes `*.events.functions` from the events image glob. Flex is not in every region.
- **`containerapp.events.tf` is `copyOnly`, so it must not name the queue.** The queue name contains
  the resource token, and `Assert-ResourceTokenSafe` fails staging if a copyOnly file contains it.
  Say "the queue in servicebus.tf" there instead.

The Dockerfiles (`src/template.api/Dockerfile`, one per events host) take the **scaffold root** as
their build context, because the API references three sibling projects and each events host compiles
`src/shared/` in through a glob. The API's builds from `base-project/` as-is; the events ones
describe the staged layout and will not build from `event-project/`, which is the same asymmetry
their `csproj` servicedefaults path already has.

### Central package management

Versions live in `Directory.Packages.props`; `.csproj` files carry `PackageReference` with **no**
`Version` attribute. There are now **three** such files — `base-project/`, `event-project/` and
`.aspire/` — and they do not merge: NuGet stops at the nearest one walking up from a project, so
`event-project/` inherits nothing from `base-project/` and repeats what it needs. A workspace-root
file would be shadowed by `base-project/`'s and so would serve `event-project/` alone; that is why
there isn't one. Two deliberate opt-outs:

- `Template.Blazor.Maui.csproj` sets `ManagePackageVersionsCentrally=false`, because MAUI packages
  resolve through `$(MauiVersion)`, which the workload sets after `Directory.Packages.props` has
  already been evaluated.
- `.aspire/Directory.Packages.props` turns CPM off for the AppHost directory. `aspire run` builds
  the file-based AppHost by generating a synthetic `apphost.cs.csproj` next to it; that project
  sits inside the repository, so any `Directory.Packages.props` an ancestor directory holds would
  apply to it and NuGet would reject the version on the `#:package Aspire.Hosting.JavaScript@13.5.1`
  directive with **NU1008**. Since `.aspire/` moved to the workspace root nothing above it declares
  CPM today, so the file is currently belt-and-braces — keep it: it is what makes the AppHost
  self-describing, which is what `aspire update` expects.

Three pins in `base-project/Directory.Packages.props` are load-bearing rather than lazy, and are
commented there (`event-project/Directory.Packages.props` repeats the two that apply to it): **NSubstitute must stay 5.3.0** (AutoFixture.AutoNSubstitute 4.18.1 requires
`[2.0.3, 6.0.0)`), **xUnit stays on v2** (AutoFixture.Xunit2 is the stable AutoData integration),
and **Shouldly's string `ShouldContain` is case-insensitive by default** — pass `Case.Sensitive`.

`Directory.Build.props` declares `Configurations=Debug;Release;Development`. `Development` is a
real third configuration: the `#if DEBUG || DEVELOPMENT` blocks in `MauiProgram.cs` key off it. It
must be declared there **and** in `template.slnx` or `dotnet build -c Development` fails with
MSB4126.

### Code conventions

**C#** — `net10.0`, nullable and implicit usings enabled, file-scoped namespaces, top-level
`Program.cs`. Every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`
(that is what supplies `/health` and `/alive`, which the AppHost uses to gate dependents — note
both are mapped only in Development). `src/template.servicedefaults/Extensions.cs` is the stock
Aspire file — extend it, don't rewrite it.

`template.api` also serves its OpenAPI document at `/openapi/v1.json` and a Swagger UI rendering
of it at `/api-docs`, via `Swashbuckle.AspNetCore.SwaggerUI` — UI middleware only, no second
document generator, no transitive dependencies (note it is *not* `SwaggerGen`, one letter away).
The page has a working **Try it out** button; it is same-origin with the API, so it needs no CORS
entry, and every asset is embedded, so it works offline. `RoutePrefix` must be set explicitly —
it defaults to `swagger`, and `/api-docs` is what the AppHost link, the README and the tests
expect; `SwaggerEndpoint` is what aims it at `MapOpenApi`'s document. Unlike
`/harness/*`, **neither route is gated on Development**; that is deliberate, and the README records
the consequence — the document advertises the Development-only `/harness/*` endpoints, which are
now one click from executing.

**Blazor** — all UI lives in `Template.Blazor.Maui.Pages`; neither host has UI of its own. To add
a page: a ViewModel in `ViewModels/`, a `.razor` in `Pages/` with
`@inherits ViewModelComponentBase<T>`, a `NavLink` in `Layout/NavMenu.razor`, and one
`AddTransient` line in `Mvvm/MvvmServiceCollectionExtensions.AddSharedViewModels`. That single
method is called by both hosts, which is what stops their service graphs drifting apart. A page
with no state of its own needs neither — `Home.razor` and `StyleGuide.razor` are plain components.

**New UI goes through the design system, on both sides.** React composes from
`src/ui/react/src/components/ui/`; Blazor composes from FluentUI, through the mapping the style
guide documents. `Template.Blazor.Maui.Pages/Components/` holds almost nothing on purpose —
wrapping FluentUI component-for-component would be an abstraction over single-use code.
`AppField.razor` is the one exception, and it earns it: FluentUI's label, description and
validation wiring differs between `FluentTextField`, `FluentSelect` and `FluentCheckbox`, so
without it every form hand-wires `for` and `aria-describedby` and gets one wrong. A field is
invalid when `Error` is non-empty — there is no separate flag to fall out of step with the message.

There is **no `StateHasChanged` and no manual refresh call anywhere**. `[ObservableProperty]`
raises `PropertyChanged`; `ViewModelBinder` subscribes and dispatches
`InvokeAsync(StateHasChanged)`. Use `ViewModelLayoutBase<T>` for layouts and
`ViewModelComponentBase<T>` for everything else. Three things that will bite you:

- **Swap collections, don't mutate them.** Nothing listens to `CollectionChanged`. Assign a new
  list (`Items = [.. Items, created];`) so `PropertyChanged` fires.
- **`ValueExpression` cannot reference a static member.** Every FluentUI input builds a
  `FieldIdentifier` from its `ValueExpression`, and that rejects statics. Bind to an instance
  property that proxies the static — `MainLayout.IsDarkTheme` over `ThemeStore.IsDark`, and
  `WidgetsViewModel.Density` over `PreferencesStore.Density`.
- **A FluentUI component that ships a JS module is a risk, not a ban.** bUnit runs with
  `JSRuntimeMode.Loose`, under which an imported module resolves to `null` — so what throws is a
  component that then *calls into* it. Plenty ship a module and never do: `FluentButton`,
  `FluentTextField`, `FluentCheckbox`, `FluentTab`, `FluentDivider` and `FluentDialogProvider` all
  render fine, which `StyleGuidePageTests` is what proves — it renders every approved component in
  one place, so a FluentUI upgrade that breaks one fails there. `FluentNavMenu` and `FluentDataGrid`
  are the two that genuinely throw and stay out; the shared nav and the list table are hand-rolled
  because of them. If a component does throw, the fix is a `JSInterop.SetupModule` in
  `FluentUiTestContext`, not deleting the usage.

ViewModels are **transient**, not scoped: a WASM host has one DI scope for the whole application
lifetime and a MAUI BlazorWebView's scope spans the entire WebView, so a scoped ViewModel is
effectively a singleton. Genuinely shared state belongs in `Stores/` (`ThemeStore`,
`PreferencesStore`) instead.

**TypeScript** — `@/*` aliases to `src/`. Typed fetch wrappers live in `src/lib/api.ts` and
nowhere else. Routes are files under `src/routes`; the path *is* the URL.
`src/routeTree.gen.ts` is generated by the router plugin — **never edit it**. Note `strict` is
off but `noUnusedLocals` is on, so an unused import fails the build.

**State, in order of preference:**

1. TanStack Query for one-off reads (`src/routes/index.tsx` shows the plain form).
2. A TanStack DB collection in `src/db/collections.ts`, read with `useLiveQuery`, for server
   data several components share or mutate.
3. TanStack Store (`src/store/preferences.ts`) for client-only UI state the server doesn't own.

**Design system** — one token vocabulary, two implementations, and a living style guide per front
end at `/styleguide`. **The rules live in [`docs/DESIGN.md`](docs/DESIGN.md)** — the token names and
what each group is for, why a raw colour or a literal `rounded-lg` is refused, why a retuned fill has
to have its pair re-measured, the per-component accessibility contracts, and why the FluentUI
JS-module constraint is a test-harness limit rather than licence to hand-roll a component. Its
*Page composition* section owns the screen-level half: the home route is a dashboard giving a
complete view of every entity, a detail view is a **modal over its list** addressed by the URL
rather than a page you navigate away to, and a full-page detail is an exception with three named
conditions. Read it before touching a screen; do not restate it here.

The two files it refers to, in this workspace:

- `src/ui/react/src/index.css` — Tailwind v4 `@theme`, both palettes in one declaration via
  `light-dark()`. There is no `tailwind.config.js`. `src/store/preferences.ts` writes
  `<html data-theme>` and the attribute selectors override the OS setting in both directions.
- `src/ui/blazor/Template.Blazor.Maui.Pages/wwwroot/app.css` — the same names, aliased onto FluentUI's
  own variables with literal fallbacks, so `FluentDesignTheme` carries them when the theme flips.

**Adding an endpoint** — follow the three-step recipe in `src/ui/react/README.md` for the React
side; on the Blazor side add the method to the Refit interface in `.Pages/Providers`.

### Template readiness

These are the rules that make this repo different from a normal app.

- The `template.` prefix on `template.api` / `template.data` / `template.servicedefaults` /
  `template.api.migrations.postgres` / `template.api.migrations.sqlserver` is the intended
  `sourceName` for `dotnet new` substitution. **Do not rename these projects.** Note the two
  migrations assemblies are also named as *strings* in `template.data/DatabaseProvider.cs`, so a
  rename has to reach there too or EF fails to find them at run time. Do not introduce
  the bare string `template` anywhere blind substitution would corrupt it.
- No absolute paths, no machine-specific values, nothing that assumes this folder's location.
- Keep package versions pinned explicitly, as `Directory.Packages.props` already does. A scaffold
  has to build on a clean machine.
- Don't add a dependency without asking. Every one lands in every scaffolded project.
- Keep `src/ui/react` runnable standalone (`npm run dev`, no AppHost). The no-op path in
  `telemetry.ts` and the `VITE_API_TARGET` fallback in `vite.config.ts` are load-bearing for that.

### Templating — how this workspace becomes `dotnet new` templates

**It is implemented.** Four templates ship in one pack, and a Claude skill drives them.

| shortName | Emits | Parameters |
| --- | --- | --- |
| `aspire-base` | solution, `.aspire/`, API, data, servicedefaults, the chosen migrations project, API tests, `contracts/`, `docs/` | `--dbProvider postgres\|sqlserver\|sqlite`, `--resource` |
| `aspire-ui-react` | `src/ui/react/` | `--resource` |
| `aspire-ui-blazor` | Pages RCL always; WASM host if `--web`; MAUI host if `--maui`; the bUnit suite | `--web`, `--maui`, `--resource` |
| `aspire-events` | `src/shared/`, the one host matching the store, `tests/template.events.tests/` | `--eventStore marten\|polecat\|fisher` (must match `--dbProvider`), `--resource` |

**`--resource` must be passed to every template in a scaffold, exactly like `-n`.** It names the
sample resource — `--resource Product` yields `Product.cs`, `ProductsController`, `/products` and a
`contracts/api.openapi.json` describing `/Products`. Pass different values to two templates and the
UI calls an endpoint the API does not serve; nothing fails at build time. The default is `Widget`.

The plural is **mechanically the singular plus `s`**: every plural in the source is spelled
`Widgets`, so substituting `Widget` gets `Products` for free. This is why there is no second
parameter for it — two `replaces` tokens where one is a prefix of the other have no defined
ordering. `--resource Person` scaffolds `Persons`, and fixing that is a rename after the fact.

Each of the four also carries its share of `IaC/`: the base takes everything but the five add-on
`.tf` files, and each add-on takes exactly the ones it owns. Terraform loads every `*.tf` in a
directory, so that is the whole of the composition — see "IaC" above.

```bash
pwsh templating/stage.ps1                                            # assemble artifacts/templates/
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
# Uninstall first, and do NOT use --force. See "Reinstalling the pack" in templating/README.md.
dotnet new uninstall AspireStarter.Templates
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg
```

`/boot-strap-app` (in `.claude/skills/`) is the entry point: it interviews the user on the four
decisions — data store, events, and which single front end — runs the templates, applies
the wiring, generates the scaffold's README and CLAUDE.md, and verifies the result.

**Staging, not `replaces`.** The live layout is not the scaffold layout: the solution and `.aspire/`
sit at the workspace root while the code sits under `base-project/`, and the events slice is a
sibling that reaches back into `base-project/`. `templating/stage.ps1` copies each template into the
shape a scaffold wants — flattening `base-project/` onto the root, folding the events slice into
`src/` and `tests/` — and rewrites the handful of paths that implies. Doing it there rather than
with `replaces` symbols keeps every path rewrite in one readable, testable place.

It also resolves the conditional regions whose symbols are statically false for a template:
`aspire-base` ships no React, Blazor or events source, so its `//#if (includeReact)` and
`//#if (includeEventsPolecat)` regions are removed at staging time. Note the events symbols are
boolean-shaped (`includeEventsMarten` / `includeEventsPolecat` / `includeEventsFisher`) rather than
comparisons against an `eventStore` value, because `Remove-FalseRegions` substitutes symbol *names*
with `false` and then collapses `||` — it has no idea what `eventStore == "polecat"` means, and it
does not collapse `&&` at all. That is why those symbols appear in no `template.json` — nobody can pass a
flag that yields a scaffold referencing files that were never packed. `dbProvider` regions are left
intact; `dotnet new` resolves those.

**Two conditional syntaxes, both inert in this workspace.** `//#if (cond)` / `//#endif` in `.cs`,
`.json` and `.ps1`; `<!--#if (cond) -->` / `<!--#endif -->` in `.csproj`, `.props` and `.slnx`. All
of them are comments here, so the live workspace keeps every branch and every branch stays
compiled and tested. Because all branches coexist, **a conditional region may never contain a
declaration that another region also declares** — that is why the AppHost assigns into one nullable
`appdb` slot from independently guarded blocks instead of using an if/else chain, and why
`PersistenceConfigurator` is three guarded `if`s rather than `if`/`else if`/`else`.

**Naming and casing.** `sourceName` renames *files* with the new name verbatim but rewrites file
*contents* with the casing of the token it matched. The lowercase-prefixed templates
(`aspire-base`, `aspire-events`) therefore carry a `nameLowerFile` symbol so directories come out
lowercase too and match their contents — otherwise a scaffold only builds on a case-insensitive
filesystem. `aspire-ui-blazor` is all PascalCase and uses `sourceName: "Template"` instead. Each
template is internally consistent; the two conventions do not meet inside one template.

Every lowercase-named project also pins `<AssemblyName>` and `<RootNamespace>`, so assembly identity
follows the lowercase content form rather than the file name's casing. Without those pins
`MigrationsAssembly` lookups and `InternalsVisibleTo` name an assembly that does not exist.

**Day-2 add-ons: the `aspire-addon` CLI.** `tooling/AspireStarter.Cli` is a `dotnet tool`
(`AspireStarter.Addon`, command `aspire-addon`) that adds an add-on to a scaffold that already
exists — and, since the scaffolding skill uses it too, at creation as well. Four things hold it up:

- **The manifest.** `stage.ps1` writes `.aspirestarter.json` into `aspire-base` (it is not in
  `base-project/`, which would make the live workspace claim to be a scaffold), and `template.json`
  substitutes it: `nameManifest` keeps `-n` *verbatim*, because contents only ever see the lowercase
  form and the Blazor add-on needs the casing back; `dbProviderManifest` records the store;
  `packVersion` is stamped from `packaging/*.csproj`. The CLI passes `-n`, `--resource` and the
  derived `--eventStore` to every add-on from it — which retires the two silent mismatches the
  skill used to guard by hand — and records each add-on it applies. `aspire-addon adopt` writes one
  into a scaffold made before this existed.
- **Annotated wiring.** A fence in `templating/wiring/*.wiring.md` whose info string reads
  ` ```csharp wire file=… at=<<marker>> pos=above|below [when=web|maui]` (or `kind=packages`, or
  `kind=json-member after=<key>`) is applied by the CLI; ` ```hcl manual` is reported as the user's to
  set; an unannotated fence is prose. The wiring file is still the single source, and
  `WiringParserTests` pins the block count of each one against the real files.
- **Nothing half-applied.** The add-on is instantiated in a temp directory; a file that exists
  identical, line endings aside, is skipped (that is how two front ends share `docs/DESIGN.md`), one
  that differs refuses the run; every edit is planned in memory and written only if all of them resolve. A block
  whose first code line is already present is skipped, which makes a re-run harmless.
- **Markers, else structure.** A missing `<<addon-*>>` marker falls back to placement by structure:
  after the last `#:` directive, before the top-level statement calling `builder.Build()` (Roslyn),
  or at the end of the matching `.slnx` folder. A pack version that differs from the manifest's is
  refused — upgrading a scaffold is not built.

`.claude-plugin/marketplace.json` publishes both skills as the `aspire-starter` plugin, pointing at
`.claude/skills/` rather than copying them, so there is one copy of each.

**Still open**

- The pack is versioned `1.0.0` by hand in `packaging/AspireStarter.Templates.csproj`, and the CLI
  must be released at the same version in `tooling/AspireStarter.Cli/AspireStarter.Cli.csproj`.
- `templating/wiring/*.wiring.md` duplicates the resource blocks that live in `.aspire/apphost.cs`.
  Nothing enforces that they agree; the scaffold matrix is what catches drift.
- Staging needs PowerShell 7 on `PATH`. Fine for a maintainer, a constraint for CI.
- `specs/aspire-addon-day2.md` proposes the next three items: versioning with `upgrade`, MAUI on an
  existing Blazor host, and `add resource`.
- `aspire-addon` covers today's four add-ons (events, ui-react, ui-blazor, auth). A second resource (`add resource`), new capability
  add-ons, `upgrade`, and adding MAUI to an existing Blazor web host are not built.

### Agent-facing contract

```bash
dotnet new install AspireStarter.Templates          # or the local .nupkg
dotnet tool install -g AspireStarter.Addon          # locally: --configfile tooling/install.nuget.config
dotnet new aspire-base -n MyProject -o MyProject --dbProvider postgres
cd MyProject && aspire-addon add ui-react           # reads .aspirestarter.json; --dry-run, --json
```

Prefer `/boot-strap-app` for a new project and `/enhance-app` for an existing one: the
CLI does the merge, and the skills do the parts it cannot — the interview, the docs, verification.

### specs/ — proposals, not implemented

`specs/` is workspace-level. It holds design proposals for work that has **not** landed in
`base-project/`. Nothing in `base-project/` implements anything in `specs/`, and a spec being
present is not license to start building it — treat these as reading material until asked.

**One exception now.** `event-project/` implements the *event-sourcing half* of
`wolverine-adoption-spec.md` as a working slice: Marten, Wolverine aggregate handlers, the
transactional outbox and an async projection. Its §6/§10 migration machinery is **not** implemented
and deliberately so. See "Events" above before reading the spec as a to-do list.

**`specs/wolverine-adoption-spec.md`** (629 lines, status: *Proposed*) — a plan for introducing
Wolverine into an existing .NET application behind feature-controlled routing. Its shape, so a
future session doesn't have to re-read it:

- **Stack it proposes:** Wolverine for in-process and distributed message handling, Marten on
  PostgreSQL for the event store and projections, the Marten/Wolverine transactional outbox for
  atomic persist-and-publish, and `Microsoft.FeatureManagement` for flag evaluation. At-least-once
  delivery with idempotent consumers; one authoritative writer per aggregate stream.
- **Its central idea:** a `Wolverine.ExecutionMode` variant flag — `LegacyOnly` → `ObserveOnly` →
  `Canary` → `WolverinePrimary` → `WolverineOnly` — routing each request to exactly one
  authoritative path, plus a `Wolverine.KillSwitch` that overrides everything. It explicitly
  rejects a single boolean, and forbids running both paths for commands with business effects.
- **How it relates to this repo:** it targets "an existing .NET application" generically, not
  `base-project` specifically. `event-project/` took its §12 event-sourcing requirements and left
  the rest. Its §7 solution structure (`Application/`, `Contracts/`,
  `Infrastructure.Messaging/`, `Infrastructure.EventStore/`, `Host/`) does not match this repo's
  layout, and it says project names may follow existing repository conventions. Its §21 lists
  twelve open decisions — target framework, legacy messaging framework, first transport, pilot
  aggregate — that would all have to be answered against `base-project` before any of it is
  actionable.
- **What adopting it *further* would cost, if it is ever pointed at `base-project` itself:** the
  §6/§10 routing machinery plus `Microsoft.FeatureManagement` and a broker transport, every package
  of which lands in every scaffolded project (see "Don't add a dependency without asking"). The
  PostgreSQL collision the spec's §2 warns about — Marten event sourcing is out of scope without
  PostgreSQL, while `Database:Provider` may say `sqlserver` — is already sidestepped in
  `event-project/` — though no longer by isolating Postgres. The event store now follows
  `Database:Provider`, using Polecat on SQL Server and Fisher on SQLite, so the collision the spec
  warns about does not arise at all: there is no branch on which Marten is asked to run somewhere it
  cannot. Pointing the spec's routing machinery at `template.api`'s own persistence would bring back
  its other costs, but not that one.

### Things NOT to do

- Don't run `dotnet test` or `dotnet ef` from the workspace root, and don't run `dotnet build
  template.slnx` from `base-project/`. The events suite wants a third cwd again
  (`event-project/`). See "Commands" for which cwd each one wants; `aspire run` is the one that
  works from anywhere.
- Don't let the two `nuget.config` files drift. The root one is what the AppHost in `.aspire/`
  restores through — without it the machine-level private feed 401s and `aspire run` fails NU1301 —
  and the `base-project/` one is the copy destined for the pack. Don't add a third under
  `event-project/`: `nuget.config` files merge up the tree, so that directory already picks up the
  root one — unlike `Directory.Packages.props`, which does not merge and which `event-project/`
  therefore does have.
- Don't reference workspace-level files (`CLAUDE.md`, `.claude/`, `specs/`) from anything inside
  `base-project/`. A `base-project/` file pointing up and out of the pack root breaks the moment the
  template is packed or scaffolded. The solution and AppHost are the deliberate exception, and
  question 0 under "Templating status" is the bill for it.
- Don't edit `src/ui/react/src/routeTree.gen.ts` — it is regenerated on save. Adding a route file
  and then running `tsc` first will fail until the Vite plugin has regenerated it; run
  `npx vite build` (or `npm run dev`) once, then typecheck.
- Don't call `ConfigureSerilog()` after `ConfigureOpenTelemetry()` in `AddServiceDefaults`. Its
  `ClearProviders()` would remove the OpenTelemetry logging provider, the only way logs leave a
  service, and nothing would fail: the console would still look fine.
- Don't switch `AddSerilog` to `writeToProviders: true` in place of `OpenTelemetryLogForwarder`.
  The OpenTelemetry provider cannot read the state that bridge passes, so logs would still arrive,
  stripped of every property.
- Don't reintroduce a `Logging:LogLevel` section for `template.api` or `template.events`. Serilog
  does not read it, so it would be configuration that silently does nothing.
- Don't let the Blazor telemetry build its OTLP exporters with the SDK's default `HttpClient`, and
  don't build the providers inline instead of registering them as factories. The WebAssembly host
  cannot send with that client and says nothing about it — spans reach the exporter and no request
  is ever made.
- Don't add `OpenTelemetry.Extensions.Hosting` to `Template.Blazor.Maui.Pages`, and don't register a
  `MeterProvider` for the WebAssembly host. Both depend on something that never runs there — a
  hosted service in the first case, a background thread in the second — and both fail silently.
- Don't wrap `ClientTelemetryController` in a `//#if (includeBlazorWeb)` region. `stage.ps1` strips
  that symbol as statically false for `aspire-base`, so the region would be deleted from every base
  scaffold, including one that adds the Blazor UI afterwards.
- Don't remove the API's CORS configuration. The React proxy does not need it; the WebAssembly
  host does.
- Don't add a token to one front end's stylesheet and not the other, don't add a component to one
  style guide and not the other, and don't reach for a raw color, radius or shadow in either front
  end. Those three and the rest of the design rules are owned by
  [`docs/DESIGN.md`](docs/DESIGN.md) - *The rules*, *Page composition* and *Adding to the system*.
  Note the asymmetry it
  records: `StyleGuidePageTests` fails a section added to Blazor and not React, and nothing at all
  catches the other direction or a token drift.
- Don't hand-roll a component in raw HTML to get it past bUnit's loose JSInterop. That is a
  test-harness limit, not a design decision; the fix is a `JSInterop.SetupModule` in
  `FluentUiTestContext`. [`docs/DESIGN.md`](docs/DESIGN.md) - *Blazor: the FluentUI JS-module
  constraint* - carries the case where doing it the other way produced a UI of black rectangles that
  every test passed.
- Don't remove `FluentDialogProvider` or `FluentToastProvider` from `MainLayout`. `IDialogService`
  and `IToastService` resolve whether or not their provider is mounted, so removing one breaks the
  style guide's Dialog and Toast sections at run time with nothing failing at build time.
  `MainLayoutTests` exists for exactly that.
- Don't add `UseHttpsRedirection` to the API. It would break the WASM host's CORS preflight and the
  Android emulator's `10.0.2.2` loopback.
- Don't run `dotnet new install ... --force` on the pack. It appends a duplicate registration
  rather than replacing the existing one, and from then on every `dotnet new <shortName>` fails with
  `Sequence contains more than one matching element` — an error that names the template rather than
  the duplicate. `dotnet new uninstall AspireStarter.Templates` first, then install without
  `--force`; plain install on top of an existing one leaves the old content in place. See
  *Reinstalling the pack* in `templating/README.md`.
- Don't hand-edit `contracts/api.openapi.json`, and don't hand-write a DTO in either front end.
  The document is rewritten by `UPDATE_CONTRACT=1 dotnet test`, and the two clients are generated
  from it. A hand-edit is overwritten on the next regeneration and disagrees with the API until
  then.
- Don't make either codegen part of a build. `npm run build` must work with no .NET SDK and
  `dotnet build` with no Node; the committed generated files are what buys that.
- Don't return an anonymous type from a controller action. It produces a 200 with no schema, and
  the generated clients get an untyped member for it — `ContractTests` fails the build rather than
  letting it through.
- Don't pass a different `--resource` to two templates in one scaffold. The UI would call an
  endpoint the API does not serve, and nothing fails until it is run.
- Don't hand-edit `package-lock.json`.
- Don't point an Azure Container Apps probe at `/health`. `MapDefaultEndpoints()` maps it in
  Development only and the containers run as Production, so the probe fails every revision while
  looking like a broken image. `/openapi/v1.json` is the route that is always mapped.
- Don't put a `//#if` region in a `.tf` file. `.tf` is not a file type the scaffolding engine
  conditionally processes and `Remove-FalseRegions` cannot resolve a `dbProvider` comparison, so the
  region would ship verbatim into every scaffold. Prune a file or switch the module `source` line
  instead.
- Don't write the bare `sourceName` word into a `.tf` file, and don't use HCL's `templatefile()`.
  `azurerm_container_app` already declares a block with that name, which is why those two files are
  `copyOnly`; anywhere else substitution silently corrupts the configuration.
  `Assert-TerraformSubstitutionSafe` in `stage.ps1` is what stops it.
- Don't write the bare `sourceName` word into *any* packed file except as a project-name prefix.
  Substitution is a plain substring rewrite, so it hits prose and identifiers alike: "templating"
  and "a starter template" reach a scaffold with the project's name spliced into the word, and
  `grid-template-columns` became `grid-<name>-columns` in every Blazor scaffold until `app.css` was
  made `copyOnly` — a dead CSS declaration that no test catches, because bUnit does not evaluate
  CSS. The mirror case is just as quiet: the three lowercase-prefixed templates do not substitute a
  PascalCase occurrence at all, which is what the `nameBlazorProject` symbol in `aspire-base` and
  `aspire-ui-react` exists to rewrite. `Assert-ProseSubstitutionSafe` in `stage.ps1` fails staging
  over both; if a file genuinely needs the token, make it `copyOnly` and pass it to that check.
- Don't add an output to one `IaC/modules/database-*` module without adding it to all three.
  `containerapp.api.tf` reads them by name and only one module ships, so a missing output breaks
  exactly the scaffolds that chose a different provider.
- Don't make `MigrateAsync` unconditional to "fix" the deployed schema. The deploy workflow's
  `dotnet ef database update` step is the answer; migrating from the container would race across
  replicas and deploy a schema change nobody reviewed.
- Don't commit `terraform.tfstate` or a `.tfplan`. Both hold the generated database password. Do
  commit `.terraform.lock.hcl` and `IaC/envs/*.tfvars` — the lock pins providers the way
  `Directory.Packages.props` pins packages, and the tfvars are the environment definitions.
- Don't re-enable test parallelism in `tests/.../Infrastructure/AssemblyInfo.cs` — the fault
  injector is a singleton, `ThemeStore` / `PreferencesStore` are static, and the API suite now
  shares one in-memory SQLite database per class.
- Don't move `template.data` back into `template.api`, and don't point both migrations projects at
  one assembly. Both break the build for reasons recorded under **Persistence** above.
- Don't add a migration for one provider only. Run all three `dotnet ef migrations add` commands or
  the providers drift apart silently — nothing fails until someone scaffolds the other one.
- Don't call `MigrateAsync` unconditionally in `InitializeDatabaseAsync`. The `GetMigrations` branch
  is what keeps `dotnet test` free of Docker.
- Don't call `SaveChangesAsync()` in a Wolverine handler. `Policies.AutoApplyTransactions()` owns
  the transaction; committing by hand takes the outgoing messages out of the same unit of work and
  quietly breaks the outbox's atomicity guarantee.
- Don't point the event store at the `appdb` **database**, on any branch. On `sqlserver` it is a
  second database on the same *server*, which is deliberate and is not the same thing; on `postgres`
  the store gets its own `events-pg` container. Keeping the two schemas apart is what lets either be
  dropped without touching the other.
- Don't relax the `sqlserver` branch's `WithImageTag("2025-latest")`. Polecat needs SQL Server 2025's
  native `json` type, and on 2022 the event store fails at startup while the API keeps working —
  which reads as an events bug rather than an image-tag one.
- Don't reference two event stores from one project. `JasperFx.Events` ships a source generator that
  emits a type per projection, so the second one fails the build with duplicate generated types a
  long way from the cause. That is why there are three host projects and a shared `.domain`.
- Don't put anything store-specific into `event-project/src/shared/`. It is compiled into all three
  hosts and half of it into the test suite, and the moment it names a store the suite needs a
  database.
- Don't turn `event-project/src/shared/` into a project. It is linked source because the
  `JasperFx.Events` generator has to see the aggregate and the event store in one compilation; as a
  referenced assembly it builds clean, tests green, and then throws
  `InvalidProjectionException` on the first command.
- Don't reach for MassTransit, or add a second messaging framework beside Wolverine. v9 is
  source-available and key-gated and v8 is end-of-life at the end of 2026; Wolverine's own
  `WolverineFx.AzureServiceBus` covers the same ground under MIT.
- Don't put the outbox in a different store from the event store — Cosmos DB under Polecat, say.
  Wolverine's outbox guarantee is that message persistence and the event append share **one**
  database transaction; splitting them across two stores silently turns append-and-publish into two
  commits that can disagree.
- Don't add a Wolverine transport package (RabbitMQ, SQS, SNS, Kafka) without asking.
  `UseDurableLocalQueues()` already exercises the whole outbox path with no broker and no extra
  container, and every package added lands in every scaffolded project. `WolverineFx.AzureServiceBus`
  is the one exception already made, on every event store.
- Don't add `ListenToAzureServiceBusQueue` for `widget-completed` to the events service. The Function
  is the queue's only consumer; a Wolverine listener competes with it and each quietly receives about
  half the messages. `ServiceBusTransportTests` fails over it.
- Don't read the Service Bus body with `JsonSerializerOptions.Default`, or anything but
  `WidgetCompletedV1Wire.Read`. Wolverine writes camelCase, and default options deserialize every
  field of a real message to its default value without throwing.
- Don't add `Microsoft.Azure.Functions.Worker.ApplicationInsights` to the Functions project.
  `AddServiceDefaults()` already ships the worker's logs and traces through the Azure Monitor
  distro, and `OpenTelemetryLogForwarder` would write every event to that package's provider as
  well - a second copy of each. The Functions host records invocations in Application Insights on
  its own, beside the worker; those duplicates are known and accepted (spec R7).
- Don't register `Widget` as a projection. It is live-aggregated from its stream, which is
  what `FetchForWriting<Widget>` needs; `WidgetSummary` is the registered read model.
- Don't reference anything in `event-project/` from `base-project/`. The dependency runs one way
  only, and reversing it would make `base-project/` unpackable on its own too.
- **Never write a C# preprocessor directive into template source.** The scaffolding engine reads
  `#if` in a `.cs` file as one of *its* conditionals and deletes the region, whatever the symbol is
  named — and it matches the token anywhere on the line, including inside a comment or a `<c>` tag.
  The MAUI host's developer-tools registration is a partial method selected by an MSBuild
  `Condition` for exactly this reason (`DeveloperTools.cs`). If you need conditional compilation,
  select a file from the csproj instead, and don't mention `#`+`if` in prose either.
- Don't put a declaration inside a `//#if` region that another region also declares. Every region is
  live in this workspace, so two regions declaring the same variable simply will not compile. Assign
  into one slot from separately guarded blocks instead — see `.aspire/apphost.cs`.
- Don't remove `<AssemblyName>`/`<RootNamespace>` from the lowercase-named projects. They are what
  keep assembly identity matching the lowercase strings that name it, and without them a scaffold's
  `MigrationsAssembly` lookup and `InternalsVisibleTo` point at an assembly that does not exist.
- Don't drop `copyOnly` on `src/ui/react/package-lock.json` in `templating/aspire-ui-react/`. It
  names `@babel/template`; substitution would rewrite those package names and break `npm install`.
- Don't add `packaging/`, `templating/` or `tooling/` to `template.slnx`. That solution is itself
  template content, so anything listed in it lands in every scaffold.
- Don't change a wiring block's fence info string, or add a block, without running the CLI suite
  (`dotnet test tooling/tooling.slnx`). The annotations are what `aspire-addon` applies, and
  `WiringParserTests` pins each file's block count; a fence that loses its `wire` tag silently
  becomes prose the CLI skips.
- Don't hand-edit a scaffold's `.aspirestarter.json` to get past an `aspire-addon` refusal. Every
  refusal is a case where carrying on yields a scaffold that builds and is wrong — a version skew
  in particular means the add-on's package pins were never tested against the base's.
- Don't rename `.aspirestarter.json`, or the `AspireStarterProjectName` and `"dbProvider": "postgres"`
  tokens `stage.ps1` writes into it, without changing `template.json` and the CLI with them.
- Don't edit `artifacts/` — it is generated by `templating/stage.ps1` and rebuilt on every pack.
- Don't commit `bin/`, `obj/`, `node_modules/`, `.tanstack/`, or `artifacts/`.
- Don't touch `.claude/settings.local.json`.

---

## How to Know These Guidelines Are Working

- Fewer unnecessary changes in diffs — only requested changes appear.
- Fewer rewrites due to overcomplication — code is simple the first time.
- Clarifying questions come before implementation, not after mistakes.
- Clean, minimal PRs — no drive-by refactoring or "improvements."

---

*Sections 1–4 derived from Andrej Karpathy's observations on LLM coding pitfalls (via
[multica-ai/andrej-karpathy-skills](https://github.com/multica-ai/andrej-karpathy-skills), MIT).*
