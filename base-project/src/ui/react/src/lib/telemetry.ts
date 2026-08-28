/**
 * Browser RUM (real user monitoring) wired to the Aspire dashboard.
 *
 * The AppHost injects `OTEL_*` variables into the Vite process
 * (`WithOtlpExporter(OtlpProtocol.HttpProtobuf)` in .aspire/apphost.cs)
 * and vite.config.ts widens `envPrefix` so they reach the bundle. Without an
 * endpoint — `npm run dev` outside Aspire, or a production build — every call
 * here is a no-op and no OTel code paths run.
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
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-proto'
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto'
import { registerInstrumentations } from '@opentelemetry/instrumentation'
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load'
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch'
import { UserInteractionInstrumentation } from '@opentelemetry/instrumentation-user-interaction'
import { resourceFromAttributes } from '@opentelemetry/resources'
import { BatchLogRecordProcessor, LoggerProvider } from '@opentelemetry/sdk-logs'
import { BatchSpanProcessor, WebTracerProvider } from '@opentelemetry/sdk-trace-web'
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions'

/**
 * `OTEL_EXPORTER_OTLP_HEADERS` and `OTEL_RESOURCE_ATTRIBUTES` both arrive in
 * the W3C Baggage shape the OTel spec defines — `key1=value1,key2=value2`,
 * percent-encoded values. The dashboard uses the former to pass
 * `x-otlp-api-key`.
 */
function parseKeyValueList(raw: string | undefined): Record<string, string> {
  if (!raw) return {}

  return Object.fromEntries(
    raw
      .split(',')
      .map((pair) => {
        const separator = pair.indexOf('=')
        if (separator < 0) return null
        return [
          pair.slice(0, separator).trim(),
          decodeURIComponent(pair.slice(separator + 1).trim()),
        ] as const
      })
      .filter((entry) => entry !== null),
  )
}

/** Joins the base OTLP endpoint with a signal path, tolerating a trailing slash. */
function signalUrl(endpoint: string, signal: 'traces' | 'logs'): string {
  return `${endpoint.replace(/\/+$/, '')}/v1/${signal}`
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

/**
 * Starts RUM. Safe to call unconditionally — returns immediately when the
 * AppHost has not supplied an OTLP endpoint.
 */
export function startTelemetry(): void {
  const endpoint = import.meta.env.OTEL_EXPORTER_OTLP_ENDPOINT
  if (!endpoint) return

  const headers = parseKeyValueList(import.meta.env.OTEL_EXPORTER_OTLP_HEADERS)

  const resource = resourceFromAttributes({
    // service.instance.id and friends, so the dashboard files browser
    // telemetry under the same "ui" resource as the Vite process itself.
    ...parseKeyValueList(import.meta.env.OTEL_RESOURCE_ATTRIBUTES),
    [ATTR_SERVICE_NAME]: import.meta.env.OTEL_SERVICE_NAME ?? 'ui',
  })

  const tracerProvider = new WebTracerProvider({
    resource,
    spanProcessors: [
      new BatchSpanProcessor(
        new OTLPTraceExporter({ url: signalUrl(endpoint, 'traces'), headers }),
        // Short delay so spans show up in the dashboard while you are still
        // looking at the page that produced them.
        { scheduledDelayMillis: 1000 },
      ),
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
        exporter: new OTLPLogExporter({ url: signalUrl(endpoint, 'logs'), headers }),
        scheduledDelayMillis: 1000,
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
        // The API is reached through the Vite proxy at /api, so requests are
        // same-origin and `traceparent` is injected without a CORS preflight.
        // Add absolute API origins here if you ever bypass the proxy.
        propagateTraceHeaderCorsUrls: [],
        // Don't trace the exporter's own uploads.
        ignoreUrls: [new RegExp(`^${endpoint.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`)],
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
