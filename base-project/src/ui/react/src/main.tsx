import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { QueryClientProvider } from '@tanstack/react-query'
import { initAuth } from '@/lib/auth'
import { queryClient } from '@/lib/query-client'
import { startTelemetry } from '@/lib/telemetry'
import { routeTree } from './routeTree.gen'
import './index.css'

// Before anything else, so document-load timings and the first fetches are captured.
startTelemetry()

function makeRouter() {
  return createRouter({
    routeTree,
    defaultPreload: 'intent',
    scrollRestoration: true,
    context: { queryClient },
  })
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof makeRouter>
  }
}

// The router is made after initAuth, not before: its history reads the location once, when it is
// created, and a return from the sign-in redirect carries a code and state that initAuth strips
// from the URL first. initAuth never throws.
void initAuth().then(() =>
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={makeRouter()} />
      </QueryClientProvider>
    </StrictMode>,
  ),
)
