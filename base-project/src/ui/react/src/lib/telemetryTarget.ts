/**
 * Where this page's telemetry goes, decided once at startup:
 *
 *   - under the Aspire AppHost, straight to the dashboard over OTLP/protobuf,
 *     from the `OTEL_*` variables the AppHost injects into the Vite process;
 *   - in Azure, to the API's own relay (`/api/otel`) as OTLP/JSON, which
 *     re-emits it to Application Insights. The API names the relay at run time
 *     in `GET /api/client-telemetry/config`, so a deployed build needs no
 *     build-time setting and the connection string never reaches the bundle;
 *   - otherwise nowhere.
 *
 * Kept apart from telemetry.ts, which needs a browser, so it can be tested.
 */

import type { LogRecordExporter } from '@opentelemetry/sdk-logs'
import type { SpanExporter } from '@opentelemetry/sdk-trace-web'
import { OTLPLogExporter as JsonLogExporter } from '@opentelemetry/exporter-logs-otlp-http'
import { OTLPLogExporter as ProtoLogExporter } from '@opentelemetry/exporter-logs-otlp-proto'
import { OTLPTraceExporter as JsonTraceExporter } from '@opentelemetry/exporter-trace-otlp-http'
import { OTLPTraceExporter as ProtoTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto'

/** The `OTEL_*` variables the AppHost injects; see src/vite-env.d.ts. */
export interface OtlpEnv {
  readonly OTEL_EXPORTER_OTLP_ENDPOINT?: string
  readonly OTEL_EXPORTER_OTLP_HEADERS?: string
  readonly OTEL_RESOURCE_ATTRIBUTES?: string
}

export interface TelemetryTarget {
  /** `proto` for the Aspire dashboard, `json` for the API's relay. */
  readonly format: 'proto' | 'json'
  readonly tracesUrl: string
  readonly logsUrl: string
  readonly headers: Record<string, string>
  /** What the fetch instrumentation must not trace: the exporters' own uploads. */
  readonly exportUrlPrefix: string
  /**
   * How often batches are sent. Short for the dashboard, so spans show up
   * while you are still looking at the page; the relay's default, so a page
   * stays well inside the relay's per-client rate limit.
   */
  readonly scheduledDelayMillis: number
}

/** Same-origin: the API is at /api behind Vite's proxy and the Static Web App alike. */
const apiBase = '/api/'

/**
 * `OTEL_EXPORTER_OTLP_HEADERS` and `OTEL_RESOURCE_ATTRIBUTES` both arrive in
 * the W3C Baggage shape the OTel spec defines — `key1=value1,key2=value2`,
 * percent-encoded values. The dashboard uses the former to pass
 * `x-otlp-api-key`.
 */
export function parseKeyValueList(raw: string | undefined): Record<string, string> {
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

/** Joins a base with a signal path, tolerating a trailing slash. */
function signalUrl(base: string, signal: 'traces' | 'logs'): string {
  return `${base.replace(/\/+$/, '')}/v1/${signal}`
}

/** Mirrors the API's ClientTelemetryConfig; only `relay` is read here. */
interface ClientTelemetryConfig {
  relay?: string | null
}

/**
 * The Aspire dashboard, when the AppHost supplied an endpoint. Synchronous, so
 * under `aspire run` telemetry starts before the first fetch, as it always has.
 */
export function dashboardTarget(env: OtlpEnv): TelemetryTarget | null {
  const endpoint = env.OTEL_EXPORTER_OTLP_ENDPOINT
  if (!endpoint) return null

  return {
    format: 'proto',
    tracesUrl: signalUrl(endpoint, 'traces'),
    logsUrl: signalUrl(endpoint, 'logs'),
    headers: parseKeyValueList(env.OTEL_EXPORTER_OTLP_HEADERS),
    exportUrlPrefix: endpoint,
    scheduledDelayMillis: 1000,
  }
}

export async function resolveTelemetryTarget(
  env: OtlpEnv,
  fetchConfig: (url: string) => Promise<Response> = fetch,
  // The exporters accept only absolute URLs.
  origin: string = globalThis.location?.origin ?? '',
): Promise<TelemetryTarget | null> {
  const dashboard = dashboardTarget(env)
  if (dashboard) return dashboard

  // A page must still start when its telemetry cannot: no API yet, an API
  // without Application Insights (404 outside Development), or one that
  // predates the relay all leave telemetry off.
  try {
    const response = await fetchConfig(`${apiBase}client-telemetry/config`)
    if (!response.ok) return null

    const { relay } = (await response.json()) as ClientTelemetryConfig
    if (!relay) return null

    const base = `${origin}${apiBase}${relay}`
    return {
      format: 'json',
      tracesUrl: signalUrl(base, 'traces'),
      logsUrl: signalUrl(base, 'logs'),
      headers: {},
      exportUrlPrefix: `${base}/`,
      scheduledDelayMillis: 5000,
    }
  } catch {
    return null
  }
}

export function createExporters(target: TelemetryTarget): {
  traceExporter: SpanExporter
  logExporter: LogRecordExporter
} {
  const options = (url: string) => ({ url, headers: target.headers })

  return target.format === 'json'
    ? {
        traceExporter: new JsonTraceExporter(options(target.tracesUrl)),
        logExporter: new JsonLogExporter(options(target.logsUrl)),
      }
    : {
        traceExporter: new ProtoTraceExporter(options(target.tracesUrl)),
        logExporter: new ProtoLogExporter(options(target.logsUrl)),
      }
}
