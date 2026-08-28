---
name: scaffold-aspire-app
description: Scaffold a new Aspire solution from the AspireStarter template series. Interviews the user on four decisions - data store, event sourcing, React or Blazor, web plus mobile hybrid - then runs the dotnet new templates, wires the solution and AppHost together, generates the project's README and CLAUDE.md, and verifies the result builds and tests green. Use when someone asks to create, scaffold, start or bootstrap a new Aspire project from these templates.
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

| Question | Header | Options |
| --- | --- | --- |
| What is the data store? | `Data store` | **PostgreSQL** (default; container, needs Docker) / **SQL Server Express** (container, needs Docker) / **SQLite** (a local file, no Docker at all) |
| Does it need events? | `Events` | **No** (default) / **Yes — Wolverine + Marten event sourcing**, which adds a second always-Postgres container for the event store |
| React or Blazor? | `Front end` | **React** (Vite + TanStack + Tailwind, installable as a PWA) / **Blazor** (WebAssembly, FluentUI, MVVM) / **Both** (same three pages against the same API) / **Neither — API only** |
| Web and mobile hybrid? | `Mobile` | **Web only** (default) / **Web + mobile hybrid**, which adds the MAUI Blazor Hybrid host |

Two things to state plainly rather than let the user discover:

- **Mobile means the MAUI Blazor Hybrid host, and it brings the Blazor UI library with it.** Picking
  React *and* mobile is supported and gives React on the web with MAUI on the device, sharing one
  API. There is no React Native host here.
- **The data store is baked at scaffold time.** Only the provider chosen is generated; the others'
  migrations projects, packages and code branches are not shipped. Changing it later is a code
  change, not a configuration change.

### Mapping answers to commands

Let `<Name>` be the project name and `<Dir>` the output directory.

| Answer | Command |
| --- | --- |
| always | `dotnet new aspire-base -n <Name> -o <Dir> --dbProvider <postgres\|sqlserver\|sqlite>` |
| front end includes React | `dotnet new aspire-ui-react -n <Name> -o <Dir>` |
| front end includes Blazor, or mobile is yes | `dotnet new aspire-ui-blazor -n <Name> -o <Dir> --web <true\|false> --maui <true\|false>` |
| events is yes | `dotnet new aspire-events -n <Name> -o <Dir>` |

For `aspire-ui-blazor`: `--web true` when the front end includes Blazor, `--maui true` when mobile
is yes. If both are false the template emits nothing, so do not run it at all.

**Pass `-n <Name>` to every add-on.** Project renaming is driven by it; an add-on run without it
produces projects named after the wrong thing.

---

## Phase 2 — Install and scaffold

Templates are installed from the pack. If it is not built yet, from the template workspace:

```bash
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg --force
```

Then run `aspire-base` first, then each selected add-on. Run them one at a time and check each
succeeded before the next.

---

## Phase 3 — Wire

Each add-on drops `.scaffold/<name>.wiring.md`. **Read every one of them and apply what it says.**
Do not improvise the blocks from memory; the wiring files are the source of truth and they have
already had the project name substituted into them.

The base template plants six markers. Insert relative to the marker line, never replacing it:

| Marker | File | Insert |
| --- | --- | --- |
| `<<addon-packages>>` | `.aspire/apphost.cs` | **above** — `#:package` directives |
| `<<addon-resources>>` | `.aspire/apphost.cs` | **below** — resource declarations |
| `<<addon-publish>>` | `.aspire/apphost.cs` | **below** — publish-time configuration |
| `<<addon-src-projects>>` | `<Name>.slnx` | **above** — `<Project>` entries under `/src/` |
| `<<addon-ui-projects>>` | `<Name>.slnx` | **above** — `<Project>` entries under `/src/ui/blazor/` |
| `<<addon-test-projects>>` | `<Name>.slnx` | **above** — `<Project>` entries under `/tests/` |

Order matters in `apphost.cs`: the React resource declares `var ui`, and the publish block uses it.

Three edits live outside the markers and are easy to miss:

- **Events** adds a `Wolverine + Marten` `ItemGroup` to `Directory.Packages.props` and an `Events`
  block to `.aspire/appsettings.json`.
- **Events on a non-Postgres scaffold** also needs `#:package Aspire.Hosting.PostgreSQL@13.5.1`
  added at `<<addon-packages>>` — the event store is always Postgres. Check whether that line is
  already there before adding it.
- **Blazor with `--web true`** needs `Cors:AllowedOrigins` populated in the API's `appsettings.json`.
  The WebAssembly host is the only client that is genuinely cross-origin.

**Delete `.scaffold/` when every wiring file has been applied.** It is scaffolding, not source.

---

## Phase 4 — Generate the docs

The scaffold ships no `README.md`, and its solution file already lists one. Write both files
describing **only** what was actually generated — never the options that were not chosen.

`README.md`:

- What this is, in a sentence, naming the actual stack.
- The layout, as an annotated tree of what is really there.
- **Running it**: `aspire run`, the Docker prerequisite (omit it for SQLite — that is the whole
  point of that choice), and a resource/port table listing only the resources that exist.
- The reproducing command line, verbatim, including every flag.
- A **failure-mode index**: symptom in bold, then cause, then fix.

`CLAUDE.md`: the non-obvious rules that survive into the scaffold. Include only the ones that apply:

- The `GetMigrations().Any()` branch in `InitializeDatabaseAsync` is load-bearing — it is what lets
  `dotnet test` run without Docker. Do not make `MigrateAsync` unconditional.
- `WeatherSeeder` checks staleness, not emptiness, because the data volume outlives `aspire stop`.
- The API's `http` launch profile is pinned, and both Blazor hosts hard-code that port. *(Blazor)*
- ViewModels are transient, not scoped; shared state belongs in `Stores/`. *(Blazor)*
- Swap collections, don't mutate them — nothing listens to `CollectionChanged`. *(Blazor)*
- Avoid FluentUI components that ship a JS module; bUnit's loose JS runtime returns `null` for
  them. *(Blazor)*
- Never edit `src/routeTree.gen.ts`; use the semantic Tailwind tokens, not raw palette colours.
  *(React)*
- No handler may call `SaveChangesAsync()` — `AutoApplyTransactions` owns the transaction.
  *(Events)*
- `GET /orders/{id}` is eventually consistent by design. *(Events)*
- `WolverineFx.RuntimeCompilation` is load-bearing and unreferenced by name — do not prune it.
  *(Events)*
- Don't add `UseHttpsRedirection` to the API; it breaks the WASM host's preflight and the Android
  emulator's `10.0.2.2` loopback.
- Which data store is baked in, and that changing it is a code change.

Do **not** copy the template workspace's own `CLAUDE.md`. Most of it — the templating status, the
`specs/` section, the `base-project/` path convention — is meaningless in a scaffold.

---

## Phase 5 — Verify, then report

```bash
dotnet build <Name>.slnx
dotnet test tests/<name>.api.tests
# plus each other test project that exists
dotnet build .aspire/apphost.cs      # proves the AppHost wiring compiles
```

For a React scaffold also run `npm install && npm run build` in `src/ui/react`.

Report what was created, the decisions it encodes, and the verification results **as they actually
came out**. If something failed, say so with the output rather than describing the scaffold as
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
