# event-project — `template.events`

An **optional** Wolverine + Marten event-sourcing slice, sitting beside `base-project/` in the same
solution and the same Aspire AppHost. It is off by default and starts nothing until you ask for it.

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

With the flag on, two resources join the dashboard: an `events-pg` Postgres container holding the
`eventstore` database, and the `events` service itself. Its dashboard link opens `/api-docs`.

The flag gates the **AppHost only**. Nothing inside `template.events` reads it — when the flag is
off the resource is never declared, so there is nothing running that would need gating. That is why
this is a plain configuration read rather than `Microsoft.FeatureManagement`.

## Trying it

```bash
# Place an order. Returns { "orderId": "..." }.
curl -X POST http://localhost:5090/orders \
  -H "Content-Type: application/json" \
  -d '{"customer":"acme","total":42.00}'

# Approve it. Appends OrderApproved and publishes order-approved-v1 in the same transaction.
curl -X POST http://localhost:5090/orders/<id>/approve

# Read the projection.
curl http://localhost:5090/orders/<id>
```

**The GET is eventually consistent.** `OrderSummaryProjection` runs on Marten's async daemon, so a
read issued immediately after either POST can return the previous state, or `404` for an order
placed a moment ago. That is correct event-sourcing behaviour, not a race to fix — retry the GET.
If a project scaffolded from this wants read-your-writes instead, change the lifecycle to
`ProjectionLifecycle.Inline` in `Program.cs` and drop the `AddAsyncDaemon` call; the projection
then runs inside the write transaction.

Approving twice returns `409`: `Order.EnsureCanBeApproved` rejects it. That matters because
delivery is at-least-once, so the same command really can arrive twice.

## What it implements

| Piece | Where |
| --- | --- |
| Aggregate, live-aggregated from its stream | `Domain/Order.cs` |
| Domain events — internal, past tense, immutable | `Domain/OrderEvents.cs` |
| Versioned public contract with a stable wire name | `Contracts/OrderApprovedV1.cs` |
| Stream creation | `Handlers/PlaceOrderHandler.cs` |
| `[AggregateHandler]`: load, check invariants, decide | `Handlers/ApproveOrderHandler.cs` |
| Integration-event consumer over a durable local queue | `Handlers/OrderApprovedV1Handler.cs` |
| Async read model + its projection | `Projections/` |
| Marten + Wolverine + outbox bootstrap | `Program.cs` |

The append and the publish happen in **one Postgres transaction** through the Marten/Wolverine
outbox: `ApproveOrderHandler` returns both an `Events` and an `OutgoingMessages`, and Wolverine
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
- **Any external broker.** No RabbitMQ, Azure Service Bus, SQS or Kafka package, and no extra
  container. `Policies.UseDurableLocalQueues()` keeps messages in-process but still durable, backed
  by the same Postgres outbox tables. Adding a real transport later is a registration in
  `Program.cs`; no handler changes.
- **`Microsoft.FeatureManagement`.** See the flag note above.
- **Integration tests.** Exercising Marten or Wolverine for real needs a live Postgres, and
  `dotnet test` across this workspace is deliberately Docker-free. What that leaves uncovered is the
  code Wolverine generates around `ApproveOrderHandler`: the `FetchForWriting` load, the
  expected-version append and the outbox commit. The unit tests cover the decision logic those wrap.

## Commands

Run from `event-project/`:

```bash
dotnet test tests/template.events.tests    # unit tests; no Docker required
```

Standalone, without the AppHost — needs a local Postgres matching the connection string in
`src/template.events/appsettings.Development.json`:

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

`template.events` does reference `base-project/src/template.servicedefaults`, which is what supplies
`AddServiceDefaults()`, the OTel wiring and the `/health` endpoint the AppHost gates on. That
reference means **`event-project/` is not independently packable today** — see the Templating-status
notes in the workspace `CLAUDE.md`.
