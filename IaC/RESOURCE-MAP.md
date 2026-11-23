# Resource map

Every resource the AppHost runs locally has an Azure twin here, and the twin is what a deployed
environment - dev, staging, or a factory job's ephemeral preview - runs instead. The rule is
equivalence: the application code does not change between `aspire run` and Azure; configuration
does.

`aspire-addon infra check` enforces this table. It reads every `builder.Add<Kind>("name")` call in
`.aspire/apphost.cs`, looks the kind up below, and fails when the twin's file is missing from `IaC/`
or the kind has no row. The rows are kept in step with the tool's own copy of this map
by a test.

## Rules

- **Compute is Container Apps or Function Apps.** No virtual machines, scale sets or AKS; `infra
  check` refuses them.
- **Prefer registry-verified modules** (Azure Verified Modules, `Azure/avm-res-*`) for a new twin,
  over hand-written resources and over modules of our own.
- **No production.** `guards.tf` refuses `prod`; environments are dev, staging and `j<NNN>`
  previews.
- A twin the add-ons do not provide yet is a gap in these add-ons, not in the project: write it by
  hand to get the job done, and record it as a lesson so the add-on can absorb it.

## Map

| AppHost call | Local | Azure twin | IaC file |
| --- | --- | --- | --- |
| `AddCSharpApp`, `AddProject`, `AddContainer`, `AddDockerfile` | a process or container | Container App in the shared environment | `containerapp.<name>.tf` (`api` is in the base) |
| `AddCSharpApp("blazor-web")` | Blazor WebAssembly host | Static Web App, cross-origin to the API | `frontend.blazor-web.tf` |
| `AddCSharpApp("maui")` | device client | none - not hosted | - |
| `AddViteApp`, `AddJavaScriptApp` | Vite dev server | Static Web App with the API as its linked backend | `frontend.react.tf` |
| `AddAzureFunctionsProject` | Functions host | Function App on Flex Consumption | `functions.tf` |
| `AddAzureServiceBus`, `AddRabbitMQ` | emulator / broker container | Azure Service Bus | `servicebus.tf` |
| `AddKeycloak` | Keycloak container, realm import | Entra External ID (app registration, user flow, seeded QA user) | `auth.entra-external.tf` |
| `AddRedis` | Redis container | Azure Managed Redis | `cache.redis.tf` (no add-on yet) |
| `AddPostgres` | Postgres container | Azure Database for PostgreSQL flexible server | `modules/database-postgres` |
| `AddSqlServer` | SQL Server container | Azure SQL Database | `modules/database-sqlserver` |
| (dbProvider `sqlite`) | a file beside the API | the same file on an Azure Files share; one replica | `modules/database-sqlite` |

The data store row is chosen by the manifest's `dbProvider`, not by the calls: `database.tf` must
use that provider's module.

## Observability

The Aspire dashboard's twin is **Application Insights** (`observability.tf`), one per environment,
a preview's dying with it. It is not an AppHost call, so it has no row above: every environment gets
it. The services export to it themselves through servicedefaults' Azure Monitor exporter, switched on
by `APPLICATIONINSIGHTS_CONNECTION_STRING`, which every container app and Function app running
the application's code is given (`tests/platform*.tftest.hcl`). The browser's telemetry reaches it through
the API's `/api/otel` relay. Where to look - Transaction search, Failures, Performance - is in
`base-project/README.md`, "In Azure: Application Insights".
