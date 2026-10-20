/**
 * Typed client for the dotnet `template.api` service.
 *
 * Requests go to `/api/*` and are proxied to the API by Vite in dev
 * (see vite.config.ts), so the browser stays same-origin.
 *
 * The types are not written here — they are generated from
 * `contracts/api.openapi.json`, which the API's own test suite produces. Run
 * `npm run api:types` after changing an endpoint. See docs/ADDING-A-RESOURCE.md.
 */
import type { components } from './api.generated'
import { authHeaders } from './auth'

/** Generated from the API's OpenAPI document — do not hand-edit the shape. */
export type Widget = components['schemas']['Widget']

const API_BASE = '/api'

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    signal,
    headers: { Accept: 'application/json', ...(await authHeaders()) },
  })

  if (!response.ok) {
    throw new Error(
      `GET ${path} failed: ${response.status} ${response.statusText}`,
    )
  }

  return (await response.json()) as T
}

export function fetchWidgets(signal?: AbortSignal): Promise<Array<Widget>> {
  return getJson<Array<Widget>>('/Widgets', signal)
}
