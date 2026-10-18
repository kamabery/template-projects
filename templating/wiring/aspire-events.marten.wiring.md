# Wiring: Wolverine + Marten event sourcing (PostgreSQL)

`dotnet new` can write files but cannot merge into an existing solution, `apphost.cs` or
`Directory.Packages.props`. These are the edits that finish the job. Apply them exactly, then delete
the `.scaffold/` directory.

This is the **Marten** variant, for a scaffold created with `--dbProvider postgres`. If the data
store is SQL Server or SQLite, the wrong wiring file was emitted — re-run `dotnet new aspire-events`
with `--eventStore polecat` or `--eventStore fisher`.

---

## 1. `Directory.Packages.props` — Wolverine and Marten

The events slice is folded into `src/` and `tests/`, so it uses the scaffold's single central
package file rather than carrying one of its own. Add this `ItemGroup`:

```xml wire file=Directory.Packages.props kind=packages
  <ItemGroup Label="Wolverine + Marten">
    <!-- Every WolverineFx.* satellite must stay on the core's exact version: their Wolverine
         dependency is an exact-ish range, and mixing them is how you get a MissingMethodException
         at first message rather than a restore error. -->
    <PackageVersion Include="WolverineFx" Version="6.33.0" />
    <PackageVersion Include="WolverineFx.Marten" Version="6.33.0" />
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
    <!-- Marten arrives transitively via WolverineFx.Marten; pinned explicitly anyway so the event
         store's version is visible here rather than inferred from a dependency graph. -->
    <PackageVersion Include="Marten" Version="9.30.0" />
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
    <Project Path="src/template.events/template.events.csproj" />
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

`Enabled` is off by default: turning it on starts a second Postgres container for the event store,
which nobody who only wants the base project should pay for. `Transport` is `local` by default,
which uses Wolverine's durable local queues — backed by the same Postgres outbox tables, so the
whole outbox path is exercised with no broker. Setting it to `azureservicebus` starts the Azure
Service Bus emulator and the Functions consumer instead, which is the shape a deployment runs.
Override either for one run with `$env:Events__Enabled = "true"` and
`$env:Events__Transport = "azureservicebus"`.

`Transport` is written **before** `Enabled` on purpose, so that removing it leaves valid JSON.

## 4. `.aspire/apphost.cs` — the resources

Insert **below** the line containing `<<addon-resources>>`:

```csharp wire file=.aspire/apphost.cs at=<<addon-resources>> pos=below
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

if (eventsEnabled && databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    // Its own container rather than a second database on the API's pg, so the two stores stay
    // independent and the event store can be dropped without touching the application's data.
    var eventstore = builder.AddPostgres("events-pg")
                            .WithDataVolume()
                            .AddDatabase("eventstore");

    var events = builder.AddCSharpApp("events", "../src/template.events/template.events.csproj",
                                      options => options.LaunchProfileName = "http")
        // WithReference, like api above: this is a .NET process that reads
        // ConnectionStrings:eventstore straight out of its configuration. WaitFor matters because
        // Marten applies its schema at startup in Development.
        .WithReference(eventstore)
        .WaitFor(eventstore)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url =>
        {
            url.DisplayText = "Events API docs";
            url.Url = "/api-docs";
        });

    // "local" by default: Wolverine's durable local queues are backed by the same Postgres outbox
    // tables, so the whole outbox path is exercised with no broker. "azureservicebus" is the shape a
    // deployment runs, and locally it costs two more containers plus Azurite.
    var eventsTransport = builder.Configuration["Events:Transport"] ?? "local";

    // Forwarded so the service and this file cannot disagree about the transport, exactly as
    // Database__Provider is forwarded to the API above.
    events.WithEnvironment("Events__Transport", eventsTransport);

    if (eventsTransport.Equals("azureservicebus", StringComparison.OrdinalIgnoreCase))
    {
        // The emulator, so this runs with no Azure subscription. It is not free: the image requires
        // accepting a EULA and starts its own companion SQL Server, which is why this is opt-in.
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

This variant pairs with `--dbProvider postgres`, so the line
`#:package Aspire.Hosting.PostgreSQL@13.5.1` is **already present**. The transport and Functions
packages are not. Insert them **above** the line containing `<<addon-packages>>`:

```csharp wire file=.aspire/apphost.cs at=<<addon-packages>> pos=above
#:package Aspire.Hosting.Azure.ServiceBus@13.5.1
#:package Aspire.Hosting.Azure.Functions@13.5.1
```

Both are needed even when `Events:Transport` is `local`, because both branches of the transport
switch are compiled.

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
events service publishes there through its Postgres outbox. `IaC/functions.tf` puts the consumer
on **Flex Consumption**, which scales with the length of that queue and to zero when it is empty.
The Function has its own storage account for its deployment package, and it reaches both the
storage account and the namespace with the same managed identity, so no connection string exists.
Flex Consumption is not offered in every region; check `az functionapp list-flexconsumption-locations`
if the service plan fails to create.

---

## Verify

```bash
dotnet build <Name>.slnx
dotnet test tests/template.events.tests          # unit tests only; no Docker needed
$env:Events__Enabled = "true"; aspire run        # events-pg and events should appear

# and the Service Bus path, which adds the emulator's two containers, Azurite and the Function.
# Needs Azure Functions Core Tools (func) on PATH.
$env:Events__Enabled = "true"; $env:Events__Transport = "azureservicebus"; aspire run
```

Then exercise the slice — `POST /widgets`, `POST /widgets/{id}/complete`, `GET /widgets/{id}`. The GET
returns 404 or the old status for a moment: that is the async projection, not a bug. On the Service
Bus run, the `functions` resource should log `widget-completed-v1 received`, and the trace should
show the hop through the emulator.

## Notes for the generated docs

- **The event store follows the data store.** Marten is PostgreSQL-only, which is why this variant
  pairs with `--dbProvider postgres`. Changing the data store later means changing the event store
  with it — a code change, not a configuration change.
- **No handler may call `SaveChangesAsync()`.** `Policies.AutoApplyTransactions()` owns the Marten
  transaction and commits it around each handler. Calling it yourself commits early and takes the
  outgoing messages out of the same unit of work — which is precisely the atomicity the outbox
  exists to provide.
- **`GET /widgets/{id}` is eventually consistent.** `WidgetSummaryProjection` is registered
  `ProjectionLifecycle.Async` and runs on Marten's daemon, so a read straight after a write can
  return the old state or a 404. That is correct behaviour, not a race. `ProjectionLifecycle.Inline`
  is the one-line switch if read-your-writes is wanted instead.
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
