# Wiring: Wolverine event sourcing

`dotnet new` can write files but cannot merge into an existing solution, `apphost.cs` or
`Directory.Packages.props`. These are the edits that finish the job. Apply them exactly, then delete
the `.scaffold/` directory.

---

## 1. `Directory.Packages.props` — Wolverine and Marten

The events slice is folded into `src/` and `tests/`, so it uses the scaffold's single central
package file rather than carrying one of its own. Add this `ItemGroup`:

```xml
  <ItemGroup Label="Wolverine + Marten">
    <!-- WolverineFx and WolverineFx.Marten ship together and must stay on the same version:
         the .Marten package's Wolverine dependency is an exact-ish range and mixing them is how
         you get a MissingMethodException at first message, not a restore error. -->
    <PackageVersion Include="WolverineFx" Version="6.30.0" />
    <PackageVersion Include="WolverineFx.Marten" Version="6.30.0" />
    <!-- Not optional. As of Wolverine 6 the core package no longer ships the Roslyn runtime
         compiler, and Wolverine's default TypeLoadMode.Dynamic compiles handler glue at startup -
         without this the service throws InvalidOperationException before serving anything. Nothing
         in the source names it, so do not prune it as an unused dependency. -->
    <PackageVersion Include="WolverineFx.RuntimeCompilation" Version="6.30.0" />
    <!-- Marten arrives transitively via WolverineFx.Marten; pinned explicitly anyway so the event
         store's version is visible here rather than inferred from a dependency graph. -->
    <PackageVersion Include="Marten" Version="9.29.0" />
  </ItemGroup>
```

Every other package the slice needs — `Microsoft.AspNetCore.OpenApi`,
`Swashbuckle.AspNetCore.SwaggerUI`, and the whole test stack — is already pinned in that file.

## 2. `template.slnx` — projects

Insert **above** the line containing `<<addon-src-projects>>`:

```xml
    <Project Path="src/template.events/template.events.csproj" />
```

Insert **above** the line containing `<<addon-test-projects>>`:

```xml
    <Project Path="tests/template.events.tests/template.events.tests.csproj" />
```

## 3. `.aspire/appsettings.json` — the flag

Add after the `Database` block, keeping the trailing comma:

```json
  "Events": {
    "Enabled": false
  },
```

Off by default: turning it on starts a second Postgres container for the event store, which nobody
who only wants the base project should pay for. Override for one run with
`$env:Events__Enabled = "true"`.

## 4. `.aspire/apphost.cs` — the resources

Insert **below** the line containing `<<addon-resources>>`:

```csharp
// The event-sourcing slice, behind Events:Enabled. Read the same way Database:Provider is above,
// and with the same intent: one configuration value decides the shape of the run, in one place.
//
// The flag gates the AppHost only. Nothing inside template.events reads it - when the flag is off
// neither the service nor its container is declared, so there is nothing running to gate. That is
// why this is a plain configuration read rather than Microsoft.FeatureManagement.
// bool.TryParse rather than Configuration.GetValue<bool>: GetValue is an extension method from
// Microsoft.Extensions.Configuration.Binder, and a file-based AppHost has only what its `#:package`
// directives pull in - which does not include it.
if (bool.TryParse(builder.Configuration["Events:Enabled"], out var eventsEnabled) && eventsEnabled)
{
    // Its own container, and always Postgres. Marten has no other backend, so binding the event
    // store to Database:Provider would break the sqlserver and sqlite branches outright. A separate
    // container keeps the two stores independent instead.
    var eventstore = builder.AddPostgres("events-pg")
                            .WithDataVolume()
                            .AddDatabase("eventstore");

    builder.AddCSharpApp("events", "../src/template.events/template.events.csproj",
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
}
```

## 5. `.aspire/apphost.cs` — package directive

The event store is always Postgres, even when the API's data store is not. If the line
`#:package Aspire.Hosting.PostgreSQL@13.5.1` is **not** already present (that is, the scaffold was
created with `--db sqlserver` or `--db sqlite`), insert it **above** the line containing
`<<addon-packages>>`:

```csharp
#:package Aspire.Hosting.PostgreSQL@13.5.1
```

---

## Verify

```bash
dotnet build <Name>.slnx
dotnet test tests/template.events.tests          # unit tests only; no Docker needed
$env:Events__Enabled = "true"; aspire run        # events-pg and events should appear
```

## Notes for the generated docs

- **No handler may call `SaveChangesAsync()`.** `Policies.AutoApplyTransactions()` owns the Marten
  transaction and commits it around each handler. Committing by hand takes the outgoing messages
  out of the same unit of work, which is exactly the atomicity the outbox exists to provide.
- **`GET /orders/{id}` is eventually consistent.** `OrderSummaryProjection` is registered
  `ProjectionLifecycle.Async` and runs on Marten's daemon, so a read straight after a write can
  return the old state or a 404. That is correct behaviour, not a race.
  `ProjectionLifecycle.Inline` is the one-line switch if read-your-writes is wanted.
- **`Order` is not a registered projection.** It is live-aggregated from its stream on every
  command, which is what `FetchForWriting<Order>` needs. `OrderSummary` is the registered read model.
- **Domain events are not integration contracts.** `OrderApproved` stays inside the aggregate
  boundary; `OrderApprovedV1` is the versioned public fact. A breaking change means a new type.
- **No broker.** `Policies.UseDurableLocalQueues()` keeps messages in-process but still durable on
  the Postgres outbox tables. Adding a transport package is a deliberate decision, not a default.
