import { createContext, useCallback, useContext, useState } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import type { Intent } from './alert'
import { Button } from './button'

interface Toast {
  id: number
  intent: Intent
  message: ReactNode
}

interface ToastContextValue {
  show: (message: ReactNode, intent?: Intent) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

/** Throws outside the provider rather than silently dropping the message. */
export function useToast(): ToastContextValue {
  const context = useContext(ToastContext)

  if (!context) {
    throw new Error('useToast must be used inside a ToastProvider.')
  }

  return context
}

const intents: Record<Intent, string> = {
  info: 'border-intent-info',
  success: 'border-intent-success',
  warning: 'border-intent-warning',
  danger: 'border-intent-danger',
}

let nextId = 0

/**
 * Toast. Mounted once in the root layout.
 *
 * The region is `aria-live="polite"` and permanently in the DOM: a live region
 * added at the same moment as its content is usually not announced, because the
 * screen reader has nothing to observe changing. Messages auto-dismiss, so
 * nothing that matters may be delivered only this way.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Array<Toast>>([])

  const show = useCallback((message: ReactNode, intent: Intent = 'info') => {
    const id = nextId++

    setToasts((current) => [...current, { id, intent, message }])
    setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id))
    }, 5000)
  }, [])

  function dismiss(id: number) {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }

  return (
    <ToastContext.Provider value={{ show }}>
      {children}

      <div
        role="status"
        aria-live="polite"
        className="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex flex-col items-center gap-2 px-4"
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={cn(
              'pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-surface border-l-4',
              'border border-border-subtle bg-surface-raised p-3 text-sm text-content shadow-overlay',
              intents[toast.intent],
            )}
          >
            <span className="min-w-0 flex-1">{toast.message}</span>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => dismiss(toast.id)}
              aria-label="Dismiss notification"
            >
              x
            </Button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
