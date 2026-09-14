# Wiring: Wolverine + Fisher event sourcing (SQLite)

`dotnet new` can write files but cannot merge into an existing solution, `apphost.cs` or
`Directory.Packages.props`. These are the edits that finish the job. Apply them exactly, then delete
the `.scaffold/` directory.

This is the **Fisher** variant, for a scaffold created with `--dbProvider sqlite`. Fisher is the
Critter Stack's SQLite event store, so the slice is the same code and the same programming model as
the PostgreSQL and SQL Server variants — `[AggregateHandler]`, `FetchForWriting`, an async
projection daemon and a transactional outbox — against a local file.

**This variant adds no container and no Docker dependency.** That is the whole point of it: choosing
SQLite means `aspire run` comes up on a machine with no Docker at all, and turning events on must
not quietly take that away.

---

## 1. `Directory.Packages.props` — Wolverine and Fisher

The events slice is folded into `src/` and `tests/`, so it uses the scaffold's single central
package file rather than carrying one of its own. Add this `ItemGroup`:

```xml
  <ItemGroup Label="Wolverine + Fisher">
    <!-- Every WolverineFx.* satellite must stay on the core's exact version: their Wolverine
         dependency is an exact-ish range, and mixing them is how you get a MissingMethodException
         at first message rather than a restore error. -->
    <PackageVersion Include="WolverineFx" Version="6.33.0" />
    <PackageVersion Include="WolverineFx.Fisher" Version="6.33.0" />
    <!-- Not optional. As of Wolverine 6 the core package no longer ships the Roslyn runtime
         compiler, and Wolverine's default TypeLoadMode.Dynamic compiles handler glue at startup -
         without this the service throws InvalidOperationException before serving anything. Nothing
         in the source names it, so do not prune it as an unused dependency. -->
    <PackageVersion Include="WolverineFx.RuntimeCompilation" Version="6.33.0" />
    <!-- Fisher arrives transitively via WolverineFx.Fisher; pinned explicitly anyway so the event
         store's version is visible here rather than inferred from a dependency graph. -->
    <PackageVersion Include="Fisher" Version="1.0.7" />
  </ItemGroup>
```

Every other package the slice needs — `Microsoft.AspNetCore.OpenApi`,
`Swashbuckle.AspNetCore.SwaggerUI`, and the whole test stack — is already pinned in that file.

## 2. `<Name>.slnx` — projects

Insert **above** the line containing `<<addon-src-projects>>`:

```xml
    <Project Path="src/template.events.fisher/template.events.fisher.csproj" />
```

Insert **above** the line containing `<<addon-test-projects>>`:

```xml
    <Project Path="tests/template.events.tests/template.events.tests.csproj" />
```

**One project, not two.** `src/shared/` holds everything that is not specific to an event store —
the aggregate, the events, the versioned contract, the decision logic, the read model and the
controller — but it is **linked source, not a project**: the host compiles it in through a
`<Compile Include="..\shared\**\*.cs">` glob, and the test project links the store-free half of it
the same way. That is why neither appears here as a second `<Project>` entry.

It has to be linked rather than referenced. `JasperFx.Events` emits an aggregate's `Apply`/`Create`
dispatcher with a compile-time source generator, and the type it emits is the event store's own
closed generic — so the aggregate and the store must end up in the **same compilation**. A shared
assembly would give the generator the aggregate but no store, and `FetchForWriting<Widget>` would
then throw `InvalidProjectionException` on the first command, after a clean build and a green test
run. `src/shared/README.md` records this.

## 3. `.aspire/appsettings.json` — the flag

Add after the `Database` block, keeping the trailing comma:

```json
  "Events": {
    "Enabled": false
  },
```

Off by default, for consistency with the other variants rather than because it costs anything — on
this branch turning it on starts no container at all. There is no `Transport` key here: every broker
worth reaching for would need a container, and this variant exists precisely so that none is needed.
Override for one run with `$env:Events__Enabled = "true"`.

## 4. `.aspire/apphost.cs` — the resources

Insert **below** the line containing `<<addon-resources>>`:

```csharp
// The event-sourcing slice, behind Events:Enabled. Read the same way Database:Provider is above,
// and with the same intent: one configuration value decides the shape of the run, in one place.
//
// The flag gates the AppHost only. Nothing inside the events service reads it - when the flag is off
// the service is not declared, so there is nothing running to gate. That is why this is a plain
// configuration read rather than Microsoft.FeatureManagement.
// bool.TryParse rather than Configuration.GetValue<bool>: GetValue is an extension method from
// Microsoft.Extensions.Configuration.Binder, and a file-based AppHost has only what its `#:package`
// directives pull in - which does not include it.
var eventsEnabled = bool.TryParse(builder.Configuration["Events:Enabled"], out var eventsFlag) && eventsFlag;

if (eventsEnabled && databaseProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    // No store resource at all, which is the entire point of this variant. The connection string is
    // set by hand for the same reason the API's is above - there is no resource to reference - and
    // is relative, so the file lands beside the built service rather than anywhere absolute.
    builder.AddCSharpApp("events",
                         "../src/template.events.fisher/template.events.fisher.csproj",
                         options => options.LaunchProfileName = "http")
        .WithEnvironment("ConnectionStrings__eventstore", "Data Source=eventstore.db")
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url =>
        {
            url.DisplayText = "Events API docs";
            url.Url = "/api-docs";
        });
}
```

## 5. `.aspire/apphost.cs` — package directives

**Nothing to add.** This variant declares no container, so it needs no Aspire hosting package. If
`#:package Aspire.Hosting.PostgreSQL@13.5.1` or `#:package Aspire.Hosting.SqlServer@13.5.1` appears
in your `apphost.cs`, the base scaffold was created with a different `--dbProvider` and this is the
wrong wiring file.

---

## 6. `IaC/` — one setting, and one honest caveat

`IaC/containerapp.events.tf` and `IaC/servicebus.tf` were added by this add-on and Terraform loads
them automatically. One thing does have to be set by hand:

```hcl
# IaC/envs/<environment>.tfvars
include_events = true
```

That flag is what creates the `eventstore` database beside `appdb` in the data store. Without it
there is no connection secret for the events container app to reference, and the apply fails with
the message the app's `precondition` carries rather than something cryptic.

The events container app has **internal** ingress: it is reachable from the API inside the Container
Apps environment and not from the internet. The API is given its address as `Services__events`.
Its `min_replicas` is 1 rather than 0 on purpose - scaling to zero stops the projection daemon and
strands committed-but-undispatched outbox messages.

`IaC/servicebus.tf` provisions a namespace, the `widget-completed` queue and the RBAC for them.
**On this event store it is idle.** Only the Polecat host reads `Events:Transport` and has an
`azureservicebus` branch; this host publishes through Wolverine's durable local queues, backed by
its own event store's outbox tables, and needs no broker at all. The namespace is there so the seam
and the role assignment already exist - not because something is using it. Delete the file if you
would rather not pay for it.

---

## Verify

```bash
dotnet build <Name>.slnx
dotnet test tests/template.events.tests          # unit tests only
$env:Events__Enabled = "true"; aspire run        # events should appear, and NO container should
```

Confirm in the dashboard that the only resources are `api` and `events` (plus any front end) — if a
container appears, something other than this wiring introduced it.

Then exercise the slice — `POST /widgets`, `POST /widgets/{id}/complete`, `GET /widgets/{id}`. The GET
returns 404 or the old status for a moment: that is the async projection, not a bug.

## Notes for the generated docs

- **The event store follows the data store.** Fisher pairs with `--dbProvider sqlite`. Changing the
  data store later means changing the event store with it — a code change, not a configuration
  change.
- **Fisher is much newer than Marten and Polecat.** It is MIT and from the same authors, and it
  presents the same API, but it is on a 1.0.x line with far fewer users behind it. That is the trade
  this branch makes in exchange for needing no Docker. If the project outgrows it, moving to
  PostgreSQL and Marten is a change of store, not a rewrite of the slice.
- **The daemon runs `Solo`, not `HotCold`.** A SQLite file is single-writer and this service is not
  something you scale out, so there is no election to hold.
- **No handler may call `SaveChangesAsync()`.** `Policies.AutoApplyTransactions()` owns the Fisher
  transaction and commits it around each handler. Calling it yourself commits early and takes the
  outgoing messages out of the same unit of work — which is precisely the atomicity the outbox
  exists to provide.
- **`GET /widgets/{id}` is eventually consistent.** `WidgetSummaryProjection` is registered
  `ProjectionLifecycle.Async` and runs on the daemon, so a read straight after a write can return
  the old state or a 404. That is correct behaviour, not a race. `ProjectionLifecycle.Inline` is the
  one-line switch if read-your-writes is wanted instead.
- **Domain events are not integration contracts.** `WidgetCompleted` stays inside the aggregate
  boundary; `WidgetCompletedV1` is the versioned public fact, carries a stable wire name via
  `[MessageIdentity]`, and is mapped explicitly in `WidgetDecisions.Approve`. A breaking change means
  a new type, not an edited one.
- **`WolverineFx.RuntimeCompilation` is load-bearing and unreferenced by name.** Wolverine 6 removed
  the Roslyn compiler from the core package, and the default `TypeLoadMode.Dynamic` compiles handler
  glue at startup, so without that reference the service throws at run time — after `dotnet build`
  and `dotnet test` have both passed. Do not prune it.
- **The event store is a file, and it is not in the data volume of anything.** `eventstore.db` sits
  beside the built service, so a `dotnet clean` or a fresh clone starts from an empty stream. That is
  usually what you want in development and never what you want in production.
