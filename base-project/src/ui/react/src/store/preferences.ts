import { Store, useStore } from '@tanstack/react-store'

export type TemperatureUnit = 'C' | 'F'

/** `system` follows the OS setting; the other two override it. */
export type Theme = 'system' | 'light' | 'dark'

interface PreferencesState {
  unit: TemperatureUnit
  theme: Theme
}

/**
 * Client-only UI state. Server data lives in TanStack Query / DB — keep this
 * store for things the API does not own.
 */
export const preferencesStore = new Store<PreferencesState>({
  unit: 'C',
  theme: 'system',
})

export function setUnit(unit: TemperatureUnit) {
  preferencesStore.setState((state) => ({ ...state, unit }))
}

export function useUnit(): TemperatureUnit {
  return useStore(preferencesStore, (state) => state.unit)
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
