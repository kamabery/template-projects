import type { InputHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { useFieldControlProps } from './field'

/**
 * The shared look of every text-shaped control. `select.tsx` and `textarea.tsx`
 * import it so the three never drift apart.
 */
export const controlClass = cn(
  'w-full rounded-control border bg-surface-raised px-3 py-2 text-sm text-content',
  'placeholder:text-content-muted',
  'transition-colors duration-[var(--motion-fast)]',
  'focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-focus-ring',
  'disabled:cursor-not-allowed disabled:bg-surface-sunken disabled:text-content-disabled',
)

export const controlBorderClass = 'border-border-subtle hover:border-border-strong'
export const controlInvalidClass = 'border-intent-danger hover:border-intent-danger'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean
}

/** Text Input. Inside a `Field` it adopts that field's id and validation state. */
export function Input({ invalid, className, ...props }: InputProps) {
  const field = useFieldControlProps()
  const isInvalid = invalid ?? field['aria-invalid'] === true

  return (
    <input
      {...field}
      className={cn(
        controlClass,
        isInvalid ? controlInvalidClass : controlBorderClass,
        className,
      )}
      aria-invalid={isInvalid || undefined}
      {...props}
    />
  )
}
