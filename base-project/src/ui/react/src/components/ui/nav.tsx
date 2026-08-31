import { Link } from '@tanstack/react-router'
import type { ComponentProps, ReactNode } from 'react'
import { cn } from '@/lib/cn'

const navItemClass =
  'rounded-control px-3 py-1.5 text-sm font-medium text-content-muted transition-colors duration-[var(--motion-fast)] hover:text-content focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring'

const navItemActiveClass =
  'rounded-control px-3 py-1.5 text-sm font-medium text-content bg-surface'

/**
 * Navigation. One `<nav>` landmark with a label, so a screen reader can list it
 * and skip to it - the reason this is a component rather than a bare element.
 */
export function NavBar({
  label = 'Main',
  children,
  className,
}: {
  label?: string
  children: ReactNode
  className?: string
}) {
  return (
    <nav aria-label={label} className={cn('flex items-center gap-1', className)}>
      {children}
    </nav>
  )
}

type NavItemProps = ComponentProps<typeof Link> & {
  /** Match the path exactly. Needed for "/" or every route marks it active. */
  exact?: boolean
}

/**
 * The router supplies `aria-current="page"` on the active link, so the active
 * state is not carried by color alone.
 */
export function NavItem({ exact, className, ...props }: NavItemProps) {
  return (
    <Link
      className={cn(navItemClass, className)}
      activeProps={{ className: navItemActiveClass }}
      activeOptions={exact ? { exact: true } : undefined}
      {...props}
    />
  )
}
