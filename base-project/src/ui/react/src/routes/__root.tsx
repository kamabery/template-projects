import { Link, Outlet, createRootRouteWithContext } from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import type { QueryClient } from '@tanstack/react-query'
import type { Theme } from '@/store/preferences'
import { setTheme, useTheme } from '@/store/preferences'
import { signIn, signOut, useAuth } from '@/lib/auth'
import { Button } from '@/components/ui/button'
import { NavBar, NavItem } from '@/components/ui/nav'
import { ToastProvider } from '@/components/ui/toast'

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
      className="flex items-center gap-1 rounded-control border border-border-subtle p-0.5"
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
              ? 'rounded-control bg-brand px-2 py-1 text-xs font-medium text-brand-contrast'
              : 'rounded-control px-2 py-1 text-xs font-medium text-content-muted hover:text-content'
          }
        >
          {themeLabels[option]}
        </button>
      ))}
    </div>
  )
}

/** Nothing at all unless the API says writes need a signed-in user. */
function AuthControl() {
  const { enabled, user, error } = useAuth()
  if (!enabled) return null

  return user ? (
    <div className="flex items-center gap-2">
      <span className="text-xs text-content-muted">{user}</span>
      <Button variant="ghost" size="sm" onClick={() => void signOut()}>
        Sign out
      </Button>
    </div>
  ) : (
    <div className="flex items-center gap-2">
      {error && (
        <span role="alert" className="text-xs text-intent-danger">
          Sign-in did not complete: {error}
        </span>
      )}
      <Button variant="secondary" size="sm" onClick={() => void signIn()}>
        Sign in
      </Button>
    </div>
  )
}

function RootLayout() {
  return (
    <ToastProvider>
      <div className="min-h-dvh">
        <header className="border-b border-border-subtle bg-surface-raised">
          <div className="mx-auto flex max-w-4xl items-center gap-1 px-6 py-3">
            <span className="mr-4 font-semibold">AspireStarterAppName</span>

            <NavBar>
              <NavItem to="/" exact>
                Home
              </NavItem>
              <NavItem to="/widgets">Widgets</NavItem>
              <NavItem to="/styleguide">Style guide</NavItem>
            </NavBar>

            <div className="ml-auto flex items-center gap-3">
              <AuthControl />
              <ThemeToggle />
            </div>
          </div>
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
    </ToastProvider>
  )
}
