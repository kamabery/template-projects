# Azure Monitor telemetry: what a scaffold emits reaches Application Insights

**Status:** decided, not started. Written 2026-11-23; Kurt answered the open questions the same day (see Decisions),
then a review against the code found gaps and he settled those too (see "Logging" and "Review follow-ups"; where they
differ from Scope or Tests, they win).
**Owner of the decisions:** Kurt.

## Why

On 2026-11-23, Save on recipebox's J-039 preview (an `aspire-base` + `ui-react` scaffold, made by the factory
on the shared preview platform) sat on its spinner for a long time before it came back. The POST had
landed, since the recipe was in the list, so the time went somewhere between the API, the database and
the page's refetch. Nothing could say where:

- `appi-recipebox-j039` exists and the API gets `APPLICATIONINSIGHTS_CONNECTION_STRING`, but a query
  over the last two days of `requests`, `dependencies`, `traces`, `exceptions` and `pageViews`
  returned **no rows at all**.
- The template already emits OpenTelemetry. `template.servicedefaults/Extensions.cs` has
  `ConfigureOpenTelemetry` (ASP.NET Core, HttpClient and runtime instrumentation, plus OTel logging).
  But `AddOpenTelemetryExporters` exports only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, which happens
  only under the Aspire AppHost. The `UseAzureMonitor()` block is still the stock commented-out lines.
- `IaC/observability.tf` says "the Container Apps environment forwards them". It doesn't. Neither the
  shared `cae-factory-platform` nor a scaffold's own environment (`containerapps.tf`) sets
  `openTelemetryConfiguration` or `appInsightsConfiguration`; live, both are `null`. So in Azure, the
  telemetry has nowhere to go.
- The browser side (`aspire-ui-react` `src/lib/telemetry.ts`, the Blazor `Telemetry/` options) is
  wired to the Aspire dashboard's OTLP endpoint. By design, it is a no-op in a production build.

So the template looks observable under `aspire run` and is blind in every Azure environment: previews,
dev and staging alike.

## Goal

Every service a scaffold deploys sends its traces, metrics and logs to that environment's Application
Insights through OpenTelemetry. One trace then follows a request from the browser through the API and
into the database. `aspire run` keeps the Aspire dashboard exactly as it is today. A scaffold built
today can get all of this by an `infra adopt` or a documented by-hand change.

## Scope

1. **API and events services:** add `Azure.Monitor.OpenTelemetry.AspNetCore` to `template.servicedefaults`
   (this is a new package dependency, so Kurt has to approve it; see Q1). In `AddOpenTelemetryExporters`,
   call `UseAzureMonitor()` when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set. OTLP stays as it is for the
   AppHost. Both may be on at once.
   - Keep `ConfigureSerilog`'s ordering note true: the Serilog path must still reach Application
     Insights, or the OTel logging provider must be kept for it (Q3).
   - Check that `aspire-events`' Wolverine services and its Functions host get the same treatment.
2. **Database spans:** EF Core / Npgsql instrumentation (`Npgsql.OpenTelemetry`, or the EF Core
   instrumentation) on both providers, so a slow query shows up as a dependency.
3. **Browser:** in an Azure build, `ui-react`'s OTel web SDK sends page views, fetch spans and errors to
   `/api/otel` on the API (Q2), which re-emits them as Activities the distro exports (Q2a), under the Q2b guard.
   The `traceparent` it already puts on `/api` calls carries the trace id through. Blazor gets the same.
4. **IaC:**
   - Fix `observability.tf`'s comment.
   - Every container app that runs template code gets the connection string, not only the API (check
     `containerapp.events.tf` and the SWA's API link).
   - Decide whether the environment-level OTel agent is used at all (Q4). The default is no: the SDK
     exporter is enough and works the same on the shared platform.
   - Previews keep their own `appi-*` per job, so a preview's telemetry dies with it.
5. **Sampling and cost:**
   - A default sampling ratio, and the `log_retention_days` default rechecked.
   - On the shared platform, the factory's ingestion cap (todoapp J-023, 0.5 GB a day) still holds.
6. **Docs:**
   - `base-project/README.md` and the addon docs: where to look in Azure (Application Insights →
     Transaction search, Failures, Performance) and what the Aspire dashboard maps to.
   - `RESOURCE-MAP.md` lists Application Insights as the dashboard's twin.
7. **Existing scaffolds:** say whether `aspire-addon infra adopt` brings this forward. If the change is
   in `servicedefaults`, it does not, so document the by-hand change. recipebox, booklist, pantry and
   todoapp-two are the known scaffolds.

## Tests

Seen failing, then passing, as usual:

- Unit, in `template.api.tests`:
  - with `APPLICATIONINSIGHTS_CONNECTION_STRING` set, the Azure Monitor exporter is registered
  - without it, it isn't
  - OTLP is unaffected
  - extend `LoggingConfigurationTests` so the Serilog/OTel ordering still holds
- Vitest, for `ui-react`'s telemetry: it is off with no config, and the Azure path is on with it and
  posts to `/api/otel`.
- Unit, the relay: a span's trace id, times, status and attributes survive re-emission; a body over 64 KB, a
  cross-origin request, a metrics payload and a burst over the rate limit are each refused.
- `terraform test`: every container app that runs template code has the connection string.
- **Live:** a factory canary or a preview, then a KQL query that finds, for one Save, the
  `pageView` → `request` → database `dependency` chain under one `operation_Id`. This is the check
  that failed on 2026-11-23.

## Decisions (Kurt, 2026-11-23)

| Question | Decision | Not chosen |
| --- | --- | --- |
| Q1 exporter | **Approved: `Azure.Monitor.OpenTelemetry.AspNetCore`** in `template.servicedefaults`, `UseAzureMonitor()` when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, beside the existing OTLP exporter. | A plain OTLP exporter to Azure Monitor's preview OTLP endpoint |
| Q2 browser | **Keep the OTel web SDK and relay through `/api/otel` on the API.** The connection string stays out of the bundle. | The Application Insights JS SDK |
| Q2a relay | **Re-emit as API activities:** the relay parses OTLP/HTTP JSON and re-creates each span as an `Activity` on the API's own `ActivitySource`, keeping the trace id (`operation_Id`), times, status and attributes, so the distro exports it. Browser span ids are regenerated, so parent links inside the page are approximate; the trace id carries the chain. | An OpenTelemetry Collector sidecar |
| Q2b relay guard | **Anonymous, capped:** same-origin only, a 64 KB body cap, traces and logs only, the ASP.NET Core rate limiter per client IP. The same with or without the auth add-on. | `[SignInRequired]` |
| Q3 logs | **The OTel logging provider ships logs** to Azure Monitor through the distro; Serilog stays the console sink. One pipeline, and logs correlate to the trace. | `Serilog.Sinks.ApplicationInsights` |
| Q4 env agent | **No: SDK only.** No `openTelemetryConfiguration` / `appInsightsConfiguration` on any environment, and no change to the factory's shared platform. `observability.tf`'s comment is fixed to say so. | Turning the Container Apps agent on |

Under `aspire run` the browser keeps exporting straight to the Aspire dashboard as it does today; the relay is the
Azure path (the Goal's "the Aspire dashboard exactly as it is today").

### Logging (Kurt, 2026-11-23, after review)

Q3 as first written could not work: `ConfigureSerilog` runs `ClearProviders()` after `ConfigureOpenTelemetry`, and
`AddSerilog` does not write to providers, so the provider `UseAzureMonitor()` registers would be removed. Serilog
stays the structured logging front for every log call; the intent is to track Information and above.

| # | Decision | Not chosen |
| --- | --- | --- |
| L1 path | **Serilog forwards to the OTel logging provider** (`writeToProviders: true`, no `ClearProviders()`), and the distro ships it. Template properties and `LogContext` enrichments become `customDimensions`; `operation_Id` comes from the current Activity. | `Serilog.Sinks.ApplicationInsights`; Serilog's OTLP sink to Azure (needs an agent or collector) |
| L2 dashboard | **One path:** drop Serilog's OTLP sink and `Serilog.Sinks.OpenTelemetry`. The OTel provider exports to the dashboard (`UseOtlpExporter`) and to Azure Monitor alike. The dashboard's log view may render properties slightly differently; that is accepted. | Keeping Serilog's sink under the AppHost |
| L3 levels | **App code Information+, framework Warning+:** `Default` Information; `Microsoft` and `System` Warning; `Microsoft.Hosting.Lifetime` Information. EF Core's SQL command logs drop out; SQL shows as dependency spans. | Everything Information+; today's levels unchanged |
| L4 console | **Console sink Warning+ in Production**, unchanged in Development. The Container Apps environment already sends console output to the same Log Analytics workspace, so this stops every Information line being stored twice. | Compact JSON console; console as is |
| L5 sampling | **Keep everything by default:** sampling ratio 1.0, one setting to lower it (`AzureMonitor:SamplingRatio` or similar), so no Information log is dropped with an unsampled trace. The ingestion cap is the backstop. | Distro defaults; sampled traces with unsampled logs |
| L6 destructuring | **Nested structures become a JSON string property** before forwarding (`parse_json(customDimensions.Order)` in KQL); top-level properties stay separate fields. | The rendered string; dotted keys |
| L7 browser logs | **The relay writes browser log records through `ILogger`** under a `Browser` category, with their attributes as structured properties and the browser trace id kept, so they follow L3, L4 and the enrichers. | Re-creating OTel LogRecords directly |

### Review follow-ups (Kurt, 2026-11-23, all as recommended)

| # | Decision |
| --- | --- |
| R1 relay guard | Q2b's "same-origin only" would refuse Blazor WebAssembly, which is cross-origin on its own Static Web App. The guard accepts **same-origin, judged by `Sec-Fetch-Site`** (behind the React Static Web App's linked backend the API sees the container app's host, so Origin vs Host is wrong), **or an origin in `Cors:AllowedOrigins`**. MAUI has no Azure path. |
| R2 wire format | The relay takes **OTLP/HTTP JSON**. React's Azure path uses **`@opentelemetry/exporter-trace-otlp-http` and `@opentelemetry/exporter-logs-otlp-http` (approved new npm packages)**; the `-proto` exporters stay for the dashboard. The .NET OTLP exporter has no JSON mode, so Blazor gets a small hand-written OTLP-JSON exporter in `Template.Blazor.Maui.Pages`. |
| R3 database spans | Postgres and SQL Server already emit them: the Aspire EF integrations bring `Npgsql.OpenTelemetry` and the SqlClient instrumentation. That slice becomes a test that their sources reach the tracer. **SQLite gets no database spans** (the EF Core instrumentation is still beta). There are three providers, not two. |
| R4 live check | The relay re-emits through the API's exporter, which writes `requests` and `dependencies`, never `pageViews`. The check is: **the relayed browser span → the API `request` → the database `dependency`, under one `operation_Id`.** Relayed spans carry an attribute marking them as from the browser. |
| R5 discovery | The browser finds the relay **at run time**: `GET /client-telemetry/config` (today Development only) also answers in Production with the relay path when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set; otherwise telemetry stays off. React and Blazor read the same answer, and factory previews need no build-time variable. |
| R6 rate limit key | Behind the Static Web App and ingress, `RemoteIpAddress` is a proxy. The limiter partitions on the **first `X-Forwarded-For` entry**, read only there; no forwarded-headers middleware for the whole app. Spoofable, which is acceptable for a rate limit. |
| R7 Functions | The Functions worker exports through `AddServiceDefaults` too, beside the host's own Application Insights records. The duplicates are **documented, not fixed** (no `Microsoft.Azure.Functions.Worker.OpenTelemetry`). |
| R8 IaC | The events container app and `functions.tf` already get the connection string, so the new `terraform test` is shown red by a deliberate break, not a real gap. The `observability.tf` comment is still fixed. |

## The questions as they were asked

- **Q1.** Is the `Azure.Monitor.OpenTelemetry.AspNetCore` dependency approved? The alternative is a
  plain OTLP exporter to an Azure Monitor OTLP endpoint. That is still preview-grade and would need the
  environment agent or a collector.
- **Q2.** Browser telemetry: the Application Insights JS SDK (`@microsoft/applicationinsights-web`,
  which propagates W3C trace context), or keep the OTel web SDK and send it through a small `/api/otel`
  relay on the API? The first is simpler. The second keeps one SDK and the connection string out of
  the bundle.
- **Q3.** Logs: keep Serilog as the console sink and let the OTel logging provider ship to Azure
  Monitor, or send Serilog to Application Insights through its own sink?
- **Q4.** Should the template ever turn on Container Apps' environment-level OpenTelemetry agent? It
  would also need a change on the factory's shared platform environment, in
  `ai-factory/factory/infra/previews`.

## Done when

A new scaffold's preview, with no change by hand, shows a Save as one end-to-end transaction in its
Application Insights. The Aspire dashboard still works under `aspire run`. The README says where to
look.
