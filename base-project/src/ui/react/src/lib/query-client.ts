import { QueryClient } from '@tanstack/react-query'

/**
 * Shared across TanStack Query hooks and TanStack DB collections, so both
 * read the same cache entry for a given query key.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})
