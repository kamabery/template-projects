/**
 * Typed client for the dotnet `template.api` service.
 *
 * Requests go to `/api/*` and are proxied to the API by Vite in dev
 * (see vite.config.ts), so the browser stays same-origin.
 */

/** Mirrors template.data/WeatherForecast.cs. */
export interface WeatherForecast {
  /** The database key. Present on the wire, but nothing here reads it: rows are keyed by `date`
   *  in the TanStack DB collection (see db/collections.ts). */
  id: number
  /** ISO date, e.g. "2026-08-10" — serialized from DateOnly. */
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

const API_BASE = '/api'

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    signal,
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(
      `GET ${path} failed: ${response.status} ${response.statusText}`,
    )
  }

  return (await response.json()) as T
}

export function fetchWeatherForecast(
  signal?: AbortSignal,
): Promise<Array<WeatherForecast>> {
  return getJson<Array<WeatherForecast>>('/WeatherForecast', signal)
}
