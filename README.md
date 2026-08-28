# template-projects

A workspace of **`dotnet new` templates** for scaffolding new .NET 10 + Aspire
applications, plus a Claude Code skill that drives them.

The code under `base-project/` and `event-project/` is a working reference
implementation. `templating/` packages it into four templates; `packaging/`
builds the NuGet pack; `artifacts/` is the generated output.

## The templates

| shortName | Emits | Parameters |
| --- | --- | --- |
| `aspire-base` | Solution, Aspire AppHost, API, data + migrations, service defaults, API tests | `--dbProvider postgres\|sqlserver\|sqlite` |
| `aspire-ui-react` | React + Vite + TanStack + Tailwind front end | — |
| `aspire-ui-blazor` | Blazor UI (Razor Class Library) + optional hosts and bUnit tests | `--web`, `--maui` |
| `aspire-events` | Wolverine + Marten event-sourcing slice | — |

The data store is baked in at scaffold time; only the chosen provider ships.

## Scaffolding with Claude (recommended)

Run the **`/scaffold-aspire-app`** skill in Claude Code. It interviews you on
four decisions — data store, event sourcing, React or Blazor, web + mobile
hybrid — runs the templates, merges the new projects into the solution and
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
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg --force
```

Then scaffold — `aspire-base` first, each add-on after, passing `-n <Name>` to
every one:

```bash
dotnet new aspire-base    -n MyProject -o MyProject --dbProvider postgres
dotnet new aspire-ui-react -n MyProject -o MyProject
# ...then apply .scaffold/*.wiring.md and delete .scaffold/
```

## Running the reference implementation

```bash
aspire run          # API + both front ends + dashboard
```

See `CLAUDE.md` for the full workspace layout, architecture notes, and the
rules that keep this repo template-ready.
