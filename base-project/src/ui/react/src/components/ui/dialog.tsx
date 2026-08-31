import { useEffect, useId, useRef } from 'react'
import type { MouseEvent, ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { Button } from './button'

interface DialogProps {
  open: boolean
  /** Called for every dismissal: the close button, Esc, and the backdrop. */
  onClose: () => void
  title: ReactNode
  description?: ReactNode
  children?: ReactNode
  footer?: ReactNode
  className?: string
}

/**
 * Dialog, built on the browser's own `<dialog>` element.
 *
 * `showModal()` supplies the focus trap, Esc-to-close, the inert background and
 * the top layer - four things a div-based dialog has to reimplement and usually
 * gets wrong. That is why this needs no dependency and no portal.
 */
export function Dialog({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  className,
}: DialogProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const id = useId()

  useEffect(() => {
    const element = dialog.current

    if (!element) {
      return
    }

    if (open && !element.open) {
      element.showModal()
    } else if (!open && element.open) {
      element.close()
    }
  }, [open])

  // The dialog element fills the viewport up to its own box, so a click that
  // lands on the element itself rather than on the panel is a backdrop click.
  function onBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === dialog.current) {
      onClose()
    }
  }

  return (
    <dialog
      ref={dialog}
      // Fires for Esc and for close(), so this is the one place dismissal lands.
      onClose={onClose}
      onClick={onBackdropClick}
      aria-labelledby={`${id}-title`}
      aria-describedby={description ? `${id}-description` : undefined}
      className={cn(
        'm-auto w-[min(32rem,calc(100vw-2rem))] rounded-surface border border-border-subtle',
        'bg-surface-raised p-0 text-content shadow-overlay',
        'backdrop:bg-black/40',
        className,
      )}
    >
      <div className="flex flex-col gap-3 p-5">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <h2 id={`${id}-title`} className="text-base font-semibold">
              {title}
            </h2>
            {description && (
              <p id={`${id}-description`} className="mt-1 text-sm text-content-muted">
                {description}
              </p>
            )}
          </div>
          <Button variant="ghost" size="sm" onClick={onClose} aria-label="Close">
            x
          </Button>
        </div>

        {children && <div className="text-sm">{children}</div>}

        {footer && <div className="flex justify-end gap-2 pt-2">{footer}</div>}
      </div>
    </dialog>
  )
}

interface ConfirmDialogProps {
  open: boolean
  onClose: () => void
  onConfirm: () => void
  title: ReactNode
  description?: ReactNode
  confirmLabel?: string
  cancelLabel?: string
  /** Styles the confirm control as destructive and focuses Cancel instead. */
  destructive?: boolean
}

/**
 * Confirmation dialog. When the action is destructive the safe control takes
 * initial focus, so Enter on a dialog the user did not read cancels rather than
 * destroys.
 */
export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  destructive = false,
}: ConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={title}
      description={description}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} autoFocus={destructive}>
            {cancelLabel}
          </Button>
          <Button
            variant={destructive ? 'danger' : 'primary'}
            onClick={onConfirm}
            autoFocus={!destructive}
          >
            {confirmLabel}
          </Button>
        </>
      }
    />
  )
}
