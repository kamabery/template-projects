import type { SelectHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { useFieldControlProps } from './field'
import { controlBorderClass, controlClass, controlInvalidClass } from './input'

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  invalid?: boolean
}

/**
 * Select. Deliberately the native `<select>`: it gets the platform's own picker
 * on touch devices, which is the behaviour §4.2 of the spec asks for and which
 * no custom listbox reproduces.
 */
export function Select({ invalid, className, children, ...props }: SelectProps) {
  const field = useFieldControlProps()
  const isInvalid = invalid ?? field['aria-invalid'] === true

  return (
    <div className="relative">
      <select
        {...field}
        className={cn(
          controlClass,
          'appearance-none pr-9',
          isInvalid ? controlInvalidClass : controlBorderClass,
          className,
        )}
        aria-invalid={isInvalid || undefined}
        {...props}
      >
        {children}
      </select>
      <svg
        aria-hidden="true"
        viewBox="0 0 16 16"
        className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-content-muted"
      >
        <path
          d="M4 6l4 4 4-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    </div>
  )
}
