# event-project — `template.events`

An **optional** Wolverine event-sourcing slice, sitting beside `base-project/` in the same solution
and the same Aspire AppHost. It is off by default and starts nothing until you ask for it.

**The event store follows `Database:Provider`.** It is not a separate choice:

| `Database:Provider` | Event store | Host project | Where the store lives |
| --- | --- | --- | --- |
| `postgres` | Marten | `src/template.events/` | its own `events-pg` container |
| `sqlserver` | Polecat | `src/template.events.polecat/` | the `eventstore` database on the API's `sql` container |
| `sqlite` | Fisher | `src/template.events.fisher/` | a local `eventstore.db` file — no container |

All three are JasperFx, all three are MIT, and all three present the same API, so the handler code
is identical on every branch. They are separate projects because each library declares its own
`IDocumentSession`, `SingleStreamProjection<,>` and `[AggregateHandler]`, and because
`JasperFx.Events` ships a source generator that emits a type per projection — so a project
referencing two stores will not compile. Everything that is *not* store-specific lives once, in
`src/shared/`, which is **linked source rather than a project** - see that folder's README for the
reason an assembly cannot work here.

It exists as a working reference implementation of the event-sourcing half of
`specs/wolverine-adoption-spec.md` — small enough to read in one sitting, real enough to run.

## Turning it on

The flag lives in `.aspire/appsettings.json` at the workspace root:

```jsonc
"Events": { "Enabled": false }
```

Flip it there, or override it for a single run:

```powershell
$env:Events__Enabled = "true"; aspire run
```

With the flag on, the `events` service joins the dashboard — plus, on the `postgres` branch only, an
`events-pg` container holding the `eventstore` database. On `sqlserver` the store is a second
database on the container that is already running; on `sqlite` there is no container at all. The
service's dashboard link opens `/api-docs`.

On the `sqlserver` branch there is a second setting, `Events:Transport`. It is `local` by default —
Wolverine's durable local queues, backed by the same SQL Server outbox tables, with no broker and no
extra container. Set it to `azureservicebus` to start the Azure Service Bus emulator instead, which
costs two more containers and accepts a EULA:

```powershell
$env:Database__Provider = "sqlserver"; $env:Events__Enabled = "true"; $env:Events__Transport = "azureservicebus"; aspire run
```

The `Enabled` flag gates the **AppHost only**. Nothing inside the events service reads it — when the flag is
off the resource is never declared, so there is nothing running that would need gating. That is why
this is a plain configuration read rather than `Microsoft.FeatureManagement`.

## Trying it

```bash
# Create a widget. Returns { "widgetId": "..." }.
curl -X POST http://localhost:5090/widgets \
  -H "Content-Type: application/json" \
  -d '{"customer":"acme","total":42.00}'

# Complete it. Appends WidgetCompleted and publishes widget-completed-v1 in the same transaction.
curl -X POST http://localhost:5090/widgets/<id>/complete

# Read the projection.
curl http://localhost:5090/widgets/<id>
```

**The GET is eventually consistent.** `WidgetSummaryProjection` runs on the async daemon, so a
read issued immediately after either POST can return the previous state, or `404` for a widget
created a moment ago. That is correct event-sourcing behaviour, not a race to fix — retry the GET.
If a project scaffolded from this wants read-your-writes instead, change the lifecycle to
`ProjectionLifecycle.Inline` in `Program.cs` and drop the `AddAsyncDaemon` call; the projection
then runs inside the write transaction.

Completing twice returns `409`: `Widget.EnsureCanBeCompleted` rejects it. That matters because
delivery is at-least-once, so the same command really can arrive twice.

## What it implements

Shared, in `src/shared/` — written once, compiled into all three hosts:

| Piece | Where |
| --- | --- |
| Aggregate, live-aggregated from its stream | `Domain/Widget.cs` |
| Domain events — internal, past tense, immutable | `Domain/WidgetEvents.cs` |
| The decision logic both handlers wrap, including the domain-to-contract mapping | `Domain/WidgetDecisions.cs` |
| Versioned public contract with a stable wire name | `Contracts/WidgetCompletedV1.cs` |
| Integration-event consumer over a durable local queue | `Handlers/WidgetCompletedV1Handler.cs` |
| Read model, and the rules its projection applies | `Projections/WidgetSummary.cs` |
| HTTP surface | `Controllers/WidgetsController.cs` |
| The one thing the controller needs from a store | `Abstractions/IWidgetSummaries.cs` |

Per host — four small files each, all of them adapters over the above:

| Piece | Where |
| --- | --- |
| Stream creation | `Handlers/CreateWidgetHandler.cs` |
| `[AggregateHandler]`: load, check invariants, decide | `Handlers/CompleteWidgetHandler.cs` |
| The projection type | `Projections/WidgetSummaryProjection.cs` |
| The document load behind `IWidgetSummaries` | `Storage/WidgetSummaries.cs` |
| Event store + Wolverine + outbox bootstrap | `Program.cs` |

`WidgetsController` is linked in rather than referenced, so it compiles into each host and
`AddControllers()` finds it by its normal entry-assembly scan — no `AddApplicationPart` required.

The append and the publish happen in **one database transaction** through the outbox: `CompleteWidgetHandler` returns both an `Events` and an `OutgoingMessages`, and Wolverine
commits them together. Kill the process between the commit and the dispatch and the message is
still in the outbox table when it comes back up.

`WolverineFx.RuntimeCompilation` is referenced but never named in the source. Wolverine 6 dropped
the Roslyn compiler from its core package while still defaulting to `TypeLoadMode.Dynamic`, so
without it the service throws at startup — after a clean build and a green test run, which makes
it an easy dependency to "tidy up" and a confusing one to get back. The alternative is
pre-generating the code and switching to `TypeLoadMode.Static`, which trades a build step for a
faster cold start; not worth it for a scaffold.

No handler calls `SaveChangesAsync()` — `Policies.AutoApplyTransactions()` owns the transaction.
Calling it yourself commits early and takes the outgoing messages out of the same unit of work.

## What it deliberately does not implement

- **The spec's §6/§10 migration machinery** — `Wolverine.ExecutionMode`, `KillSwitch`,
  `CanaryPercentage`, `IApplicationDispatcher`, legacy adapters. All of that exists to route
  traffic between a legacy path and Wolverine during a migration. There is no legacy path here, so
  the legacy adapter would be a stub with nothing behind it.
- **Any broker other than Azure Service Bus, and that one only on the `sqlserver` branch and only
  when asked.** No RabbitMQ, SQS or Kafka package. `Policies.UseDurableLocalQueues()` keeps messages
  in-process but still durable, backed by the same outbox tables, which is the default everywhere.
  Adding another transport is a registration in `Program.cs`; no handler changes.
- **MassTransit.** v9 is source-available and needs a licence key at run time; v8 is Apache-2.0 but
  end-of-life at the end of 2026. Wolverine's own Azure Service Bus transport covers the same ground
  under MIT, so a second messaging framework would buy nothing.
- **Cosmos DB.** There is no maintained, C#-idiomatic open-source event-sourcing library for it, and
  `WolverineFx.CosmosDb` is outbox and saga storage only. Putting the outbox in Cosmos underneath a
  Polecat event store would also split append-and-publish across two transactions, which is exactly
  the guarantee this slice exists to demonstrate. Polecat on Azure SQL is the Azure answer.
- **`Microsoft.FeatureManagement`.** See the flag note above.
- **Integration tests.** Exercising an event store or Wolverine for real needs a live database, and
  `dotnet test` across this workspace is deliberately Docker-free. What that leaves uncovered is the
  code Wolverine generates around `CompleteWidgetHandler`: the `FetchForWriting` load, the
  expected-version append and the outbox commit. The unit tests cover the decision logic those wrap
  — and they link `src/shared/Domain` and `src/shared/Contracts` only, never a host, so they are
  the same tests whichever event store a scaffold ends up with.

## Commands

Run from `event-project/`:

```bash
dotnet test tests/template.events.tests    # unit tests; no Docker required
```

Standalone, without the AppHost — each host needs whatever its own `appsettings.Development.json`
points at: a local Postgres for `template.events`, a local SQL Server 2025 for `.polecat`, nothing
at all for `.fisher`. All three listen on the same port, since only one of them ever runs:

```bash
cd src/template.events && dotnet run       # http://localhost:5090, docs at /api-docs
```

Build with the rest of the workspace from the root: `dotnet build template.slnx`.

## Layout notes

`event-project/` carries its own `Directory.Build.props` and `Directory.Packages.props`. It is a
sibling of `base-project/`, not a child, and both MSBuild and NuGet stop at the nearest such file
walking up — so nothing is inherited from there and the properties and versions are repeated here
on purpose.

There is no `nuget.config`: `nuget.config` files *merge* up the tree, so this directory already
picks up the workspace-root one. `base-project/` keeps its own copy only because that directory is
a pack root.

Each host references `base-project/src/template.servicedefaults`, which is what supplies
`AddServiceDefaults()`, the OTel wiring and the `/health` endpoint the AppHost gates on. That
reference means **`event-project/` is not independently packable today** — see the Templating-status
notes in the workspace `CLAUDE.md`.
