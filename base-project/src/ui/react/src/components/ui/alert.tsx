import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { Button } from './button'

export type Intent = 'info' | 'success' | 'warning' | 'danger'

interface AlertProps {
  intent?: Intent
  title?: ReactNode
  children?: ReactNode
  /** Renders a dismiss control. Omit for messages the user must not lose. */
  onDismiss?: () => void
  className?: string
}

const intents: Record<Intent, string> = {
  info: 'border-intent-info bg-intent-info-subtle',
  success: 'border-intent-success bg-intent-success-subtle',
  warning: 'border-intent-warning bg-intent-warning-subtle',
  danger: 'border-intent-danger bg-intent-danger-subtle',
}

const glyphs: Record<Intent, string> = {
  info: 'i',
  success: 'OK',
  warning: '!',
  danger: '!',
}

const glyphColors: Record<Intent, string> = {
  info: 'bg-intent-info text-intent-info-contrast',
  success: 'bg-intent-success text-intent-success-contrast',
  warning: 'bg-intent-warning text-intent-warning-contrast',
  danger: 'bg-intent-danger text-intent-danger-contrast',
}

/**
 * Alert. `role="alert"` interrupts the screen reader, which is right for a
 * failure the user has to act on and wrong for a confirmation - so warning and
 * danger assert, info and success only announce.
 */
export function Alert({
  intent = 'info',
  title,
  children,
  onDismiss,
  className,
}: AlertProps) {
  const assertive = intent === 'danger' || intent === 'warning'

  return (
    <div
      role={assertive ? 'alert' : 'status'}
      className={cn(
        'flex items-start gap-3 rounded-surface border-l-4 p-3.5 text-sm text-content',
        intents[intent],
        className,
      )}
    >
      <span
        aria-hidden="true"
        className={cn(
          'mt-0.5 inline-flex size-5 shrink-0 items-center justify-center rounded-pill text-[10px] font-bold',
          glyphColors[intent],
        )}
      >
        {glyphs[intent]}
      </span>

      <div className="min-w-0 flex-1">
        {title && <p className="font-semibold">{title}</p>}
        {children && (
          <div className={cn('text-content-muted', title ? 'mt-1' : undefined)}>{children}</div>
        )}
      </div>

      {onDismiss && (
        <Button variant="ghost" size="sm" onClick={onDismiss} aria-label="Dismiss">
          x
        </Button>
      )}
    </div>
  )
}
