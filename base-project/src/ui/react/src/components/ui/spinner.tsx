import { cn } from '@/lib/cn'

type Size = 'sm' | 'md' | 'lg'

const sizes: Record<Size, string> = {
  sm: 'size-4 border-2',
  md: 'size-5 border-2',
  lg: 'size-8 border-[3px]',
}

interface SpinnerProps {
  size?: Size
  /** Announced to assistive technology. Rendered visibly when `showLabel` is set. */
  label?: string
  showLabel?: boolean
  className?: string
}

/**
 * Loading Indicator. The ring is `aria-hidden`; the label is what carries the
 * meaning, so it is never optional — only its visibility is.
 */
export function Spinner({
  size = 'md',
  label = 'Loading…',
  showLabel = false,
  className,
}: SpinnerProps) {
  return (
    <span
      role="status"
      className={cn('inline-flex items-center gap-2 text-content-muted', className)}
    >
      <span
        aria-hidden="true"
        className={cn(
          'inline-block animate-spin rounded-pill border-current border-t-transparent',
          'motion-reduce:animate-none',
          sizes[size],
        )}
      />
      <span className={showLabel ? 'text-sm' : 'sr-only'}>{label}</span>
    </span>
  )
}
