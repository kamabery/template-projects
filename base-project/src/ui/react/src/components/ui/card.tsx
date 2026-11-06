import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/cn'

export function Card({
  className,
  ...props
}: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        'rounded-surface border border-border-subtle bg-surface-raised p-5 shadow-raised',
        className,
      )}
      {...props}
    />
  )
}

export function CardTitle({ children }: { children: ReactNode }) {
  return <h2 className="text-sm font-semibold tracking-wide uppercase text-content-muted">{children}</h2>
}
