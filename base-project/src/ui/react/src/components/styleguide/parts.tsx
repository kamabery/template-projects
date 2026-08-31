import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'
import { Button } from '@/components/ui/button'
import { useTheme } from '@/store/preferences'
import { useToast } from '@/components/ui/toast'

/**
 * The style guide's own chrome. Kept out of the route file so the route reads as
 * a list of entries rather than a wall of layout markup, and kept out of
 * components/ui because none of it is approved for product screens.
 *
 * The Blazor guide has the same parts in Components/StyleGuideSection.razor, and
 * the two guides list the same sections in the same order on purpose - that
 * side-by-side comparison is the point of this repository carrying two front ends.
 */

export function Group({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-6">
      <h2 className="border-b border-border-subtle pb-2 text-xs font-semibold tracking-widest text-content-muted uppercase">
        {title}
      </h2>
      {children}
    </section>
  )
}

interface SectionProps {
  title: string
  /** What the component is for, and when to reach for something else. */
  intent: ReactNode
  /** The keyboard, focus, naming or announcement behaviour that is contractual. */
  a11y: ReactNode
  code?: string
  children: ReactNode
}

export function Section({ title, intent, a11y, code, children }: SectionProps) {
  return (
    <article className="rounded-surface border border-border-subtle bg-surface-raised">
      <header className="border-b border-border-subtle px-5 py-4">
        <h3 className="text-base font-semibold">{title}</h3>
        <p className="mt-1 text-sm text-content-muted">{intent}</p>
      </header>

      <div className="space-y-5 p-5">{children}</div>

      <footer className="space-y-3 border-t border-border-subtle px-5 py-4">
        <p className="text-sm">
          <span className="font-medium">Accessibility.</span>{' '}
          <span className="text-content-muted">{a11y}</span>
        </p>
        {code && <Snippet code={code} />}
      </footer>
    </article>
  )
}

/** One row of the matrix: a variant or a state, labelled with which it is. */
export function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid gap-2 sm:grid-cols-[10rem_1fr] sm:items-start sm:gap-4">
      <span className="pt-1 text-xs font-medium tracking-wide text-content-muted uppercase">
        {label}
      </span>
      <div className="flex flex-wrap items-center gap-3">{children}</div>
    </div>
  )
}

export function Snippet({ code }: { code: string }) {
  const toast = useToast()

  async function copy() {
    try {
      await navigator.clipboard.writeText(code)
      toast.show('Copied to the clipboard.', 'success')
    } catch {
      // Clipboard access is blocked outside a secure context, which the dev
      // server over plain http can be. Say so rather than failing silently.
      toast.show('The browser blocked clipboard access.', 'warning')
    }
  }

  return (
    <div className="relative">
      <pre className="overflow-x-auto rounded-control bg-surface-sunken p-3 pr-20 text-xs text-content">
        <code>{code}</code>
      </pre>
      <Button
        variant="secondary"
        size="sm"
        onClick={copy}
        className="absolute top-2 right-2"
      >
        Copy
      </Button>
    </div>
  )
}

/**
 * A color token, shown as the value it actually resolves to.
 *
 * `getPropertyValue` would hand back the literal `light-dark(...)` text, so the
 * resolved color is read off the rendered chip instead. It is re-read when the
 * theme changes, which is what makes this honest in both palettes.
 */
export function Swatch({ token }: { token: string }) {
  const theme = useTheme()
  const chip = useRef<HTMLDivElement>(null)
  const [resolved, setResolved] = useState('')

  useEffect(() => {
    if (chip.current) {
      setResolved(getComputedStyle(chip.current).backgroundColor)
    }
  }, [theme, token])

  return (
    <div className="flex items-center gap-3">
      <div
        ref={chip}
        style={{ background: `var(--color-${token})` }}
        className="size-10 shrink-0 rounded-control border border-border-subtle"
      />
      <div className="min-w-0">
        <p className="truncate font-mono text-xs text-content">--color-{token}</p>
        <p className="truncate text-xs text-content-muted">{resolved}</p>
      </div>
    </div>
  )
}

export function SwatchGrid({ children }: { children: ReactNode }) {
  return (
    <div className={cn('grid gap-4 sm:grid-cols-2 lg:grid-cols-3')}>{children}</div>
  )
}
