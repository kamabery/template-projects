/**
 * Browser RUM (real user monitoring), to the Aspire dashboard under the
 * AppHost and to Application Insights, through the API's /api/otel relay, in
 * Azure. Which one, if either, is decided in telemetryTarget.ts; with neither -
 * `npm run dev` outside Aspire, or an API with no Application Insights - every
 * call here is a no-op and no OTel code paths run.
 *
 * What gets collected:
 *   - document load spans (navigation + resource timings)
 *   - fetch spans, including `traceparent` on same-origin `/api` calls, so a
 *     browser trace continues into the API's ASP.NET Core spans
 *   - click/user-interaction spans
 *   - console.error / window error / unhandled rejection as log records
 */

import { logs } from '@opentelemetry/api-logs'
import { ZoneContextManager } from '@opentelemetry/context-zone'
import { registerInstrumentations } from '@opentelemetry/instrumentation'
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load'
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch'
import { UserInteractionInstrumentation } from '@opentelemetry/instrumentation-user-interaction'
import { resourceFromAttributes } from '@opentelemetry/resources'
import { BatchLogRecordProcessor, LoggerProvider } from '@opentelemetry/sdk-logs'
import { BatchSpanProcessor, WebTracerProvider } from '@opentelemetry/sdk-trace-web'
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions'
import {
  type TelemetryTarget,
  createExporters,
  dashboardTarget,
  parseKeyValueList,
  resolveTelemetryTarget,
} from '@/lib/telemetryTarget'

/** An entity as the API's logs name it: its type, and its surrogate key. */
export interface LoggedEntity {
  type: string
  id?: string | number
}

/**
 * Logs an error about an entity, with the same keys the API puts on its own
 * logs - `entity.type`, `entity.id` (always a string) and `entity.related` as
 * `Type:id` - so one query finds the entity's history from the browser to the
 * database. The id is the surrogate key, never a natural one such as an email.
 * A no-op until telemetry has started.
 */
export function logError(
  message: string,
  options: { entity?: LoggedEntity; related?: Array<Required<LoggedEntity>> } = {},
): void {
  const attributes: Record<string, string | Array<string>> = {}
  if (options.entity) {
    attributes['entity.type'] = options.entity.type
    if (options.entity.id !== undefined) attributes['entity.id'] = String(options.entity.id)
  }
  if (options.related?.length) {
    attributes['entity.related'] = options.related.map(({ type, id }) => `${type}:${id}`)
  }

  logs.getLogger('browser').emit({
    severityNumber: 17,
    severityText: 'Error',
    body: message,
    attributes,
  })
}

function installErrorLogging(): void {
  const logger = logs.getLogger('browser')

  // SeverityNumber.ERROR — inlined to keep this module free of a value import
  // that would survive tree-shaking when telemetry is disabled.
  const emitError = (message: string, attributes?: Record<string, string>) => {
    logger.emit({
      severityNumber: 17,
      severityText: 'Error',
      body: message,
      attributes,
    })
  }

  window.addEventListener('error', (event) => {
    emitError(event.message, {
      'exception.type': event.error?.name ?? 'Error',
      'exception.stacktrace': String(event.error?.stack ?? ''),
    })
  })

  window.addEventListener('unhandledrejection', (event) => {
    emitError(`Unhandled rejection: ${String(event.reason)}`, {
      'exception.type': 'UnhandledRejection',
    })
  })

  const originalConsoleError = console.error.bind(console)
  console.error = (...args: Array<unknown>) => {
    emitError(args.map((arg) => String(arg)).join(' '))
    originalConsoleError(...args)
  }
}

function start(target: TelemetryTarget): void {
  const { traceExporter, logExporter } = createExporters(target)

  const resource = resourceFromAttributes({
    // service.instance.id and friends, so the dashboard files browser
    // telemetry under the same "ui" resource as the Vite process itself.
    // Absent in Azure, where the relay files it under the API's resource.
    ...parseKeyValueList(import.meta.env.OTEL_RESOURCE_ATTRIBUTES),
    [ATTR_SERVICE_NAME]: import.meta.env.OTEL_SERVICE_NAME ?? 'ui',
  })

  const tracerProvider = new WebTracerProvider({
    resource,
    spanProcessors: [
      new BatchSpanProcessor(traceExporter, { scheduledDelayMillis: target.scheduledDelayMillis }),
    ],
  })

  // ZoneContextManager keeps the active span attached across async boundaries,
  // so fetches started inside a click handler nest under the click span.
  tracerProvider.register({ contextManager: new ZoneContextManager() })

  const loggerProvider = new LoggerProvider({
    resource,
    processors: [
      // Unlike BatchSpanProcessor, this one takes the exporter inside its options.
      new BatchLogRecordProcessor({
        exporter: logExporter,
        scheduledDelayMillis: target.scheduledDelayMillis,
      }),
    ],
  })
  logs.setGlobalLoggerProvider(loggerProvider)

  registerInstrumentations({
    tracerProvider,
    loggerProvider,
    instrumentations: [
      new DocumentLoadInstrumentation(),
      new FetchInstrumentation({
        // The API is reached at /api - through the Vite proxy in development,
        // the Static Web App's linked backend in Azure - so requests are
        // same-origin and `traceparent` is injected without a CORS preflight.
        // Add absolute API origins here if you ever bypass the proxy.
        propagateTraceHeaderCorsUrls: [],
        // Don't trace the exporters' own uploads.
        ignoreUrls: [new RegExp(target.exportUrlPrefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))],
        ignoreNetworkEvents: true,
      }),
      new UserInteractionInstrumentation(),
    ],
  })

  installErrorLogging()

  // Flush buffered telemetry when the tab goes away.
  window.addEventListener('pagehide', () => {
    void tracerProvider.forceFlush()
    void loggerProvider.forceFlush()
  })
}

/**
 * Starts RUM. Safe to call unconditionally. Under the AppHost it starts at
 * once; otherwise it asks the API whether there is a relay first, and starts
 * only if there is.
 */
export function startTelemetry(): void {
  const dashboard = dashboardTarget(import.meta.env)
  if (dashboard) {
    start(dashboard)
    return
  }

  void resolveTelemetryTarget(import.meta.env).then((target) => {
    if (target) start(target)
  })
}
