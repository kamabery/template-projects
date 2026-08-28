import { createFileRoute } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Card, CardTitle } from '@/components/ui/card'
import { increment, useCount } from '@/store/counter'

export const Route = createFileRoute('/counter')({ component: CounterPage })

function CounterPage() {
  const count = useCount()

  return (
    <section className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Counter</h1>
        <p className="text-sm text-content-muted">
          TanStack Store → component, with nothing else in the way
        </p>
      </div>

      <Card>
        <CardTitle>Store binding</CardTitle>
        <p className="mt-3 text-sm text-content-muted">
          This page has no HTTP, no configuration and no services. If the count
          stops changing, the store-to-component binding is broken and nothing
          else is implicated.
        </p>

        <p className="mt-4 text-sm" role="status">
          Current count:{' '}
          <strong className="text-base font-semibold" data-testid="count">
            {count}
          </strong>
        </p>

        <Button className="mt-4" onClick={increment}>
          Click me
        </Button>
      </Card>
    </section>
  )
}
