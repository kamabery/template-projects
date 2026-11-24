# Application Insights Workbook: a telemetry dashboard in every environment

**Status:** draft, written 2026-11-23 from Kurt's grill on how the factory "gets proof" of
observability. This is spec 1 of 3. Spec 2 (factory) adds `factory check telemetry` and has the canary
run it. Spec 3 (factory) adds the SRE agent.
**Owner of the decisions:** Kurt.

## Why

`azure-monitor-telemetry` (template-projects#12, merged 2026-11-23) makes every scaffold send its
traces and logs to its environment's Application Insights. The live proof, one user action traced
from the page to the database, was never run. When it is run, it is a KQL query pasted by hand.

Kurt wants that proof to be standing and repeatable, and he wants something to look at when
something goes wrong:

- **For a person:** each environment gets a Workbook. It is the Aspire dashboard's nearest twin in
  Azure: recent transactions, failures, slow dependencies and browser logs, all on one page and
  built from KQL.
- **For the factory (spec 2):** the preview publishes what a deterministic check needs: the App
  Insights app id to query, and the page whose load makes a transaction every scaffold can produce.

## Goal

Every environment a scaffold deploys (a factory preview, dev or staging) has an Application Insights
Workbook named for it. The Workbook shows, for a chosen time range:

1. **Transactions:** recent operations, each with whether it has a relayed browser span, an API
   request and a database dependency, plus its duration and outcome. Selecting one shows its items
   in order.
2. **Failures:** failed requests by operation and result code, plus exceptions by type.
3. **Dependencies:** database and HTTP dependencies by target, with call count, failure count and
   p50/p95 duration.
4. **Browser logs:** `traces` relayed under the `Browser` category, by severity, newest first.

The IaC outputs what spec 2's check needs: the App Insights app id, the Workbook's portal URL, and
the path of the resource page.

## Decisions (from the grill, 2026-11-23)

- **Form:** an `azurerm_application_insights_workbook` in the template IaC. It costs $0 and every
  environment gets one. Not an Azure portal dashboard, and not Managed Grafana.
- **Which transaction:** the template's own read. Loading the resource page gives a page-load span,
  then the fetch, then `GET /api/<Resources>`, then the database dependency. It needs no feature and
  works on every scaffold, including a bootstrap and the canary.
- **Who proves it:** a deterministic factory check (spec 2), not an LLM. This spec only supplies the
  check's inputs.

### Changed from the grill (Kurt to confirm)

The grill put "the fixed read-trace Playwright step" in this spec. The template has no Playwright,
though: no config, no `*.spec.ts` and no package. Shipping it would add a new npm package to every
scaffold, which needs Kurt's yes. The factory already runs Playwright for the sign-in smoke
(`factory/scripts/signin-smoke.mjs`), so:

- **This spec** outputs `trace_page_path`, the resource page's path (`/widgets`, which becomes
  `/<resources>` in a scaffold).
- **Spec 2** runs the read step from the factory, beside the sign-in smoke: it loads
  `<frontend url><trace_page_path>` and waits for the list to render.

## Scope

### In

- **`IaC/observability.tf`:**
  - An `azurerm_application_insights_workbook.main` in every environment, with:
    - `name`: a UUID, `uuidv5("url", ...)` of the resource group name and a fixed suffix, so it is
      known at plan and stable across applies. Do not derive it from a resource id.
    - `display_name`: `"<base_name> <environment> telemetry"`.
    - `source_id`: `lower(azurerm_application_insights.main.id)`.
    - `category`: `"workbook"`.
    - `data_json`: from `IaC/workbooks/telemetry.json`.
    - `tags`: `local.tags`.
  - The file's top comment says the Workbook is there and what it shows.
- **`IaC/workbooks/telemetry.json`:** the Workbook definition (`Notebook/1.0`). It has one
  `TimeRange` parameter (default 1 hour) and the four sections above, each a KQL query against the
  App Insights resource:
  - Transactions: `union requests, dependencies` grouped by `operation_Id`, with
    `hasBrowser = countif(customDimensions["relay.source"] == "browser") > 0`,
    `hasRequest = countif(itemType == "request") > 0`, and `hasDb` for a dependency whose `type` is
    the database's (`postgresql`, `SQL`). The exact type strings are checked live, not guessed (see
    Risks). Selecting a row fills a second grid with that operation's items in timestamp order: the
    README's "One Save" query, parameterised.
  - Failures, Dependencies and Browser logs: queries over `requests`, `exceptions`, `dependencies`
    and `traces` (`customDimensions.CategoryName == "Browser"`).
  - The file must not contain the template's sourceName token or the resource name (see Risks).
- **`IaC/outputs.tf`:**
  - `application_insights_app_id`: the id `az monitor app-insights query --app` takes.
  - `application_insights_name`
  - `telemetry_workbook_url`: the Workbook's portal deep link.
  - `trace_page_path`: `"/widgets"`, which the resource symbol rewrites to the scaffold's
    resource. Its description says it is the page whose load spec 2's check traces.
- **`IaC/tests/`:** `terraform test` runs, written red first, mocked like the existing ones:
  - `every_environment_gets_a_telemetry_workbook`: at plan, for a preview on the platform, a full
    preview and dev. Its `source_id` is the App Insights resource, lowercased.
  - `the_workbook_name_is_stable`: two plans of the same environment give the same `name`, and
    different environments give different names.
  - `the_workbook_has_its_four_sections`: `jsondecode(data_json)` has a `TimeRange` parameter and
    four query items, and the transactions query mentions `operation_Id` and `relay.source`.
  - `the_proof_outputs_are_set`: all four outputs are non-null, and `trace_page_path` starts with `/`.
- **Docs:**
  - **`base-project/README.md`, "In Azure: Application Insights":**
    - A **Workbook** row in the "Where to look" table.
    - The `telemetry_workbook_url` output.
    - The read transaction and what each column means.
    - SQLite has no database spans, so `hasDb` is always false there.
  - **`IaC/RESOURCE-MAP.md`:** the Workbook under "Observability", in the same `appi-`
    resource group.
  - **`CLAUDE.md`:** one line under "### Azure — the browser relay" pointing at the Workbook.
- **Packs:**
  - `IaC/workbooks/**` is shipped by `aspire-base`, so check its `template.json` sources.
  - `stage.ps1` builds the packs without a guard refusal.
  - If the JSON trips `Assert-ProseSubstitutionSafe` or the sourceName rewrite, it becomes
    `copyOnly`, as `platform.tftest.hcl` did.

### Out

- `factory check telemetry`, the read step, the canary change and the dashboard link to the Workbook
  (spec 2). The SRE agent (spec 3).
- Alerts, availability tests and Managed Grafana. Availability tests would cost about $50 a month
  per test at preview scale.
- Changing what the services emit. That is `azure-monitor-telemetry`, which is merged.
- Existing scaffolds (todoapp, recipebox, telproof): they get this by copying the files by hand, as the
  README describes. `aspire-addon infra adopt` overwrites only pack-owned files, and
  `observability.tf` and `outputs.tf` are the project's.
- Event services and Functions panels. Event-driven telemetry comes later (Kurt, 2026-11-23).

## Tests

- **Red first:** each `terraform test` run above fails on `main` (no Workbook, no outputs), then
  passes.
- **Suites:**
  - Workspace `terraform test` (22 runs today).
  - The tooling suite (127).
  - A fresh scaffold (postgres + react) with its `terraform test`.
  - A second scaffold on sqlserver, to check the substitution once more.
- **The Workbook JSON loads:** `terraform validate`. The deep link is checked live (below).
- **Live (ask Kurt first):** one apply of a preview or `factory canary -Full`, then:
  - Open the Workbook from `telemetry_workbook_url`.
  - Load the resource page once.
  - See a Transactions row with `hasBrowser`, `hasRequest` and `hasDb` all true.
  - Run each of the four queries through `az monitor app-insights query --app
    <application_insights_app_id>` and read the rows.

  This live check is the proof `azure-monitor-telemetry` never got. It also gives spec 2 its KQL.

## Risks

- **Dependency `type` strings:** Npgsql and SqlClient spans arrive through the Azure Monitor
  exporter as `dependencies` with a `type` that has to be read from real data. Guessing wrong gives
  `hasDb = false` everywhere. Read the type off the live preview before fixing the query.
- **Relayed browser spans:** they land in `dependencies`, under the API's role name, and are found
  only through `customDimensions["relay.source"]`. A query that looks in `pageViews` finds nothing.
- **Ingestion delay:** typically 1–5 minutes, sometimes over 10. The Workbook only reads, so this
  matters only to spec 2's check, which needs a polling window and a "no data yet" result distinct
  from "broken".
- **Substitution:** the pack rewrites the sourceName token as a case-insensitive substring, and the
  resource name as `Widget`/`widget`. A Workbook JSON that mentions either (for example in a role
  name or a route) is rewritten in every scaffold. The JSON must use neither. Role names come from
  the data (`cloud_RoleName`), not from literals.
- **Workbook name collisions:** names are per resource group, and every preview has its own group
  (`rg-<base>-<env>`), so `uuidv5` of the group name is unique.
- **The portal deep link format:** check it live before documenting it.

## Cost

- **Azure:** $0. Workbooks are free, the queries run on the Analytics tier at no charge, and
  ingestion is unchanged.
- **Build:** one session. It's about the size of a single slice of `azure-monitor-telemetry`, plus
  one live check.

## Live check (2026-11-24, J-043 on telproof, preview `j043`)

The proof `azure-monitor-telemetry` never got. telproof (postgres + react, resource `Note`) adopted the
Workbook by hand through factory job J-043 (J-042 was discarded for a gate line it could never pass).
Canary #37 on this branch's templates also passed: a fresh preview applies with the Workbook.

- **The Workbook exists:** `Microsoft.Insights/workbooks/bfc105e0-…`, `telproof j043 telemetry`,
  `kind: shared`, its `sourceId` the App Insights id lowercased, as Azure stores it.
- **One read, traced:** loading `/notes` gave a Transactions row `GET Notes` with `hasBrowser`,
  `hasRequest` and `hasDb` all true. Its items, in order: the browser's `GET /api/Notes` (`HTTP`,
  `relay.source = browser`, 216 ms), the request `GET Notes` (7 ms), the `postgresql` dependency
  (3 ms), then the trace `received-first-response`.
- **Dependency types, read off real data:** Postgres is `postgresql`. Relayed browser spans are
  `HTTP` (the fetch) and `InProc` (`documentLoad`, `documentFetch`, `resourceFetch`, `click`). The page
  load (`documentLoad`) is its own operation, not the fetch's parent, so a transaction starts at the
  fetch. **SQL Server's `SQL` is still unverified:** no sqlserver preview was run.
- **A bug the live run found:** Failures and Exceptions named a column `last` (and `count`), which KQL
  reserves, so both grids failed to parse. Renamed to `lastSeen` and `occurrences`; a test now refuses
  reserved column names. J-043 round 2 carried the rename to telproof and redeployed j043; all four queries
  then ran live without error, and a fresh `/notes` load gave `GET Notes` with all three flags true.
- **Browser logs** were empty: the page logged no error. The query ran.
- **The deep link** `https://portal.azure.com/#resource<workbook id>/workbook` opens the Workbook: Kurt
  checked it in the portal after round 2, with `GET Notes` listed and Failures and Exceptions rendering.
- **Querying:** the `application-insights` az extension is not installed, so the queries ran through
  `az rest --resource https://api.applicationinsights.io` against
  `https://api.applicationinsights.io/v1/apps/<application_insights_app_id>/query`.

### KQL for spec 2's check

Wait for ingestion (1–5 minutes, sometimes over 10), then pass when a row comes back:

```kusto
union requests, dependencies
| where timestamp > ago(30m)
| summarize hasBrowser = countif(tostring(customDimensions["relay.source"]) == "browser") > 0,
            hasRequest = countif(itemType == "request") > 0,
            hasDb      = countif(itemType == "dependency" and type in~ ("postgresql", "SQL")) > 0
            by operation_Id
| where hasBrowser and hasRequest and (hasDb or <provider is sqlite>)
| take 1
```

No row yet is "no data yet" until the window closes. After that, it is a failure.
