import { Link, Outlet, createRootRouteWithContext } from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import type { QueryClient } from '@tanstack/react-query'
import type { Theme } from '@/store/preferences'
import { setTheme, useTheme } from '@/store/preferences'

const navLinkClass =
  'rounded-lg px-3 py-1.5 text-sm font-medium text-content-muted transition-colors hover:text-content'

const navLinkActiveClass =
  'rounded-lg px-3 py-1.5 text-sm font-medium text-content bg-surface'

const themes: Array<Theme> = ['system', 'light', 'dark']

const themeLabels: Record<Theme, string> = {
  system: 'Auto',
  light: 'Light',
  dark: 'Dark',
}

/** Available to every route via `Route.useRouteContext()` and loaders. */
export interface RouterContext {
  queryClient: QueryClient
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
  notFoundComponent: () => (
    <div className="p-10 text-center text-content-muted">
      Page not found.{' '}
      <Link to="/" className="text-brand underline">
        Go home
      </Link>
    </div>
  ),
})

function ThemeToggle() {
  const theme = useTheme()

  return (
    <div
      className="flex items-center gap-1 rounded-lg border border-border-subtle p-0.5"
      role="group"
      aria-label="Theme"
    >
      {themes.map((option) => (
        <button
          key={option}
          type="button"
          onClick={() => setTheme(option)}
          aria-pressed={theme === option}
          className={
            theme === option
              ? 'rounded-md bg-brand px-2 py-1 text-xs font-medium text-brand-contrast'
              : 'rounded-md px-2 py-1 text-xs font-medium text-content-muted hover:text-content'
          }
        >
          {themeLabels[option]}
        </button>
      ))}
    </div>
  )
}

function RootLayout() {
  return (
    <div className="min-h-dvh">
      <header className="border-b border-border-subtle bg-surface-raised">
        <nav className="mx-auto flex max-w-4xl items-center gap-1 px-6 py-3">
          <span className="mr-4 font-semibold">template.ui</span>
          <Link
            to="/"
            className={navLinkClass}
            activeProps={{ className: navLinkActiveClass }}
            activeOptions={{ exact: true }}
          >
            Home
          </Link>
          <Link
            to="/counter"
            className={navLinkClass}
            activeProps={{ className: navLinkActiveClass }}
          >
            Counter
          </Link>
          <Link
            to="/weather"
            className={navLinkClass}
            activeProps={{ className: navLinkActiveClass }}
          >
            Weather
          </Link>
          <div className="ml-auto">
            <ThemeToggle />
          </div>
        </nav>
      </header>

      <main className="mx-auto max-w-4xl px-6 py-10">
        <Outlet />
      </main>

      {import.meta.env.DEV && (
        <>
          <TanStackRouterDevtools position="bottom-left" />
          <ReactQueryDevtools initialIsOpen={false} />
        </>
      )}
    </div>
  )
}
