/**
 * Sign-in, when the API asks for it.
 *
 * The API says at `/api/auth/config` whether writes need a signed-in user and
 * where to sign in - Keycloak under the AppHost, Entra External ID in Azure.
 * The bundle is the same in every environment, so it never carries an
 * authority of its own. When the API says auth is off, nothing here does
 * anything and no token is sent.
 *
 * Authorization code with PKCE through oidc-client-ts, which speaks plain OIDC
 * to either provider. Tokens live in sessionStorage: a refresh keeps the user
 * signed in, a new tab or a closed browser does not.
 */
import { Store, useStore } from '@tanstack/react-store'
import { ErrorResponse, UserManager, WebStorageStateStore } from 'oidc-client-ts'
import type { User } from 'oidc-client-ts'
import type { components } from './api.generated'

type AuthConfig = components['schemas']['AuthConfig']
type CurrentUser = components['schemas']['CurrentUser']

interface AuthState {
  enabled: boolean
  /** Display name of the signed-in user, or null. */
  user: string | null
  /**
   * Why the last sign-in did not complete, in the provider's words, or null.
   * Without it a failed sign-in looks like the page simply reloaded.
   */
  error: string | null
}

export const authStore = new Store<AuthState>({ enabled: false, user: null, error: null })

let manager: UserManager | null = null

/**
 * Reads the API's auth config and, when the URL is the provider's redirect
 * back, completes the sign-in. Never throws: an API that cannot be reached
 * leaves auth off and the app usable for reads.
 */
export async function initAuth(): Promise<void> {
  let config: AuthConfig
  try {
    const response = await fetch('/api/auth/config', {
      headers: { Accept: 'application/json' },
    })
    if (!response.ok) return
    config = (await response.json()) as AuthConfig
  } catch {
    return
  }

  if (!config.enabled || !config.authority || !config.clientId) return

  const origin = window.location.origin
  manager = new UserManager({
    authority: config.authority,
    client_id: config.clientId,
    redirect_uri: `${origin}/`,
    post_logout_redirect_uri: `${origin}/`,
    scope: config.scope ?? 'openid profile',
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  })

  // The provider sends the browser back to `/` with either `code` and `state`
  // (signed in) or `error`, `error_description` and `state` (it refused). Both
  // are handled: an ignored error is a sign-in that "just refreshes the page".
  let error: string | null = null
  const params = new URLSearchParams(window.location.search)
  if (params.has('state') && (params.has('code') || params.has('error'))) {
    try {
      await manager.signinRedirectCallback()
    } catch (failure) {
      error = params.get('error_description') ?? params.get('error') ?? describe(failure)
      console.error('Sign-in did not complete:', failure)
    } finally {
      // Drop code, error and state from the address bar before the router reads it.
      window.history.replaceState(null, '', window.location.pathname)
    }
  }

  let signedIn: User | null = null
  try {
    const user = await manager.getUser()
    signedIn = user && !user.expired ? user : null
  } catch (failure) {
    console.error('Could not read the signed-in user:', failure)
  }
  const me = signedIn ? await fetchMe(signedIn.access_token) : null

  authStore.setState(() => ({
    enabled: true,
    user: signedIn ? (me?.name ?? displayName(signedIn.profile)) : null,
    error: signedIn ? null : error,
  }))
}

/** The provider's own description when it gave one (an `ErrorResponse`), else the message. */
function describe(failure: unknown): string {
  if (failure instanceof ErrorResponse) {
    return failure.error_description ?? failure.error ?? failure.message
  }
  return failure instanceof Error ? failure.message : String(failure)
}

/** The API's view of the caller. Null if it cannot be had; the token's own name is then shown. */
async function fetchMe(accessToken: string): Promise<CurrentUser | null> {
  try {
    const response = await fetch('/api/me', {
      headers: { Accept: 'application/json', Authorization: `Bearer ${accessToken}` },
    })
    return response.ok ? ((await response.json()) as CurrentUser) : null
  } catch {
    return null
  }
}

function displayName(profile: {
  name?: string
  preferred_username?: string
  email?: string
  sub: string
}): string {
  return profile.name ?? profile.preferred_username ?? profile.email ?? profile.sub
}

/**
 * Leaves for the provider's sign-in page. Never rejects: if the page cannot be
 * reached (its discovery document fails to load, say), the reason is shown
 * instead of the click doing nothing.
 */
export async function signIn(): Promise<void> {
  if (!manager) return
  authStore.setState((state) => ({ ...state, error: null }))
  try {
    await manager.signinRedirect()
  } catch (failure) {
    console.error('Could not start sign-in:', failure)
    authStore.setState((state) => ({
      ...state,
      error: `Could not reach the sign-in page: ${describe(failure)}`,
    }))
  }
}

export function signOut(): Promise<void> {
  return manager?.signoutRedirect() ?? Promise.resolve()
}

/**
 * `Authorization: Bearer …` for the signed-in user, or nothing. Spread into
 * every request's headers; the API ignores it on reads and requires it on
 * writes.
 */
export async function authHeaders(): Promise<Record<string, string>> {
  const user = await manager?.getUser()
  return user && !user.expired
    ? { Authorization: `Bearer ${user.access_token}` }
    : {}
}

export function useAuth(): AuthState {
  return useStore(authStore, (state) => state)
}
