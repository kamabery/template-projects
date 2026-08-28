import { createFileRoute } from '@tanstack/react-router'
import { useLiveQuery } from '@tanstack/react-db'
import { Button } from '@/components/ui/button'
import { Card, CardTitle } from '@/components/ui/card'
import { weatherCollection } from '@/db/collections'
import { setUnit, useUnit, type TemperatureUnit } from '@/store/preferences'
import type { WeatherForecast } from '@/lib/api'

export const Route = createFileRoute('/weather')({
  component: WeatherPage,
})

const units: Array<TemperatureUnit> = ['C', 'F']

const dateFormat = new Intl.DateTimeFormat(undefined, {
  weekday: 'short',
  month: 'short',
  day: 'numeric',
})

/**
 * The API sends a DateOnly ("2026-08-10"). `new Date()` would read that as UTC
 * midnight and render the previous day west of Greenwich, so parse it as local.
 */
function formatForecastDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  return dateFormat.format(new Date(year, month - 1, day))
}

function WeatherPage() {
  const unit = useUnit()

  // Reads the DB collection, which syncs itself from the API through Query.
  const { data, isLoading, isError } = useLiveQuery((q) =>
    q.from({ forecast: weatherCollection }).orderBy(({ forecast }) => forecast.date, 'asc'),
  )

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-semibold tracking-tight">Weather forecast</h1>
          <p className="mt-1 text-sm text-content-muted">
            GET /api/WeatherForecast → template.api
          </p>
        </div>

        <div className="flex items-center gap-2">
          <div className="flex gap-1 rounded-lg border border-border-subtle p-1">
            {units.map((value) => (
              <Button
                key={value}
                variant={unit === value ? 'primary' : 'ghost'}
                onClick={() => setUnit(value)}
                aria-pressed={unit === value}
              >
                °{value}
              </Button>
            ))}
          </div>
          <Button
            variant="secondary"
            onClick={() => weatherCollection.utils.refetch()}
          >
            Refresh
          </Button>
        </div>
      </div>

      {isLoading && <p className="text-content-muted">Loading forecast…</p>}

      {isError && (
        <Card>
          <CardTitle>Could not reach the API</CardTitle>
          <p className="mt-2 text-sm text-content-muted">
            Start it with <code>dotnet run</code> in{' '}
            <code>src/template.api</code>, then hit Refresh. The dev server
            proxies <code>/api</code> to <code>http://localhost:5058</code>.
          </p>
        </Card>
      )}

      {!isLoading && !isError && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.map((forecast) => (
            <ForecastCard key={forecast.date} forecast={forecast} unit={unit} />
          ))}
        </div>
      )}
    </div>
  )
}

function ForecastCard({
  forecast,
  unit,
}: {
  forecast: WeatherForecast
  unit: TemperatureUnit
}) {
  const temperature =
    unit === 'C' ? forecast.temperatureC : forecast.temperatureF

  return (
    <Card className="space-y-1">
      <CardTitle>{formatForecastDate(forecast.date)}</CardTitle>
      <p className="text-3xl font-semibold tabular-nums">
        {temperature}°{unit}
      </p>
      <p className="text-sm text-content-muted">{forecast.summary ?? '—'}</p>
    </Card>
  )
}
