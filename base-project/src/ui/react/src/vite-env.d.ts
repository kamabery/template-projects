/// <reference types="vite/client" />

/**
 * OTLP settings the Aspire AppHost injects into the Vite process. They reach
 * the bundle because vite.config.ts widens `envPrefix` to include `OTEL_`.
 * All are absent when the UI runs outside the AppHost, which disables RUM
 * (see src/lib/telemetry.ts).
 */
interface ImportMetaEnv {
  readonly OTEL_EXPORTER_OTLP_ENDPOINT?: string
  readonly OTEL_EXPORTER_OTLP_HEADERS?: string
  readonly OTEL_SERVICE_NAME?: string
  readonly OTEL_RESOURCE_ATTRIBUTES?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
