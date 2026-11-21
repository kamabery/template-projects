import type { ReactNode } from 'react'
import { Link, createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { Alert } from '@/components/ui/alert'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/cn'
import { fetchWidgets } from '@/lib/api'
import { widgetsQueryKey } from '@/db/collections'

export const Route = createFileRoute('/')({
  component: HomePage,
})

/**
 * The dashboard home (docs/DESIGN.md, 4.1): a complete view of the entities,
 * not a description of the application. One tile and one panel per entity; the
 * scaffold owns one, so it has one of each. A new entity adds its own here. The
 * Blazor home (Pages/Home.razor) is the same page.
 */
function HomePage() {
  // Plain TanStack Query - the simplest way to talk to the dotnet API. It shares
  // the widgets collection's key, so the list route reuses this cache.
  const widgets = useQuery({
    queryKey: widgetsQueryKey,
    queryFn: ({ signal }) => fetchWidgets(signal),
  })

  const rows = widgets.data ?? []
  // At most five rows: a panel that wants more is a list route (DESIGN.md 4.1).
  const recent = [...rows]
    .sort((a, b) => b.updatedOn.localeCompare(a.updatedOn))
    .slice(0, 5)

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Home</h1>

      <div className="flex flex-wrap gap-3">
        <Link
          to="/widgets"
          data-testid="count-tile"
          aria-label={`${rows.length} widgets`}
          className={cn(
            'block min-w-40 flex-1 rounded-surface border border-border-subtle bg-surface-raised p-4',
            'shadow-raised transition-colors duration-[var(--motion-fast)] hover:border-border-strong',
            'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring',
          )}
        >
          <span className="text-xs font-semibold tracking-wide text-content-muted uppercase">
            Widgets
          </span>
          <span className="mt-1 block text-3xl font-semibold tabular-nums">
            {widgets.isPending ? '…' : rows.length}
          </span>
          <span className="text-sm text-content-muted">in total</span>
        </Link>
      </div>

      <Panel title="Recently updated widgets" busy={widgets.isPending}>
        {widgets.isPending && (
          <div className="space-y-2">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-4/5" />
            <Skeleton className="h-4 w-2/3" />
          </div>
        )}

        {widgets.isError && (
          <Alert intent="danger" title="Could not load widgets">
            {widgets.error.message}
          </Alert>
        )}

        {widgets.isSuccess && rows.length === 0 && (
          <p data-testid="first-run" className="text-sm text-content-muted">
            No widgets yet. They appear here and on{' '}
            <Link to="/widgets" className="text-brand underline">
              the list
            </Link>{' '}
            as soon as the API has one.
          </p>
        )}

        {widgets.isSuccess && rows.length > 0 && (
          <ul className="space-y-2 text-sm">
            {recent.map((widget) => (
              <li
                key={widget.id}
                data-testid="recent-row"
                className="flex items-center justify-between gap-3"
              >
                <Link to="/widgets" className="text-brand underline">
                  {widget.name}
                </Link>
                <span className="tabular-nums text-content-muted">
                  {widget.quantity}
                </span>
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  )
}

/** Its own labelled region with its own aria-busy, so one slow query blocks nothing else. */
function Panel({
  title,
  busy,
  children,
}: {
  title: string
  busy?: boolean
  children: ReactNode
}) {
  return (
    <section
      aria-label={title}
      aria-busy={busy}
      data-testid="recent-panel"
      className="w-full rounded-surface border border-border-subtle bg-surface-raised"
    >
      <header className="flex items-center justify-between gap-4 border-b border-border-subtle px-4 py-3">
        <h2 className="text-sm font-semibold">{title}</h2>
        <Link
          to="/widgets"
          className="rounded-control text-sm text-brand underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        >
          View all
        </Link>
      </header>
      <div className="p-4">{children}</div>
    </section>
  )
}
