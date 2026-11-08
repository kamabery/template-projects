# templating/

How this workspace becomes a `dotnet new` template pack. Workspace-level: nothing here is packed
into a scaffold.

```
stage.ps1                    assembles artifacts/templates/ from the live source
aspire-base/template.json    one per template; copied to <staged>/.template.config/template.json
aspire-ui-react/template.json
aspire-ui-blazor/template.json
aspire-events/template.json
wiring/*.wiring.md           copied to <staged>/.scaffold/; the edits dotnet new cannot make.
                             A template may ship several - aspire-events has one per event store -
                             and its template.json excludes all but the one that applies
```

## Build the pack

```bash
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
# Uninstall first, and do NOT use --force. See "Reinstalling the pack" in templating/README.md.
dotnet new uninstall AspireStarter.Templates
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg
```

`dotnet pack` runs `stage.ps1` itself, so the first line is only needed when inspecting the staged
trees by hand. PowerShell 7 must be on `PATH`.

## Reinstalling the pack

**Never `dotnet new install --force`.** On a local `.nupkg` it appends a *second* registration to
`~/.templateengine/packages.json` instead of replacing the first — same `PackageId`, same version,
same `MountPointUri`, byte-identical. Once two exist, every `dotnet new <shortName>` dies with

```
Could not find the template package containing template 'AspireStarter.Base'
Sequence contains more than one matching element
```

because `TemplatePackageManager.GetTemplatePackageAsync` resolves the owning package with `.Single()`.
The error names the template, not the duplicate, so it reads like a broken pack. `dotnet new uninstall`
removes **one** entry per call, which is why recovering takes as many uninstalls as there are copies.

Plain `dotnet new install` on an already-installed pack is no good either: it fails with
`File ...\AspireStarter.Templates.1.0.0.nupkg already exists` and leaves the **old** content in
place, so the templates silently stay stale.

The recipe that is idempotent and actually picks up a rebuild:

```bash
dotnet new uninstall AspireStarter.Templates   # fails harmlessly if it is not installed
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg
```

To check the state directly:

```bash
dotnet new uninstall            # lists everything; AspireStarter.Templates must appear once
```

## Why staging exists

The live layout is not the scaffold layout. `template.slnx`, `aspire.config.json`, `nuget.config`
and `.aspire/` sit at the workspace root while the code sits under `base-project/`, and the events
slice is a sibling directory that reaches back into `base-project/` for servicedefaults.

`stage.ps1` copies each template into the shape a scaffold wants — flattening `base-project/` onto
the root, folding the events slice into `src/` and `tests/`, and distributing `IaC/` — and rewrites
the few paths that implies. Expressing the same thing as `replaces` symbols in `template.json` would mean blind string
substitution of path prefixes across every packed file; doing it here keeps all of it in one place
that can be read and tested.

Every rewrite in `Edit-StagedFile` **must** match at least once or staging fails. That is the point:
renaming something in the live workspace breaks the build here rather than producing a silently
wrong scaffold.

## Wiring annotations

`aspire-addon` (in `tooling/`) applies a wiring file by reading the info string of its fences:

| Info string | Applied as |
| --- | --- |
| `<lang> wire file=<path> at=<<marker>> pos=above\|below` | inserted relative to the marker line; blocks sharing a marker keep file order |
| `… when=web` / `when=maui` | only when that `aspire-ui-blazor` host was asked for |
| `xml wire file=Directory.Packages.props kind=packages` | its `ItemGroup`s, minus every `PackageVersion` id the scaffold already pins |
| `json wire file=<path> kind=json-member after=<key>` | a top-level member after `<key>`, by text insertion so comments survive |
| `<lang> manual` | not applied; reported as a setting the user chooses |
| no annotation | prose or an example; ignored |

`file=*.slnx` means the one solution at the scaffold root. Paths are substituted like the rest of the
file, so `src/template.api/...` names the scaffold's own project. A block is skipped when its first
code line is already in the target, so write that line to be distinctive.

## `.aspirestarter.json`

`stage.ps1` writes the manifest into `aspire-base` with three tokens `template.json` substitutes: the
name verbatim (`nameManifest`), the resource, and the data store (`dbProviderManifest`). `packVersion`
comes from `packaging/AspireStarter.Templates.csproj`. The CLI refuses to add to a scaffold whose
`packVersion` differs from the installed pack.

## `IaC/`

Terraform loads every `*.tf` in a directory, so an add-on contributes infrastructure by dropping a
file in rather than by merging into one. That is why no `<<addon->>` marker exists for any of it.

| Template | Takes |
| --- | --- |
| `aspire-base` | all of `IaC/` except the add-on files below |
| `aspire-ui-react` | `frontend.react.tf` |
| `aspire-ui-blazor` | `frontend.blazor-web.tf` (dropped by `template.json` when `--web false`) |
| `aspire-events` | `containerapp.events.tf`, `servicebus.tf`, `functions.tf`, `tests/events.tftest.hcl` |
| `aspire-auth` | `auth.entra-external.tf`, `tests/auth.tftest.hcl`, and `auth.redirects.react.tf` or `auth.redirects.blazor-web.tf` per `--frontend`; its wiring inserts `Auth__*` below `<<addon-api-env>>` in `containerapp.api.tf` |

The data store is the exception, and has to be: `containerapp.api.tf` must reference whichever
provider shipped, and three sibling `database.*.tf` files would collide here or dangle there. It is
a module per provider under `IaC/modules/`, all exposing the same outputs; `template.json` prunes the
two that were not chosen and the `dbProviderModule` switch symbol rewrites the one `source` line in
`database.tf`.

**No `//#if` regions in `.tf` files.** The engine has no conditional syntax registered for that
extension, and `Remove-FalseRegions` cannot resolve a `dbProvider` comparison — the region would ship
verbatim. Prune the file or switch the `source` line.

## Conditional regions

Two syntaxes, both plain comments in this workspace, so every branch stays compiled and tested:

| Files | Syntax |
| --- | --- |
| `.cs`, `.json` | `//#if (cond)` … `//#endif` |
| `.csproj`, `.props`, `.slnx` | `<!--#if (cond) -->` … `<!--#endif -->` |

`dbProvider` regions are resolved by `dotnet new` at instantiation. The `includeReact`,
`includeBlazorWeb`, `includeMaui`, `includeEventsMarten`, `includeEventsPolecat` and
`includeEventsFisher` regions are resolved by **staging**, because they are statically false for
every template that contains them — `aspire-base` ships no React, Blazor or events source. That is
why those six symbols appear in no `template.json`: nobody can pass a flag that yields a scaffold
referencing files that were never packed.

`Remove-FalseRegions` simplifies rather than deletes when a condition mixes the two, so
`(dbProvider == "postgres" || includeEventsMarten)` survives staging as `(dbProvider == "postgres")`.

**This is why the events symbols are boolean-shaped rather than comparisons against the `eventStore`
parameter.** `Remove-FalseRegions` substitutes a symbol *name* with `false` and then collapses `||`;
it has no notion of what `eventStore == "polecat"` evaluates to, and it does not collapse `&&` at
all. A condition it cannot reduce to the literal `false` survives into the staged output, where
`dotnet new` then meets a symbol nothing declares.

## Rules that are easy to break

- **No C# preprocessor directives in template source.** The engine reads `#if` in a `.cs` file as
  one of its own conditionals and deletes the region, whatever the symbol is named — and it matches
  the token anywhere on the line, including inside a comment. Select a file from MSBuild instead;
  `Template.Blazor.Maui/DeveloperTools.cs` is the worked example.
- **No duplicate declarations across regions.** All regions are live here, so two regions declaring
  the same variable will not compile. Assign into one slot from separately guarded blocks — see the
  `appdb`, `sqlServer` and `eventsEnabled` slots in `.aspire/apphost.cs`. Statements are fine:
  the three event-store regions each call `AddCSharpApp("events", ...)`, and they do not collide
  because each sits inside its own `if` block and only one can be true at run time.
- **A conditional marker must be the whole line.** `Remove-FalseRegions` matches `//#if (cond)` and
  `//#endif` only when they are the entire trimmed line, so an inline `"Enabled": false//#if (...)`
  is silently treated as ordinary text and ships verbatim.
- **Removing a JSON region must leave valid JSON.** Order the keys so the optional one is not last —
  `Events:Transport` precedes `Events:Enabled` in `.aspire/appsettings.json` for exactly this reason,
  so dropping it cannot leave a trailing comma.
- **One event store per project.** `JasperFx.Events` ships a source generator that emits a type per
  projection, so a project referencing two of Marten, Polecat and Fisher fails the build with
  duplicate generated types. That is why `aspire-events` stages four projects and prunes three, and
  why `aspire-events` stages three hosts and prunes two.
- **`src/shared/` is linked source, not a project.** The same generator emits the aggregate's
  dispatcher for the *store's* closed generic, so the aggregate and the store must be in one
  compilation. As a referenced assembly it builds clean and then throws at the first command.
- **Casing has to agree within a template.** `sourceName` renames files with the new name verbatim
  but rewrites contents with the casing of the token it matched. `aspire-base` and `aspire-events`
  are lowercase-prefixed and carry a `nameLowerFile` symbol so their directories come out lowercase
  too; `aspire-ui-blazor` is PascalCase throughout and uses `sourceName: "Template"`. Mixing the two
  inside one template produces paths that only resolve on a case-insensitive filesystem.
- **`package-lock.json` is `copyOnly`.** It names `@babel/template`, and substitution would rewrite
  those package names and break `npm install`.
- **The resource token is substituted too, and a `copyOnly` file never sees it.** `--resource`
  replaces the bare words `Widget` and `widget` in contents and file names, exactly as `sourceName`
  does for the project prefix. A `copyOnly` file containing one would ship verbatim into a scaffold
  that asked for `Product`, so `Assert-ResourceTokenSafe` throws if one does. The plural is
  mechanically `+ "s"` — there is no second token, deliberately: two `replaces` where one is a
  prefix of the other have no defined ordering.
- **`containerapp.api.tf` and `containerapp.events.tf` are `copyOnly` too**, and for a worse reason:
  `azurerm_container_app` declares a block whose name is exactly the `sourceName` these templates
  use, so substitution renames it and the configuration stops parsing. Every other `.tf` file must
  therefore not contain that bare word at all, and `Assert-TerraformSubstitutionSafe` in `stage.ps1`
  throws if one does — reword the comment, or add the file to both the `copyOnly` list in
  `template.json` and the list passed to that function. It also rules out HCL's `templatefile()`.

## Verification

There is no automated matrix yet. Before releasing a change, scaffold at least:

| # | Command | Asserts |
| --- | --- | --- |
| 1 | `aspire-base --dbProvider postgres` | base alone builds and tests |
| 1b | any row, with `--resource Product` | no `Widget`/`widget` string survives anywhere; `contracts/api.openapi.json` describes `/Products`; the contract test passes, which is what proves the committed document and the substituted API still agree |
| 2 | `aspire-base --dbProvider sqlserver` | no Postgres package in `apphost.cs` |
| 3 | `aspire-base --dbProvider sqlite` | no database resource at all; tests still Docker-free |
| 4 | 1 + `aspire-ui-react` | `package-lock.json` untouched; AppHost compiles after wiring |
| 5 | 1 + `aspire-ui-blazor --web true --maui true` | MAUI builds in Debug, Development and Release |
| 6 | 1 + react + `aspire-ui-blazor --web false --maui true` + `aspire-events --eventStore marten` | all three suites pass |
| 7 | 2 + `aspire-events --eventStore polecat` | `Aspire.Hosting.Azure.ServiceBus` directive added; `sql` pinned `2025-latest`; only the `.polecat` host ships |
| 8 | 3 + `aspire-events --eventStore fisher` | **no** container and **no** Aspire hosting package anywhere in `apphost.cs` |
| 9 | 3 + `aspire-events --eventStore fisher` | only `IaC/modules/database-sqlite/`; the Azure Files volume is in the module's outputs; `servicebus.tf` present and documented as idle |
| 10 | 1 + react + `aspire-ui-blazor --web true` | both `frontend.*.tf` present; **only** `frontend.react.tf` declares a linked backend; `cors_allowed_origins` still empty |

For each: `dotnet build <name>.slnx`, `dotnet test` on every suite, `dotnet build .aspire/apphost.cs`,
and confirm no `template.` or `base-project` strings survive.

For the `IaC/` half of every row: `terraform init -backend=false && terraform validate` and
`terraform fmt -check -recursive` inside the scaffold's `IaC/`, confirm the right database module
survived and the other two did not, and confirm that the only surviving `sourceName` token anywhere
under `IaC/` is the `template {` block in the two `copyOnly` files. Row 4 is also where to run
`docker build -f src/<name>.api/Dockerfile .` — the build context is the scaffold root, and getting
that wrong is invisible until someone deploys.

Then the late-add rows, which exercise `aspire-addon` against a scaffold that already exists. Run
`dotnet test tooling/tooling.slnx` first, then pack and install the tool.

| # | Steps | Asserts |
| --- | --- | --- |
| L1 | `aspire-base --dbProvider postgres --resource Product`, then `aspire-addon add events` | `.aspirestarter.json` lists `events`/`marten`; builds, both suites pass, AppHost compiles |
| L2 | as L1 on `sqlserver`, and on `sqlite` | only the `.polecat` / `.fisher` host; `sqlite` has no container resource |
| L3 | base, delete every `<<addon-*>>` line, then `add ui-react`, then `add ui-blazor --web true` | every edit reports `AppliedByFallback`; builds, AppHost compiles, `npm run build` passes; `docs/DESIGN.md` reported identical |
| L4 | `add events` twice | the second is refused; `git status` clean |
| L5 | edit `packVersion` in the manifest, then `add` | refused with the skew message; tree unchanged |
| L6 | a pre-manifest scaffold, `aspire-addon adopt`, then `add events` | `adopt` asks for `--name` unless a Blazor project carries it; then succeeds |

For rows 6–8, also confirm exactly one `.scaffold/aspire-events.*.wiring.md` was emitted and that
`--eventStore` matched `--dbProvider`. A mismatch is the failure mode worth rehearsing: the scaffold
still builds, but the AppHost guards the events resource on `Database:Provider`, so the service is
simply never declared and `aspire run` comes up without it.
