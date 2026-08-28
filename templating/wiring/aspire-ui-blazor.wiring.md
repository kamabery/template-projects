# Wiring: Blazor front end

`dotnet new` can write files but cannot merge into an existing solution or `apphost.cs`. These are
the edits that finish the job. Apply only the sections matching the hosts you asked for, then delete
the `.scaffold/` directory.

---

## 1. `template.slnx` — projects

Insert **above** the line containing `<<addon-ui-projects>>`:

```xml
    <Project Path="src/ui/blazor/Template.Blazor.Maui.Pages/Template.Blazor.Maui.Pages.csproj" />
```

Then, **if the WebAssembly host was included** (`--web`), also insert:

```xml
    <Project Path="src/ui/blazor/Template.Blazor.Maui.Web/Template.Blazor.Maui.Web.csproj" />
```

And **if the MAUI host was included** (`--maui`), also insert:

```xml
    <Project Path="src/ui/blazor/Template.Blazor.Maui/Template.Blazor.Maui.csproj" />
```

Insert **above** the line containing `<<addon-test-projects>>`:

```xml
    <Project Path="tests/Template.Blazor.Maui.Tests/Template.Blazor.Maui.Tests.csproj" />
```

`Template.Blazor.Maui.Pages` is a Razor Class Library with no entry point, so it is never an AppHost
resource - it is built as a dependency of whichever host consumes it.

## 2. `.aspire/apphost.cs` — the WebAssembly host

**Only if `--web`.** Insert **below** the line containing `<<addon-resources>>`:

```csharp
// Blazor WebAssembly. WaitFor, not WithReference: WithReference injects service-discovery
// environment variables and this client cannot read them - the browser fetches its configuration
// from wwwroot/appsettings.json. WaitFor still gates startup on the health check above, which is
// the half of WithReference that actually buys something here. Because it is a real cross-origin
// browser client, it is also the only client that needs CORS on the API.
builder.AddCSharpApp("blazor-web",
                     "../src/ui/blazor/Template.Blazor.Maui.Web/Template.Blazor.Maui.Web.csproj",
                     options => options.LaunchProfileName = "http")
    .WaitFor(api)
    .WithExternalHttpEndpoints();
```

## 3. `.aspire/apphost.cs` — the MAUI host

**Only if `--maui`.** Insert **below** the line containing `<<addon-resources>>`:

```csharp
// The MAUI host is a desktop and mobile client, not a service, and it only builds where its
// workload and target framework line up - net10.0-windows10.0.19041.0 on Windows, ios/maccatalyst
// on a Mac. WithExplicitStart keeps it out of the automatic startup set: it appears in the
// dashboard as "Not started" with a Start button, so `aspire run` still comes up green on a box
// that has no MAUI workload installed.
builder.AddCSharpApp("maui", "../src/ui/blazor/Template.Blazor.Maui/Template.Blazor.Maui.csproj")
    .WithExplicitStart();
```

## 4. `src/template.api/appsettings.json` — CORS

**Only if `--web`.** The WebAssembly host is the one client that is genuinely cross-origin. Set
`Cors:AllowedOrigins` to match the WASM host's `Properties/launchSettings.json`:

```json
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5280",
      "https://localhost:7115"
    ]
  }
```

If those two values do not match `launchSettings.json`, requests still reach the controller and
still return 200 - they simply arrive without an `Access-Control-Allow-Origin` header and the
browser blocks them on the client. `WarnOnMissingCorsOrigins` reports the empty case at startup so
that asymmetry does not have to be diagnosed the hard way.

---

## Verify

```bash
dotnet build <Name>.slnx
dotnet test tests/Template.Blazor.Maui.Tests
aspire run    # blazor-web should be running; maui appears as "Not started"
```

## Notes for the generated docs

- **The API port is load-bearing.** Both hosts read a hard-coded `ApiBaseAddress`, so the AppHost
  pins the API's `http` launch profile. Changing the API's port means changing
  `Template.Blazor.Maui.Web/wwwroot/appsettings.json` and `Template.Blazor.Maui/appsettings.json`
  (two entries there - the second is the Android emulator's `10.0.2.2`).
- **No `StateHasChanged` anywhere.** `[ObservableProperty]` raises `PropertyChanged`;
  `ViewModelBinder` subscribes and dispatches `InvokeAsync(StateHasChanged)`.
- **Swap collections, don't mutate them** - nothing listens to `CollectionChanged`. Assign a new
  list (`Items = [.. Items, created];`) so `PropertyChanged` fires.
- **ViewModels are transient, not scoped.** A WASM host has one DI scope for the whole application
  lifetime, so a scoped ViewModel is effectively a singleton. Shared state belongs in `Stores/`.
- **`ValueExpression` cannot reference a static member**, so bind FluentUI inputs to an instance
  property that proxies the static one.
- **Avoid FluentUI components that ship a JS module** (`FluentNavMenu`, `FluentDataGrid`, the
  `Fluent*Provider` family): bUnit runs with `JSRuntimeMode.Loose`, under which an imported module
  resolves to `null` and those components throw.
- To add a page: a ViewModel in `ViewModels/`, a `.razor` in `Pages/` with
  `@inherits ViewModelComponentBase<T>`, a `NavLink` in `Layout/NavMenu.razor`, and one
  `AddTransient` line in `MvvmServiceCollectionExtensions.AddSharedViewModels`.
