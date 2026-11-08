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

You scaffold from **another workspace's** Claude Code session, not from this
one. This workspace is the source of the templates, the CLI and the skills;
the new project lives somewhere else.

### One-time setup

1. **Point the skills at this workspace.** Both skills build and install the
   pack and the CLI from the directory `ASPIRE_STARTER_DIR` names, and ask
   you for it when it is unset. Set it once, at user scope:

   ```powershell
   [Environment]::SetEnvironmentVariable("ASPIRE_STARTER_DIR", "<path-to>\template-projects", "User")
   ```

   Restart the terminal (and Claude Code) so new sessions see it.

2. **Install the skills as a plugin, per project.** This repository is its own
   plugin marketplace (`.claude-plugin/marketplace.json`). From the directory
   whose Claude sessions should have the skills:

   ```powershell
   claude plugin marketplace add <path-to>\template-projects --scope project
   claude plugin install aspire-starter@aspirestarter --scope project
   ```

   That records both in that directory's `.claude/settings.json`, so only
   sessions opened there see the skills. Do it in the directory that will
   **contain** new projects (for `boot-strap-app`) and again in each
   scaffold's root (for `enhance-app`) — a fresh scaffold does not inherit it.
   The marketplace entry is a local path, so use `--scope local` instead
   (`.claude/settings.local.json`, not committed) where the directory is
   shared with anyone else.

   It is also a plugin directory in its own right (`.claude-plugin/plugin.json`),
   so `claude --plugin-dir <path-to>\template-projects` loads the skills with no
   install. That is how claude-factory's headless jobs get them; its bootstrap
   job runs `boot-strap-app` with every answer supplied, so there is no interview.

3. **Optionally, pre-install the pack and the CLI** with the commands under
   "Scaffolding by hand" below. If you skip this, the skills do it on first
   use from `ASPIRE_STARTER_DIR`.

   > **An installed `aspire-addon` does not update itself.** After pulling or
   > changing `tooling/AspireStarter.Cli`, rebuild and update it, from this
   > workspace's root:
   >
   > ```powershell
   > dotnet pack tooling/AspireStarter.Cli -c Release -o artifacts/nupkg
   > dotnet tool update -g AspireStarter.Addon --configfile tooling/install.nuget.config
   > ```
   >
   > The version stays `1.0.0`, so `dotnet tool list` cannot tell you whether
   > this worked. Check that `~/.dotnet/tools/aspire-addon.exe` has a new
   > timestamp.

### Creating a project

Open Claude Code in the directory that should **contain** the new project
(e.g. `D:\code\Scratchpad`) and run:

```text
/aspire-starter:boot-strap-app
```

or just ask for it ("scaffold a new Aspire app called Inventory"). It
interviews you on four decisions — the first resource, the data store, event
sourcing, and which single front end — runs the templates, merges the new
projects into the solution and AppHost, generates the project's README and
CLAUDE.md, builds a [graphify](https://github.com/Graphify-Labs/graphify)
knowledge graph of the result, and verifies it builds and tests green. The
graph step installs `graphifyy[terraform]` with `uv` (or `pipx`) if graphify is
missing, and is skipped, with a note in the report, when neither installer is on
`PATH`.

The merge step is the hard part: `dotnet new` writes files but cannot
edit an existing `.slnx` or `apphost.cs`. Each add-on ships a
`.scaffold/*.wiring.md` describing the exact blocks to insert, and the
`aspire-addon` CLI in `tooling/` applies them.

### Adding to an existing project

Open Claude Code **in the scaffold's root** (where `.aspirestarter.json` is)
and run:

```text
/aspire-starter:enhance-app
```

or ask ("add a React front end"). It drives the same CLI, which reads
`.aspirestarter.json` so the add-on gets the same name, resource and data
store as the base. A scaffold made before that file existed can be adopted
with `aspire-addon adopt`.

### After changing this workspace

- **Skills changed:** nothing to refresh. The marketplace is a local
  directory, so the plugin is read from this folder. An edit, committed or
  not, takes effect at the next session start or after `/reload-plugins`.
  `claude plugin update` reports "nothing to update" for that reason.
- **Templates or CLI changed:** rebuild and reinstall both (see "Scaffolding
  by hand", and step 3 of "One-time setup" for updating an installed CLI).
  `aspire-addon` refuses to add to a scaffold whose recorded pack
  version differs from the installed one; upgrading a scaffold is not built.

## Scaffolding by hand

Build and install the pack (needs PowerShell 7 on `PATH`):

```bash
pwsh templating/stage.ps1
dotnet pack packaging/AspireStarter.Templates.csproj -c Release -o artifacts/nupkg
# Uninstall first, and do NOT use --force. See "Reinstalling the pack" in templating/README.md.
dotnet new uninstall AspireStarter.Templates
dotnet new install artifacts/nupkg/AspireStarter.Templates.1.0.0.nupkg
```

and the `aspire-addon` CLI:

```bash
dotnet pack tooling/AspireStarter.Cli -c Release -o artifacts/nupkg
dotnet tool install -g AspireStarter.Addon --configfile tooling/install.nuget.config
```

Then scaffold the base, and add each add-on from inside it. The CLI takes the
name, resource and data store from the base's `.aspirestarter.json`:

```bash
dotnet new aspire-base -n MyProject -o MyProject --dbProvider postgres --resource Product
cd MyProject
aspire-addon add ui-react          # or: events | ui-blazor --web true --maui false
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

### Testing the IaC for real

`tooling/verify/standalone.ps1` deploys a fresh postgres + React scaffold from the current commit to
Azure, with nothing borrowed from claude-factory: its own HCP Terraform organization, its own Entra
apps and its own private GitHub repository. It is the check that the templates work for someone who
has never seen the factory.

One-time setup:

1. **An HCP Terraform organization used for nothing else** (the free plan is enough). In its
   *Settings → Teams → owners*, create a team token.
2. **Two user-scope environment variables.** The organization's name is configuration, not code:

   ```powershell
   [Environment]::SetEnvironmentVariable('STANDALONE_HCP_ORG', '<your-org>', 'User')
   $t = Read-Host 'Token' -AsSecureString; [Environment]::SetEnvironmentVariable('STANDALONE_HCP_TOKEN', [Net.NetworkCredential]::new('', $t).Password, 'User'); Remove-Variable t
   ```

   The script refuses a token that can see any organization but that one.
3. **Sign-ins.** `az login` as someone who can create app registrations and grant roles on the
   subscription, and `gh auth login`. GitHub needs SSH for the push, and Cleanup needs
   `gh auth refresh -s delete_repo`.

Then, from the workspace root:

```powershell
pwsh tooling/verify/standalone.ps1 -Mode Setup      # pack, scaffold, Entra apps, HCP, repo
pwsh tooling/verify/standalone.ps1 -Mode Deploy     # deploy.yml for dev, then a smoke test
pwsh tooling/verify/standalone.ps1 -Mode Teardown   # destroy through HCP
pwsh tooling/verify/standalone.ps1 -Mode Cleanup    # everything but the organization
```

It deploys to `centralus` (`-Location` changes it) and creates real, billed resources until
Teardown. `IaC/README.md`, *Setting up the deploy workflow*, describes each thing Setup creates.

See `CLAUDE.md` for the full workspace layout, architecture notes, and the
rules that keep this repo template-ready.
