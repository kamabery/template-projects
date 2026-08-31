import { createContext, useContext, useId } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

interface RadioGroupContextValue {
  name: string
  value?: string
  onValueChange?: (value: string) => void
}

const RadioGroupContext = createContext<RadioGroupContextValue | null>(null)

interface RadioGroupProps {
  label: ReactNode
  value?: string
  onValueChange?: (value: string) => void
  /** Only needed when two groups render in the same form. */
  name?: string
  children: ReactNode
  className?: string
}

/**
 * Radio group. A `<fieldset>`/`<legend>` rather than `role="radiogroup"`: the
 * native pair already groups the controls and names the group for assistive
 * technology, and it keeps working with no ARIA at all.
 */
export function RadioGroup({
  label,
  value,
  onValueChange,
  name,
  children,
  className,
}: RadioGroupProps) {
  const generatedName = useId()

  return (
    <RadioGroupContext.Provider
      value={{ name: name ?? generatedName, value, onValueChange }}
    >
      <fieldset className={cn('flex flex-col gap-2', className)}>
        <legend className="mb-1 text-sm font-medium text-content">{label}</legend>
        {children}
      </fieldset>
    </RadioGroupContext.Provider>
  )
}

interface RadioProps {
  value: string
  label: ReactNode
  disabled?: boolean
}

export function Radio({ value, label, disabled }: RadioProps) {
  const group = useContext(RadioGroupContext)

  return (
    <label
      className={cn(
        'inline-flex cursor-pointer items-center gap-2 text-sm text-content',
        'has-[:disabled]:cursor-not-allowed has-[:disabled]:text-content-disabled',
      )}
    >
      <input
        type="radio"
        name={group?.name}
        value={value}
        checked={group?.value === value}
        disabled={disabled}
        onChange={() => group?.onValueChange?.(value)}
        className={cn(
          'size-4 accent-brand',
          'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring',
          'disabled:cursor-not-allowed',
        )}
      />
      <span>{label}</span>
    </label>
  )
}
