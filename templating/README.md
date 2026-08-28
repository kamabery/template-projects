# templating/

How this workspace becomes a `dotnet new` template pack. Workspace-level: nothing here is packed
into a scaffold.

```
stage.ps1                    assembles artifacts/templates/ from the live source
aspire-base/template.json    one per template; copied to <staged>/.template.config/template.json
aspire-ui-react/template.json
aspire-ui-blazor/template.json
aspire-events/template.json
wiring/*.wiring.md           copied to <staged>/.scaffold/; the edits dotnet new cannot make
```

## Build the pack

```bash
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg --force
```

`dotnet pack` runs `stage.ps1` itself, so the first line is only needed when inspecting the staged
trees by hand. PowerShell 7 must be on `PATH`.

## Why staging exists

The live layout is not the scaffold layout. `template.slnx`, `aspire.config.json`, `nuget.config`
and `.aspire/` sit at the workspace root while the code sits under `base-project/`, and the events
slice is a sibling directory that reaches back into `base-project/` for servicedefaults.

`stage.ps1` copies each template into the shape a scaffold wants — flattening `base-project/` onto
the root, folding the events slice into `src/` and `tests/` — and rewrites the few paths that
implies. Expressing the same thing as `replaces` symbols in `template.json` would mean blind string
substitution of path prefixes across every packed file; doing it here keeps all of it in one place
that can be read and tested.

Every rewrite in `Edit-StagedFile` **must** match at least once or staging fails. That is the point:
renaming something in the live workspace breaks the build here rather than producing a silently
wrong scaffold.

## Conditional regions

Two syntaxes, both plain comments in this workspace, so every branch stays compiled and tested:

| Files | Syntax |
| --- | --- |
| `.cs`, `.json` | `//#if (cond)` … `//#endif` |
| `.csproj`, `.props`, `.slnx` | `<!--#if (cond) -->` … `<!--#endif -->` |

`dbProvider` regions are resolved by `dotnet new` at instantiation. The `includeReact`,
`includeBlazorWeb`, `includeMaui` and `includeEvents` regions are resolved by **staging**, because
they are statically false for every template that contains them — `aspire-base` ships no React,
Blazor or events source. That is why those four symbols appear in no `template.json`: nobody can
pass a flag that yields a scaffold referencing files that were never packed.

`Remove-FalseRegions` simplifies rather than deletes when a condition mixes the two, so
`(dbProvider == "postgres" || includeEvents)` survives staging as `(dbProvider == "postgres")`.

## Rules that are easy to break

- **No C# preprocessor directives in template source.** The engine reads `#if` in a `.cs` file as
  one of its own conditionals and deletes the region, whatever the symbol is named — and it matches
  the token anywhere on the line, including inside a comment. Select a file from MSBuild instead;
  `Template.Blazor.Maui/DeveloperTools.cs` is the worked example.
- **No duplicate declarations across regions.** All regions are live here, so two regions declaring
  the same variable will not compile. Assign into one slot from separately guarded blocks.
- **Casing has to agree within a template.** `sourceName` renames files with the new name verbatim
  but rewrites contents with the casing of the token it matched. `aspire-base` and `aspire-events`
  are lowercase-prefixed and carry a `nameLowerFile` symbol so their directories come out lowercase
  too; `aspire-ui-blazor` is PascalCase throughout and uses `sourceName: "Template"`. Mixing the two
  inside one template produces paths that only resolve on a case-insensitive filesystem.
- **`package-lock.json` is `copyOnly`.** It names `@babel/template`, and substitution would rewrite
  those package names and break `npm install`.

## Verification

There is no automated matrix yet. Before releasing a change, scaffold at least:

| # | Command | Asserts |
| --- | --- | --- |
| 1 | `aspire-base --dbProvider postgres` | base alone builds and tests |
| 2 | `aspire-base --dbProvider sqlserver` | no Postgres package in `apphost.cs` |
| 3 | `aspire-base --dbProvider sqlite` | no database resource at all; tests still Docker-free |
| 4 | 1 + `aspire-ui-react` | `package-lock.json` untouched; AppHost compiles after wiring |
| 5 | 1 + `aspire-ui-blazor --web true --maui true` | MAUI builds in Debug, Development and Release |
| 6 | 1 + react + `aspire-ui-blazor --web false --maui true` + `aspire-events` | all three suites pass |

For each: `dotnet build <name>.slnx`, `dotnet test` on every suite, `dotnet build .aspire/apphost.cs`,
and confirm no `template.` or `base-project` strings survive.
