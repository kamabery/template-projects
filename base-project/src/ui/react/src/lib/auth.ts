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
import { UserManager, WebStorageStateStore } from 'oidc-client-ts'
import type { components } from './api.generated'

type AuthConfig = components['schemas']['AuthConfig']

interface AuthState {
  enabled: boolean
  /** Display name of the signed-in user, or null. */
  user: string | null
}

export const authStore = new Store<AuthState>({ enabled: false, user: null })

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

  const params = new URLSearchParams(window.location.search)
  if (params.has('code') && params.has('state')) {
    try {
      await manager.signinRedirectCallback()
    } finally {
      // Drop code and state from the address bar before the router reads it.
      window.history.replaceState(null, '', window.location.pathname)
    }
  }

  const user = await manager.getUser()
  authStore.setState(() => ({
    enabled: true,
    user: user && !user.expired ? displayName(user.profile) : null,
  }))
}

function displayName(profile: {
  name?: string
  preferred_username?: string
  email?: string
  sub: string
}): string {
  return profile.name ?? profile.preferred_username ?? profile.email ?? profile.sub
}

export function signIn(): Promise<void> {
  return manager?.signinRedirect() ?? Promise.resolve()
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
