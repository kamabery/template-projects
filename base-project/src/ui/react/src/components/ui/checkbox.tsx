import type { InputHTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { useFieldControlProps } from './field'

interface CheckboxProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  /** Rendered beside the box. A checkbox without one has no accessible name. */
  label: ReactNode
}

/**
 * Checkbox. The native input wrapped in its own `<label>`, so the label text is
 * part of the hit target and the accessible name at once — no `htmlFor` to keep
 * in sync, and no `FieldLabel` needed above it.
 */
export function Checkbox({ label, className, ...props }: CheckboxProps) {
  const field = useFieldControlProps()

  return (
    <label
      className={cn(
        'inline-flex cursor-pointer items-start gap-2 text-sm text-content',
        'has-[:disabled]:cursor-not-allowed has-[:disabled]:text-content-disabled',
        className,
      )}
    >
      <input
        {...field}
        type="checkbox"
        className={cn(
          'mt-0.5 size-4 shrink-0 rounded-[3px] accent-brand',
          'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring',
          'disabled:cursor-not-allowed',
        )}
        {...props}
      />
      <span>{label}</span>
    </label>
  )
}
