import type { InputHTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { useFieldControlProps } from './field'

interface SwitchProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'role'> {
  label: ReactNode
}

/**
 * Switch. A checkbox with `role="switch"`, which is the APG pattern: the native
 * input keeps space-to-toggle, form participation and the disabled state, and
 * the role is what makes a screen reader say "on"/"off" rather than
 * "checked"/"unchecked". The visible track is drawn from the peer state, so
 * there is no JavaScript in the rendering path.
 */
export function Switch({ label, className, ...props }: SwitchProps) {
  const field = useFieldControlProps()

  return (
    <label
      className={cn(
        'inline-flex cursor-pointer items-center gap-2.5 text-sm text-content',
        'has-[:disabled]:cursor-not-allowed has-[:disabled]:text-content-disabled',
        className,
      )}
    >
      <input
        {...field}
        type="checkbox"
        role="switch"
        className="peer sr-only"
        {...props}
      />
      <span
        aria-hidden="true"
        className={cn(
          'relative inline-block h-5 w-9 shrink-0 rounded-pill border border-border-strong bg-surface-sunken',
          'transition-colors duration-[var(--motion-fast)]',
          'peer-checked:border-brand peer-checked:bg-brand',
          'peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-focus-ring',
          'peer-disabled:opacity-50',
          'after:absolute after:top-0.5 after:left-0.5 after:size-3.5 after:rounded-pill',
          'after:bg-surface-raised after:shadow-raised',
          'after:transition-transform after:duration-[var(--motion-fast)]',
          'peer-checked:after:translate-x-4 peer-checked:after:bg-brand-contrast',
          'motion-reduce:transition-none motion-reduce:after:transition-none',
        )}
      />
      <span>{label}</span>
    </label>
  )
}
