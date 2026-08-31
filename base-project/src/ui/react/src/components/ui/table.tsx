import type { HTMLAttributes, TdHTMLAttributes, ThHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'

interface TableProps extends HTMLAttributes<HTMLTableElement> {
  /** Names the table for assistive technology. Give one unless a heading does it. */
  caption?: string
}

/**
 * Table. The wrapper is what scrolls, not the page - a wide table must never
 * make the document scroll sideways.
 */
export function Table({ caption, className, children, ...props }: TableProps) {
  return (
    <div className="overflow-x-auto">
      <table className={cn('w-full border-collapse text-sm', className)} {...props}>
        {caption && <caption className="sr-only">{caption}</caption>}
        {children}
      </table>
    </div>
  )
}

export function Thead(props: HTMLAttributes<HTMLTableSectionElement>) {
  return <thead {...props} />
}

export function Tbody(props: HTMLAttributes<HTMLTableSectionElement>) {
  return <tbody {...props} />
}

export function Tr({ className, ...props }: HTMLAttributes<HTMLTableRowElement>) {
  return (
    <tr
      className={cn('border-b border-border-subtle last:border-0', className)}
      {...props}
    />
  )
}

interface CellProps {
  /** Right-aligns and applies tabular figures, so columns of numbers line up. */
  numeric?: boolean
}

type ThProps = ThHTMLAttributes<HTMLTableCellElement> & CellProps
type TdProps = TdHTMLAttributes<HTMLTableCellElement> & CellProps

export function Th({ numeric, className, scope = 'col', ...props }: ThProps) {
  return (
    <th
      scope={scope}
      className={cn(
        'px-3 py-2 text-start font-semibold text-content-muted',
        numeric && 'text-end tabular-nums',
        className,
      )}
      {...props}
    />
  )
}

export function Td({ numeric, className, ...props }: TdProps) {
  return (
    <td
      className={cn(
        'px-3 py-2 text-start',
        numeric && 'text-end tabular-nums',
        className,
      )}
      {...props}
    />
  )
}
