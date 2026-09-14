# Wiring: Wolverine + Polecat event sourcing (SQL Server 2025 / Azure SQL)

`dotnet new` can write files but cannot merge into an existing solution, `apphost.cs` or
`Directory.Packages.props`. These are the edits that finish the job. Apply them exactly, then delete
the `.scaffold/` directory.

This is the **Polecat** variant, for a scaffold created with `--dbProvider sqlserver`. Polecat is
Marten's API on SQL Server, so the slice is the same code and the same programming model as the
PostgreSQL variant — `[AggregateHandler]`, `FetchForWriting`, an async projection daemon and a
transactional outbox — against a different engine.

**It requires SQL Server 2025.** Polecat stores events and documents in the native `json` column
type and generates `JSON_OBJECT` and `GENERATE_SERIES`, none of which exist in 2022. Your AppHost
already pins `mssql/server:2025-latest` for that reason. Azure SQL Database has the native
`json` type generally available, so the deployed path needs nothing extra.

---

## 1. `Directory.Packages.props` — Wolverine and Polecat

The events slice is folded into `src/` and `tests/`, so it uses the scaffold's single central
package file rather than carrying one of its own. Add this `ItemGroup`:

```xml
  <ItemGroup Label="Wolverine + Polecat">
    <!-- Every WolverineFx.* satellite must stay on the core's exact version: their Wolverine
         dependency is an exact-ish range, and mixing them is how you get a MissingMethodException
         at first message rather than a restore error. -->
    <PackageVersion Include="WolverineFx" Version="6.33.0" />
    <PackageVersion Include="WolverineFx.Polecat" Version="6.33.0" />
    <!-- Not optional. As of Wolverine 6 the core package no longer ships the Roslyn runtime
         compiler, and Wolverine's default TypeLoadMode.Dynamic compiles handler glue at startup -
         without this the service throws InvalidOperationException before serving anything. Nothing
         in the source names it, so do not prune it as an unused dependency. -->
    <PackageVersion Include="WolverineFx.RuntimeCompilation" Version="6.33.0" />
    <!-- The optional transport. Referenced unconditionally because both branches of
         Events:Transport are live code; it costs nothing until the configuration selects it. -->
    <PackageVersion Include="WolverineFx.AzureServiceBus" Version="6.33.0" />
    <!-- Polecat arrives transitively via WolverineFx.Polecat; pinned explicitly anyway so the event
         store's version is visible here rather than inferred from a dependency graph. -->
    <PackageVersion Include="Polecat" Version="5.22.0" />
  </ItemGroup>
```

Every other package the slice needs — `Microsoft.AspNetCore.OpenApi`,
`Swashbuckle.AspNetCore.SwaggerUI`, and the whole test stack — is already pinned in that file.

## 2. `<Name>.slnx` — projects

Insert **above** the line containing `<<addon-src-projects>>`:

```xml
    <Project Path="src/template.events.polecat/template.events.polecat.csproj" />
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

## 3. `.aspire/appsettings.json` — the flag and the transport

Add after the `Database` block, keeping the trailing comma:

```json
  "Events": {
    "Transport": "local",
    "Enabled": false
  },
```

`Enabled` is off by default. `Transport` is `local` by default, which uses Wolverine's durable local
queues — backed by the same SQL Server outbox tables, so the whole outbox path is exercised with no
broker and no extra container. Setting it to `azureservicebus` starts the Azure Service Bus emulator
instead. Override either for one run with `$env:Events__Enabled = "true"` and
`$env:Events__Transport = "azureservicebus"`.

`Transport` is written **before** `Enabled` on purpose, so that removing it
leaves valid JSON. The order does not matter once it is in your file.

## 4. `.aspire/apphost.cs` — the resources

Insert **below** the line containing `<<addon-resources>>`:

```csharp
// The event-sourcing slice, behind Events:Enabled. Read the same way Database:Provider is above,
// and with the same intent: one configuration value decides the shape of the run, in one place.
//
// The flag gates the AppHost only. Nothing inside the events service reads it - when the flag is off
// neither the service nor its store is declared, so there is nothing running to gate. That is why
// this is a plain configuration read rather than Microsoft.FeatureManagement.
// bool.TryParse rather than Configuration.GetValue<bool>: GetValue is an extension method from
// Microsoft.Extensions.Configuration.Binder, and a file-based AppHost has only what its `#:package`
// directives pull in - which does not include it.
var eventsEnabled = bool.TryParse(builder.Configuration["Events:Enabled"], out var eventsFlag) && eventsFlag;

if (eventsEnabled
    && databaseProvider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase)
    && sqlServer is not null)
{
    // A second database on the API's SQL Server rather than a container of its own. Polecat is
    // Marten's API on SQL Server, so unlike the PostgreSQL variant there is no engine mismatch to
    // work around - the two stores are already isolated by database, and a second SQL Server
    // container plus the Service Bus emulator's own companion would be three engines on one machine.
    var eventstore = sqlServer.AddDatabase("eventstore");

    var eventsTransport = builder.Configuration["Events:Transport"] ?? "local";

    var events = builder.AddCSharpApp("events",
                                      "../src/template.events.polecat/template.events.polecat.csproj",
                                      options => options.LaunchProfileName = "http")
        .WithReference(eventstore)
        .WaitFor(eventstore)
        // Forwarded so the service and this file cannot disagree about the transport, exactly as
        // Database__Provider is forwarded to the API above.
        .WithEnvironment("Events__Transport", eventsTransport)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url =>
        {
            url.DisplayText = "Events API docs";
            url.Url = "/api-docs";
        });

    if (eventsTransport.Equals("azureservicebus", StringComparison.OrdinalIgnoreCase))
    {
        // The emulator, so this runs with no Azure subscription. It is not free: the image requires
        // accepting a EULA and starts its own companion SQL Server, which is why this is opt-in
        // rather than the default.
        //
        // The AMQP port is pinned because Wolverine's UseAzureServiceBusEmulator() addresses the
        // emulator at its well-known ports rather than through service discovery. It also needs the
        // management endpoint on 5300; if that turns out not to be reachable, drop AutoProvision()
        // in the events service and rely on the queue declared just below, which Aspire writes into
        // the emulator's own configuration.
        var serviceBus = builder.AddAzureServiceBus("servicebus")
            .RunAsEmulator(emulator => emulator.WithHostPort(5672));

        serviceBus.AddServiceBusQueue("widget-completed");

        events.WithReference(serviceBus).WaitFor(serviceBus);
    }
}
```

## 5. `.aspire/apphost.cs` — package directives

`#:package Aspire.Hosting.SqlServer@13.5.1` is **already present** — this variant pairs with
`--dbProvider sqlserver`. The transport package is not. Insert it **above** the line containing
`<<addon-packages>>`:

```csharp
#:package Aspire.Hosting.Azure.ServiceBus@13.5.1
```

It is needed even when `Events:Transport` is `local`, because both branches of the transport switch
are compiled.

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

`IaC/servicebus.tf` provisions a namespace, the `widget-completed` queue and `Azure Service Bus Data
Owner` for the workload identity - the same queue name the AppHost declares on the emulator, so the
local and deployed topologies match. It stays unused until `Events:Transport` is `azureservicebus`,
and moving there for real also means replacing `UseAzureServiceBusEmulator()` in the events host
with `UseAzureServiceBus(fullyQualifiedNamespace, credential)`. The namespace's address is already
supplied to the container as `AzureServiceBus__FullyQualifiedNamespace`, and `AZURE_CLIENT_ID` is
set so `DefaultAzureCredential` picks the right managed identity.

---

## Verify

```bash
dotnet build <Name>.slnx
dotnet test tests/template.events.tests          # unit tests only; no Docker needed
$env:Events__Enabled = "true"; aspire run        # the eventstore database and events should appear

# and the Service Bus path, which adds two containers
$env:Events__Enabled = "true"; $env:Events__Transport = "azureservicebus"; aspire run
```

Then exercise the slice — `POST /widgets`, `POST /widgets/{id}/complete`, `GET /widgets/{id}`. The GET
returns 404 or the old status for a moment: that is the async projection, not a bug. On the Service
Bus run, confirm in the dashboard's trace view that the integration event actually round-tripped
through the emulator rather than a local queue.

## Notes for the generated docs

- **The event store follows the data store.** Polecat pairs with `--dbProvider sqlserver` and shares
  that SQL Server as a second database. Changing the data store later means changing the event store
  with it — a code change, not a configuration change.
- **SQL Server 2025 is required and the image tag is pinned for it.** Polecat needs the native
  `json` column type. Do not relax `mssql/server:2025-latest` back to 2022 or the event store fails
  at startup, while the API itself carries on working — which makes it look like an events bug.
- **No handler may call `SaveChangesAsync()`.** `Policies.AutoApplyTransactions()` owns the Polecat
  transaction and commits it around each handler. Calling it yourself commits early and takes the
  outgoing messages out of the same unit of work — which is precisely the atomicity the outbox
  exists to provide.
- **`GET /widgets/{id}` is eventually consistent.** `WidgetSummaryProjection` is registered
  `ProjectionLifecycle.Async` and runs on the daemon, so a read straight after a write can return
  the old state or a 404. That is correct behaviour, not a race. `ProjectionLifecycle.Inline` is the
  one-line switch if read-your-writes is wanted instead.
- **`Widget` is not a registered projection.** It is live-aggregated from its stream on every command
  via the `Apply` methods, which is what `FetchForWriting<Widget>` — the call `[AggregateHandler]`
  generates — uses. `WidgetSummary` is the registered one, and it is read-side only.
- **Domain events are not integration contracts.** `WidgetCompleted` stays inside the aggregate
  boundary; `WidgetCompletedV1` is the versioned public fact, carries a stable wire name via
  `[MessageIdentity]`, and is mapped explicitly in `WidgetDecisions.Approve`. A breaking change means
  a new type, not an edited one.
- **`WolverineFx.RuntimeCompilation` is load-bearing and unreferenced by name.** Wolverine 6 removed
  the Roslyn compiler from the core package, and the default `TypeLoadMode.Dynamic` compiles handler
  glue at startup, so without that reference the service throws at run time — after `dotnet build`
  and `dotnet test` have both passed. Do not prune it.
- **Moving to real Azure Service Bus** is replacing `UseAzureServiceBusEmulator()` in the events
  service with `UseAzureServiceBus(fullyQualifiedNamespace, credential)` and dropping the emulator
  from the AppHost. The emulator caps out at 50 entities and 10 concurrent connections, does not
  persist across a container restart, and does not support partitioned entities — it is a
  development affordance, not a small production broker.
