import { logs } from '@opentelemetry/api-logs'
import {
  InMemoryLogRecordExporter,
  LoggerProvider,
  SimpleLogRecordProcessor,
} from '@opentelemetry/sdk-logs'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { logError } from './telemetry'

describe('logError', () => {
  const exporter = new InMemoryLogRecordExporter()

  beforeEach(() => {
    logs.disable()
    logs.setGlobalLoggerProvider(
      new LoggerProvider({ processors: [new SimpleLogRecordProcessor({ exporter })] }),
    )
  })

  afterEach(() => {
    exporter.reset()
    logs.disable()
  })

  it('names the entity with the same keys the API logs, the id as a string', () => {
    logError('Saving the widget failed.', {
      entity: { type: 'Widget', id: 42 },
      related: [{ type: 'Tag', id: 3 }],
    })

    const [record] = exporter.getFinishedLogRecords()
    expect(record.body).toBe('Saving the widget failed.')
    expect(record.severityText).toBe('Error')
    expect(record.attributes).toEqual({
      'entity.type': 'Widget',
      'entity.id': '42',
      'entity.related': ['Tag:3'],
    })
  })

  it('leaves the id out for a list', () => {
    logError('Loading the widgets failed.', { entity: { type: 'Widget' } })

    const [record] = exporter.getFinishedLogRecords()
    expect(record.attributes).toEqual({ 'entity.type': 'Widget' })
  })

  it('logs with no entity at all', () => {
    logError('Something failed.')

    const [record] = exporter.getFinishedLogRecords()
    expect(record.attributes).toEqual({})
  })
})
