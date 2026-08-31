import { createContext, useContext, useEffect, useId, useState } from 'react'
import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/cn'

interface FieldContextValue {
  controlId: string
  descriptionId: string
  errorId: string
  invalid: boolean
  hasDescription: boolean
  registerDescription: (present: boolean) => void
}

const FieldContext = createContext<FieldContextValue | null>(null)

/** Null outside a `Field`, so every control below stays usable on its own. */
export function useFieldContext(): FieldContextValue | null {
  return useContext(FieldContext)
}

/**
 * Props a control spreads to join its Field: the generated id, the invalid
 * state, and the description/error it is described by.
 *
 * `aria-describedby` names only ids that are actually rendered. That is why
 * `FieldDescription` registers itself: a control may render before the
 * description that follows it, so its presence cannot be read off the children
 * during render. `FieldError` needs no such signal - it renders exactly when the
 * field is invalid, which the Field already knows.
 *
 * The Blazor side does the same in Components/AppField.razor, where it is a
 * plain conditional because one component owns the whole field.
 */
export function useFieldControlProps(): {
  id?: string
  'aria-invalid'?: true
  'aria-describedby'?: string
} {
  const field = useFieldContext()

  if (!field) {
    return {}
  }

  const describedBy = [
    field.hasDescription ? field.descriptionId : null,
    field.invalid ? field.errorId : null,
  ].filter(Boolean)

  return {
    id: field.controlId,
    'aria-invalid': field.invalid ? true : undefined,
    'aria-describedby': describedBy.length ? describedBy.join(' ') : undefined,
  }
}

interface FieldProps extends HTMLAttributes<HTMLDivElement> {
  invalid?: boolean
}

/**
 * Form Validation Message contract. `Field` owns the ids; `FieldLabel`,
 * `FieldDescription`, `FieldError` and the control claim them. Nothing below
 * has to hand-wire `htmlFor` or `aria-describedby` again.
 */
export function Field({ invalid = false, className, ...props }: FieldProps) {
  const id = useId()
  const [hasDescription, setHasDescription] = useState(false)

  const value: FieldContextValue = {
    controlId: `${id}-control`,
    descriptionId: `${id}-description`,
    errorId: `${id}-error`,
    invalid,
    hasDescription,
    registerDescription: setHasDescription,
  }

  return (
    <FieldContext.Provider value={value}>
      <div className={cn('flex flex-col gap-1.5', className)} {...props} />
    </FieldContext.Provider>
  )
}

export function FieldLabel({
  children,
  required,
}: {
  children: ReactNode
  required?: boolean
}) {
  const field = useFieldContext()

  return (
    <label
      htmlFor={field?.controlId}
      className="text-sm font-medium text-content"
    >
      {children}
      {required && (
        <span className="ml-0.5 text-intent-danger" aria-hidden="true">
          *
        </span>
      )}
    </label>
  )
}

export function FieldDescription({ children }: { children: ReactNode }) {
  const field = useFieldContext()
  const register = field?.registerDescription

  useEffect(() => {
    register?.(true)

    return () => register?.(false)
  }, [register])

  return (
    <p id={field?.descriptionId} className="text-xs text-content-muted">
      {children}
    </p>
  )
}

/**
 * Renders nothing when there is no message, so a form can pass its validation
 * result straight through. `role="alert"` announces the message when it appears.
 */
export function FieldError({ children }: { children?: ReactNode }) {
  const field = useFieldContext()

  if (!children) {
    return null
  }

  return (
    <p
      id={field?.errorId}
      role="alert"
      className="text-xs font-medium text-intent-danger"
    >
      {children}
    </p>
  )
}
