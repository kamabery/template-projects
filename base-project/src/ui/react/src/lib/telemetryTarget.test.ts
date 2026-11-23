import { OTLPLogExporter as JsonLogExporter } from '@opentelemetry/exporter-logs-otlp-http'
import { OTLPLogExporter as ProtoLogExporter } from '@opentelemetry/exporter-logs-otlp-proto'
import { OTLPTraceExporter as JsonTraceExporter } from '@opentelemetry/exporter-trace-otlp-http'
import { OTLPTraceExporter as ProtoTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto'
import { describe, expect, it, vi } from 'vitest'
import { createExporters, resolveTelemetryTarget } from './telemetryTarget'

// The page's own origin: the relay is same-origin, and the exporters take only absolute URLs.
const origin = 'https://app.example.test'

const configAnswer =(body: unknown, status = 200) =>
  vi.fn(async () => new Response(JSON.stringify(body), { status }))

describe('resolveTelemetryTarget', () => {
  it('is off with no endpoint and no relay', async () => {
    const fetchConfig = configAnswer({ endpoint: null, protocol: null, headers: null, relay: null })

    expect(await resolveTelemetryTarget({}, fetchConfig)).toBeNull()
    expect(fetchConfig).toHaveBeenCalledWith('/api/client-telemetry/config')
  })

  it('is off when the config endpoint is not there', async () => {
    expect(await resolveTelemetryTarget({}, configAnswer({}, 404))).toBeNull()
  })

  it('is off when the API cannot be reached', async () => {
    const unreachable = vi.fn(async () => {
      throw new TypeError('Failed to fetch')
    })

    expect(await resolveTelemetryTarget({}, unreachable)).toBeNull()
  })

  it('posts OTLP JSON to /api/otel when the API names its relay', async () => {
    const target = await resolveTelemetryTarget(
      {},
      configAnswer({ endpoint: null, protocol: null, headers: null, relay: 'otel' }),
      origin,
    )

    expect(target).toMatchObject({
      format: 'json',
      tracesUrl: 'https://app.example.test/api/otel/v1/traces',
      logsUrl: 'https://app.example.test/api/otel/v1/logs',
      headers: {},
    })
  })

  it('exports to the Aspire dashboard when the AppHost supplied an endpoint, without asking the API', async () => {
    const fetchConfig = configAnswer({ relay: 'otel' })

    const target = await resolveTelemetryTarget(
      {
        OTEL_EXPORTER_OTLP_ENDPOINT: 'http://localhost:19114/',
        OTEL_EXPORTER_OTLP_HEADERS: 'x-otlp-api-key=s%3Dcret',
      },
      fetchConfig,
    )

    expect(target).toMatchObject({
      format: 'proto',
      tracesUrl: 'http://localhost:19114/v1/traces',
      logsUrl: 'http://localhost:19114/v1/logs',
      headers: { 'x-otlp-api-key': 's=cret' },
    })
    expect(fetchConfig).not.toHaveBeenCalled()
  })
})

describe('createExporters', () => {
  it('uses the JSON exporters for the relay', async () => {
    const target = await resolveTelemetryTarget({}, configAnswer({ relay: 'otel' }), origin)

    const { traceExporter, logExporter } = createExporters(target!)

    expect(traceExporter).toBeInstanceOf(JsonTraceExporter)
    expect(logExporter).toBeInstanceOf(JsonLogExporter)
  })

  it('keeps the protobuf exporters for the dashboard', async () => {
    const target = await resolveTelemetryTarget({ OTEL_EXPORTER_OTLP_ENDPOINT: 'http://localhost:19114' }, vi.fn())

    const { traceExporter, logExporter } = createExporters(target!)

    expect(traceExporter).toBeInstanceOf(ProtoTraceExporter)
    expect(logExporter).toBeInstanceOf(ProtoLogExporter)
  })
})
