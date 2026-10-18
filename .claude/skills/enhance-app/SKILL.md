---
name: enhance-app
description: Add an AspireStarter add-on - Wolverine event sourcing, a React front end, or a Blazor front end (web and/or MAUI) - to an EXISTING scaffold made from the AspireStarter templates. Drives the aspire-addon CLI, which reads the scaffold's .aspirestarter.json, instantiates the add-on with the same name, resource and data store, and merges it into apphost.cs, the solution, Directory.Packages.props and appsettings.json; then updates the scaffold's README and CLAUDE.md and verifies the build. Use when someone asks to add events, React, Blazor or mobile to a project that already exists. For a brand-new project use boot-strap-app instead.
---

# Add to an existing Aspire scaffold

The merge is the CLI's job, not yours. `aspire-addon add` works out every file and every edit
against a temp copy before it touches the scaffold, and refuses rather than half-applying. Your job
is the part a tool cannot do: choose what to add, read what it reports, write the docs, verify.

Work through the phases in order.

---

## Phase 0 — Preconditions

From the scaffold's root:

```bash
aspire-addon status
```

- **`aspire-addon` not found:** install it from the template workspace, which
  `$env:ASPIRE_STARTER_DIR` names (if it is unset, ask the user where the workspace is) —
  `cd $env:ASPIRE_STARTER_DIR`, `dotnet pack tooling/AspireStarter.Cli -c Release -o artifacts/nupkg`, then
  `dotnet tool install -g AspireStarter.Addon --configfile tooling/install.nuget.config`.
- **"No .aspirestarter.json":** the scaffold predates the manifest. Run `aspire-addon adopt`. It
  infers the resource, data store and existing add-ons from the tree. It cannot infer the
  project name's original casing unless a Blazor project carries it, so it may ask for
  `--name <Name>`. Ask the user for the `-n` they scaffolded with rather than guessing: it becomes
  the PascalCase Blazor project names.
- **The pack version differs from the installed one:** `add` will refuse. Say so; do not edit the
  manifest to get past it. Upgrading a scaffold is not supported yet.

`status` also lists which `<<addon-*>>` markers survive. A missing one is not a blocker: the CLI
places that block structurally instead and reports `AppliedByFallback` for it.

## Phase 1 — Decide

Ask, in **one** `AskUserQuestion` call, only what is not already settled by the manifest:

| Add-on | Command | Ask |
| --- | --- | --- |
| Events | `aspire-addon add events` | nothing — the event store follows the data store (Marten / Polecat / Fisher) and is never a separate choice |
| React | `aspire-addon add ui-react` | nothing |
| Blazor | `aspire-addon add ui-blazor --web <true\|false> --maui <true\|false>` | web, mobile (MAUI), or both |

A **second front end is allowed** here, unlike at scaffold time: React and Blazor coexist in the
template workspace itself. The only file both ship, `docs/DESIGN.md`, is identical and is skipped.
Mention that the scaffold will then carry two style guides over one token vocabulary.

An add-on the manifest already lists is refused. Adding MAUI to a scaffold that already has the
Blazor web host is not supported by the CLI yet — say so rather than improvising it.

## Phase 2 — Dry run, then apply

```bash
aspire-addon add <add-on> [flags] --dry-run
```

Show the user the files and edits it plans. Then run the same command without `--dry-run`. Use
`--json` if you want to parse the report: it carries `filesAdded`, `filesIdentical`, `edits` (each
with `file`, `wiringLine` and `outcome`), `manual` and `notes`.

On **`Refused:`** stop and report the message verbatim. Every refusal leaves the scaffold untouched,
so there is nothing to clean up. Do not work around a refusal by applying the wiring by hand — the
refusals are the cases where doing so produces a scaffold that builds and is wrong.

## Phase 3 — Finish what the tool reports

- **Manual follow-ups** are settings the user owns, such as `include_events = true` in
  `IaC/envs/*.tfvars`. Tell the user each one; apply it only if they say to.
- **Notes for the generated docs** are the add-on's non-obvious rules. Fold the ones that apply into
  the scaffold's `CLAUDE.md` and add the add-on to `README.md`: its layout entries, its resources
  and ports, its failure modes, and the reproducing command line (`aspire-addon add ...`). Edit the
  existing documents; do not rewrite them and do not describe options that were not chosen.

## Phase 4 — Verify, then report

```bash
dotnet build <Name>.slnx
dotnet test tests/<name>.api.tests          # plus every other test project that exists
dotnet build .aspire/apphost.cs             # proves the AppHost wiring compiles
```

For React also `npm install && npm run build` in `src/ui/react`. For anything that touched `IaC/`:
`terraform init -backend=false && terraform validate` inside `IaC/` — it needs Terraform 1.9 or
newer; if that is not installed, say so rather than claiming the infrastructure was verified.

Report what was added, every `AppliedByFallback` edit by name (those are the ones worth a human
glance), the manual follow-ups still open, and the verification results as they actually came out.
