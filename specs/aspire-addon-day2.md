# aspire-addon, day 2: versioning and `upgrade`, MAUI on an existing Blazor host, `add resource`

**Status:** proposed, not started. Written 2026-11-08 to be picked up in a clean session.
**Owner of the decisions:** Kurt. The open questions in each part are his to answer before building.

## Read first

1. `CLAUDE.md`, especially *Day-2 add-ons: the `aspire-addon` CLI*, *Still open* and *Things NOT to do*.
2. `tooling/AspireStarter.Cli/Commands/AddCommand.cs`, `InfraCommand.cs`, `AdoptCommand.cs` and `Manifest.cs`.
3. `templating/README.md` (staging, the template table) and `templating/wiring/*.wiring.md` (how a fence
   becomes an edit).
4. `base-project/docs/ADDING-A-RESOURCE.md`, for Part 3.

The rules that apply to everything below: each change needs a test that is seen failing and then
passing. `dotnet test tooling/tooling.slnx` is the suite (114 tests at the time of writing). No new
package dependency without asking. Nothing the CLI writes may be half-applied.

## Where the CLI is today

| Command | What it does |
|---|---|
| `add <events\|ui-react\|ui-blazor\|auth>` | Instantiates the add-on in a temp directory, then copies new files and skips files identical apart from line endings. Refuses if any file differs. Plans every wiring edit in memory, then writes everything and records the add-on in `.aspirestarter.json`. |
| `adopt` | Writes a manifest into a scaffold made before manifests existed, inferring its add-ons from its folders. |
| `infra check` | Proves every AppHost resource has its Azure twin: state on HCP, no production, no VMs. |
| `infra adopt` | Overwrites a fixed list of pack-owned infra files from a fresh `aspire-base` instance of the **installed** pack. |

The facts that drive this spec:

- **The version check never fires.** `add` refuses when the manifest's `packVersion` differs from the
  installed pack's version, read from `dotnet new uninstall` output. Both have been `1.0.0` since the
  start. The pack and the CLI are versioned by hand, in `packaging/AspireStarter.Templates.csproj` and
  `tooling/AspireStarter.Cli/AspireStarter.Cli.csproj`. So a scaffold from any old pack is treated as
  current.
- **Bumping the version today would lock out every existing scaffold**, including the factory's. With no
  `upgrade`, `add` would refuse them all.
- **Fixes don't reach existing scaffolds.** On 2026-11-08 `platform.tftest.hcl` was fixed so
  `terraform test` passes without the events and auth add-ons (PR #2), and dev and staging moved to
  centralus (PR #3). `infra adopt` brings forward only `backend.tf`, `guards.tf`, `envs/ephemeral.tfvars`,
  `tests/ephemeral.tftest.hcl`, `RESOURCE-MAP.md` and `deploy.yml`, plus `staging.tfvars` if it is
  missing. So every scaffold made before those PRs keeps the broken test and the eastus2 default
  forever. `infra adopt` also never updates `packVersion`.
- **A same-version tool cannot be updated.** `dotnet tool update` keeps the old build when the version
  has not changed (README, "One-time setup", has the probe that showed it). Real versions fix that as a
  side effect.

## Part 1: versioning and `upgrade` (the priority)

### Goal

A scaffold made with pack N can be brought to pack N+k with one command. Files the project never
touched are replaced. Files it changed are reported, not overwritten. Afterwards `add` works again.

### Non-goals

Merging a project's edits into a changed pack file automatically. Downgrades. Upgrading anything
outside what the pack and its add-ons shipped.

### The key design decision: how `upgrade` tells untouched files from edited ones

| Option | How it works | Cost |
|---|---|---|
| A. A fixed pack-owned list, generalized from `infra adopt` | Overwrite named files, leave the rest | Simple. But the list is hand-kept, overwrites user edits in owned files, and misses everything not on it. Today's `platform.tftest.hcl` gap is this failure. |
| B. **Record what was shipped** (recommended) | When `aspire-base` or `add` writes a file, record its path and a line-ending-normalized SHA-256 in the manifest, or a sidecar `.aspirestarter.lock.json`. `upgrade` re-instantiates base plus every recorded add-on from the new pack. A file whose current hash matches its recorded hash is untouched, so it's replaced. One that differs is reported. New files are added, and files the new pack dropped are listed for removal. | One hash per shipped file, maybe 150–300 entries. The file has to stay out of substitution: `stage.ps1` writes the manifest and `template.json` substitutes it, so a lock file needs the same care. |
| C. Three-way merge against an instance of the old pack | Re-instantiate the old version as the merge base | Needs every old pack available forever. Merge conflicts in C#, `.slnx` and HCL are worse than a report. |

Option B is the smallest that is correct. It reuses `Text.SameIgnoringLineEndings`'s idea, and it
gives a clean answer for the scaffolds the factory already holds.

### Edits as well as files

Wiring edits to `apphost.cs`, the `.slnx`, `Directory.Packages.props` and `appsettings.json` are not
files the pack owns. Proposal: `upgrade` re-plans the add-ons' wiring against the new pack.
- A block already present is skipped, which works today.
- A block whose old body is present but whose new body differs is reported, and so is one that is
  missing.
- Nothing in an edited file is rewritten automatically in v1.

### Versioning

- **One version for the pack and the CLI**, declared once and read by both `.csproj` files.
  `Directory.Build.props` under `tooling/` and `packaging/` would work. Today `stage.ps1` already reads
  the pack version from `packaging/AspireStarter.Templates.csproj`.
- **A release bumps it.** SemVer, where a change to shipped content is at least a minor bump. Decide
  whether `stage.ps1` should refuse to stage a content change without a bump; that is probably too
  strict for local work.
- **Keep the `packVersion` check in `add`**, and change its message to point at `aspire-addon upgrade`.

### Factory impact

Every factory scaffold made so far reports `1.0.0` and has no lock data. `upgrade` therefore needs a
first-time path:
- With no recorded hashes, compare each file against a fresh instance of **1.0.0 at the commit the
  scaffold recorded**. Check what the manifest records; today that is only the version.
- Or treat everything as edited and print a report.

Decide this before building. The factory's `deploy-app` and `enhance-app` skills run `infra adopt` and
would switch to `upgrade`. Changing a skill is a factory change, so it goes to Kurt first.

### Acceptance

- [ ] A scaffold from version N, with one shipped file edited by hand, upgrades to N+1. Untouched files
  match N+1 byte for byte, line endings aside. The edited file is unchanged and reported. `add` then
  succeeds.
- [ ] `terraform test` passes after upgrading a pre-PR-#2 scaffold without events and auth.
- [ ] `--dry-run` and `--json` work as they do for `add`.
- [ ] A refusal at any step leaves the scaffold untouched, as `AddCommand` does today.
- [ ] `infra adopt` either becomes a thin alias for `upgrade --only IaC/` or is retired.

### Open questions for Kurt

1. Hashes in `.aspirestarter.json`, or a separate lock file?
2. How should the first upgrade of a hashless 1.0.0 scaffold behave?
3. What should `upgrade` do with a file the new pack deleted: remove it, or only report it?
4. Is a version bump enforced at staging, or left to discipline?

## Part 2: MAUI on an existing Blazor web host (small; a good warm-up)

### Goal

`aspire-addon add ui-blazor --web false --maui true` on a scaffold whose manifest records `ui-blazor`
with `maui: false` adds the MAUI host, and the reverse adds the web host. Today it is refused with
"This scaffold already has ui-blazor".

### Design

- Allow a second `add ui-blazor` when it would turn on a host that is off. Refuse it if both requested
  hosts are already on.
- Instantiate with only the new host on. Files the add-on always ships are the Pages RCL and the bUnit
  suite. If they are identical, skip them, which already happens. If they differ, **skip them as
  well** rather than refusing, because the project owns them by now. Only files under the new host's
  directory are added.
- Apply only the wiring blocks for the new host: `when=maui` (`.slnx`, `apphost.cs`) or `when=web`
  (`.slnx`, `apphost.cs`, CORS in `appsettings.json`, and `IaC/frontend.blazor-web.tf`). Shared blocks
  are already present and are skipped.
- Update the manifest entry in place, `maui: true` or `web: true`, without adding a second entry.
- `auth` follows the web host. Adding `--web` after `auth` was applied with no web front end is
  impossible, since `auth` refuses without one. Adding `--maui` to a scaffold with auth needs no auth
  change, because MAUI is not a redirect target.

### Acceptance

- [ ] Web-only scaffold, then `--maui true`: MAUI host added, `.slnx` and `apphost.cs` gain only the
  MAUI blocks, a changed `Pages/*.razor` is untouched, and the build and bUnit suite pass.
- [ ] MAUI-only, then `--web true`: the web host, CORS and `frontend.blazor-web.tf` are added, and
  `infra check` passes.
- [ ] Both on already: refused, and nothing is written.

## Part 3: `add resource` (the largest; scope it before building)

### Goal

Add a second resource end to end: entity, `DbSet`, a migration per provider, controller, API tests,
contract, both generated clients, and the UI page and nav entry for whichever front end exists. This is
what `docs/ADDING-A-RESOURCE.md` describes by hand.

### Why it is harder than the other two

Today the CLI only moves files and inserts text. This needs **toolchains run in order**:
- `dotnet ef migrations add` for the scaffold's one provider;
- `UPDATE_CONTRACT=1 dotnet test --filter Category=ContractTest`;
- `npm run api:types` and/or `dotnet refitter`.

Each is slow, can fail on a machine missing a tool, and leaves partial output, which breaks the CLI's
nothing-half-applied rule.

### Options

| Option | Split | Note |
|---|---|---|
| A. CLI does files, a skill runs the tools (recommended) | `aspire-addon add resource Product` instantiates `aspire-base` (and the UI template) with `--resource Product` in temp and copies only the resource-specific files: `Product.cs`, `ProductsController.cs`, its tests, its page. It wires `DbSet<Product>`, the nav link and the ViewModel registration through new marker blocks. `enhance-app` then runs the migration, contract and codegen steps and verifies. | Keeps the CLI's guarantees. Needs new `<<addon-*>>` markers in `AppDbContext`, the React nav, `NavMenu.razor` and `AddSharedViewModels`, so template source changes too. |
| B. CLI does all of it | Same, plus it shells out to the tools | One command, but the CLI can no longer promise nothing half-applied. |

### Constraints to respect

- The plural is mechanically the singular plus `s`; `--resource Person` gives `Persons`. Keep that, or
  solve it here.
- The sample resource's files are also seed and test fixtures (`WidgetSeeder` and its tests).
  Decide whether a second resource gets a seeder.
- Migrations for **one** provider only: a scaffold has exactly one migrations project.
- `contracts/api.openapi.json` is generated, never hand-edited, so the contract step must run before
  either codegen.

### Open questions for Kurt

1. Option A or B?
2. Should the new resource be a copy of the sample's shape (name, quantity, updatedOn), or should the
   CLI accept fields? Accepting fields is a much bigger feature; recommend the copy.
3. Does `add resource` need to work for the events slice too, with an aggregate and projection per
   resource, or for the CRUD API only?

## Suggested order

1. **Part 2.** Small, self-contained, and it exercises the add path with no new concepts.
2. **Part 1.** It unblocks real versions. Ship versioning and `upgrade` together, and bump to `1.1.0`
   only when `upgrade` exists.
3. **Part 3.** After its options are decided. It is the only part that changes template source (new
   markers) as well as the CLI.

Each part is its own branch and PR, with the CLI suite and `tooling/verify/standalone.ps1` run before
merging any part that changes shipped content.
