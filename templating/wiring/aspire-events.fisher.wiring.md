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

```xml wire file=Directory.Packages.props kind=packages
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
    <!-- The transport. Referenced unconditionally because both branches of Events:Transport are
         live code; it costs nothing until the configuration selects it. -->
    <PackageVersion Include="WolverineFx.AzureServiceBus" Version="6.33.0" />
    <!-- DefaultAzureCredential, for the deployed namespace: it picks up the container app's
         managed identity through AZURE_CLIENT_ID, so no connection string exists. -->
    <PackageVersion Include="Azure.Identity" Version="1.21.0" />
    <!-- Fisher arrives transitively via WolverineFx.Fisher; pinned explicitly anyway so the event
         store's version is visible here rather than inferred from a dependency graph. -->
    <PackageVersion Include="Fisher" Version="1.0.7" />
  </ItemGroup>

  <ItemGroup Label="Azure Functions - the widget-completed consumer">
    <PackageVersion Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />
    <PackageVersion Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.1.0" />
    <PackageVersion Include="Microsoft.Azure.Functions.Worker.Extensions.ServiceBus" Version="5.24.0" />
  </ItemGroup>
```

Every other package the slice needs — `Microsoft.AspNetCore.OpenApi`,
`Swashbuckle.AspNetCore.SwaggerUI`, and the whole test stack — is already pinned in that file.

## 2. `<Name>.slnx` — projects

Insert **above** the line containing `<<addon-src-projects>>`:

```xml wire file=*.slnx at=<<addon-src-projects>> pos=above
    <Project Path="src/template.events.fisher/template.events.fisher.csproj" />
    <Project Path="src/template.events.functions/template.events.functions.csproj" />
```

The second project is the `widget-completed` consumer on Azure Functions. It is store-agnostic and
reads nothing but the Service Bus message body, so it is the same project on every event store.

Insert **above** the line containing `<<addon-test-projects>>`:

```xml wire file=*.slnx at=<<addon-test-projects>> pos=above
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

```json wire file=.aspire/appsettings.json kind=json-member after=Database
  "Events": {
    "Transport": "local",
    "Enabled": false
  },
```

`Enabled` is off by default, for consistency with the other variants rather than because it costs
anything — on this branch turning it on starts no container at all. `Transport` is `local` by
default, and that is what keeps that promise: Wolverine's durable local queues on the SQLite outbox
tables, no broker, no Docker. Setting it to `azureservicebus` starts the Azure Service Bus emulator
and the Functions consumer, which is the shape a deployment runs. It is the **only** way containers
reach this branch, and only by explicit opt-in. Override either for one run with
`$env:Events__Enabled = "true"` and `$env:Events__Transport = "azureservicebus"`.

`Transport` is written **before** `Enabled` on purpose, so that removing it leaves valid JSON.

## 4. `.aspire/apphost.cs` — the resources

Insert **below** the line containing `<<addon-resources>>`:

```csharp wire file=.aspire/apphost.cs at=<<addon-resources>> pos=below
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
    var events = builder.AddCSharpApp("events",
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

    // "local" by default: Wolverine's durable local queues are backed by the same SQLite outbox
    // tables, so the whole outbox path is exercised with no broker - and, on this branch, with no
    // Docker at all. "azureservicebus" is the shape a deployment runs; locally it is the one thing
    // that brings containers onto this branch, which is why it is an explicit opt-in.
    var eventsTransport = builder.Configuration["Events:Transport"] ?? "local";

    // Forwarded so the service and this file cannot disagree about the transport, exactly as
    // Database__Provider is forwarded to the API above.
    events.WithEnvironment("Events__Transport", eventsTransport);

    if (eventsTransport.Equals("azureservicebus", StringComparison.OrdinalIgnoreCase))
    {
        // The emulator, so this runs with no Azure subscription. It is not free: the image requires
        // accepting a EULA and starts its own companion SQL Server.
        //
        // The AMQP port is pinned because Wolverine's UseAzureServiceBusEmulator() addresses the
        // emulator at its well-known ports rather than through service discovery. It also needs the
        // management endpoint on 5300; if that turns out not to be reachable, drop AutoProvision()
        // in src/shared/Messaging/ServiceBusTransport.cs and rely on the queue declared just below,
        // which Aspire writes into the emulator's own configuration.
        var serviceBus = builder.AddAzureServiceBus("servicebus")
            .RunAsEmulator(emulator => emulator.WithHostPort(5672));

        serviceBus.AddServiceBusQueue("widget-completed");

        events.WithReference(serviceBus).WaitFor(serviceBus);

        // The queue's only consumer under this transport - the events service publishes and does not
        // listen. Runs under Azure Functions Core Tools, which must be on PATH, and brings an
        // implicit host storage account that Aspire runs on Azurite. The resource name "servicebus"
        // is the trigger's Connection name, which is how the emulator's connection reaches it.
        builder.AddAzureFunctionsProject("functions",
                                         "../src/template.events.functions/template.events.functions.csproj")
            .WithReference(serviceBus)
            .WaitFor(serviceBus);
    }
}
```

## 5. `.aspire/apphost.cs` — package directives

This variant declares no store container, so it needs no database hosting package. The transport and
Functions packages are needed, though. Insert them **above** the line containing `<<addon-packages>>`:

```csharp wire file=.aspire/apphost.cs at=<<addon-packages>> pos=above
#:package Aspire.Hosting.Azure.ServiceBus@13.5.1
#:package Aspire.Hosting.Azure.Functions@13.5.1
```

Both are needed even when `Events:Transport` is `local`, because both branches of the transport
switch are compiled. Restoring them needs no Docker; only running the `azureservicebus` branch does.

If `#:package Aspire.Hosting.PostgreSQL@13.5.1` or `#:package Aspire.Hosting.SqlServer@13.5.1`
appears in your `apphost.cs`, the base scaffold was created with a different `--dbProvider` and this
is the wrong wiring file.

---

## 6. `IaC/` — one setting

`IaC/containerapp.events.tf`, `IaC/servicebus.tf` and `IaC/functions.tf` were added by this add-on
and Terraform loads them automatically. One thing does have to be set by hand:

```hcl manual
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
Owner` for the workload identity. Deployed, `events_transport` defaults to `azureservicebus`, so the
events service publishes there through its SQLite outbox. `IaC/functions.tf` puts the consumer on
**Flex Consumption**, which scales with the length of that queue and to zero when it is empty. That
matters most on this branch: the events service is pinned to one replica because SQLite has one
writer, but the Function never touches the event store, so it scales freely. It has its own
storage account for its deployment package, and it reaches both the storage account and the
namespace with the same managed identity, so no connection string exists. Flex Consumption is not
offered in every region; check `az functionapp list-flexconsumption-locations` if the service plan
fails to create.

---

## Verify

```bash
dotnet build <Name>.slnx
dotnet test tests/template.events.tests          # unit tests only
$env:Events__Enabled = "true"; aspire run        # events should appear, and NO container should

# and the Service Bus path - the explicit opt-in that does bring containers: the emulator's two,
# Azurite and the Function. Needs Azure Functions Core Tools (func) on PATH.
$env:Events__Enabled = "true"; $env:Events__Transport = "azureservicebus"; aspire run
```

On the default run, confirm in the dashboard that the only resources are `api` and `events` (plus
any front end) — if a container appears, something other than this wiring introduced it.

Then exercise the slice — `POST /widgets`, `POST /widgets/{id}/complete`, `GET /widgets/{id}`. The GET
returns 404 or the old status for a moment: that is the async projection, not a bug. On the Service
Bus run, the `functions` resource should log `widget-completed-v1 received`, and the trace should
show the hop through the emulator.

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
- **Two transports, one handler.** Under `local`, `WidgetCompletedV1Handler` runs in-process on
  Wolverine's durable local queues. Under `azureservicebus`, the events service only publishes, and
  the Function in `src/<Name>.events.functions` calls the same handler class. The transport is
  chosen in `src/shared/Messaging/ServiceBusTransport.cs`.
- **The events service must not listen on `widget-completed`.** The Function is the queue's only
  consumer. A Wolverine listener would compete with it for messages, so each would quietly see about
  half of them. `ServiceBusTransportTests` fails if one is added.
- **Service Bus delivers at least once.** A throw in the Function abandons the message, and it is
  redelivered up to the queue's max delivery count before it goes to the dead-letter queue. A handler
  that does real work has to be idempotent on `MessageId`.
- **The event store is a file, and it is not in the data volume of anything.** `eventstore.db` sits
  beside the built service, so a `dotnet clean` or a fresh clone starts from an empty stream. That is
  usually what you want in development and never what you want in production.
