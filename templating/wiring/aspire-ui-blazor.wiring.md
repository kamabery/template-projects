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

## 5. Client telemetry

Nothing to wire — but worth knowing, because it spans both hosts.

Both hosts export their own traces and logs to the Aspire dashboard, from
`src/ui/blazor/Template.Blazor.Maui.Pages/Telemetry/`. The MAUI host also exports metrics; the
WebAssembly host does not, because its metric reader collects on a background thread the browser
never runs.

The MAUI host reads its OTLP settings from the environment the AppHost starts it with. The
WebAssembly host cannot — a browser has no environment, and `wwwroot/appsettings.json` is a static
file that cannot carry the dashboard's per-run API key — so it fetches them from the API at
`GET /client-telemetry/config`, which `aspire-base` already ships (Development only). That endpoint
is why the `api` resource carries `WithOtlpExporter(OtlpProtocol.HttpProtobuf)`: a browser cannot
speak gRPC, so the endpoint the API hands out has to be one it can post to. Leave that call in place
if you want RUM from the WebAssembly host.

---

## 6. `IaC/` — one file, and a CORS pass that is not optional

`IaC/frontend.blazor-web.tf` was added by this add-on when `--web true`, and Terraform loads it
automatically - there is nothing to merge. But **two steps happen outside Terraform and both fail
silently**:

- **The Static Web App's hostname has to reach the API's `Cors:AllowedOrigins`.** It cannot happen
  in one pass: `IaC/containerapp.api.tf` is part of the base scaffold and cannot reference a
  resource declared in an add-on file. So apply once, then put `terraform output blazor_web_url`
  into `cors_allowed_origins` in `IaC/envs/<environment>.tfvars` and apply again. The shipped
  `.github/workflows/deploy.yml` does this automatically. Skip it and the page loads, the API logs
  healthy 200s, and every request fails in the browser - the exact asymmetry
  `WarnOnMissingCorsOrigins` exists to warn about.
- **`wwwroot/appsettings.json` has `ApiBaseAddress` hard-coded** to the local API port. It is a
  static file the browser fetches, so nothing in the platform can inject it; the deploy workflow
  rewrites it to the API's address before uploading.

The MAUI host, if this scaffold has one, provisions nothing. It is a client, not a workload.

---

## Verify

```bash
dotnet build <Name>.slnx
dotnet test tests/Template.Blazor.Maui.Tests
aspire run    # blazor-web should be running; maui appears as "Not started"
```

Open the WebAssembly host and move between pages: the dashboard should show a `blazor-web` resource
with `page_view` spans and the API's own span nested under the client's HTTP call.

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
- **A FluentUI component that ships a JS module is a risk, not a ban.** bUnit runs with
  `JSRuntimeMode.Loose`, under which an imported module resolves to `null` - so what throws is a
  component that then *calls into* it. `FluentButton`, `FluentTextField`, `FluentCheckbox`,
  `FluentTab` and `FluentDialogProvider` all ship one and render fine; `FluentNavMenu` and
  `FluentDataGrid` are the two that genuinely throw, which is why the nav menu and the forecast
  table are hand-rolled. `Pages/StyleGuide.razor` renders every approved component in one place and
  `StyleGuidePageTests` renders that, so an upgrade that breaks one fails there.
- **`MainLayout` mounts `FluentDialogProvider` and `FluentToastProvider`.** `IDialogService` and
  `IToastService` resolve whether or not their provider is mounted, so removing one breaks the
  dialog and toast paths at run time with nothing failing at build time. `MainLayoutTests` guards it.
- **The design system is `/styleguide` plus the token block at the top of
  `Pages/wwwroot/app.css`.** Those tokens (`--color-surface`, `--color-intent-danger`,
  `--radius-control`, `--shadow-raised`, `--motion-fast`, …) are aliases onto FluentUI's own
  variables, so they follow `FluentDesignTheme` with no dark-mode duplicate. If this scaffold also
  has the React front end, the same names are declared there in `src/ui/react/src/index.css` and the
  two are kept in step by hand - adding a token to one means adding it to the other.
- **`Components/AppField.razor` is the form contract.** Label, description, validation message and
  the `aria-describedby` that ties them to the control. Use it rather than wiring FluentUI's own
  label and validation per input; a field is invalid when `Error` is non-empty.
- **Client telemetry is registered once, for both hosts**, by `AddSharedTelemetry` followed by
  `ActivateSharedTelemetry` on the built application. Page-view spans are started from `MainLayout`,
  the one component both hosts wrap every page in.
- To add a page: a ViewModel in `ViewModels/`, a `.razor` in `Pages/` with
  `@inherits ViewModelComponentBase<T>`, a `NavLink` in `Layout/NavMenu.razor`, and one
  `AddTransient` line in `MvvmServiceCollectionExtensions.AddSharedViewModels`. A page with no
  state of its own needs neither the ViewModel nor the registration.
