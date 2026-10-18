---
name: boot-strap-app
description: Scaffold a new Aspire solution from the AspireStarter template series. Interviews the user on three decisions - data store, event sourcing, and which single front end - then runs the dotnet new templates, wires the solution and AppHost together, generates the project's README and CLAUDE.md, builds a graphify knowledge graph of it, and verifies the result builds and tests green. Use when someone asks to create, scaffold, start or bootstrap a new Aspire project from these templates.
---

# Scaffold an Aspire application

`dotnet new` writes files but cannot merge into an existing `.slnx` or `apphost.cs`. That merge is
your job, and it is the reason this skill exists: the base template plants named markers, each
add-on ships a `.scaffold/*.wiring.md` naming the exact blocks to insert, and you apply them.

Work through the phases in order. Do not skip the interview, and do not guess an answer the user
has not given.

---

## Phase 1 — Interview

Ask all four decisions in **one** `AskUserQuestion` call. Also settle the project name and output
directory; if the user already gave a name, do not ask for it again.

**Answers supplied.** A caller that runs without a user (the claude-factory's bootstrap job) gives
all six answers up front: name, output directory, resource, data store, events and front end. Then
do not interview, and do not run Phase 2: check that `dotnet new list aspire-base` lists the template
and that `aspire-addon` is on `PATH`, and if either is missing stop and say so rather than building
or installing anything. Such a caller may also say to skip Phase 5; list graphify as a follow-up in
the report. Everything else, from Phase 3 on, is unchanged.

| Question | Header | Options |
| --- | --- | --- |
| What is the first resource? | `Resource` | Free text, singular PascalCase — **Product**, **Invoice**, **Customer**. Default **Widget**. This is the sample entity the scaffold is built around; the user's own noun means they never have to delete someone else's |
| What is the data store? | `Data store` | **PostgreSQL** (default; container, needs Docker) / **SQL Server Express** (container, needs Docker) / **SQLite** (a local file, no Docker at all) |
| Does it need events? | `Events` | **No** (default) / **Yes — Wolverine event sourcing**, on the event store that matches the data store above: Marten on PostgreSQL (its own extra container), Polecat on SQL Server (a second database on the same container), Fisher on SQLite (a local file, still no Docker) |
| What is the front end? | `Front end` | **React** (Vite + TanStack + Tailwind, installable as a PWA) / **Blazor** (WebAssembly, FluentUI, MVVM) / **Blazor + mobile hybrid**, which adds the MAUI Blazor Hybrid host beside the web one / **Neither — API only** |

**A scaffold gets one front end at creation.** Mobile is folded into that question rather than asked
separately because the MAUI host renders the same Razor Class Library the Blazor web host does — it
is the Blazor front end on a device, not an independent choice. So the three UI answers map to
exactly one add-on, and `ui-react` and `ui-blazor` are never both run here. A second front end is a
later, deliberate addition through the `enhance-app` skill, which supports it.

Five things to state plainly rather than let the user discover:

- **The resource name is substituted everywhere, and the plural is just `+ "s"`.** `--resource
  Product` produces `Product.cs`, `ProductsController`, a `/products` page and a
  `contracts/api.openapi.json` describing `/Products`. An irregular plural (`Person` → `Persons`)
  is a rename afterwards, not a second question — say so if the answer has one, and offer the
  rename rather than quietly scaffolding `Persons`.

- **There is no React Native host here**, so "React plus mobile" is not on the list — choosing
  mobile is choosing Blazor. Say it; do not let the option list imply it.
- **The data store is baked at scaffold time.** Only the provider chosen is generated; the others'
  migrations projects, packages and code branches are not shipped. Changing it later is a code
  change, not a configuration change.
- **The event store is not a separate choice — it follows the data store.** Do not ask which one;
  derive it. All three are JasperFx, all three are MIT, and all three present the same API, so the
  handler code is identical whichever way the data-store question was answered. Two consequences
  worth saying out loud when they apply: picking SQL Server pins the container image to
  `mssql/server:2025-latest`, because Polecat stores events in SQL Server 2025's native `json` type;
  and picking SQLite keeps the no-Docker promise intact, at the cost of Fisher being much newer
  (1.0.x) than Marten and Polecat.
- **Azure Service Bus is available on every event store, and is off by default locally.** It is a
  run-time setting (`Events:Transport`), not a scaffold parameter, so both paths ship and either can
  be chosen later without regenerating anything. Mention it rather than asking about it: the default
  uses Wolverine's durable local queues on the event store's own outbox and starts no extra
  container, while `azureservicebus` adds the emulator's two containers plus Azurite, requires
  accepting a EULA, and needs Azure Functions Core Tools — on SQLite it is the only thing that
  brings Docker in. Deployed, it is the default.

### Mapping answers to commands

Let `<Name>` be the project name and `<Dir>` the output directory.

| Answer | Command | Run from |
| --- | --- | --- |
| always | `dotnet new aspire-base -n <Name> -o <Dir> --dbProvider <postgres\|sqlserver\|sqlite> --resource <Resource>` | anywhere |
| front end is React | `aspire-addon add ui-react` | `<Dir>` |
| front end is Blazor | `aspire-addon add ui-blazor --web true --maui <false\|true>` | `<Dir>` |
| events is yes | `aspire-addon add events` | `<Dir>` |

The add-ons go through the `aspire-addon` CLI, not `dotnet new` directly. `aspire-base` writes
`.aspirestarter.json` recording `-n`, `--resource` and `--dbProvider`, and the CLI passes those to
every add-on itself — so the two silent failures this step used to carry by hand are gone: a
`--resource` mismatch (the UI calling an endpoint the API does not serve) and an `--eventStore` that
disagrees with `--dbProvider` (the events service never declared). The CLI derives the event store
from the data store, then merges the add-on in — which is Phase 3 below.

For `aspire-ui-blazor`: `--maui true` only when the mobile answer was chosen. Run it only for one of
the two Blazor answers, and `ui-react` only for the React one: **one front end at scaffold time.**
(A second one can be added later with the `enhance-app` skill; that is a later decision, not
an interview answer.)

---

## Phase 2 — Install and scaffold

Templates are installed from the pack. If it is not built yet, from the template workspace, which
`$env:ASPIRE_STARTER_DIR` names (if it is unset, ask the user where the workspace is):

```bash
cd $env:ASPIRE_STARTER_DIR
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
# Uninstall first, and do NOT use --force. See "Reinstalling the pack" in templating/README.md.
dotnet new uninstall AspireStarter.Templates
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg
```

The add-ons also need the `aspire-addon` tool. If `aspire-addon` is not on `PATH`, from the template
workspace:

```bash
cd $env:ASPIRE_STARTER_DIR
dotnet pack tooling/AspireStarter.Cli -c Release -o artifacts/nupkg
dotnet tool install -g AspireStarter.Addon --configfile tooling/install.nuget.config
```

Then run `aspire-base` first, then `cd <Dir>` and run each selected add-on. Run them one at a time
and check each succeeded before the next.

---

## Phase 3 — Wire

`aspire-addon add` does the wiring: it reads the add-on's `.scaffold/*.wiring.md`, applies every
block annotated `wire`, and deletes nothing it did not create. Its report lists each edit's outcome,
then two things that are still yours:

- **Manual follow-ups** — settings the user chooses, such as `include_events = true` in the tfvars.
- **Notes for the generated docs** — the add-on's rules, for Phase 4.

If it prints `Refused:`, stop and report the message; the scaffold has not been touched. Do not
apply the wiring by hand to get past a refusal.

The rest of this phase describes what the CLI applies, so you can check its work and so the wiring
files stay intelligible. The base template plants six markers, and blocks are inserted relative to
the marker line, never replacing it:

| Marker | File | Insert |
| --- | --- | --- |
| `<<addon-packages>>` | `.aspire/apphost.cs` | **above** — `#:package` directives |
| `<<addon-resources>>` | `.aspire/apphost.cs` | **below** — resource declarations |
| `<<addon-publish>>` | `.aspire/apphost.cs` | **below** — publish-time configuration |
| `<<addon-src-projects>>` | `<Name>.slnx` | **above** — `<Project>` entries under `/src/` |
| `<<addon-ui-projects>>` | `<Name>.slnx` | **above** — `<Project>` entries under `/src/ui/blazor/` |
| `<<addon-test-projects>>` | `<Name>.slnx` | **above** — `<Project>` entries under `/tests/` |

Order matters in `apphost.cs`: the React resource declares `var ui`, and the publish block uses it.

Three edits live outside the markers, so they are the ones to check in the CLI's report:

- **Events** adds its event store's `ItemGroup` to `Directory.Packages.props` and an `Events` block
  to `.aspire/appsettings.json`. It adds exactly **two** `/src/` solution entries — the host project
  and `src/<name>.events.functions`, the store-agnostic Service Bus consumer. The code the hosts
  share lives in `src/shared/` and is *linked* into whichever host shipped, so it is not a project
  and gets no solution entry of its own.
- **Events, on every store,** needs `#:package Aspire.Hosting.Azure.ServiceBus@13.5.1` and
  `#:package Aspire.Hosting.Azure.Functions@13.5.1` added at `<<addon-packages>>`, even though the
  local transport is the default — both branches of the `Events:Transport` switch are compiled. The
  wiring file you were handed has the exact lines; follow it rather than this summary.
- **Blazor with `--web true`** needs `Cors:AllowedOrigins` populated in the API's `appsettings.json`.
  The WebAssembly host is the only client that is genuinely cross-origin.

**`IaC/` needs no merge at all.** Terraform loads every `*.tf` in a directory, so each add-on has
already dropped its own file in. Two things do need doing, and each add-on's wiring file says which
apply:

- **Events:** set `include_events = true` in `IaC/envs/*.tfvars`. That flag creates the `eventstore`
  database beside `appdb`; without it the events container app's `precondition` fails the apply.
- **Blazor WebAssembly:** nothing at scaffold time, but tell the user in the README that the Static
  Web App's origin reaches the API's CORS only on a **second** `terraform apply` — the shipped
  workflow does it automatically, by hand it is one line in the tfvars.

**No `.scaffold/` directory reaches the scaffold.** The CLI instantiates each add-on in a temp
directory and copies everything but `.scaffold/` across, so there is nothing to delete.

---

## Phase 4 — Generate the docs

The scaffold ships no `README.md`, and its solution file does not list one. Once it is written, add
`<File Path="README.md" />` to the `/Solution Items/` folder of `<Name>.slnx`. Write both files
describing **only** what was actually generated — never the options that were not chosen.

`README.md`:

- What this is, in a sentence, naming the actual stack.
- The layout, as an annotated tree of what is really there.
- **Running it**: `aspire run`, the Docker prerequisite (omit it for SQLite — that is the whole
  point of that choice), and a resource/port table listing only the resources that exist.
- The reproducing command line, verbatim, including every flag.
- If a UI was generated: the **`/styleguide` route**, one line saying it renders every approved
  component and that `docs/DESIGN.md` is the rules behind it. It ships in every configuration, so a
  README that omits it hands someone a route they will find by accident.
- **Deploying it**: a short section pointing at `IaC/README.md`, naming the Azure services that
  actually apply to this scaffold and no others — Container Apps for the API (and the events service
  if there is one), Static Web Apps for the front ends that exist, and the managed store matching
  the provider chosen. Mention `.github/workflows/deploy.yml` and the three OIDC secrets it needs.
- A **failure-mode index**: symptom in bold, then cause, then fix.

`CLAUDE.md`: the non-obvious rules that survive into the scaffold. Include only the ones that apply:

- The `GetMigrations().Any()` branch in `InitializeDatabaseAsync` is load-bearing — it is what lets
  `dotnet test` run without Docker. Do not make `MigrateAsync` unconditional.
- `<Resource>Seeder` checks staleness, not emptiness, because the data volume outlives `aspire stop`.
- **`contracts/api.openapi.json` is the contract, and it is generated.** Both front ends' types come
  from it and neither declares a DTO by hand. The loop is: change the endpoint →
  `UPDATE_CONTRACT=1 dotnet test tests/<name>.api.tests --filter "Category=ContractTest"` →
  `npm run api:types` / `dotnet refitter`. `ContractTests` fails when the committed document and the
  API disagree, so a forgotten first step is caught; a forgotten second is not, except by the
  compiler. Point at `docs/ADDING-A-RESOURCE.md` rather than restating the recipe.
- **Every action needs `[ProducesResponseType<T>]`.** Without it the response has no schema and the
  generated clients get an untyped member — `ContractTests` fails the build over it, which is why
  `HarnessController` returns a declared record rather than an anonymous type.
- Neither codegen runs during a build and both outputs are committed, which is what lets the React
  app build with no .NET SDK and the Blazor library with no Node. Don't wire either into a build.
- **The sample resource is the place to start editing.** It is a working vertical slice named after
  the user's own noun, not a sample to delete — rename it, reshape it, or add the second one with
  `docs/ADDING-A-RESOURCE.md`.
- The API's `http` launch profile is pinned, and both Blazor hosts hard-code that port. *(Blazor)*
- ViewModels are transient, not scoped; shared state belongs in `Stores/`. *(Blazor)*
- Swap collections, don't mutate them — nothing listens to `CollectionChanged`. *(Blazor)*
- Avoid FluentUI components that ship a JS module; bUnit's loose JS runtime returns `null` for
  them — but that is a test-harness limit, not licence to hand-roll the component in raw HTML.
  `docs/DESIGN.md`, *Blazor: the FluentUI JS-module constraint*. *(Blazor)*
- Never edit `src/routeTree.gen.ts`. *(React)*
- **The design system is `docs/DESIGN.md`** — the scaffold ships it, and it owns the token
  vocabulary, the "never a raw value" rule, the accessibility contracts and the fact that a missing
  variant belongs in `/styleguide` first rather than in one screen. Link to it; do not restate it.
  *(any UI)*
- No handler may call `SaveChangesAsync()` — `AutoApplyTransactions` owns the transaction.
  *(Events)*
- `GET /widgets/{id}` is eventually consistent by design. *(Events)*
- `WolverineFx.RuntimeCompilation` is load-bearing and unreferenced by name — do not prune it.
  *(Events)*
- Which event store is baked in, and that it follows the data store rather than being independent.
  Everything store-specific is in the host project; `src/shared/` is written once and linked in,
  and the test suite links the store-free half of it, which is why the tests need no database.
  Do not turn `src/shared/` into a class library — the JasperFx source generator has to see the
  aggregate and the event store in the same compilation. *(Events)*
- **SQL Server only:** the container image is pinned to `mssql/server:2025-latest` because Polecat
  needs the native `json` type — relaxing it back to 2022 breaks the event store at startup while
  the API keeps working. And `Events:Transport` switches between durable local queues (default, no
  extra container) and the Azure Service Bus emulator. *(Events)*
- **SQLite only:** the events slice adds no container, which is the point — if one appears,
  something is wrong. Fisher is on a 1.0.x line and far less proven than Marten and Polecat.
  *(Events)*
- Don't add `UseHttpsRedirection` to the API; it breaks the WASM host's preflight and the Android
  emulator's `10.0.2.2` loopback.
- Which data store is baked in, and that changing it is a code change — and that changing it now
  means changing `IaC/database.tf`'s module `source` with it.
- **`IaC/` is Terraform for Azure, and four things in it are non-obvious.** A container app probe
  must target `/openapi/v1.json`, never `/health`, because `MapDefaultEndpoints` maps that in
  Development only. Nothing applies migrations — the workflow's `dotnet ef database update` does.
  Never put a `//#if` region in a `.tf` file or use HCL's `templatefile()`. And an output added to
  the database module has no sibling here to keep it honest, because only one module shipped.
  *(always)*
- The Blazor WebAssembly origin reaches the API's CORS on a **second** `terraform apply`; skipped, the
  API logs healthy 200s while the browser blocks everything. *(Blazor web)*

Do **not** copy the template workspace's own `CLAUDE.md`. Most of it — the templating status, the
`specs/` section, the `base-project/` path convention — is meaningless in a scaffold.

---

## Phase 5 — Knowledge graph

Every scaffold starts with a [graphify](https://github.com/Graphify-Labs/graphify) knowledge graph,
so a later session can run `graphify query` instead of grepping. This runs after Phase 4 on purpose:
`graphify install --project` appends its own section to the `CLAUDE.md` you just wrote. Do not ask
about it; it is not an interview question. From `<Dir>`:

```bash
# Only if graphify is not on PATH. The PyPI name has a double y. The terraform extra is what
# lets IaC/ into the graph; without it every .tf file contributes nothing.
uv tool install "graphifyy[terraform]"      # or: pipx install "graphifyy[terraform]"

graphify install --project   # .claude/skills/graphify/, a graphify section in CLAUDE.md,
                             # .claude/CLAUDE.md, and PreToolUse hooks in .claude/settings.json
graphify update .            # builds graphify-out/: graph.json, GRAPH_REPORT.md, graph.html.
                             # Code only, local tree-sitter, no LLM and no API key
```

Then append `graphify-out/` to the scaffold's `.gitignore`. The graph is regenerated, not source.

**Git hooks only if `<Dir>` is already a git repository.** `graphify hook install` fails with
`No git repository found` otherwise, and the scaffold is often a temp folder that becomes a repo
later. If `.git` exists, run it: it installs post-commit, post-checkout, post-merge and
post-rewrite hooks that rebuild the graph, and registers a merge driver for `graph.json` in
`.gitattributes`. If not, list `graphify hook install` in the report as the follow-up for after
`git init`. Do not run `git init` to get past this.

Add one line to the README's running section: `graphify update .` rebuilds the graph by hand, and
`GRAPH_REPORT.md` is the overview.

If neither `uv` nor `pipx` is on `PATH`, skip this phase and say so in the report. The scaffold is
complete without the graph. Three things to expect, none of them failures:

- `.aspire/apphost.cs` reports a syntax error and contributes no symbols. graphify's C# parser does
  not understand the `#:` directives of a file-based app. The AppHost is simply absent from the graph.
- The hooks in `.claude/settings.json` call `graphify`. A clone opened on a machine without it gets
  a failing hook on each search and read, so the README names graphify as a prerequisite.
- The docs pass is not run. `/graphify .` inside a session in the scaffold adds the Markdown and
  READMEs on the session's model. It is the user's call, and it costs tokens.

---

## Phase 6 — Verify, then report

```bash
dotnet build <Name>.slnx
dotnet test tests/<name>.api.tests
# plus each other test project that exists
dotnet build .aspire/apphost.cs      # proves the AppHost wiring compiles

cd IaC && terraform init -backend=false && terraform validate && terraform fmt -check -recursive
```

The Terraform check needs Terraform 1.9 or newer on `PATH`. If it is not installed, say so in the
report rather than claiming the infrastructure was verified.

For a React scaffold also run `npm install && npm run build` in `src/ui/react`.

Report what was created, the decisions it encodes, whether the graph was built (node and edge
counts from `graphify update`, or why Phase 5 was skipped), any open follow-up such as
`graphify hook install`, and the verification results **as they actually came out**. If something failed, say so with the output rather than describing the scaffold as
finished. Offer `aspire run` as the next step; do not start it unasked, and note that it needs
Docker unless the store is SQLite.

---

## Things that will bite you

- **Project directories are lowercase.** `aspire-base` and `aspire-events` name their projects with
  a lowercase prefix, so `-n ContosoOrders` produces `src/contosoorders.api/`. The Blazor projects
  are PascalCase (`ContosoOrders.Blazor.Maui.Pages`). Both are intentional and internally
  consistent; do not "fix" either.
- **Never write a C# preprocessor directive into template source.** The scaffolding engine reads
  `#if` in a `.cs` file as one of its own conditionals and deletes the region, whatever the symbol
  is named — even inside a comment. The MAUI host uses a partial method selected by MSBuild for
  exactly this reason.
- **`package-lock.json` is copied verbatim**, because it names `@babel/template`. Do not hand-edit
  it and do not be surprised that it was not renamed.
- The API returns `404` at `/`; nothing is routed there. The dashboard link points at `/api-docs`.
