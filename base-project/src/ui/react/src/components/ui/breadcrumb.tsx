import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

/** Breadcrumb. The landmark label is what tells a screen reader which nav this is. */
export function Breadcrumb({
  children,
  className,
}: {
  children: ReactNode
  className?: string
}) {
  return (
    <nav aria-label="Breadcrumb" className={className}>
      <ol className="flex flex-wrap items-center gap-1.5 text-sm">{children}</ol>
    </nav>
  )
}

interface BreadcrumbItemProps {
  /** The last crumb. Rendered as text with `aria-current`, never as a link. */
  current?: boolean
  href?: string
  children: ReactNode
}

export function BreadcrumbItem({ current, href, children }: BreadcrumbItemProps) {
  return (
    <li className="flex items-center gap-1.5">
      {current || !href ? (
        <span
          aria-current={current ? 'page' : undefined}
          className={cn(current ? 'font-medium text-content' : 'text-content-muted')}
        >
          {children}
        </span>
      ) : (
        <a
          href={href}
          className="text-content-muted underline-offset-2 hover:text-content hover:underline"
        >
          {children}
        </a>
      )}
      {!current && (
        <span aria-hidden="true" className="text-content-muted">
          /
        </span>
      )}
    </li>
  )
}
