import type { TextareaHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { useFieldControlProps } from './field'
import { controlBorderClass, controlClass, controlInvalidClass } from './input'

interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalid?: boolean
}

export function Textarea({ invalid, className, rows = 3, ...props }: TextareaProps) {
  const field = useFieldControlProps()
  const isInvalid = invalid ?? field['aria-invalid'] === true

  return (
    <textarea
      {...field}
      rows={rows}
      className={cn(
        controlClass,
        'resize-y',
        isInvalid ? controlInvalidClass : controlBorderClass,
        className,
      )}
      aria-invalid={isInvalid || undefined}
      {...props}
    />
  )
}
