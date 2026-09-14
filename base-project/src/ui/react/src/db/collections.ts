import { createCollection } from '@tanstack/react-db'
import { queryCollectionOptions } from '@tanstack/query-db-collection'
import { fetchWidgets, type Widget } from '@/lib/api'
import { queryClient } from '@/lib/query-client'

export const widgetsQueryKey = ['widgets'] as const

/**
 * TanStack DB collection backed by TanStack Query. Components read it with
 * `useLiveQuery`, which re-runs incrementally instead of re-filtering on
 * every render.
 */
export const widgetsCollection = createCollection(
  queryCollectionOptions<Widget>({
    id: 'widgets',
    queryClient,
    queryKey: widgetsQueryKey,
    queryFn: ({ signal }) => fetchWidgets(signal),
    // The primary key from the API. `id` is optional in the generated type
    // because the server assigns it, but every row that has been served has one.
    getKey: (widget) => widget.id!,
  }),
)
