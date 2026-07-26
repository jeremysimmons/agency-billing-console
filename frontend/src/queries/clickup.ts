import { useQuery, useMutation, useQueryCache } from '@pinia/colada'
import { http } from '../api/http'
import type {
  ClickUpHierarchyNode,
  ClickUpSyncProgressEvent,
  ClickUpSyncResult,
  ClickUpSyncRun,
  ClickUpSyncRunSummary,
  CsvImportResult,
} from '../api/types'

export function useClickUpHierarchy() {
  return useQuery({
    key: ['clickup', 'hierarchy'],
    query: async () => (await http.get<ClickUpHierarchyNode[]>('/clickup/hierarchy')).data,
  })
}

export function useClickUpSyncRuns(limit = 20) {
  return useQuery({
    key: ['clickup', 'sync-runs', limit],
    query: async () =>
      (await http.get<ClickUpSyncRunSummary[]>('/clickup/sync-runs', { params: { limit } })).data,
  })
}

export async function fetchClickUpSyncRun(id: string): Promise<ClickUpSyncRun> {
  return (await http.get<ClickUpSyncRun>(`/clickup/sync-runs/${id}`)).data
}

function parseSseDataBlocks(buffer: string): { events: ClickUpSyncProgressEvent[]; rest: string } {
  const parts = buffer.split('\n\n')
  const rest = parts.pop() ?? ''
  const events: ClickUpSyncProgressEvent[] = []
  for (const block of parts) {
    if (!block.trim()) continue
    const dataLine = block.split('\n').find((line) => line.startsWith('data: '))
    if (!dataLine) continue
    events.push(JSON.parse(dataLine.slice(6)) as ClickUpSyncProgressEvent)
  }
  return { events, rest }
}

function completedToResult(event: ClickUpSyncProgressEvent): ClickUpSyncResult {
  return {
    syncedAt: event.syncedAt ?? new Date().toISOString(),
    containersUpserted: event.containersUpserted ?? 0,
    tasksCreated: event.tasksCreated ?? 0,
    tasksUpdated: event.tasksUpdated ?? 0,
    clientsCreated: event.clientsCreated ?? 0,
    summary: event.summary ?? event.message ?? 'Sync completed.',
    syncRunId: event.syncRunId ?? null,
    parentsFetched: event.parentsFetched ?? 0,
  }
}

export function formatClickUpSyncStatus(event: ClickUpSyncProgressEvent): string {
  switch (event.phase) {
    case 'started':
      return event.message ?? 'Sync started…'
    case 'hierarchy':
      return `Hierarchy upserted (${event.containersUpserted ?? 0} containers)`
    case 'page':
      return `Page ${(event.page ?? 0) + 1}: ${event.tasksCreated ?? 0} new, ${event.tasksUpdated ?? 0} updated tasks, ${event.clientsCreated ?? 0} new clients`
    case 'parents':
      return event.message ?? 'Fetching missing parents…'
    case 'bill_fields': {
      const total = event.clientsTotal ?? 0
      const done = event.clientsProcessed ?? 0
      if (total === 0) return 'Checking billable fields…'
      return `Checking billable fields (${done}/${total})`
    }
    case 'hours':
      return event.message ?? 'Filling hours…'
    case 'invoices':
      return event.message ?? 'Updating invoices…'
    case 'log':
      return event.message ?? 'Syncing…'
    case 'completed':
      return event.summary ?? event.message ?? 'Sync completed.'
    case 'error':
      return event.error ?? 'Sync failed.'
    default:
      return event.message ?? 'Syncing…'
  }
}

export async function syncClickUpWithProgress(
  onEvent: (event: ClickUpSyncProgressEvent) => void,
  signal?: AbortSignal,
): Promise<ClickUpSyncResult> {
  const res = await fetch('/api/clickup/sync', { method: 'POST', signal })
  if (!res.ok) {
    const contentType = res.headers.get('content-type') ?? ''
    if (contentType.includes('application/json')) {
      const body = (await res.json()) as { error?: string }
      throw new Error(body.error ?? 'Sync failed.')
    }
    throw new Error('Sync failed.')
  }

  const reader = res.body?.getReader()
  if (!reader) throw new Error('Sync failed.')

  const decoder = new TextDecoder()
  let buffer = ''
  let result: ClickUpSyncResult | null = null

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const { events, rest } = parseSseDataBlocks(buffer)
    buffer = rest
    for (const event of events) {
      onEvent(event)
      if (event.phase === 'completed') result = completedToResult(event)
      if (event.phase === 'error') throw new Error(event.error ?? 'Sync failed.')
    }
  }

  if (buffer.trim()) {
    const { events } = parseSseDataBlocks(`${buffer}\n\n`)
    for (const event of events) {
      onEvent(event)
      if (event.phase === 'completed') result = completedToResult(event)
      if (event.phase === 'error') throw new Error(event.error ?? 'Sync failed.')
    }
  }

  if (!result) throw new Error('Sync ended without completion.')
  return result
}

export type ClickUpSyncMutationVars = {
  onProgress?: (event: ClickUpSyncProgressEvent) => void
  signal?: AbortSignal
}

export function useClickUpSync() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (vars: ClickUpSyncMutationVars = {}) =>
      syncClickUpWithProgress(vars.onProgress ?? (() => {}), vars.signal),
    onSettled: () => {
      cache.invalidateQueries({ key: ['clickup'] })
      cache.invalidateQueries({ key: ['tasks'] })
      cache.invalidateQueries({ key: ['clients'] })
      cache.invalidateQueries({ key: ['agency'] })
    },
  })
}

export function useCsvImport() {
  const cache = useQueryCache()
  return useMutation({
    mutation: async (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return (await http.post<CsvImportResult>('/clickup/import-csv', form)).data
    },
    onSettled: () => {
      cache.invalidateQueries({ key: ['tasks'] })
      cache.invalidateQueries({ key: ['clients'] })
    },
  })
}
