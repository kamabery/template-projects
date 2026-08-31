import { cn } from '@/lib/cn'

/**
 * Skeleton. Purely decorative and so `aria-hidden`: the loading state is
 * announced by the Spinner or the `aria-busy` region around it, not by this.
 */
export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'animate-pulse rounded-control bg-surface-sunken motion-reduce:animate-none',
        className,
      )}
    />
  )
}
