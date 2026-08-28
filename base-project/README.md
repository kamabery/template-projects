# base-project

A starting point for a .NET 10 app with **one API and two front ends**. A React SPA and a Blazor UI
render the same three pages against the same `template.api`, so you can pick a stack without
rebuilding the backend — or keep both and compare them side by side.

The Blazor UI is written **once** and rendered by two hosts: a .NET MAUI Blazor Hybrid app and a
Blazor WebAssembly app. Every page, component and ViewModel lives in one Razor Class Library;
neither host has UI of its own.

## Layout

The solution, the AppHost and the two files they need sit at the **workspace root**, one level above
this directory; everything they build lives under `base-project/`.

```
template.slnx                       the solution — its paths are base-project/-prefixed
aspire.config.json                      points the Aspire CLI at .aspire/apphost.cs
nuget.config                            mirrors base-project/nuget.config, for the AppHost's restore
.aspire/
  apphost.cs                            file-based Aspire AppHost — five resources
  apphost.run.json                      dashboard + OTLP ports for the AppHost itself
  Directory.Packages.props              turns CPM off here (see "NU1008" below)
base-project/
  Directory.Build.props                 shared properties, incl. the Development configuration
  Directory.Packages.props              central package versions + the three load-bearing pins
  nuget.config                          <clear/> + nuget.org  (see "Restore fails" below)
  src/
    template.api/                       Sdk.Web    the one API, incl. /harness/* fault injection
    template.data/                      entity, DbContext, seeder, generic repository
    template.api.migrations.postgres/   EF migrations, Postgres only
    template.api.migrations.sqlserver/  EF migrations, SQL Server only
    template.servicedefaults/           stock Aspire shared project (OTel, health, resilience)
    ui/
      react/                            React 19 + Vite 8 + TanStack + Tailwind v4
      blazor/
        Template.Blazor.Maui.Pages/     Sdk.Razor              ALL the Blazor UI + the MVVM framework
        Template.Blazor.Maui/           Sdk.Razor + UseMaui    MAUI Blazor Hybrid host
        Template.Blazor.Maui.Web/       Sdk.BlazorWebAssembly  Blazor WASM host
  tests/
    Template.Blazor.Maui.Tests/         xUnit + bUnit, plus the API's integration suite
```

Every other path in this README is relative to `base-project/`.

The two Blazor hosts contain only a router, an `_Imports.razor`, a host page and a composition
root. If you find yourself adding a `.razor` file to a host project, it probably belongs in
`.Pages` instead.

## Running it

One terminal, from the workspace root — or from anywhere below it, since the CLI walks up to
`aspire.config.json`:

```powershell
aspire run
```

That starts a database container, the API, the Vite dev server and the WebAssembly host, waits for
the API's `/health` check before letting the two front ends through, and opens the dashboard. **A
running Docker is now a prerequisite** — the forecast lives in a container rather than in the API's
memory. The MAUI host is
registered too but does **not** start automatically — press Start on the `maui` resource in the
dashboard, or:

```powershell
aspire resource maui start
```

It is held back deliberately. It is a desktop client rather than a service, and it only builds
where its workload and target framework line up, so auto-starting it would break `aspire run` on
any box without the MAUI workload. `Template.Blazor.Maui.Pages` is not a resource at all — a Razor
Class Library has no entry point to launch.

| Resource | Where |
| --- | --- |
| `pg` *or* `sql` | a Postgres or SQL Server Express container, port assigned by Aspire |
| `api` | `http://localhost:5058` — the dashboard link opens `/api-docs` |
| `ui` (React) | assigned by the AppHost; `http://localhost:5173` standalone |
| `blazor-web` | `http://localhost:5280` |
| Aspire dashboard | `http://localhost:15087` / `https://localhost:17143` |

The API's port is **not** dynamic: `apphost.cs` pins its `http` launch profile, because both Blazor
hosts read a hard-coded `ApiBaseAddress` and because serving the WASM page over https while that
address stays http is a mixed-content block in the browser. Change the port and
`Template.Blazor.Maui.Web/wwwroot/appsettings.json` and `Template.Blazor.Maui/appsettings.json`
(two entries) have to change with it.

`aspire logs <resource>`, `aspire describe` and `aspire stop` cover the rest; `aspire start` runs
the whole thing detached. Everything still runs standalone if you would rather not use Aspire:

The API now needs a database, so standalone means starting one yourself first:

```powershell
# 0. A database for the API to read. Skip if you are using `aspire run`, which starts one for you.
docker run -d --name weatherdb -e POSTGRES_PASSWORD=devonly -e POSTGRES_DB=weatherdb -p 5432:5432 postgres:18.3

# 1. The backend. http://localhost:5058
$env:ConnectionStrings__weatherdb = "Host=localhost;Port=5432;Database=weatherdb;Username=postgres;Password=devonly"
dotnet run --project src/template.api --launch-profile http
#    GET  /health           -> Healthy
#    GET  /openapi/v1.json  -> the generated spec
#    GET  /api-docs         -> that spec, rendered by Swagger UI (with Try it out)
#    GET  /WeatherForecast  -> five days of forecast

# 2. React. http://localhost:5173
cd src/ui/react && npm install && npm run dev

# 3. The WebAssembly host. http://localhost:5280
dotnet run --project src/ui/blazor/Template.Blazor.Maui.Web

# 4. The MAUI host (Windows)
dotnet build src/ui/blazor/Template.Blazor.Maui -f net10.0-windows10.0.19041.0 -t:Run
```

## The pages

All three front ends serve the same three routes:

- **`/`** — a landing page. In React it also probes the API and reports online/offline.
- **`/counter`** has no HTTP, no configuration and no services. It exists so that when something
  breaks you can tell instantly whether the state-to-render plumbing is at fault or the network is.
- **`/weather`** exercises the full path to the API, with loading and error states as first-class
  UI and a °C/°F toggle. React reads it through a TanStack DB collection; Blazor reads it through a
  ViewModel and a Refit provider.

React additionally has an Auto/Light/Dark control in the header; Blazor has the equivalent switch
in its `FluentHeader`.

## Progressive web apps

Both browser front ends are installable and keep an offline **app shell**. API responses are
deliberately never cached, so offline `/weather` falls through to the error state it already has
while `/counter` — which is pure client state — keeps working.

Each stack uses its own standard wiring rather than anything shared:

| | React | Blazor WebAssembly |
| --- | --- | --- |
| Worker | `vite-plugin-pwa` (Workbox) | `<ServiceWorker>` + `ServiceWorkerAssetsManifest` |
| Registration | injected into `index.html` by the plugin | a one-line `<script>` in `index.html` |
| Precache list | generated at `vite build` | `service-worker-assets.js`, generated at publish |
| Live in dev? | yes | registers, but caches nothing |

Neither app needed a change to `main.tsx` or `Program.cs` — registration is entirely a build
concern in both.

**The one asymmetry worth knowing.** `service-worker-assets.js` is generated on `dotnet publish`
only, so under `aspire run` the Blazor host registers `wwwroot/service-worker.js`, which is
deliberately inert. The app is still *installable* in dev — a manifest plus any registered worker
plus `localhost` as a secure context is enough — but it does not cache offline until published.
That is the framework's own design, and the right one: the DevServer serves unfingerprinted assets
under hot reload and caching those is a reliable way to lose an afternoon. Verify Blazor's offline
behaviour with `dotnet publish`, not `dotnet run`.

React's worker *is* live under `npm run dev`, but it precaches nothing there either — Vite serves
unhashed ES modules in development, so there is no manifest to build. If it ever does serve
something stale, the fix is *Application → Service Workers → Unregister*, then a hard reload.
`registerType: 'autoUpdate'` exists to make that rare: it calls `skipWaiting`, so a new worker
takes over instead of queueing behind the old one.

Two things that are load-bearing rather than decorative:

- **`navigateFallbackDenylist: [/^\/api\//]`** in `vite.config.ts`. The React app is same-origin
  with the API — the Vite proxy in dev, YARP after `PublishAsStaticWebsite` — so without the
  denylist the worker answers `/api/*` navigations with the app shell.
- **The cross-origin early return** in `service-worker.published.js`. The Blazor host reaches the
  API at an absolute origin (`wwwroot/appsettings.json`), unlike React. Nothing cross-origin is in
  the assets manifest, so letting those requests through the worker only adds a hop — and invites
  someone to start caching API responses there.

Installing needs a secure context. `localhost` counts, so dev is fine; a real deployment needs
https, and for the WASM host that means moving `ApiBaseAddress` to https at the same time (see
**Ports and resources** in `CLAUDE.md` — it is a coupled change).

### Icons are placeholders

`src/ui/react/public/icon-maskable.svg` is the existing favicon mark padded into the maskable safe
zone; Blazor reuses the stock `wwwroot/icon-192.png`. Replace both with real branding. Two gaps
left open on purpose, because filling them means either inventing a logo or adding a rasterizer to
a scaffold:

- **iOS ignores SVG for home-screen icons.** Safari still installs, falling back to a screenshot of
  the page. A proper iOS icon means adding a PNG `apple-touch-icon`.
- **Blazor has no 512px or maskable icon**, so Android letterboxes its icon rather than masking it.

To generate PNGs, rasterize once with a throwaway `npx --yes @resvg/resvg-js-cli` and commit the
output — don't add a rasterizer to `package.json`. Note `favicon.svg` uses `feGaussianBlur` and
`color(display-p3 …)`, which not every non-browser rasterizer handles.

## The API

Classic MVC controllers, one class per resource under `Controllers/`, routed by attribute — not
minimal-API endpoint maps. `AddControllers()` and a single `MapControllers()` are the whole
registration; `AddControllers` also brings in the API explorer, which is what puts the actions in
the OpenAPI document with no `AddEndpointsApiExplorer` anywhere.

### The API reference at `/api-docs`

`Microsoft.AspNetCore.OpenApi` generates the document but ships no UI, so
`Swashbuckle.AspNetCore.SwaggerUI` renders it at **`/api-docs`** — the swagger.io UI, with a working
**Try it out** button, so you can send a real request to any endpoint straight from the page. That
package is UI middleware only: it has no dependencies of its own and is deliberately *not*
`Swashbuckle.AspNetCore.SwaggerGen` (one letter apart), so `AddOpenApi()` remains the only thing
producing the spec and `SwaggerEndpoint` merely points the page at it. `RoutePrefix` **has** to be
set — it defaults to `swagger`, and `api-docs` is the route the dashboard link, this README and the
tests all expect. The Aspire dashboard's link for the `api` resource points here — see
`.aspire/apphost.cs`.

The page is served from the API's own origin and the generated document's `servers` entry names that
same origin, so Try it out is a same-origin request — it needs no CORS entry. `/api-docs` itself is a
301 to `/api-docs/index.html`; every asset (`swagger-ui-bundle.js`, `swagger-ui.css`, the favicons)
is an embedded resource on a relative path, so the page is **fully self-contained and works
offline** — no CDN, no webfonts. `SwaggerUi_IsServedAtApiDocs` asserts that last point directly, by
failing if any absolute URL appears in the page.

Unlike `/health` and `/harness/*`, neither `/openapi/v1.json` nor `/api-docs` is gated on Development.
One consequence worth knowing before shipping a scaffold: **the document advertises `/harness/*`**,
which returns 404 outside Development — and those endpoints are now one click from executing rather
than a `curl` command away. Gate the UI behind `app.Environment.IsDevelopment()` if that matters —
Microsoft's own guidance is that OpenAPI UIs belong in development environments only.

Arming a fault from the page is the intended workflow: expand `POST /harness/fail-next`, *Try it
out*, send `{"count":10}`, then reload `/weather` in either front end. `template.api.http` and the
`curl` calls below remain the scripted, repeatable path for the same thing.

`[Route("[controller]")]` on `WeatherForecastController` is what puts the forecast at
`/WeatherForecast`, and the `Name` on `[HttpGet(Name = "GetWeatherForecast")]` becomes the OpenAPI
`operationId`. Routing is case-insensitive, so `/weatherforecast` works too; the Refit interface in
`.Pages/Providers` spells it like the controller so the two read alike.

### Where the forecast lives

In a container, reached with EF Core through a generic repository. The AppHost starts **either**
Postgres **or** SQL Server Express and the API picks the matching EF provider, both driven by one
value so they cannot disagree:

```powershell
aspire run                                        # Postgres (the default)

$env:Database__Provider = "sqlserver"; aspire run  # SQL Server Express
```

The default lives in `.aspire/appsettings.json` under `Database:Provider`. The AppHost reads it to
decide which container to start and forwards it to the API as `Database__Provider`;
`src/template.api/Startup/PersistenceConfigurator.cs` reads it again to choose the EF provider.

| Piece | Where |
| --- | --- |
| Entity, `DbContext`, seeder, `IRepository<T>` | `src/template.data/` |
| Provider choice, DI, startup migrate + seed | `src/template.api/Startup/PersistenceConfigurator.cs` |
| Migrations | `src/template.api.migrations.postgres/`, `...sqlserver/` |

**Why migrations live in two extra projects.** EF discovers migrations by scanning a single
assembly, and two providers' migrations cannot share one — the generated SQL differs (`character
varying` vs `nvarchar`) and the migration ids collide. A project per provider is Microsoft's
documented answer. That in turn is why `template.data` exists at all: `template.api` has to
reference both migrations projects so EF's `Assembly.Load` can find them at run time, and they need
the `DbContext` type, so keeping it in a third project is what breaks the reference cycle.

Adding a migration, from `base-project/`:

```powershell
dotnet ef migrations add <Name> --project src/template.api.migrations.postgres  --startup-project src/template.api -- --provider postgres
dotnet ef migrations add <Name> --project src/template.api.migrations.sqlserver --startup-project src/template.api -- --provider sqlserver
```

Run both, or the two providers drift. `dotnet ef` must be 10.x — `dotnet tool update --global dotnet-ef`.

**Rows survive a restart.** Both containers use `WithDataVolume()`, which is why `WeatherSeeder`
checks *staleness* rather than emptiness: a naive "seed when empty" would keep serving whatever
dates were current when the volume was first created. If the newest row is no longer five days out,
the table is rewritten. To start genuinely clean, drop the volume:

```powershell
docker volume ls | Select-String "pg-data|sql-data"   # then: docker volume rm <name>
```

The schema step is Development-only and runs before the first request is served. It applies
migrations when the configured provider has them and otherwise builds the schema from the model —
that second path is what the test suite's SQLite override lands on, which is what keeps
`dotnet test` free of Docker. A real deployment runs `dotnet ef database update` from its pipeline
instead.

### Forcing the client's failure states

Beyond serving the forecast, the API can inject faults so the clients' non-happy paths are
reachable by hand. These endpoints return 404 outside Development.

```powershell
# Make the next 10 forecast requests fail with a 500, then reload /weather in any front end.
curl -X POST http://localhost:5058/harness/fail-next -H "Content-Type: application/json" -d "{\"count\":10}"

# Delay every /WeatherForecast response by 3s, to see the loading state.
curl -X POST http://localhost:5058/harness/latency -H "Content-Type: application/json" -d "{\"milliseconds\":3000}"

# Clear injected latency and injected failures.
curl -X POST http://localhost:5058/harness/reset
```

`/harness/reset` clears faults only. It deliberately leaves the database alone: wiping rows someone
may have edited by hand is a surprise, and the seed is restored on the next startup anyway.

Note this is a **behaviour change from the stock WebAPI sample**. The forecast is read from the database
now rather than generated per request, so Reload on `/weather` returns the same five rows and no
longer proves a request went out. Two things still do: the `api` resource's trace view in the Aspire
dashboard shows the HTTP span and the EF query under it, or arm `/harness/latency` and watch the
loading state appear.

**Use a `count` above 4, not 1.** The Refit clients are registered with
`AddStandardResilienceHandler()`, which retries 5xx a few times and then opens a circuit breaker.
One injected failure gets retried away and never reaches the UI; a large count exceeds the retry
budget. If repeated failures start returning instantly without hitting the server, the breaker is
open — wait it out or call `/harness/reset`.

### CORS

CORS is configured for the WebAssembly host only, in `src/template.api/appsettings.json` under
`Cors:AllowedOrigins`. If you change the WASM host's port, change that list to match or every
request fails preflight. Neither of the other two clients needs it: React reaches the API through
the Vite dev server's `/api` proxy, so it is same-origin, and the MAUI host's requests come from a
native `HttpClient`, not from browser `fetch`.

Note that `WithOrigins([])` allows nothing while the API still logs healthy 200s — the response is
just missing the `Access-Control-Allow-Origin` header, which only the browser enforces. That is why
startup logs a warning when the list is empty.

The client and the API each declare their own `WeatherForecast`, because a real service does not
share types with its callers. The server's copy lives in `src/template.data/` and is also the EF
entity, so it carries an `Id` that appears on the wire; neither front end reads it (React mirrors it
for accuracy, the Blazor positional record drops it) and `TemperatureF` is `[NotMapped]`. Both copies compute `TemperatureF` as `32 + (int)(TemperatureC / 0.5556)`,
carried over verbatim from the stock WebAPI sample — note that dividing by `0.5556` rather than
multiplying by 9/5 truncates, so 25 °C renders as 76 °F, not 77. The server's `TemperatureF` has no
setter, so it is serialized out and ignored on the way back in; if you fix the arithmetic, fix it
on both sides.

## The MVVM framework

`src/ui/blazor/Template.Blazor.Maui.Pages/Mvvm/` is about 200 lines over
[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/). To add a page:

```csharp
// ViewModels/ThingViewModel.cs
public partial class ThingViewModel(IThingProvider provider) : ViewModelBase
{
    [ObservableProperty] private IReadOnlyList<Thing> _things = [];
    [ObservableProperty] private bool _isLoading;

    public override async Task OnInitializedAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try { Things = await provider.GetThingsAsync(); }
        finally { IsLoading = false; }
    }
}
```

```razor
@* Pages/Things.razor *@
@page "/things"
@inherits ViewModelComponentBase<ThingViewModel>

@foreach (var thing in ViewModel.Things) { <p>@thing.Name</p> }
```

Then register it in `Mvvm/MvvmServiceCollectionExtensions.AddSharedViewModels`. That single method
is called by both hosts, which is what stops their service graphs drifting apart.

There is **no `StateHasChanged` and no manual refresh call anywhere**. `[ObservableProperty]` raises
`PropertyChanged`; `ViewModelBinder` subscribes and dispatches `InvokeAsync(StateHasChanged)`. Use
`ViewModelLayoutBase<T>` for layouts and `ViewModelComponentBase<T>` for everything else.

Two things that will bite you if you forget them:

- **Swap collections, don't mutate them.** `ObservableCollection<T>` raises `CollectionChanged`,
  which nothing here listens to. Assign a new list (`Items = [.. Items, created];`) so
  `PropertyChanged` fires.
- **`ValueExpression` cannot reference a static member.** Every FluentUI input builds a
  `FieldIdentifier` from its `ValueExpression`, and that rejects statics. Bind to an instance
  property that proxies the static — see `Layout/MainLayout.razor` for the theme and
  `ViewModels/WeatherForecastViewModel.Unit` for the temperature scale.

App-wide state that must outlive a navigation lives in `Stores/`, because ViewModels are transient.

## Tests

```powershell
dotnet test tests/Template.Blazor.Maui.Tests
dotnet test tests/Template.Blazor.Maui.Tests --filter "Category=UnitTest"   # skip the API suite
```

`Mvvm/ViewModelComponentBaseTests` is the suite to keep green above all others — every page depends
on that base class. It covers the cases that are easy to regress: re-render on `PropertyChanged`,
`PropertyChanged` raised off the renderer's thread, unsubscribe on disposal, and parameter capture.

`Harness/WeatherForecastControllerTests` drives the real `template.api` through
`Infrastructure/WeatherApiFactory` — a `WebApplicationFactory<Program>` that swaps the configured
provider for **in-memory SQLite**, so the suite still needs no Docker even though the API now reads
from a database. Three removals make that work and each is load-bearing: the connection string has
to be supplied with `UseSetting` (the Aspire integration refuses to register without one, and
`ConfigureAppConfiguration` lands too late), `IDbContextOptionsConfiguration<WeatherDbContext>` has
to go (since EF Core 9 *every* registered one is applied, so Aspire's Npgsql configuration would
stack onto the same options object and EF would refuse two providers), and the replacement must be
`AddDbContextPool` rather than `AddDbContext` to match the singleton pool Aspire leaves behind.
`Get_ReadsFromTheDatabase` writes a row the seeder did not create and expects to see it served —
it is the test that would fail if the controller ever went back to generating forecasts in process.

The factory hosts the app in the Development environment — that is what makes
`/health` and the `/harness/*` endpoints reachable from there, since both are Development-gated.
`/openapi` and `/api-docs` are not, and `OpenApiAndSwaggerUi_AreServedOutsideDevelopment` re-hosts the
same factory in Production to prove it. Reaching `Program` relies on the `InternalsVisibleTo` item in `template.api.csproj`,
because top-level statements compile to an internal `Program` class.

Three pins in `Directory.Packages.props` are load-bearing rather than lazy, and are commented there:
**NSubstitute must stay 5.3.0** (AutoFixture.AutoNSubstitute 4.18.1 requires `[2.0.3, 6.0.0)`),
**xUnit stays on v2** (AutoFixture.Xunit2 is the stable AutoData integration), and **Shouldly's
string `ShouldContain` is case-insensitive by default** — pass `Case.Sensitive` explicitly.

`Harness/WeatherSeederTests` covers `WeatherSeeder` directly, against its own SQLite context. Its
reason to exist is the **stale** branch: a fresh database only ever exercises the "empty" path, so
the branch that deletes and rewrites — the one that runs on a machine last used a week ago — would
otherwise never execute in any test.

`Infrastructure/TestFixtureFactory` registers `DateOnly` and `TimeOnly` by hand. AutoFixture 4.18.1
predates both types, so it picks their greediest constructor and calls `new DateOnly(int, int, int)`
with three random ints, which throws.

Test parallelism is disabled in `Infrastructure/AssemblyInfo.cs`, deliberately: the fault injector
is a singleton and `ThemeStore` / `PreferencesStore` are static, so concurrent test classes would
consume each other's state.

bUnit runs with `JSRuntimeMode.Loose`, under which an imported JS module resolves to `null`. That
makes JS-*optional* FluentUI components testable but not JS-heavy ones, which is why the shared
layout uses plain `NavLink` rather than `FluentNavMenu`, the forecast is a plain `<table>` rather
than a `FluentDataGrid`, and the provider components (`FluentToastProvider` and friends) are absent
until something needs them.

The React app has **no test runner** — it is covered by `npm run typecheck` and `npm run lint` only.

## Environment notes

**The Aspire CLI is a global tool, not something package restore brings in.** `aspire run` needs it
on `PATH`, and `.aspire/apphost.cs` pins `Aspire.AppHost.Sdk@13.5.1` in its `#:sdk` directive, so an
older CLI will not match:

```powershell
dotnet tool update --global aspire.cli   # or `install` the first time
aspire --version
```

Bumping the SDK later is `aspire update`, which rewrites the `#:sdk` and `#:package` directives in
`apphost.cs` rather than you editing the pin by hand.

**`aspire run` fails with NU1008.** `.aspire/Directory.Packages.props` exists to prevent this. The
CLI builds the file-based AppHost by generating a synthetic `apphost.cs.csproj` next to `apphost.cs`;
that project sits inside the repository, so without that file it inherits whichever
`Directory.Packages.props` an ancestor directory holds, and central package management forbids the version on the
`#:package Aspire.Hosting.JavaScript@13.5.1` directive. Do not delete it.

**`AddCSharpApp` is experimental**, hence the `#pragma warning disable ASPIRECSHARPAPPS001` at the
top of `apphost.cs`. It is the right call anyway: it takes a path instead of a `ProjectReference`, so
the AppHost never has to be reference-compatible with the MAUI project's
`net10.0-windows10.0.19041.0` target, and `aspire run` does not drag the MAUI workload in.

**Restore fails with a wall of `NU1301`.** The repo-local `nuget.config` exists because the
machine-level config on some dev boxes includes a private Azure DevOps feed that returns 401. Do not
delete it; every package here is on nuget.org. There are **two** copies, and both are needed: the one
at the workspace root covers the AppHost in `.aspire/`, which inherits nothing from `base-project/`,
and the one in `base-project/` is the copy that gets packed. Keep them in sync.

**Android is off by default.** The `android` leg of a MAUI *Blazor* project also needs the
`wasm-tools` workload for the current SDK feature band, and `dotnet workload list` shows the band
after the slash. If yours does not match, restore fails with `NETSDK1147` for the whole solution, not
just the MAUI project. To enable Android:

```powershell
dotnet workload restore
```

then set `<IncludeAndroid>true</IncludeAndroid>` in
`src/ui/blazor/Template.Blazor.Maui/Template.Blazor.Maui.csproj`, or pass `-p:IncludeAndroid=true`.
When you do, note that `localhost` inside the Android emulator is the emulator itself: the app
resolves `ApiBaseAddressAndroidEmulator` (`10.0.2.2`) at runtime via `Services/ApiBaseAddressResolver.cs`.

Android has blocked cleartext HTTP by default since API 28, so that plain-HTTP address also needs an
exemption or every forecast call fails with `Cleartext HTTP traffic to 10.0.2.2 not permitted` — the
request never leaves the device, so the API logs nothing and only the client shows an error.
`Platforms/Android/Resources/xml/network_security_config.xml` grants it, scoped to the dev loopback
hosts and referenced from `AndroidManifest.xml` via `android:networkSecurityConfig`. Preferred over
`android:usesCleartextTraffic="true"`, which would permit cleartext to every host in Release builds
too. The csproj includes `Platforms\Android\Resources\**\*.xml` as `AndroidResource` explicitly
(with `Exclude="@(AndroidResource)"` so it cannot duplicate whatever the SDK already globs), because
the .NET Android SDK globs from `$(MonoAndroidResourcePrefix)` — which defaults to `Resources`, not
the single-project platform folder.

**iOS and Mac Catalyst** are only in the target framework list on macOS. Gating them that way — the
stock MAUI project gates them on "not Linux" — is what lets `dotnet build` on the solution succeed on
Windows.

**FluentUI inside the MAUI WebView.** Do **not** add FluentUI's `initializersLoader.webview.js` to
`wwwroot/index.html`, and leave `blazor.webview.js` on autostart. The loader is for older SDKs that
emitted the JS-initializer manifest as `<app-name>.modules.json` in the wwwroot root; it works by
rewriting the fetch of `_framework/blazor.modules.json` to that name. This SDK emits the manifest at
`_framework/blazor.modules.json` and produces no app-named variant, so the rewrite resolves to a 404
— and `blazor.webview.js` calls `.json()` on that response without checking it, so startup dies and
the app sits on `Loading...` forever. Setting `autostart="false"` does the same thing, because
nothing on the page calls `Blazor.start()`. The manifest already lists
`Microsoft.FluentUI.AspNetCore.Components.lib.module.js`, so JS-module FluentUI components register
natively without the loader.

**Adding a React route.** `src/routeTree.gen.ts` is generated by the TanStack Router Vite plugin, so
a brand-new file under `src/routes/` will fail `npm run typecheck` (which runs first in
`npm run build`) until the plugin has run. Run `npx vite build` or start `npm run dev` once, then
typecheck.

**`NU1902` on AngleSharp** is a moderate advisory on a transitive dependency of bunit 2.6.2. Fixing
it means moving off the pinned bUnit version; it affects test-time HTML parsing only.
