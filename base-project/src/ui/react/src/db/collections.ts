import { createCollection } from '@tanstack/react-db'
import { queryCollectionOptions } from '@tanstack/query-db-collection'
import { fetchWeatherForecast, type WeatherForecast } from '@/lib/api'
import { queryClient } from '@/lib/query-client'

export const weatherQueryKey = ['weather', 'forecast'] as const

/**
 * TanStack DB collection backed by TanStack Query. Components read it with
 * `useLiveQuery`, which re-runs incrementally instead of re-filtering on
 * every render.
 */
export const weatherCollection = createCollection(
  queryCollectionOptions<WeatherForecast>({
    id: 'weather-forecast',
    queryClient,
    queryKey: weatherQueryKey,
    queryFn: ({ signal }) => fetchWeatherForecast(signal),
    // The API returns one row per day, so the date is a stable key.
    getKey: (forecast) => forecast.date,
  }),
)
