# CLAUDE.md

**This workspace is template source, not an application.** The Aspire solution in
`base-project/` is destined to be packaged as `dotnet new` templates that agents invoke to
scaffold new projects. Every change you make propagates into every project someone scaffolds
from it. Treat edits accordingly: prefer the boring, portable option, and don't leave anything
here that only works on this machine, in this folder.

**Path convention for this document:** the workspace root holds `CLAUDE.md`, `.claude/`,
`base-project/`, `specs/`, and — since the solution and AppHost were lifted out of `base-project/` —
`template.slnx`, `aspire.config.json`, `nuget.config` and `.aspire/`. Every path below is
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
  `/scaffold-aspire-app` skill.
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

### Workspace layout

```
CLAUDE.md                      this file; workspace-level, NOT part of the template
.claude/                       Claude Code settings + the /scaffold-aspire-app skill; NOT packed
template.slnx                  the solution; its project paths are base-project/-prefixed
aspire.config.json             pointer to the AppHost; the CLI walks UP to find it
nuget.config                   mirrors base-project/nuget.config — the AppHost restores from here
.aspire/                       the file-based AppHost; see "Repo layout" below
base-project/                  the project source — everything below is relative to here
event-project/                 the optional Wolverine/Marten slice; see "Events" below
templating/                    stage.ps1 + one template.json per template + the wiring files
packaging/                     the template pack's .csproj; NOT in template.slnx, deliberately
artifacts/                     generated: staged template trees and the .nupkg. Not source.
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
base-project/                  ← everything below is relative to here
Directory.Build.props          shared properties, incl. the Development configuration
Directory.Packages.props       central package versions + the three load-bearing test pins
nuget.config                   <clear/> + nuget.org; the copy that gets packed
README.md                      human-facing walkthrough; its paths are base-project-relative too
.gitignore                     stock `dotnet new gitignore`; does NOT cover the workspace root
src/
  template.api/                ASP.NET Core Web API (controllers + OpenAPI + Swagger UI + /harness/*)
  template.data/               entity, WeatherDbContext, WeatherSeeder, IRepository<T>/EfRepository<T>
  template.api.migrations.postgres/   EF migrations, Postgres only (migration files and nothing else)
  template.api.migrations.sqlserver/  EF migrations, SQL Server only
  template.api.migrations.sqlite/     EF migrations, SQLite only
  template.servicedefaults/    stock Aspire shared project (OTel, health, resilience)
  ui/
    react/                     Vite + React front end, launched by the AppHost
    blazor/
      Template.Blazor.Maui.Pages/   Sdk.Razor  ALL the Blazor UI + the MVVM framework
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
  src/template.events/         Wolverine + Marten: Domain/ Contracts/ Handlers/ Projections/
                               Controllers/, and the bootstrap in Program.cs
  tests/template.events.tests/ xUnit + Shouldly + AutoFixture; unit tests only, no Docker
```

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

# Include the optional Wolverine/Marten slice for one run (default is off, same file).
# Adds an events-pg container and the events service; see "Events" below.
$env:Events__Enabled = "true"; aspire run

# Build the C# side
dotnet build template.slnx

# Build and install the dotnet new templates (needs PowerShell 7 on PATH)
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg --force

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

# --- from event-project/ ---
cd event-project

# Unit tests for the events slice. No Docker: nothing here touches Marten or Wolverine for real.
dotnet test tests/template.events.tests

# Standalone, without the AppHost. Needs a local Postgres matching the connection string in
# src/template.events/appsettings.Development.json.
cd src/template.events && dotnet run
```

`aspire run` needs the CLI on `PATH`, and `apphost.cs` pins `Aspire.AppHost.Sdk@13.5.1`, so an
older CLI will not match. Update with `dotnet tool update --global aspire.cli`. Bumping the pin
later is `aspire update`, which rewrites the `#:` directives rather than you editing them by hand.

### Ports and resources

| Resource | What | Port |
| --- | --- | --- |
| `pg` / `sql` | Postgres 18.3 or SQL Server 2022 Express, per `Database:Provider` | assigned by the AppHost |
| — | `Database:Provider=sqlite` declares **no** resource: a local file, no container, no Docker | — |
| `api` | template.api | `5058` http (`7247` https, unused — see below); dashboard link opens `/api-docs` |
| `ui` | React / Vite | assigned by the AppHost via `PORT`; `5173` standalone |
| `blazor-web` | Blazor WebAssembly | `5280` http, `7115` https |
| `maui` | MAUI Blazor Hybrid | not a service; `WithExplicitStart` |
| `events-pg` | Postgres 18.3 event store, database `eventstore` — only when `Events:Enabled` | assigned by the AppHost |
| `events` | template.events — only when `Events:Enabled` | `5090` http (`7290` https, unused); dashboard link opens `/api-docs` |
| — | Aspire dashboard | `15087` http, `17143` https |
| — | Dashboard OTLP | `19113` gRPC, `19114` http (https profile: `21222` / `21223`) |

Sources: each project's `Properties/launchSettings.json`, `.aspire/apphost.run.json` (mirrored in
the `profiles` block of `aspire.config.json`), `src/ui/react/vite.config.ts`.

The last two rows are absent from a default `aspire run`. They appear only when `Events:Enabled` is
true — see "Events" below. `events` pins its `http` launch profile like `api` does, but for a
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

The forecast is read from a store, not generated. Four rules matter here.

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
volume was created. `WeatherSeeder` rewrites the table when the newest row is no longer five days
out. Drop the volume to start genuinely clean.

Two consequences worth knowing before changing anything here:

- **The wire contract gained `id`.** `WeatherForecast` is both the EF entity and the response type,
  so its key is serialized and appears in the OpenAPI schema. Neither front end reads it — React
  mirrors it for accuracy, the Blazor positional record drops it, and the TanStack DB collection
  still keys rows by `date` (which is why `Date` carries a unique index). `TemperatureF` is
  `[NotMapped]`.
- **`/weather` no longer changes on Reload**, because the rows are stable. The README records what
  replaced that signal: the dashboard's trace view, or `/harness/latency`.

The test override lives in `tests/.../Infrastructure/WeatherApiFactory.cs` and needs three things
that are each easy to get wrong: the connection string must arrive via `UseSetting` (the Aspire
integration refuses to register without one, and `ConfigureAppConfiguration` is applied too late);
`IDbContextOptionsConfiguration<WeatherDbContext>` must be removed, because since EF Core 9 *every*
registered one is applied and Aspire's would stack Npgsql onto the same options object; and the
replacement must be `AddDbContextPool`, because the Aspire integration leaves behind a singleton
`IDbContextPool` that a scoped `AddDbContext` cannot satisfy.

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

### The fault-injection endpoints

`template.api` exposes `/harness/latency`, `/harness/fail-next` and `/harness/reset`, backed by
`Services/HarnessFaultInjector.cs`. They exist so both front ends' loading and error states can be
reached without unplugging the network:

```bash
# Delay every /WeatherForecast response by 3s, to see the loading state.
curl -X POST http://localhost:5058/harness/latency -H "Content-Type: application/json" -d '{"milliseconds":3000}'

# Make the next 10 forecast requests fail with a 500, then reload /weather in either front end.
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

`event-project/src/template.events` is a working Wolverine + Marten event-sourcing slice that is
**off by default**. `.aspire/appsettings.json` carries `Events:Enabled` (default `false`); when it
is true `apphost.cs` declares an `events-pg` Postgres container and the `events` service, and when
it is false neither exists. Override for one run with `$env:Events__Enabled = "true"`.

**The flag gates the AppHost and nothing else.** No code inside `template.events` reads it, because
with the flag off there is no process running to gate. That is why this is a plain
`builder.Configuration.GetValue` and not `Microsoft.FeatureManagement` — which the spec's §6/§10
routing machinery would need, and which is deliberately absent here (see below).

**The event store is always Postgres and always its own container.** Marten has no other backend,
so binding it to `Database:Provider` would break the `sqlserver` branch outright and would collide
with the `dbProvider` template parameter still open under "Templating status". `events-pg` therefore
runs alongside `pg` or `sql` rather than instead of either, with its own data volume.

Five things that will bite you:

- **No handler may call `SaveChangesAsync()`.** `Policies.AutoApplyTransactions()` in `Program.cs`
  owns the Marten transaction and commits it around each handler. Calling it yourself commits early
  and takes the outgoing messages out of the same unit of work — which is precisely the atomicity
  the outbox exists to provide.
- **`GET /orders/{id}` is eventually consistent.** `OrderSummaryProjection` is registered
  `ProjectionLifecycle.Async` and runs on Marten's daemon, so a read straight after a write can
  return the old state or a 404. That is correct behaviour, not a race. `ProjectionLifecycle.Inline`
  is the one-line switch if a scaffold wants read-your-writes.
- **`Order` is not a registered projection.** It is live-aggregated from its stream on every command
  via the `Apply` methods, which is what `FetchForWriting<Order>` — the call `[AggregateHandler]`
  generates — uses. `OrderSummary` is the registered one, and it is read-side only. Registering
  `Order` too would be a second answer to the same question.
- **Domain events are not integration contracts.** `OrderApproved` stays inside the aggregate
  boundary; `OrderApprovedV1` is the versioned public fact, carries a stable wire name via
  `[MessageIdentity]`, and is mapped explicitly in the handler. A breaking change means a new type,
  not an edited one.
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
expect; `SwaggerEndpoint` is what aims it at `MapOpenApi`'s document. Unlike `/health` and
`/harness/*`, **neither route is gated on Development**; that is deliberate, and the README records
the consequence — the document advertises the Development-only `/harness/*` endpoints, which are
now one click from executing.

**Blazor** — all UI lives in `Template.Blazor.Maui.Pages`; neither host has UI of its own. To add
a page: a ViewModel in `ViewModels/`, a `.razor` in `Pages/` with
`@inherits ViewModelComponentBase<T>`, a `NavLink` in `Layout/NavMenu.razor`, and one
`AddTransient` line in `Mvvm/MvvmServiceCollectionExtensions.AddSharedViewModels`. That single
method is called by both hosts, which is what stops their service graphs drifting apart.

There is **no `StateHasChanged` and no manual refresh call anywhere**. `[ObservableProperty]`
raises `PropertyChanged`; `ViewModelBinder` subscribes and dispatches
`InvokeAsync(StateHasChanged)`. Use `ViewModelLayoutBase<T>` for layouts and
`ViewModelComponentBase<T>` for everything else. Three things that will bite you:

- **Swap collections, don't mutate them.** Nothing listens to `CollectionChanged`. Assign a new
  list (`Items = [.. Items, created];`) so `PropertyChanged` fires.
- **`ValueExpression` cannot reference a static member.** Every FluentUI input builds a
  `FieldIdentifier` from its `ValueExpression`, and that rejects statics. Bind to an instance
  property that proxies the static — `MainLayout.IsDarkTheme` over `ThemeStore.IsDark`, and
  `WeatherForecastViewModel.Unit` over `PreferencesStore.Unit`.
- **Avoid FluentUI components that ship a JS module** (`FluentNavMenu`, `FluentDataGrid`, the
  `Fluent*Provider` family). bUnit runs with `JSRuntimeMode.Loose`, under which an imported module
  resolves to `null`, so those components throw the moment they call into one.

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
3. TanStack Store (`src/store/preferences.ts`, `src/store/counter.ts`) for client-only UI state
   the server doesn't own.

**Styling (React)** — Tailwind v4 configured in CSS; there is no `tailwind.config.js`. Use the
semantic tokens defined in `src/ui/react/src/index.css` (`bg-surface`, `bg-surface-raised`,
`text-content`, `text-content-muted`, `bg-brand`, `border-border-subtle`), never raw palette
colors like `bg-slate-800` — a raw color has one value and so breaks whichever theme it was not
picked for. Each token carries both palettes through `light-dark()`, resolved against the
`color-scheme` set on `:root`; `src/store/preferences.ts` writes `<html data-theme>` and the
attribute selectors in `index.css` override the OS setting in both directions.

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
| `aspire-base` | solution, `.aspire/`, API, data, servicedefaults, the chosen migrations project, API tests | `--dbProvider postgres\|sqlserver\|sqlite` |
| `aspire-ui-react` | `src/ui/react/` | — |
| `aspire-ui-blazor` | Pages RCL always; WASM host if `--web`; MAUI host if `--maui`; the bUnit suite | `--web`, `--maui` |
| `aspire-events` | `src/template.events/`, `tests/template.events.tests/` | — |

```bash
pwsh templating/stage.ps1                                            # assemble artifacts/templates/
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg --force
```

`/scaffold-aspire-app` (in `.claude/skills/`) is the entry point: it interviews the user on the four
decisions — data store, events, React or Blazor, web + mobile hybrid — runs the templates, applies
the wiring, generates the scaffold's README and CLAUDE.md, and verifies the result.

**Staging, not `replaces`.** The live layout is not the scaffold layout: the solution and `.aspire/`
sit at the workspace root while the code sits under `base-project/`, and the events slice is a
sibling that reaches back into `base-project/`. `templating/stage.ps1` copies each template into the
shape a scaffold wants — flattening `base-project/` onto the root, folding the events slice into
`src/` and `tests/` — and rewrites the handful of paths that implies. Doing it there rather than
with `replaces` symbols keeps every path rewrite in one readable, testable place.

It also resolves the conditional regions whose symbols are statically false for a template:
`aspire-base` ships no React, Blazor or events source, so its `//#if (includeReact)` regions are
removed at staging time. That is why those symbols appear in no `template.json` — nobody can pass a
flag that yields a scaffold referencing files that were never packed. `dbProvider` regions are left
intact; `dotnet new` resolves those.

**Two conditional syntaxes, both inert in this workspace.** `//#if (cond)` / `//#endif` in `.cs`,
`.json` and `.ps1`; `<!--#if (cond) -->` / `<!--#endif -->` in `.csproj`, `.props` and `.slnx`. All
of them are comments here, so the live workspace keeps every branch and every branch stays
compiled and tested. Because all branches coexist, **a conditional region may never contain a
declaration that another region also declares** — that is why the AppHost assigns into one nullable
`weatherdb` slot from independently guarded blocks instead of using an if/else chain, and why
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

**Still open**

- The pack is versioned `1.0.0` by hand in `packaging/AspireStarter.Templates.csproj`.
- `templating/wiring/*.wiring.md` duplicates the resource blocks that live in `.aspire/apphost.cs`.
  Nothing enforces that they agree; the scaffold matrix is what catches drift.
- Staging needs PowerShell 7 on `PATH`. Fine for a maintainer, a constraint for CI.

### Agent-facing contract

```bash
dotnet new install AspireStarter.Templates          # or the local .nupkg
dotnet new aspire-base -n MyProject -o MyProject --dbProvider postgres
dotnet new aspire-ui-react -n MyProject -o MyProject
# ...then apply .scaffold/*.wiring.md and delete .scaffold/
```

Prefer `/scaffold-aspire-app` over driving this by hand: the wiring step is the part that is easy to
get wrong, and the skill reads the wiring files rather than improvising the blocks.

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
  `event-project/` by giving the event store its own always-Postgres container, independent of that
  setting. Pointing the spec at `template.api`'s own persistence would bring it straight back.

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
- Don't remove the API's CORS configuration. The React proxy does not need it; the WebAssembly
  host does.
- Don't add `UseHttpsRedirection` to the API. It would break the WASM host's CORS preflight and the
  Android emulator's `10.0.2.2` loopback.
- Don't hand-edit `package-lock.json`.
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
- Don't point the event store at `weatherdb` or at the `pg` container. Marten is Postgres-only and
  `Database:Provider` may say `sqlserver`; `events-pg` is separate for that reason and for the
  `dbProvider` template parameter still open under "Templating status".
- Don't add a Wolverine transport package (RabbitMQ, Azure Service Bus, SQS, SNS, Kafka) without
  asking. `UseDurableLocalQueues()` already exercises the whole outbox path with no broker and no
  extra container, and every package added lands in every scaffolded project.
- Don't register `Order` as a Marten projection. It is live-aggregated from its stream, which is
  what `FetchForWriting<Order>` needs; `OrderSummary` is the registered read model.
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
- Don't add `packaging/` or `templating/` to `template.slnx`. That solution is itself template
  content, so anything listed in it lands in every scaffold.
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
