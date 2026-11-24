# Structured logging: W3C trace context and entity ids on every log

**Status:** draft, written 2026-11-24 from Kurt's grill. It comes after `application-insights-workbook`
(template-projects#13, merged) and before factory spec 2 (`factory check telemetry`), so that spec 2 can
assert it from the start.
**Owner of the decisions:** Kurt.

## Why

`azure-monitor-telemetry` made Serilog the structured front, with one way out through the OTel logging
provider. A log already joins its request through `operation_Id`. What it doesn't carry is *what* it is
about. The template's controller logs nothing about the Widgets it serves, and no log line names an entity,
so "show me everything that happened to Note 42" has no query.

Kurt wants logs with real structure:

- **W3C trace context** as named fields on every record, in every sink, not only where App Insights maps
  it to `operation_Id`.
- **The entity's identity**, meaning its surrogate key, as a fixed, generic pair. One query then finds an
  entity's history across logs and spans, whatever the resource is called.

## Goal

Every log record the API, ServiceDefaults or a front end writes carries:

| Field | Value | Present |
|---|---|---|
| `trace_id` | W3C trace id, 32 hex | always, when an Activity is current |
| `span_id` | W3C span id, 16 hex | always, when an Activity is current |
| `trace_flags` | `00` or `01` | always, when an Activity is current |
| `parent_span_id` | the current span's parent, 16 hex | when the span has a parent |
| `entity.type` | the resource's type name, e.g. `Note` | inside an entity scope |
| `entity.id` | the surrogate key, **as a string** (composite keys joined `a|b`) | inside an entity scope with an id |
| `entity.related` | a list of `Type:id` for the other entities involved | when the code names them |
| `enduser.id` | the signed-in user's object id | when a user is signed in |

The same `entity.*` and `enduser.id` values are also set as tags on the current Activity, so requests and
dependencies can be filtered by entity too. The fields reach App Insights (`customDimensions`), the Aspire
dashboard and the console, which writes compact JSON in Azure.

## Decisions (from the grill, 2026-11-24)

| # | Question | Decision | Not chosen |
|---|---|---|---|
| D1 | Where the structure shows | **Everywhere:** App Insights, Aspire dashboard, console as compact JSON in Azure (ACA stdout lands in Log Analytics). | App Insights and Aspire only; App Insights only |
| D2 | Trace fields | **`trace_id`, `span_id`, `trace_flags`, `parent_span_id`** (Kurt added the parent). | A `traceparent` string; `tracestate` |
| D3 | Entity naming | **Generic pair** `entity.type` / `entity.id`. It survives the resource rewrite, and one query covers every type. | `NoteId` per type; both |
| D4 | Several entities | **Primary plus `entity.related`** (`Type:id` list). | One pair only; a parent pair |
| D5 | How it attaches | **An ILogger scope and Activity tags**, opened by a filter when the route has `{id}`, and by a `ForEntity(...)` helper elsewhere (creates, related entities). | Scope only; explicit per call; filter only; helper only |
| D6 | What the template logs | **Writes and misses at Information** (created, updated, deleted, not found, refused); **reads at Debug**. | Everything at Information; writes only |
| D7 | Who acted | **`enduser.id` = object id only.** No name, email or UPN. | No user; oid plus name |
| D8 | Keeping it structured | **`[LoggerMessage]` source generation** for the template's events, **CA2254 and CA1848 as errors**, and unit tests that capture records and assert the fields. | Tests only; analyzers only |
| D9 | Names | **OTel semantic, lowercase and dotted.** | PascalCase |
| D10 | Front ends | **The same keys:** the React and Blazor log helpers take an entity, and the relay keeps `entity.*`. | Server only for now |
| D11 | Proof | **A Workbook "Entity history" section**, plus an assertion in spec 2's check. | Workbook only; tests only |
| D12 | Id format | **Always a string.** | Native type |
| D13 | Sensitive ids | **Surrogate keys only**, asserted by a test for the template's entity and stated in the docs and CLAUDE.md. | Hash natural keys; no rule |
| D14 | Template API | **Add `GET /Widgets/{id}`** (found at Debug, not found at Information), so the filter and a miss exist in every scaffold. Writes stay a scaffold concern, and ADDING-A-RESOURCE shows their pattern. | Full CRUD; plumbing only |
| D15 | Live proof | **A not-found by id:** spec 2's check calls `GET /api/<Resources>/0` and finds the Information log with `entity.id = "0"` under that request's `operation_Id`. Deterministic, creates no data, and works on SQLite. | List read at Information; wait for writes |
| D16 | Event services, Functions | **Out**, deferred with event-driven telemetry (2026-11-23). | In |

### Changed from the grill (Kurt to confirm)

- **`enduser.id` source claim.** The template's auth is generic OIDC (`Auth:Authority`), not Entra-only.
  Entra puts the object id in `oid`, which ASP.NET maps to
  `http://schemas.microsoft.com/identity/claims/objectidentifier`; other issuers have only `sub`. So
  `enduser.id` takes `oid` (either form) and falls back to `sub`. It's absent when anonymous.
- **Console in Production stays Warning and above** (`ConsoleLevelTests`): ACA copies stdout into the same
  workspace, and Information on both would be stored twice. "Everywhere" therefore means the console
  *format* is compact JSON in Azure; its level doesn't change. Outside Azure (no
  `APPLICATIONINSIGHTS_CONNECTION_STRING`) the console stays human-readable text.

## Scope

### In

- **ServiceDefaults (`template.servicedefaults`):**
  - `TraceContextEnricher` (a Serilog `ILogEventEnricher`): reads `Activity.Current` and adds `trace_id`,
    `span_id`, `trace_flags` and `parent_span_id` (only when the parent isn't default). Registered in
    `ConfigureSerilog` after `Enrich.FromLogContext()`.
  - `EnduserEnricher`: reads the `oid` / objectidentifier / `sub` claim from `IHttpContextAccessor` and
    adds `enduser.id`. It does nothing outside a request or when anonymous.
  - `EntityScope`: `ILogger.ForEntity(string type, object id, params (string Type, object Id)[] related)`
    returns an `IDisposable`. It pushes `entity.type`, `entity.id` (formatted invariant; a tuple key is
    joined `|`) and `entity.related` (a string array of `Type:id`) as a Serilog `LogContext` scope. It also
    sets the same tags on `Activity.Current`. An overload without an id covers lists.
  - **The console format:** `CompactJsonFormatter` (`Serilog.Formatting.Compact`, already a dependency of
    `Serilog.AspNetCore`, so no new package) when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set; the
    current text template otherwise. The forwarder is unchanged: the enrichers' properties are ordinary
    Serilog properties, so they already reach the OTel provider.
- **API (`template.api`):**
  - `GET /Widgets/{id:int}` on `WidgetsController`, named `GetWidget`, returning `200 Widget` or `404
    ProblemDetails`, with `[ProducesResponseType]` for both. `IRepository<Widget>` gains `GetByIdAsync` if
    it lacks one.
  - `EntityScopeFilter`: an `IAsyncActionFilter`, registered globally. When the action has an `id` route
    value, it opens `ForEntity(<entity type>, id)` for the action's duration. The entity type comes from an
    `[EntityType(typeof(Widget))]` attribute on the controller, not the controller name, because
    controller names are plural and get rewritten.
  - `WidgetLog`: a `static partial class` of `[LoggerMessage]` methods, `WidgetFound` (Debug),
    `WidgetNotFound` (Information) and `WidgetsListed` (Debug, count). The existing fault warning becomes
    `FaultInjected` (Warning). The list action opens `ForEntity("Widget")` with no id.
  - CA2254 and CA1848 become errors in `Directory.Build.props` (`<WarningsAsErrors>`) for every project. Any
    template call that trips them is converted to `[LoggerMessage]`: `AuthConfigurator`, `CorsConfigurator`
    and the relay.
- **Contract and clients:**
  - `contracts/api.openapi.json` regenerates with `GetWidget`.
  - The React and Blazor generated clients are regenerated (refitter, openapi-typescript).
  - The front ends don't call the new endpoint.
- **Front ends:**
  - **React** (`src/lib/telemetry.ts`): an exported `logError(message, { entity?: { type, id }, related? })`
    that emits with `entity.type`, `entity.id` (a string) and `entity.related`. The existing `emitError`
    callers stay as they are (no entity). The list page's load failure calls it with `{ type: 'Widget' }`.
  - **Blazor** (`WidgetsViewModel`): its `LogError` goes through a `[LoggerMessage]` method inside
    `logger.ForEntity("Widget")`, which needs a client-side `ForEntity` in
    `Template.Blazor.Maui.Pages/Telemetry` that uses `ILogger.BeginScope` with the same keys. The OTLP JSON
    log exporter writes scope values as attributes.
  - **The relay** already copies a record's attributes. A test proves `entity.*` survive it unchanged.
- **Workbook (`IaC/workbooks/telemetry.json`):** a new **Entity history** section, with parameters
  `EntityType` (text) and `EntityId` (text). Its query:
  `union traces, requests, dependencies, exceptions | where tostring(customDimensions["entity.type"]) ==
  "{EntityType}" and ("{EntityId}" == "" or tostring(customDimensions["entity.id"]) == "{EntityId}")`,
  newest first, with `trace_id`, `operation_Id`, `enduser.id`, `itemType`, `message` / `name`. No column is
  named after a KQL reserved word: the existing `telemetry.tftest.hcl` run enforces that. A new run asserts
  the section and both parameters exist.
- **Docs:**
  - **`base-project/README.md`**, under "In Azure: Application Insights": the field table above, the Entity
    history section, and a KQL example.
  - **`docs/ADDING-A-RESOURCE.md`**: a new resource gets `[EntityType]`, a `<Resource>Log` class with
    Created, Updated, Deleted (Information) and NotFound, plus `ForEntity` around creates once the id
    exists. It also states the surrogate-key rule.
  - **`CLAUDE.md`**: one rule. Log through `[LoggerMessage]` inside an entity scope, `entity.id` is always
    the surrogate key, and a natural key such as an email never goes in a log.

### Out

- **Event services and the Functions host** (Wolverine, Marten) (D16).
- **Writes in the template** (D14). Scaffolds add them, following ADDING-A-RESOURCE.
- **The `/alive` probe fix** (decided 2026-11-24, option B). It's its own change, and it doesn't touch
  logging.
- **`factory check telemetry` itself** and its not-found step (factory spec 2). This spec supplies the
  endpoint, the log and the KQL.
- **Hashing or redacting** anything other than "surrogate keys only".
- **Changing existing scaffolds.** They adopt by hand, as with the Workbook.

## Tests

Red first for each. New files follow the existing `Harness/` layout, and records are captured with
`CapturingLogProcessor`, which asserts what reaches every exporter.

- **`TraceContextLoggingTests`:**
  - a log inside a request has `trace_id` equal to the request Activity's `TraceId`, along with `span_id`
    and `trace_flags`, plus `parent_span_id` when the request has a `traceparent` header
  - a log with no Activity has none of the four
- **`EntityScopeTests`:**
  - `GET /Widgets/{id}` for a missing id logs `WidgetNotFound` at Information with `entity.type = "Widget"`
    and `entity.id = "<id>"` (a string), and the request Activity carries the same tags
  - an existing id logs at Debug only
  - `ForEntity` with related entities gives `entity.related = ["Tag:3"]`
  - a tuple key gives `"1|2"`
  - `entity.id` for the template's entity is its `Id`, never `Name` (D13)
- **`EnduserLoggingTests`:**
  - a signed-in request with `oid` logs `enduser.id = <oid>`
  - a request with only `sub` logs the `sub`
  - an anonymous request has no `enduser.id`
  - no record carries the `name`, `email` or `preferred_username` claim values
- **`ConsoleFormatTests`:**
  - with the connection string set, a console line parses as CLEF JSON and has `trace_id` and `entity.id`
  - without it, the line is text
  - the existing Production Warning level holds
- **`WidgetsControllerTests` / `ContractTests`:**
  - `GetWidget` returns 200 and 404, and is in the OpenAPI document
  - the committed contract matches
- **`BrowserTelemetryRelayTests`:** a relayed log record with `entity.type` / `entity.id` attributes keeps
  them.
- **Analyzers:** a build-time check that an interpolated `logger.LogInformation($"...")` fails the build.
  This is a test project file compiled by the test, or a `dotnet build` of a fixture asserted in the tooling
  suite. Pick whichever the tooling suite already supports.
- **React (Vitest):** `logError` with an entity emits the four attributes, with `entity.id` as a string.
- **Blazor:** the ViewModel's error log carries `entity.type = "Widget"`.
- **IaC:** `telemetry.tftest.hcl` gains `the_workbook_has_an_entity_history_section`, and the reserved-name
  run covers the new query.
- **Suites:**
  - the workspace `terraform test`
  - the tooling suite
  - a fresh postgres + react scaffold and a sqlserver scaffold, each with its own suites
- **Live (ask Kurt first):**
  - one preview, from a factory job on telproof or `factory canary -Full`
  - load the resource page, then `GET /api/<Resources>/0`
  - KQL: a `traces` row with `entity.id == "0"`, `entity.type`, `trace_id` and `span_id`, whose
    `operation_Id` matches the 404 request's
  - the Entity history section shows it
  - the container's console log in Log Analytics (`ContainerAppConsoleLogs_CL`) is JSON, if a Warning was
    logged
  - record which names App Insights keeps (see Risks)

## Risks

- **App Insights may rename or drop the trace fields.**
  - The Azure Monitor exporter maps the record's own trace context to `operation_Id` and
    `operation_ParentId`.
  - Attributes named `trace_id` or `span_id` might be treated as reserved and dropped, rather than kept in
    `customDimensions`.
  - The live check records what lands. If they're dropped, the enricher keeps them for the console and the
    dashboard, and App Insights relies on `operation_Id` / `operation_ParentId`. The README documents that
    mapping instead.
- **CLEF duplicates.** `CompactJsonFormatter` also writes `@tr` and `@sp` from the event's own trace
  context, so the console carries both. That's accepted: no `Serilog.Expressions` package for a custom
  template without Kurt's yes.
- **Analyzers as errors may break existing template code.** CA1848 flags every `logger.LogX` call. The
  scope lists the known ones, and the build finds the rest. Generated code and the MAUI host may need an
  `.editorconfig` exclusion. If so, name each one and say why.
- **The resource rewrite.**
  - `Widget` in `[EntityType(typeof(Widget))]`, `WidgetLog` and the React `{ type: 'Widget' }` is rewritten
    per scaffold, as intended.
  - The Workbook's Entity history query must not contain `Widget` or the sourceName token. Its type comes
    from the parameter.
- **Spans with entity tags.** `Activity.SetTag` on the request Activity reaches `requests.customDimensions`.
  A database dependency is a child Activity and won't have the tags. Entity history joins on `operation_Id`
  for those, which is enough.
- **`GET /Widgets/{id}` changes the contract.** The ContractTests and the generated clients must
  regenerate in the same commit. A scaffold that already added its own `{id}` route conflicts. The
  hand-adoption note in the README covers that.

## Cost

- **Azure:** about $0. One Information line per write or miss, and reads are at Debug.
- **Build:** one session, larger than the Workbook spec. It touches ServiceDefaults, the API, the contract,
  both front ends, the Workbook, the docs and the analyzers, plus one live check.
