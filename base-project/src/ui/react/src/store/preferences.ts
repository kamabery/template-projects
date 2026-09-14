import { Store, useStore } from '@tanstack/react-store'

/**
 * How much room a list gives each row. Client-only: the server has no opinion,
 * and no page is required to honour it - /widgets does.
 */
export type Density = 'comfortable' | 'compact'

/** `system` follows the OS setting; the other two override it. */
export type Theme = 'system' | 'light' | 'dark'

interface PreferencesState {
  density: Density
  theme: Theme
}

/**
 * Client-only UI state. Server data lives in TanStack Query / DB — keep this
 * store for things the API does not own.
 */
export const preferencesStore = new Store<PreferencesState>({
  density: 'comfortable',
  theme: 'system',
})

export function setDensity(density: Density) {
  preferencesStore.setState((state) => ({ ...state, density }))
}

export function useDensity(): Density {
  return useStore(preferencesStore, (state) => state.density)
}

export function setTheme(theme: Theme) {
  preferencesStore.setState((state) => ({ ...state, theme }))
}

export function useTheme(): Theme {
  return useStore(preferencesStore, (state) => state.theme)
}

/**
 * Mirrors the theme onto `<html data-theme>`, which is what index.css switches
 * `color-scheme` on. Subscribed here rather than from a component effect so the
 * attribute tracks the store even when nothing is rendering it — the same shape
 * as the Blazor side's ThemeStore. Like that store, the choice is not persisted.
 */
function applyTheme(theme: Theme) {
  const root = document.documentElement

  if (theme === 'system') {
    root.removeAttribute('data-theme')
  } else {
    root.setAttribute('data-theme', theme)
  }
}

applyTheme(preferencesStore.state.theme)
preferencesStore.subscribe(() => applyTheme(preferencesStore.state.theme))
