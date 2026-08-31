import { createContext, useContext, useId, useRef, useState } from 'react'
import type { KeyboardEvent, ReactNode } from 'react'
import { cn } from '@/lib/cn'

interface TabsContextValue {
  baseId: string
  selected: string
  select: (value: string) => void
}

const TabsContext = createContext<TabsContextValue | null>(null)

function useTabs(): TabsContextValue {
  const context = useContext(TabsContext)

  if (!context) {
    throw new Error('Tab and TabPanel must be rendered inside Tabs.')
  }

  return context
}

interface TabsProps {
  defaultValue: string
  onValueChange?: (value: string) => void
  children: ReactNode
  className?: string
}

/**
 * Tabs, following the WAI-ARIA Authoring Practices tabs pattern.
 *
 * Uncontrolled on purpose: a starter that needs the selection outside the
 * component can read it from `onValueChange` without this growing a controlled
 * mode nobody asked for.
 */
export function Tabs({
  defaultValue,
  onValueChange,
  children,
  className,
}: TabsProps) {
  const baseId = useId()
  const [selected, setSelected] = useState(defaultValue)

  function select(value: string) {
    setSelected(value)
    onValueChange?.(value)
  }

  return (
    <TabsContext.Provider value={{ baseId, selected, select }}>
      <div className={className}>{children}</div>
    </TabsContext.Provider>
  )
}

/**
 * Arrow keys, Home and End move focus and select in one step (APG's automatic
 * activation, which suits panels that are already rendered). The tab elements
 * are read off the DOM rather than from a registry: it is the same list the
 * user is looking at, and it stays right when tabs are added conditionally.
 */
export function TabList({
  children,
  className,
  ...props
}: {
  children: ReactNode
  className?: string
  'aria-label'?: string
}) {
  const list = useRef<HTMLDivElement>(null)

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const tabs = Array.from(
      list.current?.querySelectorAll<HTMLButtonElement>('[role="tab"]') ?? [],
    )
    const current = tabs.indexOf(document.activeElement as HTMLButtonElement)

    if (current < 0) {
      return
    }

    let next = -1

    if (event.key === 'ArrowRight') next = (current + 1) % tabs.length
    else if (event.key === 'ArrowLeft') next = (current - 1 + tabs.length) % tabs.length
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = tabs.length - 1

    if (next < 0) {
      return
    }

    event.preventDefault()
    tabs[next].focus()
    tabs[next].click()
  }

  return (
    <div
      ref={list}
      role="tablist"
      onKeyDown={onKeyDown}
      className={cn('flex gap-1 border-b border-border-subtle', className)}
      {...props}
    >
      {children}
    </div>
  )
}

export function Tab({ value, children }: { value: string; children: ReactNode }) {
  const { baseId, selected, select } = useTabs()
  const isSelected = selected === value

  return (
    <button
      type="button"
      role="tab"
      id={`${baseId}-tab-${value}`}
      aria-selected={isSelected}
      aria-controls={`${baseId}-panel-${value}`}
      // Roving tabindex: one stop for the whole tablist, arrows move within it.
      tabIndex={isSelected ? 0 : -1}
      onClick={() => select(value)}
      className={cn(
        '-mb-px border-b-2 px-3 py-2 text-sm font-medium',
        'transition-colors duration-[var(--motion-fast)]',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring',
        isSelected
          ? 'border-brand text-content'
          : 'border-transparent text-content-muted hover:text-content',
      )}
    >
      {children}
    </button>
  )
}

export function TabPanel({
  value,
  children,
  className,
}: {
  value: string
  children: ReactNode
  className?: string
}) {
  const { baseId, selected } = useTabs()

  return (
    <div
      role="tabpanel"
      id={`${baseId}-panel-${value}`}
      aria-labelledby={`${baseId}-tab-${value}`}
      hidden={selected !== value}
      // Focusable so keyboard users can reach panel content that holds no controls.
      tabIndex={0}
      className={cn('py-4 focus-visible:outline-2 focus-visible:outline-focus-ring', className)}
    >
      {children}
    </div>
  )
}
