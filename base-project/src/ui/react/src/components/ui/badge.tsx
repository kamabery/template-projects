import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import type { Intent } from './alert'

type BadgeIntent = Intent | 'neutral'

interface BadgeProps {
  intent?: BadgeIntent
  variant?: 'subtle' | 'solid'
  children: ReactNode
  className?: string
}

const subtle: Record<BadgeIntent, string> = {
  neutral: 'bg-surface-sunken text-content-muted',
  info: 'bg-intent-info-subtle text-intent-info',
  success: 'bg-intent-success-subtle text-intent-success',
  warning: 'bg-intent-warning-subtle text-content',
  danger: 'bg-intent-danger-subtle text-intent-danger',
}

const solid: Record<BadgeIntent, string> = {
  neutral: 'bg-content-muted text-surface',
  info: 'bg-intent-info text-intent-info-contrast',
  success: 'bg-intent-success text-intent-success-contrast',
  warning: 'bg-intent-warning text-intent-warning-contrast',
  danger: 'bg-intent-danger text-intent-danger-contrast',
}

/**
 * Badge. Decoration around text that already carries the meaning - the status
 * word is inside it, never conveyed by the color alone.
 */
export function Badge({
  intent = 'neutral',
  variant = 'subtle',
  children,
  className,
}: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-pill px-2 py-0.5 text-xs font-medium',
        variant === 'solid' ? solid[intent] : subtle[intent],
        className,
      )}
    >
      {children}
    </span>
  )
}
